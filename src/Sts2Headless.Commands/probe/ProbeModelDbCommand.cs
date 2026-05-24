using System.Collections;
using System.Reflection;
using System.Text;
using Sts2Headless.Runtime.Loading;

namespace Sts2Headless.Commands;

// Reflective dump of every "All*" enumeration on MegaCrit.Sts2.Core.Models.ModelDb
// — cards, relics, monsters, events, potions, room types, etc. — so we can
// quote concrete counts when designing coverage tooling. Output is also
// written to documentation/research/modeldb/content-inventory.txt for
// follow-up reading (not checked in by default; gitignored under research/).
//
// AD-4: no compile-time sts2 reference. Everything is reflection through the
// already-loaded vendor assembly. Designed to fail soft — a missing property
// is reported, not thrown, so the whole inventory comes out even if one
// branch of ModelDb has drifted on a future pin.
internal static class ProbeModelDbCommand
{
    public static int Run(string vendorDir, string repoRoot)
    {
        var preamble = RuntimeBootstrap.Run(vendorDir);
        if (preamble.SetupError is not null)
        {
            Console.Error.WriteLine($"probe-modeldb: bootstrap setup failed — {preamble.SetupError}");
            return 1;
        }

        var steps = BootstrapSequence.Apply(preamble.Sts2!);
        var failed = steps.Where(s => !s.Ok).ToList();
        if (failed.Count > 0)
        {
            Console.Error.WriteLine("probe-modeldb: bootstrap step failures —");
            foreach (var f in failed) Console.Error.WriteLine($"  [FAIL] {f.Label}: {f.Detail}");
            return 1;
        }

        var sts2 = preamble.Sts2!;
        var modelDbType = sts2.GetType("MegaCrit.Sts2.Core.Models.ModelDb")
            ?? throw new InvalidOperationException("ModelDb type not found in sts2.dll");

        var sb = new StringBuilder();
        sb.AppendLine($"# ModelDb content inventory  (game version: {preamble.Sts2!.GetName().Version})");
        sb.AppendLine();

        // Static properties named All* that return IEnumerable — the same
        // shape ModelDb.AllCards has — are the canonical "everything of kind X"
        // enumerations. Print one section per discovered property.
        var allProps = modelDbType
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.Name.StartsWith("All", StringComparison.Ordinal))
            .Where(p => typeof(IEnumerable).IsAssignableFrom(p.PropertyType))
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ToList();

        if (allProps.Count == 0)
        {
            sb.AppendLine("(no public static All* IEnumerable properties found on ModelDb)");
        }

        foreach (var prop in allProps)
        {
            sb.AppendLine($"## ModelDb.{prop.Name}  ({prop.PropertyType.Name})");
            object? raw;
            try { raw = prop.GetValue(null); }
            catch (Exception ex)
            {
                sb.AppendLine($"  (threw: {ex.GetType().Name}: {ex.Message})");
                sb.AppendLine();
                continue;
            }
            if (raw is not IEnumerable items)
            {
                sb.AppendLine("  (returned non-IEnumerable)");
                sb.AppendLine();
                continue;
            }

            var entries = new List<string>();
            string? modelTypeName = null;
            foreach (var item in items)
            {
                if (item is null) { entries.Add("<null>"); continue; }
                modelTypeName ??= item.GetType().FullName;
                entries.Add(DescribeModel(item));
            }

            sb.AppendLine($"  item type: {modelTypeName ?? "<empty>"}");
            sb.AppendLine($"  count:     {entries.Count}");

            // Print first 10 and last 5 entries — enough to eyeball naming
            // and confirm the property actually carries content, without
            // dumping the full ~500-entry card list inline. Full lists go
            // to per-kind text files alongside the summary.
            var preview = entries.Take(10).ToList();
            sb.AppendLine("  preview:");
            foreach (var e in preview) sb.AppendLine($"    {e}");
            if (entries.Count > preview.Count)
            {
                sb.AppendLine($"    ... ({entries.Count - preview.Count} more)");
            }
            sb.AppendLine();

            // Dump the full list to a sibling file so a researcher can grep
            // without re-running the probe. Filename derived from property
            // name — e.g. AllRelics → modeldb-AllRelics.txt.
            var outDir = Path.Combine(repoRoot, "documentation", "research", "modeldb");
            Directory.CreateDirectory(outDir);
            var outPath = Path.Combine(outDir, $"modeldb-{prop.Name}.txt");
            File.WriteAllText(outPath, string.Join('\n', entries) + '\n');
            sb.AppendLine($"  full list: {Path.GetRelativePath(repoRoot, outPath)}");
            sb.AppendLine();
        }

