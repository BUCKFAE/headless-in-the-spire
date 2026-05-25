using System.Collections;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using Sts2Headless.Runtime.Loading;

namespace Sts2Headless.Commands;

// Diagnostic provenance map: for every content-catalog entry (cards, monsters,
// events, …), walk the IL of its concrete subtype's declared methods and
// collect every Godot.* call site. Recurses one extra level into sts2-internal
// callees so that ContentModel.Foo() → SomeHelper.Do() → Godot.Tween.Pause is
// attributed back to the catalog entry that triggered the chain.
//
// This is documentation, not a coverage gate. The mandatory check that every
// Godot.* MemberReference sts2 holds resolves against GodotStubs lives in
// GodotStubsCoverageTests.All_Godot_References_From_Sts2_Resolve_On_GodotStubs;
// this command answers the orthogonal question "which Cards/Enemies/Events
// reach which Godot members," which is useful when reasoning about stub
// surface impact and content-shaped Godot dependencies.
//
// Output: documentation/coverage/godot-stub-provenance.md. Two views — per
// catalog (which Godot types does each entry touch) and reverse (which catalog
// entries touch each Godot type). Best-effort: a per-type IL decode failure is
// logged and skipped rather than aborting the whole report.
public static class ProvenanceGodotStubsCommand
{
    // How many call-graph hops past the declared catalog method we follow into
    // sts2-internal callees. Two is enough for the common
    // Model.GetActions → Helper.DoThing → Godot.Tween.Pause chain without
    // exploding the graph for very generic helpers.
    private const int RecursionDepth = 2;

    public static int Run(string vendorDir, string repoRoot)
    {
        var preamble = RuntimeBootstrap.Run(vendorDir);
        if (preamble.SetupError is not null)
        {
            Console.Error.WriteLine($"provenance-godot-stubs: bootstrap setup failed — {preamble.SetupError}");
            return 1;
        }

        var steps = BootstrapSequence.Apply(preamble.Sts2!);
        var failed = steps.Where(s => !s.Ok).ToList();
        if (failed.Count > 0)
        {
            Console.Error.WriteLine("provenance-godot-stubs: bootstrap step failures —");
            foreach (var f in failed) Console.Error.WriteLine($"  [FAIL] {f.Label}: {f.Detail}");
            return 1;
        }

        var sts2 = preamble.Sts2!;
        var (modelDbType, contentById) = GenerateContentIdsCommand.ResolveModelDb(sts2);

        // catalogName → list of (wireId, concrete type)
        var catalogs = new Dictionary<string, List<(string WireId, Type Type)>>(StringComparer.Ordinal);
        var catalogErrors = new List<string>();
        foreach (var spec in GenerateContentIdsCommand.Kinds)
        {
            try
            {
                catalogs[spec.Kind] = EnumerateCatalog(modelDbType, contentById, spec);
            }
            catch (Exception ex)
            {
                catalogErrors.Add($"{spec.Kind}: {ex.GetType().Name}: {ex.Message}");
                catalogs[spec.Kind] = new List<(string, Type)>();
            }
        }

        // Walk the IL — per-entry Godot member hits + per-entry decode failures.
        var perEntry = new Dictionary<string, List<EntryResult>>(StringComparer.Ordinal);
        var perGodotMember = new Dictionary<string, List<EntryRef>>(StringComparer.Ordinal);
        var walkErrors = new List<string>();

        foreach (var (catalogName, entries) in catalogs)
        {
            var bucket = new List<EntryResult>(entries.Count);
            foreach (var (wireId, type) in entries)
            {
                EntryResult result;
                try
                {
                    result = WalkEntry(catalogName, wireId, type);
                }
                catch (Exception ex)
                {
                    walkErrors.Add($"{catalogName}/{wireId} ({type.FullName}): {ex.GetType().Name}: {ex.Message}");
                    result = new EntryResult(catalogName, wireId, type, new(), new());
                }
                bucket.Add(result);

                foreach (var (godotMember, count, originMethod) in result.GodotHits)
                {
                    if (!perGodotMember.TryGetValue(godotMember, out var list))
                    {
                        list = new List<EntryRef>();
                        perGodotMember[godotMember] = list;
                    }
                    list.Add(new EntryRef(catalogName, wireId, originMethod, count));
                }
            }
            perEntry[catalogName] = bucket;
        }

        var outDir = Path.Combine(repoRoot, "documentation", "coverage");
        Directory.CreateDirectory(outDir);
        var outPath = Path.Combine(outDir, "godot-stub-provenance.md");
        var report = EmitReport(perEntry, perGodotMember, catalogErrors, walkErrors);
        File.WriteAllText(outPath, report);

        var totalEntries = perEntry.Values.Sum(b => b.Count);
        var uniqueGodotMembers = perGodotMember.Count;
        Console.WriteLine($"wrote {Path.GetRelativePath(repoRoot, outPath)}");
        Console.WriteLine($"  catalogs walked: {perEntry.Count}");
        Console.WriteLine($"  catalog entries: {totalEntries}");
        Console.WriteLine($"  unique Godot members observed: {uniqueGodotMembers}");
        if (catalogErrors.Count > 0)
        {
            Console.Error.WriteLine($"  catalog enumeration errors: {catalogErrors.Count}");
            foreach (var e in catalogErrors) Console.Error.WriteLine($"    {e}");
        }
        if (walkErrors.Count > 0)
        {
            Console.Error.WriteLine($"  per-entry IL walk errors: {walkErrors.Count} (logged in report)");
        }
        return 0;
    }

