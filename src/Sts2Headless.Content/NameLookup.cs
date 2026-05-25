using System.Reflection;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Content;

// O(1) cached id→displayName resolver, layered over ContentReader.
//
// Why a separate cache instead of calling ContentReader.FindCard each
// time? FindCard is a linear scan over ModelDb.AllCards (~hundreds of
// entries). The snapshot enrichment path runs per response, touching
// every hand card / relic / power / potion / reward — calling FindCard
// per item would be O(items × pool). The cache amortises the scan by
// walking each pool once and building both an enum→name map (for typed
// CardId/RelicId/etc. lookups) and a wireId→name map (for raw-string
// callers like CardSpec). Subsequent lookups are O(1) hash hits.
//
// Construction is cheap: the indices are lazy, populated only when a
// lookup of that kind happens. A host that never reads cards never pays
// the card-pool walk.
//
// Empty-string fallback is intentional: callers (the wire records) use
// "" as the unset sentinel, so a miss is indistinguishable from a host
// that has no content access at all. Same posture as
// ContentHostMethods.DescribeCard, which returns ok=false but never
// throws on a missing model.
public sealed class NameLookup
{
    private readonly ContentReader _reader;
    private readonly Lazy<KindIndex<CardId>> _cards;
    private readonly Lazy<KindIndex<RelicId>> _relics;
    private readonly Lazy<KindIndex<PotionId>> _potions;
    private readonly Lazy<KindIndex<PowerId>> _powers;
    private readonly Lazy<KindIndex<MonsterId>> _monsters;

    internal NameLookup(ContentReader reader)
    {
        _reader = reader;
        _cards = new(() => BuildIndex<CardId>(_reader.AllCards, CardIdNames.FromWire));
        _relics = new(() => BuildIndex<RelicId>(_reader.AllRelics, RelicIdNames.FromWire));
        _potions = new(() => BuildIndex<PotionId>(_reader.AllPotions, PotionIdNames.FromWire));
        _powers = new(() => BuildIndex<PowerId>(_reader.AllPowers, PowerIdNames.FromWire));
        _monsters = new(BuildMonsterIndex);
    }

    public string Card(CardId id) => _cards.Value.Lookup(id);
    public string Card(string wireId) => _cards.Value.Lookup(wireId);
    public string Relic(RelicId id) => _relics.Value.Lookup(id);
    public string Relic(string wireId) => _relics.Value.Lookup(wireId);
    public string Potion(PotionId id) => _potions.Value.Lookup(id);
    public string Potion(string wireId) => _potions.Value.Lookup(wireId);
    public string Power(PowerId id) => _powers.Value.Lookup(id);
    public string Monster(MonsterId id) => _monsters.Value.Lookup(id);

    private KindIndex<TId> BuildIndex<TId>(
        IReadOnlyList<object> models,
        Func<string, TId> fromWire) where TId : struct, Enum
    {
        var byEnum = new Dictionary<TId, string>();
        var byWire = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var model in models)
        {
            var wireId = _reader.ReadEntryId(model);
            if (string.IsNullOrEmpty(wireId) || byWire.ContainsKey(wireId)) continue;
            var enumValue = fromWire(wireId);
            // sts2 wraps display strings in a `LocString` Godot-localization
            // object whose text we can't resolve without the engine's locale
            // tables (shipped as separate JSON outside sts2.dll). The raw
            // DisplayName / Name properties therefore come back empty or as
            // the LocString class name. Fall back to the C# enum's PascalCase
            // form ("Bash", "BurningBlood", "VulnerablePower"), which mirrors
            // `content/describe_card`'s id.ToString() fallback — still more
            // readable than the SCREAMING_SNAKE wire id, and the only honest
            // option until we wire the localization tables.
            var name = FirstNonEmpty(
                _reader.ReadString(model, "DisplayName"),
                _reader.ReadString(model, "Name"));
            if (string.IsNullOrEmpty(name) || name.StartsWith("MegaCrit.", StringComparison.Ordinal))
                name = enumValue.ToString();
            byWire[wireId] = name;
            byEnum[enumValue] = name;
        }
        return new KindIndex<TId>(byEnum, byWire);
    }

    // Monsters don't live in a flat ModelDb collection — they sit on
    // encounter slots. Walk encounters and pull each monster's id+name.
    private KindIndex<MonsterId> BuildMonsterIndex()
    {
        var byEnum = new Dictionary<MonsterId, string>();
        var byWire = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var encounter in _reader.AllEncounters)
        {
            foreach (var monster in MonsterModelsOf(encounter))
            {
                var wireId = _reader.ReadEntryId(monster);
                if (string.IsNullOrEmpty(wireId) || byWire.ContainsKey(wireId)) continue;
                var enumValue = MonsterIdNames.FromWire(wireId);
                var name = FirstNonEmpty(
                    _reader.ReadString(monster, "DisplayName"),
                    _reader.ReadString(monster, "Name"));
                if (string.IsNullOrEmpty(name) || name.StartsWith("MegaCrit.", StringComparison.Ordinal))
                    name = enumValue.ToString();
                byWire[wireId] = name;
                byEnum[enumValue] = name;
            }
        }
        return new KindIndex<MonsterId>(byEnum, byWire);
    }

    private static IEnumerable<object> MonsterModelsOf(object encounter)
    {
        var slotsProp = encounter.GetType().GetProperty("Slots",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (slotsProp?.GetValue(encounter) is not System.Collections.IEnumerable slots) yield break;
        foreach (var slot in slots)
        {
            if (slot is null) continue;
            var monsterProp = slot.GetType().GetProperty("Monster",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var monster = monsterProp?.GetValue(slot);
            if (monster is not null) yield return monster;
        }
    }

    private static string FirstNonEmpty(params string?[] candidates)
    {
        foreach (var c in candidates)
        {
            if (!string.IsNullOrEmpty(c)) return c!;
        }
        return string.Empty;
    }

    private sealed record KindIndex<TId>(
        IReadOnlyDictionary<TId, string> ByEnum,
        IReadOnlyDictionary<string, string> ByWire) where TId : struct, Enum
    {
        public string Lookup(TId id) => ByEnum.TryGetValue(id, out var name) ? name : string.Empty;
        public string Lookup(string? wireId)
        {
            if (string.IsNullOrEmpty(wireId)) return string.Empty;
            return ByWire.TryGetValue(wireId, out var name) ? name : string.Empty;
        }
    }
}
