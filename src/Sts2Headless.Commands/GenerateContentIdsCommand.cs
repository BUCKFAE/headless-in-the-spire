using System.Collections;
using System.Reflection;
using System.Text;
using Sts2Headless.Runtime.Loading;

namespace Sts2Headless.Commands;

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
    // TODO: Kind should be an enum?
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

        var outDir = Path.Combine(repoRoot, "src", "Sts2Headless.Protocol", "Methods");
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

        // Emit CardOriginPool.g.cs alongside the Id manifests. Sweeps need
        // to know "which character owns this card" to start a run with the
        // right Character (Regent cards cost Stars, not Energy, etc.) and
        // to tag curse/status/quest cards as expected-Unplayable rather
        // than treating their CanPlay=false as suspicious.
        try
        {
            var membership = EnumerateCardPoolMembership(modelDbType);
            var outPath = Path.Combine(outDir, "CardOriginPool.g.cs");
            File.WriteAllText(outPath, EmitCardOriginPool(membership));
            Console.WriteLine($"wrote {Path.GetRelativePath(repoRoot, outPath)}  ({membership.Count} card→pool edges)");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"generate-content-ids: CardOriginPool — {ex.GetType().Name}: {ex.Message}");
            anyFailed = true;
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

    // ── card-pool membership ─────────────────────────────────────────────

    // Pool ids the engine ships (from documentation/research/modeldb/
    // modeldb-AllCardPools.txt). Each ID maps to a CardOriginPool enum
    // value. The 5 character pools mirror the Character enum exactly;
    // the 7 non-character pools (Colorless / Curse / Status / …) are
    // separate because they're not playable-by-a-specific-character.
    //
    // Suffix is stripped ("_CARD_POOL") to keep enum values short. If
    // the engine ever ships a new pool that doesn't match this map, the
    // enumerator throws (visible failure beats silent miscategorisation).
    private static readonly IReadOnlyDictionary<string, string> PoolIdToEnumValue =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["IRONCLAD_CARD_POOL"]    = "Ironclad",
            ["SILENT_CARD_POOL"]      = "Silent",
            ["DEFECT_CARD_POOL"]      = "Defect",
            ["REGENT_CARD_POOL"]      = "Regent",
            ["NECROBINDER_CARD_POOL"] = "Necrobinder",
            ["COLORLESS_CARD_POOL"]   = "Colorless",
            ["CURSE_CARD_POOL"]       = "Curse",
            ["DEPRECATED_CARD_POOL"]  = "Deprecated",
            ["EVENT_CARD_POOL"]       = "Event",
            ["QUEST_CARD_POOL"]       = "Quest",
            ["STATUS_CARD_POOL"]      = "Status",
            ["TOKEN_CARD_POOL"]       = "Token",
        };

    public static SortedDictionary<string, string> EnumerateCardPoolMembership(Type modelDbType)
    {
        var allPoolsProp = modelDbType.GetProperty("AllCardPools",
            BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("ModelDb.AllCardPools not found");
        if (allPoolsProp.GetValue(null) is not IEnumerable pools)
            throw new InvalidOperationException("ModelDb.AllCardPools did not return IEnumerable");

        // First pass: when a card is in BOTH a character pool AND
        // colorless/event/etc., the character pool wins (the engine treats
        // it as that character's content). Empirically: at least
        // IRONCLAD_CARD_POOL contains some shared utility cards, so the
        // tiebreak matters. Walk in pool-priority order.
        var priority = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["IRONCLAD_CARD_POOL"] = 0, ["SILENT_CARD_POOL"] = 0,
            ["DEFECT_CARD_POOL"] = 0, ["REGENT_CARD_POOL"] = 0,
            ["NECROBINDER_CARD_POOL"] = 0,
            ["CURSE_CARD_POOL"] = 1, ["STATUS_CARD_POOL"] = 1,
            ["QUEST_CARD_POOL"] = 1, ["TOKEN_CARD_POOL"] = 1,
            ["EVENT_CARD_POOL"] = 2, ["COLORLESS_CARD_POOL"] = 3,
            ["DEPRECATED_CARD_POOL"] = 9,
        };

        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var bestPriority = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var pool in pools)
        {
            if (pool is null) continue;
            var poolId = ReadIdEntry(pool);
            if (poolId is null || !PoolIdToEnumValue.TryGetValue(poolId, out var enumValue))
                throw new InvalidOperationException($"Unknown card pool id '{poolId}' — extend PoolIdToEnumValue");
            if (!priority.TryGetValue(poolId, out var prio)) prio = 99;

            var idsProp = pool.GetType().GetProperty("AllCardIds",
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            if (idsProp?.GetValue(pool) is not IEnumerable ids)
                throw new InvalidOperationException($"Pool {poolId}: no AllCardIds property");

            foreach (var id in ids)
            {
                if (id is null) continue;
                // ModelId has an Entry string property — same shape as
                // the other ReadIdEntry call sites.
                var entryProp = id.GetType().GetProperty("Entry",
                    BindingFlags.Public | BindingFlags.Instance);
                if (entryProp?.GetValue(id) is not string cardId || cardId.Length == 0) continue;

                if (!bestPriority.TryGetValue(cardId, out var currentPrio) || prio < currentPrio)
                {
                    result[cardId] = enumValue;
                    bestPriority[cardId] = prio;
                }
            }
        }
        return result;
    }

    private static string EmitCardOriginPool(SortedDictionary<string, string> membership)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("//   Source: generated by `just build::generate-content-ids` (Sts2Headless --generate-content-ids).");
        sb.AppendLine("//   Sourced from ModelDb.AllCardPools in the pinned vendor/sts2.dll.");
        sb.AppendLine("//   Do not edit by hand — re-run the generator after bumping the game pin.");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
        sb.AppendLine("namespace Sts2Headless.Protocol.Methods;");
        sb.AppendLine();
        sb.AppendLine("// Which of sts2's card pools a given card id belongs to. The 5");
        sb.AppendLine("// character values mirror the Character enum; the 7 non-character");
        sb.AppendLine("// values flag cards that can't be played by a specific character");
        sb.AppendLine("// (Curse / Status are always unplayable; Colorless / Event are");
        sb.AppendLine("// usually conditional; Token / Quest are spawned by other content;");
        sb.AppendLine("// Deprecated is engine-internal). Unknown is the sentinel for");
        sb.AppendLine("// cards introduced after this pin.");
        sb.AppendLine("public enum CardOriginPool");
        sb.AppendLine("{");
        sb.AppendLine("    Unknown,");
        sb.AppendLine("    Ironclad, Silent, Defect, Regent, Necrobinder,");
        sb.AppendLine("    Colorless, Curse, Deprecated, Event, Quest, Status, Token,");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("public static class CardOriginPools");
        sb.AppendLine("{");
        sb.AppendLine("    private static readonly Dictionary<string, CardOriginPool> _map =");
        sb.AppendLine("        new(System.StringComparer.Ordinal)");
        sb.AppendLine("        {");
        foreach (var (cardId, poolEnum) in membership)
        {
            sb.AppendLine($"            [\"{cardId}\"] = CardOriginPool.{poolEnum},");
        }
        sb.AppendLine("        };");
        sb.AppendLine();
        sb.AppendLine("    public static CardOriginPool OfCard(string cardId) =>");
        sb.AppendLine("        _map.TryGetValue(cardId, out var p) ? p : CardOriginPool.Unknown;");
        sb.AppendLine();
        sb.AppendLine("    // Character that owns this card, or null if the card belongs to a");
        sb.AppendLine("    // shared / curse / status / token pool that no character \"owns\".");
        sb.AppendLine("    // Use this to decide which Character to start a run with when");
        sb.AppendLine("    // exercising the card in isolation.");
        sb.AppendLine("    public static Character? OwningCharacter(string cardId) =>");
        sb.AppendLine("        OfCard(cardId) switch");
        sb.AppendLine("        {");
        sb.AppendLine("            CardOriginPool.Ironclad    => Character.Ironclad,");
        sb.AppendLine("            CardOriginPool.Silent      => Character.Silent,");
        sb.AppendLine("            CardOriginPool.Defect      => Character.Defect,");
        sb.AppendLine("            CardOriginPool.Regent      => Character.Regent,");
        sb.AppendLine("            CardOriginPool.Necrobinder => Character.Necrobinder,");
        sb.AppendLine("            _                          => null,");
        sb.AppendLine("        };");
        sb.AppendLine("}");
        return sb.ToString();
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
        sb.AppendLine("//   Source: generated by `just build::generate-content-ids` (Sts2Headless --generate-content-ids).");
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