    // ── catalog enumeration ──────────────────────────────────────────────

    // Mirror of GenerateContentIdsCommand's private enumeration helpers, but
    // returning (wireId, concrete Type) pairs instead of just wireIds. We
    // could refactor the original to expose this shape, but keeping a small
    // parallel here keeps GenerateContentIdsCommand's surface unchanged.
    private static List<(string WireId, Type Type)> EnumerateCatalog(
        Type modelDbType, IDictionary contentById, GenerateContentIdsCommand.KindSpec spec)
    {
        return spec.Source switch
        {
            GenerateContentIdsCommand.NativeProperty np =>
                EnumerateNative(modelDbType, np.PropertyName),
            GenerateContentIdsCommand.MergedNative mn =>
                MergeNative(modelDbType, mn.PropertyNames),
            GenerateContentIdsCommand.NamespaceFilter ns =>
                EnumerateNamespace(contentById, ns.Namespace),
            _ => throw new InvalidOperationException($"unknown EnumerationSource: {spec.Source.GetType().Name}"),
        };
    }

    private static List<(string WireId, Type Type)> EnumerateNative(Type modelDbType, string propertyName)
    {
        var prop = modelDbType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException($"ModelDb.{propertyName} not found — game version drift?");
        if (prop.GetValue(null) is not IEnumerable items)
            throw new InvalidOperationException($"ModelDb.{propertyName} did not return an IEnumerable");
        var result = new List<(string, Type)>();
        foreach (var m in items)
        {
            if (m is null) continue;
            var entry = ReadIdEntry(m);
            if (entry is null) continue;
            result.Add((entry, m.GetType()));
        }
        return result;
    }

    private static List<(string WireId, Type Type)> MergeNative(Type modelDbType, IReadOnlyList<string> propertyNames)
    {
        var combined = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (var name in propertyNames)
        {
            foreach (var (id, t) in EnumerateNative(modelDbType, name))
            {
                combined[id] = t;
            }
        }
        return combined.Select(kv => (kv.Key, kv.Value)).ToList();
    }

