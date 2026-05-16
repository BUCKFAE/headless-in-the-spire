using System.Collections;
using System.Reflection;
using System.Text;
using Sts2Headless.Runtime;

namespace Sts2Headless;

// Emits one {Kind}Id.g.cs per content kind in src/Sts2Headless.Protocol/,
// sourced from the booted sts2.dll's ModelDb. Generalises the original
// GenerateCardIdsCommand to every kind the game ships:
//
//   * Native ModelDb.All* properties (Cards / Relics / Potions / Encounters /
//     Events / Powers / Characters) → enumerate instances directly.
//   * AbstractModelSubtypes namespace filter (Monsters / Enchantments /
//     Modifiers / Afflictions / Orbs) → ModelDb has no typed AllX for these,
//     so we walk AbstractModelSubtypes.All, filter by namespace, instantiate
//     concrete subtypes, and read .Id.Entry off the instance.
//
// All outputs are gitignored (see .gitignore — proprietary content from
// vendor/sts2.dll) and superseded by *.Fallback.cs stubs on a fresh clone.
//
// ManifestDriftTests (tests/Sts2Headless.IntegrationTests/Coverage/) asserts
// each on-disk *.g.cs matches a fresh in-process walk, so a PR that bumps
// the game pin without regenerating the manifests fails CI loudly.
//
// NewContentKindTests covers the meta case: a top-level namespace under
// MegaCrit.Sts2.Core.Models that isn't in this command's KindSpec list ⇒
// new content category we haven't accounted for ⇒ test failure pointing
// at the unknown namespace.
public static class GenerateContentIdsCommand
{
    // One row per generated manifest. The wire-id collection is sourced
    // either from a ModelDb.AllX property (NativeProperty) or from a
    // namespace filter over ModelDb's canonical-instance registry
    // (NamespaceFilter).
    public sealed record KindSpec(
        string Kind,                       // "Relic"  → RelicId / RelicIdNames / RelicId.g.cs
        EnumerationSource Source);

    public abstract record EnumerationSource;
    public sealed record NativeProperty(string PropertyName) : EnumerationSource;   // e.g. "AllRelics"
    public sealed record NamespaceFilter(string Namespace) : EnumerationSource;    // e.g. "MegaCrit.Sts2.Core.Models.Monsters"
    public sealed record MergedNative(IReadOnlyList<string> PropertyNames) : EnumerationSource;

    // The full kind registry. NewContentKindTests verifies this list covers
    // every top-level namespace under MegaCrit.Sts2.Core.Models — adding a
    // kind to sts2 surfaces here as a failing test, not as silent loss of
    // coverage. Order is alphabetical for predictable output.
    public static readonly IReadOnlyList<KindSpec> Kinds =
    [
        new("Affliction",   new NamespaceFilter("MegaCrit.Sts2.Core.Models.Afflictions")),
        new("Card",         new NativeProperty("AllCards")),
        new("Encounter",    new NativeProperty("AllEncounters")),
        new("Enchantment",  new NamespaceFilter("MegaCrit.Sts2.Core.Models.Enchantments")),
        // Events: AllEvents + AllSharedEvents + AllAncients all surface as
        // game-time encountered events; merging into one enum matches how
        // the wire surfaces event option textKeys without distinguishing
        // origin.
        new("Event",        new MergedNative(["AllEvents", "AllSharedEvents", "AllAncients"])),
        new("Modifier",     new NamespaceFilter("MegaCrit.Sts2.Core.Models.Modifiers")),
        new("Monster",      new NamespaceFilter("MegaCrit.Sts2.Core.Models.Monsters")),
        new("Orb",          new NamespaceFilter("MegaCrit.Sts2.Core.Models.Orbs")),
        new("Potion",       new NativeProperty("AllPotions")),
        new("Power",        new NativeProperty("AllPowers")),
        new("Relic",        new NativeProperty("AllRelics")),
    ];

    // Enumerate the wire ids for one kind against a booted sts2 assembly.
    // Tests under tests/Sts2Headless.IntegrationTests/Coverage/ call this
    // to compare a fresh ModelDb walk against the committed *Id.g.cs
    // manifests; the CLI Run() entry point loops over Kinds and codegens.
    public static SortedSet<string> EnumerateIds(KindSpec spec, Type modelDbType, IDictionary contentById)
    {
        return EnumerateKind(modelDbType, contentById, spec);
    }