        // Dump every public+private static field/property on ModelDb so we
        // can find canonical-getter shapes that handle kinds without an
        // AllX property (Monsters/Afflictions/Orbs/Modifiers/Enchantments).
        sb.AppendLine("## ModelDb.* — all static members (public+private)");
        foreach (var m in modelDbType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
        {
            sb.AppendLine($"  field {m.FieldType.Name} {(m.IsPublic ? "pub" : "prv")} {m.Name}");
        }
        foreach (var m in modelDbType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                                     .Where(m => !m.IsSpecialName)
                                     .OrderBy(m => m.Name, StringComparer.Ordinal))
        {
            var generics = m.IsGenericMethodDefinition ? "<" + string.Join(",", m.GetGenericArguments().Select(a => a.Name)) + ">" : "";
            sb.AppendLine($"  meth  {m.ReturnType.Name} {(m.IsPublic ? "pub" : "prv")} {m.Name}{generics}({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name))})");
        }
        sb.AppendLine();

        // Also list ModelDb.GetById<T> generic methods — these tell us which
        // model types have a typed lookup, which is a strong hint at which
        // dimensions are first-class in the engine.
        sb.AppendLine("## ModelDb.GetById<T> generic overloads");
        foreach (var m in modelDbType.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            if (m.Name != "GetById") continue;
            sb.AppendLine($"  {m}");
        }
        sb.AppendLine();

        // Also: enumerate AbstractModelSubtypes — the registry the bootstrap
        // sequence walks via ModelDb.Inject. Each subtype is a kind of game
        // content (card, relic, event, potion, monster, …).
        var subtypesType = sts2.GetType("MegaCrit.Sts2.Core.Models.AbstractModelSubtypes");
        if (subtypesType is not null)
        {
            sb.AppendLine("## AbstractModelSubtypes.All");
            var allProp = subtypesType.GetProperty("All", BindingFlags.Public | BindingFlags.Static);
            object? subtypes = allProp?.GetValue(null);
            if (subtypes is null)
            {
                var allField = subtypesType.GetField("All", BindingFlags.Public | BindingFlags.Static);
                subtypes = allField?.GetValue(null);
            }
            if (subtypes is IEnumerable subEnum)
            {
                foreach (var s in subEnum) sb.AppendLine($"  {s}");
            }
            else
            {
                sb.AppendLine("  (could not enumerate AbstractModelSubtypes.All)");
            }
            sb.AppendLine();
        }

        // Card-pool → card-id membership. Useful for answering "which
        // character owns this card", which the per-id sweeps need so they
        // can run/new with the right Character before testing the card.
        DumpCardPoolMembership(sts2, modelDbType, repoRoot, sb);

        var summary = sb.ToString();
        Console.Write(summary);

        var summaryDir = Path.Combine(repoRoot, "documentation", "research", "modeldb");
        Directory.CreateDirectory(summaryDir);
        var summaryPath = Path.Combine(summaryDir, "content-inventory.txt");
        File.WriteAllText(summaryPath, summary);
        Console.WriteLine($"wrote {Path.GetRelativePath(repoRoot, summaryPath)}");
        return 0;
    }

    // Walk every CardPoolModel in ModelDb.AllCardPools (plus the shared
    // pool list) and dump pool→[cardId] membership. The per-id sweeps
    // (CardSweep, EnchantmentSweep) need this to know which character to
    // run/new with for non-Ironclad cards. Output goes to
    // documentation/research/modeldb/card-pool-membership.txt as a flat
    // "POOL_ID\tCARD_ID" table (cheap to grep).
    private static void DumpCardPoolMembership(
        Assembly sts2, Type modelDbType, string repoRoot, StringBuilder sb)
    {
        sb.AppendLine("## CardPool membership (POOL_ID → CARD_IDs)");
        var allPoolsProp = modelDbType.GetProperty("AllCardPools",
            BindingFlags.Public | BindingFlags.Static);
        if (allPoolsProp?.GetValue(null) is not IEnumerable pools)
        {
            sb.AppendLine("  (ModelDb.AllCardPools not enumerable)");
            sb.AppendLine();
            return;
        }

        var lines = new List<string>();
        var totalCards = 0;
        foreach (var pool in pools)
        {
            if (pool is null) continue;
            var poolId = DescribeModel(pool);
            // CardPoolModel exposes the card list via the AllCardIds
            // property (IEnumerable<ModelId>). GenerateAllCards() exists
            // too but takes hidden parameters in some pool subclasses;
            // AllCardIds is the simpler universal accessor.
            IEnumerable? cards = null;
            var idsProp = pool.GetType().GetProperty("AllCardIds",
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            if (idsProp?.GetValue(pool) is IEnumerable idsEnum) cards = idsEnum;
            if (cards is null)
            {
                var gen = pool.GetType().GetMethod("GenerateAllCards",
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (gen is { } g && g.GetParameters().Length == 0
                    && g.Invoke(pool, null) is IEnumerable genEnum)
                    cards = genEnum;
            }
            if (cards is null)
            {
                sb.AppendLine($"  {poolId}: (no AllCardIds / GenerateAllCards() accessor)");
                continue;
            }
            var n = 0;
            foreach (var card in cards)
            {
                if (card is null) continue;
                var cardId = DescribeModel(card);
                lines.Add($"{poolId}\t{cardId}");
                n++;
            }
            sb.AppendLine($"  {poolId}: {n} cards");
            totalCards += n;
        }
        sb.AppendLine($"  total: {totalCards} pool→card edges");
        sb.AppendLine();

        var outDir = Path.Combine(repoRoot, "documentation", "research", "modeldb");
        Directory.CreateDirectory(outDir);
        var outPath = Path.Combine(outDir, "card-pool-membership.txt");
        File.WriteAllText(outPath, string.Join('\n', lines) + '\n');
        sb.AppendLine($"  full membership: {Path.GetRelativePath(repoRoot, outPath)}");
        sb.AppendLine();
    }

    // Pull a stable, human-readable identifier from any ModelDb item. Tries
    // Id.Entry first (the canonical wire form), then Id, then ModelId, then
    // a final fall-back to ToString().
    private static string DescribeModel(object item)
    {
        var t = item.GetType();
        var idProp = t.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
        if (idProp?.GetValue(item) is object idObj)
        {
            var entryProp = idObj.GetType().GetProperty("Entry", BindingFlags.Public | BindingFlags.Instance);
            if (entryProp?.GetValue(idObj) is string entry && entry.Length > 0) return entry;
            return idObj.ToString() ?? "<id-null>";
        }
        var modelIdProp = t.GetProperty("ModelId", BindingFlags.Public | BindingFlags.Instance);
        if (modelIdProp?.GetValue(item) is object mid)
        {
            return mid.ToString() ?? "<modelid-null>";
        }
        return item.ToString() ?? "<null>";
    }
}