    private static List<(string WireId, Type Type)> EnumerateNamespace(IDictionary contentById, string @namespace)
    {
        var result = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (DictionaryEntry kv in contentById)
        {
            var instance = kv.Value;
            if (instance is null) continue;
            if (!string.Equals(instance.GetType().Namespace, @namespace, StringComparison.Ordinal)) continue;
            var entry = ReadIdEntry(instance);
            if (entry is null) continue;
            result[entry] = instance.GetType();
        }
        return result.Select(kv => (kv.Key, kv.Value)).ToList();
    }

    private static string? ReadIdEntry(object? model)
    {
        if (model is null) return null;
        var idProp = model.GetType().GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
        if (idProp?.GetValue(model) is not object idObj) return null;
        var entryProp = idObj.GetType().GetProperty("Entry", BindingFlags.Public | BindingFlags.Instance);
        return entryProp?.GetValue(idObj) as string;
    }

    // ── IL walk ──────────────────────────────────────────────────────────

    // One catalog entry's collected Godot hits, plus any per-method decode
    // errors so the report can surface partial-walk warnings.
    private sealed record EntryResult(
        string CatalogName,
        string WireId,
        Type ConcreteType,
        List<(string GodotMember, int Count, string OriginMethod)> GodotHits,
        List<string> Errors);

    // Reverse-index row: which catalog entry, via which declared method,
    // touched a given Godot member, and how many times.
    private sealed record EntryRef(string CatalogName, string WireId, string OriginMethod, int Count);