    // Resolve ModelDb._contentById once for a booted sts2 assembly. The
    // private field is the canonical-instance registry populated by
    // ModelDb.Inject during bootstrap; tests use it via EnumerateIds
    // above so they don't reimplement the reflection dance.
    public static (Type modelDbType, IDictionary contentById) ResolveModelDb(Assembly sts2)
    {
        var modelDbType = sts2.GetType("MegaCrit.Sts2.Core.Models.ModelDb")
            ?? throw new InvalidOperationException("ModelDb type not found in sts2.dll");
        var contentByIdField = modelDbType.GetField("_contentById", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ModelDb._contentById not found — game version drift?");
        if (contentByIdField.GetValue(null) is not IDictionary contentById)
            throw new InvalidOperationException("ModelDb._contentById is not an IDictionary");
        return (modelDbType, contentById);
    }

    public static int Run(string vendorDir, string repoRoot)
    {
        var preamble = RuntimeBootstrap.Run(vendorDir);
        if (preamble.SetupError is not null)
        {
            Console.Error.WriteLine($"generate-content-ids: bootstrap setup failed — {preamble.SetupError}");
            return 1;
        }

        var steps = BootstrapSequence.Apply(preamble.Sts2!);
        var failed = steps.Where(s => !s.Ok).ToList();
        if (failed.Count > 0)
        {
            Console.Error.WriteLine("generate-content-ids: bootstrap step failures —");
            foreach (var f in failed) Console.Error.WriteLine($"  [FAIL] {f.Label}: {f.Detail}");
            return 1;
        }

        var (modelDbType, contentById) = ResolveModelDb(preamble.Sts2!);

        var outDir = Path.Combine(repoRoot, "src", "Sts2Headless.Protocol");
        var anyFailed = false;

        foreach (var spec in Kinds)
        {
            SortedSet<string> ids;
            try
            {
                ids = EnumerateKind(modelDbType, contentById, spec);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"generate-content-ids: {spec.Kind} — enumeration failed: {ex.GetType().Name}: {ex.Message}");
                anyFailed = true;
                continue;
            }

            if (ids.Count == 0)
            {
                Console.Error.WriteLine($"generate-content-ids: {spec.Kind} — enumerated 0 ids; refusing to emit empty {spec.Kind}Id.g.cs.");
                anyFailed = true;
                continue;
            }

            var outPath = Path.Combine(outDir, $"{spec.Kind}Id.g.cs");
            File.WriteAllText(outPath, Emit(spec.Kind, ids));
            Console.WriteLine($"wrote {Path.GetRelativePath(repoRoot, outPath)}  ({ids.Count} {spec.Kind.ToLowerInvariant()}s)");
        }

        return anyFailed ? 1 : 0;
    }

    // ── enumeration paths ────────────────────────────────────────────────

    private static SortedSet<string> EnumerateKind(
        Type modelDbType, IDictionary contentById, KindSpec spec)
    {
        return spec.Source switch
        {
            NativeProperty np => EnumerateNative(modelDbType, np.PropertyName),
            MergedNative mn => MergeNative(modelDbType, mn.PropertyNames),
            NamespaceFilter ns => EnumerateNamespace(contentById, ns.Namespace),
            _ => throw new InvalidOperationException($"unknown EnumerationSource: {spec.Source.GetType().Name}"),
        };
    }