    // Walk every method declared on `type` (not inherited), recurse one extra
    // level into sts2-internal callees, and tally every Godot.* call-site.
    // Failures inside one method are recorded as an error but don't kill the
    // entry.
    private static EntryResult WalkEntry(string catalogName, string wireId, Type type)
    {
        var hits = new Dictionary<string, (int Count, string OriginMethod)>(StringComparer.Ordinal);
        var errors = new List<string>();

        MethodInfo[] declared;
        try
        {
            declared = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
        }
        catch (Exception ex)
        {
            errors.Add($"GetMethods({type.FullName}) threw {ex.GetType().Name}: {ex.Message}");
            return new EntryResult(catalogName, wireId, type, new(), errors);
        }

        var visited = new HashSet<MethodBase>();
        foreach (var m in declared)
        {
            if (m.IsAbstract || m.ContainsGenericParameters) continue;
            WalkMethod(m, depth: 0, type.FullName ?? type.Name, m.Name, hits, errors, visited);
        }

        var ordered = hits
            .OrderByDescending(kv => kv.Value.Count)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => (kv.Key, kv.Value.Count, kv.Value.OriginMethod))
            .ToList();
        return new EntryResult(catalogName, wireId, type, ordered, errors);
    }

    // Recursive IL walker. `originMethod` is the *catalog-declared* method we
    // entered through — every Godot hit is attributed back to it, regardless
    // of how deep in the call chain it actually appears, so the report stays
    // readable from the catalog side.
    private static void WalkMethod(
        MethodBase method,
        int depth,
        string typeFqn,
        string originMethod,
        Dictionary<string, (int Count, string OriginMethod)> hits,
        List<string> errors,
        HashSet<MethodBase> visited)
    {
        if (!visited.Add(method)) return;

        List<CodeInstruction> instructions;
        try
        {
            instructions = PatchProcessor.GetCurrentInstructions(method);
        }
        catch (Exception ex)
        {
            errors.Add($"{method.DeclaringType?.FullName ?? typeFqn}.{method.Name}: GetCurrentInstructions threw {ex.GetType().Name}: {ex.Message}");
            return;
        }

        foreach (var ins in instructions)
        {
            if (ins.opcode != OpCodes.Call && ins.opcode != OpCodes.Callvirt && ins.opcode != OpCodes.Newobj) continue;
            if (ins.operand is not MethodBase target) continue;
            var declaring = target.DeclaringType;
            if (declaring is null) continue;
            var ns = declaring.Namespace ?? "";

            if (IsGodotNamespace(ns))
            {
                var key = FormatGodotMember(target);
                if (hits.TryGetValue(key, out var existing))
                {
                    hits[key] = (existing.Count + 1, existing.OriginMethod);
                }
                else
                {
                    hits[key] = (1, originMethod);
                }
                continue;
            }

            if (depth >= RecursionDepth) continue;
            if (!IsSts2Namespace(ns)) continue;
            // Skip BCL and anything else we don't own.

            // For generic method-info we need the closed instantiation if the
            // call-site provided one; otherwise CodeInstruction already gives
            // us the right MethodBase.
            if (target is MethodInfo mi && mi.ContainsGenericParameters) continue;

            WalkMethod(target, depth + 1, declaring.FullName ?? declaring.Name, originMethod, hits, errors, visited);
        }
    }

    private static bool IsGodotNamespace(string ns)
        => ns == "Godot"
        || ns.StartsWith("Godot.", StringComparison.Ordinal);

    private static bool IsSts2Namespace(string ns)
        => ns.StartsWith("MegaCrit.", StringComparison.Ordinal);

    // The pivot for both the per-entry counts and the reverse index is the
    // Godot member as a flat string. We use `Type.Member` for methods/ctors,
    // grouping by the declaring type prefix so the reverse index can later
    // bucket by `Godot.Tween`, `Godot.Vector2`, …
    private static string FormatGodotMember(MethodBase mb)
    {
        var owner = mb.DeclaringType?.FullName ?? "?";
        var name = mb.Name;
        // Constructor name from reflection is `.ctor` — render it as `ctor`
        // (without the leading dot) so markdown renders it cleanly.
        if (name == ".ctor") name = "ctor";
        return $"{owner}.{name}";
    }

    private static string GodotTypeOf(string memberKey)
    {
        // `Godot.Tween.Pause` → `Godot.Tween`. Last dot separates type+member;
        // nested types use `+` so this is safe.
        var lastDot = memberKey.LastIndexOf('.');
        return lastDot < 0 ? memberKey : memberKey[..lastDot];
    }

    // ── report emission ──────────────────────────────────────────────────

    private static string EmitReport(
        Dictionary<string, List<EntryResult>> perEntry,
        Dictionary<string, List<EntryRef>> perGodotMember,
        List<string> catalogErrors,
        List<string> walkErrors)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Godot stub provenance");
        sb.AppendLine();
        sb.AppendLine("Per-catalog map of which content entries (cards, monsters, events, …) reach which "
            + "`Godot.*` members at IL-walk time. Built by `just runner::probe::godot-stubs-provenance`, which walks every "
            + "concrete catalog subtype's declared methods, follows one extra hop into sts2-internal callees, "
            + "and tallies every `call`/`callvirt`/`newobj` whose target lives in the `Godot` namespace. "
            + "Documentation only — the mandatory stub-coverage check lives in "
            + "`GodotStubsCoverageTests.All_Godot_References_From_Sts2_Resolve_On_GodotStubs`.");
        sb.AppendLine();

        // Summary table.
        sb.AppendLine("## Coverage summary");
        sb.AppendLine();
        sb.AppendLine("| Catalog | Entries | Unique Godot members touched |");
        sb.AppendLine("|---------|---------|------------------------------|");
        foreach (var catalog in perEntry.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            var entries = perEntry[catalog];
            var uniqueMembers = entries.SelectMany(e => e.GodotHits.Select(h => h.GodotMember))
                .ToHashSet(StringComparer.Ordinal).Count;
            sb.AppendLine($"| {catalog} | {entries.Count} | {uniqueMembers} |");
        }
        sb.AppendLine();

        if (catalogErrors.Count > 0)
        {
            sb.AppendLine("### Catalog enumeration errors");
            sb.AppendLine();
            foreach (var e in catalogErrors) sb.AppendLine($"- {e}");
            sb.AppendLine();
        }

        // Per-catalog dump.
        sb.AppendLine("## By catalog");
        sb.AppendLine();
        foreach (var catalog in perEntry.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            var entries = perEntry[catalog].OrderBy(e => e.WireId, StringComparer.Ordinal).ToList();
            sb.AppendLine($"### {catalog} ({entries.Count} entries, sorted alphabetically)");
            sb.AppendLine();
            foreach (var e in entries)
            {
                sb.AppendLine($"#### {e.WireId}");
                sb.AppendLine();
                sb.AppendLine($"`{e.ConcreteType.FullName}`");
                sb.AppendLine();
                if (e.GodotHits.Count == 0)
                {
                    sb.AppendLine("- (no Godot references reached from declared methods)");
                }
                else
                {
                    // Roll up by Godot type for terseness — one line per type,
                    // not per individual member, with the total count.
                    var byType = e.GodotHits
                        .GroupBy(h => GodotTypeOf(h.GodotMember), StringComparer.Ordinal)
                        .Select(g => (Type: g.Key, Total: g.Sum(h => h.Count)))
                        .OrderByDescending(t => t.Total)
                        .ThenBy(t => t.Type, StringComparer.Ordinal)
                        .ToList();
                    foreach (var (godotType, total) in byType)
                    {
                        sb.AppendLine($"- `{godotType}` ({total})");
                    }
                }
                if (e.Errors.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine($"<details><summary>{e.Errors.Count} IL-decode error(s)</summary>");
                    sb.AppendLine();
                    foreach (var err in e.Errors) sb.AppendLine($"- {err}");
                    sb.AppendLine();
                    sb.AppendLine("</details>");
                }
                sb.AppendLine();
            }
        }

        // Reverse index, bucketed by Godot type (not member) — keeps the file
        // tractable. Sorted by total hit count.
        sb.AppendLine("## By Godot type (reverse index, sorted by hit count)");
        sb.AppendLine();
        var byGodotType = perGodotMember
            .GroupBy(kv => GodotTypeOf(kv.Key), StringComparer.Ordinal)
            .Select(g => new
            {
                Type = g.Key,
                Refs = g.SelectMany(kv => kv.Value).ToList(),
            })
            .OrderByDescending(g => g.Refs.Sum(r => r.Count))
            .ThenBy(g => g.Type, StringComparer.Ordinal)
            .ToList();

        foreach (var grp in byGodotType)
        {
            var totalHits = grp.Refs.Sum(r => r.Count);
            // Dedup by (catalog, wireId) — same entry may touch the same Godot
            // type from multiple methods, but for the reverse index one line
            // per entry is enough.
            var perEntryRollup = grp.Refs
                .GroupBy(r => (r.CatalogName, r.WireId))
                .Select(g => new
                {
                    g.Key.CatalogName,
                    g.Key.WireId,
                    Methods = g.Select(r => r.OriginMethod).Distinct(StringComparer.Ordinal).ToList(),
                    Count = g.Sum(r => r.Count),
                })
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.CatalogName, StringComparer.Ordinal)
                .ThenBy(x => x.WireId, StringComparer.Ordinal)
                .ToList();

            sb.AppendLine($"### `{grp.Type}` ({totalHits} hits across {perEntryRollup.Count} catalog entries)");
            sb.AppendLine();
            foreach (var row in perEntryRollup)
            {
                var methods = string.Join(", ", row.Methods.Take(3));
                if (row.Methods.Count > 3) methods += $", +{row.Methods.Count - 3}";
                sb.AppendLine($"- {row.CatalogName}/{row.WireId} ({methods})");
            }
            sb.AppendLine();
        }

        if (walkErrors.Count > 0)
        {
            sb.AppendLine("## IL walk errors");
            sb.AppendLine();
            sb.AppendLine($"{walkErrors.Count} entries surfaced top-level walk errors during the run.");
            sb.AppendLine();
            foreach (var e in walkErrors) sb.AppendLine($"- {e}");
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