    private static SortedSet<string> EnumerateNative(Type modelDbType, string propertyName)
    {
        var prop = modelDbType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException($"ModelDb.{propertyName} not found — game version drift?");
        if (prop.GetValue(null) is not IEnumerable items)
            throw new InvalidOperationException($"ModelDb.{propertyName} did not return an IEnumerable");
        var ids = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var m in items)
        {
            var entry = ReadIdEntry(m);
            if (entry is not null) ids.Add(entry);
        }
        return ids;
    }

    private static SortedSet<string> MergeNative(Type modelDbType, IReadOnlyList<string> propertyNames)
    {
        var combined = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var name in propertyNames) foreach (var id in EnumerateNative(modelDbType, name)) combined.Add(id);
        return combined;
    }

    // Walk ModelDb._contentById (the canonical-instance registry populated
    // by ModelDb.Inject during bootstrap). The dict is keyed by ModelId,
    // valued by AbstractModel — we don't care about the key shape; we
    // iterate values, look at each instance's concrete Type's namespace
    // (so .Mocks. sub-namespaces drop out automatically since their types
    // live in MegaCrit.Sts2.Core.Models.<Kind>.Mocks, not the parent), and
    // read .Id.Entry. This is the same path the typed
    // ModelDb.Monster<T>() / Affliction<T>() / etc. accessors take.
    private static SortedSet<string> EnumerateNamespace(IDictionary contentById, string @namespace)
    {
        var ids = new SortedSet<string>(StringComparer.Ordinal);
        foreach (DictionaryEntry kv in contentById)
        {
            var instance = kv.Value;
            if (instance is null) continue;
            if (!string.Equals(instance.GetType().Namespace, @namespace, StringComparison.Ordinal)) continue;
            var entry = ReadIdEntry(instance);
            if (entry is not null) ids.Add(entry);
        }
        return ids;
    }

    // ── id extraction ────────────────────────────────────────────────────

    private static string? ReadIdEntry(object? model)
    {
        if (model is null) return null;
        var idProp = model.GetType().GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
        if (idProp?.GetValue(model) is not object idObj) return null;
        var entryProp = idObj.GetType().GetProperty("Entry", BindingFlags.Public | BindingFlags.Instance);
        return entryProp?.GetValue(idObj) as string;
    }

    // ── codegen ──────────────────────────────────────────────────────────

    // SCREAMING_SNAKE_CASE → PascalCase. Same rules as the original
    // GenerateCardIdsCommand — pure ASCII; sts2 ids are never unicode.
    private static string ToPascalCase(string snake)
    {
        var sb = new StringBuilder(snake.Length);
        var atWordStart = true;
        foreach (var ch in snake)
        {
            if (ch == '_') { atWordStart = true; continue; }
            sb.Append(atWordStart ? char.ToUpperInvariant(ch) : char.ToLowerInvariant(ch));
            atWordStart = false;
        }
        return sb.ToString();
    }

    private static string Emit(string kind, SortedSet<string> wireIds)
    {
        var enumName = $"{kind}Id";
        var namesClass = $"{enumName}Names";
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("//   Source: generated by `just generate-content-ids` (Sts2Headless --generate-content-ids).");
        sb.AppendLine($"//   Sourced from ModelDb in the pinned vendor/sts2.dll.");
        sb.AppendLine("//   Do not edit by hand — re-run the generator after bumping the game pin.");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine();
        sb.AppendLine("namespace Sts2Headless.Protocol.Methods;");
        sb.AppendLine();
        sb.AppendLine($"// Every {kind.ToLowerInvariant()} sts2 ships, as discovered on the pinned game");
        sb.AppendLine("// version. Unknown is the sentinel for ids that post-date the pinned enum.");
        sb.AppendLine("[OpaqueWireString]");
        sb.AppendLine($"[JsonConverter(typeof(JsonStringEnumConverter<{enumName}>))]");
        sb.AppendLine($"public enum {enumName}");
        sb.AppendLine("{");
        sb.AppendLine("    [JsonStringEnumMemberName(\"UNKNOWN\")] Unknown,");
        foreach (var wire in wireIds)
        {
            var pascal = ToPascalCase(wire);
            sb.AppendLine($"    [JsonStringEnumMemberName(\"{wire}\")] {pascal},");
        }
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine($"public static class {namesClass}");
        sb.AppendLine("{");
        sb.AppendLine($"    private static readonly System.Collections.Generic.Dictionary<string, {enumName}> _fromWire = new(System.StringComparer.Ordinal)");
        sb.AppendLine("    {");
        foreach (var wire in wireIds)
        {
            var pascal = ToPascalCase(wire);
            sb.AppendLine($"        [\"{wire}\"] = {enumName}.{pascal},");
        }
        sb.AppendLine("    };");
        sb.AppendLine();
        sb.AppendLine($"    public static {enumName} FromWire(string wireName) =>");
        sb.AppendLine("        _fromWire.TryGetValue(wireName, out var id) ? id : " + enumName + ".Unknown;");
        sb.AppendLine();
        sb.AppendLine("    public static System.Collections.Generic.IReadOnlyCollection<string> AllWireNames => _fromWire.Keys;");
        sb.AppendLine("}");
        return sb.ToString();
    }
}
