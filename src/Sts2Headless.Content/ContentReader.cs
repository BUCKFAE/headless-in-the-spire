using System.Collections;
using System.Reflection;
using Sts2Headless.Protocol.Methods;
using Sts2Headless.Runtime.Bindings;

namespace Sts2Headless.Content;

// Lazy reflection wrapper over sts2.dll's ModelDb + per-model property
// surface. One instance per host process (constructed by
// ContentHostMethods.Build); resolves types/properties on first access
// and caches them.
//
// Design rules:
//   - Never throw on missing reflection targets — content methods must
//     never break host startup. A missing model collection returns
//     empty; a missing property on a model returns the empty string.
//   - Don't load sts2 here — accept the Assembly handle from the
//     existing Sts2Bindings (which already owns the load) so AD-4
//     stays intact (no compile-time reference to sts2).
//   - Keep this file free of wire-DTO construction — ContentHostMethods
//     does that. We surface raw strings / numbers / id-collections.
internal sealed class ContentReader
{
    private readonly Assembly _sts2;

    // Lazy ModelDb surface — populated on first call. Each `Lazy<T>` is
    // null-safe: a missing collection resolves to an empty list, not a
    // null reference.
    private readonly Lazy<Type?> _modelDbType;
    private readonly Lazy<IReadOnlyList<object>> _allCards;
    private readonly Lazy<IReadOnlyList<object>> _allRelics;
    private readonly Lazy<IReadOnlyList<object>> _allPotions;
    private readonly Lazy<IReadOnlyList<object>> _allPowers;
    private readonly Lazy<IReadOnlyList<object>> _allEvents;
    private readonly Lazy<IReadOnlyList<object>> _allSharedEvents;
    private readonly Lazy<IReadOnlyList<object>> _allEncounters;
    private readonly Lazy<IReadOnlyList<object>> _allAfflictions;
    private readonly Lazy<IReadOnlyList<object>> _allEnchantments;
    private readonly Lazy<IReadOnlyList<object>> _allModifiers;
    private readonly Lazy<IReadOnlyList<object>> _allActs;

    public ContentReader(Assembly sts2)
    {
        _sts2 = sts2;
        _modelDbType = new Lazy<Type?>(() =>
            _sts2.GetType("MegaCrit.Sts2.Core.Models.ModelDb")
            ?? _sts2.GetType("MegaCrit.Sts2.Core.ModelDb"));
        _allCards = LazyStatic("AllCards");
        _allRelics = LazyStatic("AllRelics");
        _allPotions = LazyStatic("AllPotions");
        _allPowers = LazyStatic("AllPowers");
        _allEvents = LazyStatic("AllEvents");
        _allSharedEvents = LazyStatic("AllSharedEvents");
        _allEncounters = LazyStatic("AllEncounters");
        _allAfflictions = LazyStatic("AllAfflictions");
        _allEnchantments = LazyStatic("AllEnchantments");
        _allModifiers = LazyStatic("AllModifiers");
        _allActs = LazyStatic("AllActs");
    }

    private Lazy<IReadOnlyList<object>> LazyStatic(string propName) =>
        new(() =>
        {
            var md = _modelDbType.Value;
            if (md is null) return Array.Empty<object>();
            var prop = md.GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (prop is null) return Array.Empty<object>();
            var v = prop.GetValue(null);
            return ReadList(v);
        });

    private static IReadOnlyList<object> ReadList(object? value)
    {
        if (value is not IEnumerable enumerable) return Array.Empty<object>();
        var result = new List<object>();
        foreach (var item in enumerable)
        {
            if (item is not null) result.Add(item);
        }
        return result;
    }

    // ── public surface used by ContentHostMethods ──────────────────────

    public object? FindCard(CardId id) => FindByWireId(_allCards.Value, ToWire(id, CardIdNames.AllWireNames));
    public object? FindRelic(RelicId id) => FindByWireId(_allRelics.Value, ToWire(id, RelicIdNames.AllWireNames));
    public object? FindPotion(PotionId id) => FindByWireId(_allPotions.Value, ToWire(id, PotionIdNames.AllWireNames));
    public object? FindPower(PowerId id) => FindByWireId(_allPowers.Value, ToWire(id, PowerIdNames.AllWireNames));
    public object? FindAffliction(AfflictionId id) => FindByWireId(_allAfflictions.Value, ToWire(id, AfflictionIdNames.AllWireNames));
    public object? FindEnchantment(EnchantmentId id) => FindByWireId(_allEnchantments.Value, ToWire(id, EnchantmentIdNames.AllWireNames));
    public object? FindModifier(ModifierId id) => FindByWireId(_allModifiers.Value, ToWire(id, ModifierIdNames.AllWireNames));
    public object? FindMonster(MonsterId id) => FindMonsterByWire(ToWire(id, MonsterIdNames.AllWireNames));

    public object? FindEvent(string wireId)
    {
        if (string.IsNullOrEmpty(wireId)) return null;
        return FindByWireId(_allEvents.Value, wireId)
            ?? FindByWireId(_allSharedEvents.Value, wireId);
    }

    public object? FindEncounter(string wireId)
    {
        if (string.IsNullOrEmpty(wireId)) return null;
        return FindByWireId(_allEncounters.Value, wireId);
    }

    public IReadOnlyList<object> AllCards => _allCards.Value;
    public IReadOnlyList<object> AllRelics => _allRelics.Value;
    public IReadOnlyList<object> AllPotions => _allPotions.Value;
    public IReadOnlyList<object> AllPowers => _allPowers.Value;
    public IReadOnlyList<object> AllEvents => _allEvents.Value;
    public IReadOnlyList<object> AllSharedEvents => _allSharedEvents.Value;
    public IReadOnlyList<object> AllEncounters => _allEncounters.Value;
    public IReadOnlyList<object> AllActs => _allActs.Value;

    // Pick the act instance whose `ActIndex` (0-based) matches. Returns
    // null when the index is out of range or no source resolves it. The
    // 0-based axis is the wire's convention; sts2 stores Act 1 at index 0.
    //
    // Source preference:
    //   1. `ModelDb.AllActs` — if the engine ever surfaces acts as a
    //      ModelDb collection. At the current pin this is empty: sts2
    //      has only a single `ActModel` type and instances live on the
    //      live `RunState`, not in the model catalogue. The walk stays
    //      here as a forward-compatibility hook.
    //   2. The live `RunState.Act` reachable through the supplied
    //      `RunHandle`. Only the *current* act is reachable this way;
    //      callers asking for arbitrary act indices outside that one
    //      get null with a clear "no such act in catalogue" outcome.
    public object? FindAct(int actIndex, RunHandle? handle = null)
    {
        if (actIndex < 0) return null;

        // Source 1: static ModelDb.AllActs (empty at the current pin —
        // kept for future-compat).
        var list = _allActs.Value;
        if (actIndex < list.Count) return list[actIndex];
        foreach (var act in list)
        {
            var idx = ReadInt(act, "ActIndex") ?? ReadInt(act, "Index");
            if (idx == actIndex) return act;
        }

        // Source 2: the live RunState's current ActModel. Only matches
        // when the caller's requested actIndex is the *current* act.
        // The current act number lives on RunState.CurrentActIndex —
        // the ActModel itself doesn't carry an Index property at the
        // current pin (single concrete ActModel class; per-act variation
        // is on the instance's data fields, not its CLR type).
        if (handle is not null)
        {
            var act = ReadRunStateAct(handle);
            if (act is not null)
            {
                var currentIdx = ReadCurrentActIndex(handle);
                if (currentIdx == actIndex) return act;
            }
        }

        return null;
    }

    private static int? ReadCurrentActIndex(RunHandle handle)
    {
        try
        {
            var runState = handle.RunState;
            var prop = runState.GetType().GetProperty(
                "CurrentActIndex",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop?.GetValue(runState) is int n) return n;
        }
        catch
        {
            // swallow — content methods soft-fail rather than poison the wire.
        }
        return null;
    }

    // ── runtime-only reads ────────────────────────────────────────────

    // Pull `RunState.Act` off the handle by reflection. Mirrors the
    // pattern Sts2Bindings.Reveal uses (`_runStateAct?.GetValue(...)`);
    // we don't share the cached PropertyInfo because ContentReader is
    // not coupled to the Sts2Bindings field surface. Soft-fail on any
    // reflection hiccup — content methods must never break the host.
    private object? ReadRunStateAct(RunHandle handle)
    {
        try
        {
            var runState = handle.RunState;
            var prop = runState.GetType().GetProperty(
                "Act",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return prop?.GetValue(runState);
        }
        catch
        {
            return null;
        }
    }

    // Read the per-run unknown-node base odds out of
    // `RunState.Odds.UnknownMapPoint._baseOdds : Dictionary<RoomType, float>`.
    // Returns null when the runtime instance isn't reachable (no active
    // run, reflection mismatch, …) so callers can fall back to the
    // documented static priors.
    //
    // The dictionary keys are the engine's own `MegaCrit.Sts2.Core.Rooms.RoomType`
    // enum, whose member names line up with our wire `RoomType` enum
    // (MapRoom, EventRoom, CombatRoom, RestSiteRoom, MerchantRoom,
    // TreasureRoom, …). We parse via `Enum.TryParse<RoomType>(name,
    // ignoreCase: true)` so a future rename / new variant degrades to
    // RoomType.Unknown (and is filtered out) rather than throwing.
    public IReadOnlyList<(RoomType RoomType, double Weight)>? ReadUnknownNodeOdds(RunHandle handle)
    {
        try
        {
            var runState = handle.RunState;
            var oddsProp = runState.GetType().GetProperty(
                "Odds", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var oddsSet = oddsProp?.GetValue(runState);
            if (oddsSet is null) return null;

            var ump = oddsSet.GetType().GetProperty(
                "UnknownMapPoint", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(oddsSet);
            if (ump is null) return null;

            var baseField = ump.GetType().GetField(
                "_baseOdds", BindingFlags.NonPublic | BindingFlags.Instance);
            if (baseField?.GetValue(ump) is not IDictionary dict) return null;

            var rows = new List<(RoomType, double)>();
            foreach (DictionaryEntry entry in dict)
            {
                if (entry.Key is null) continue;
                var name = entry.Key.ToString();
                if (string.IsNullOrEmpty(name)) continue;
                if (!Enum.TryParse<RoomType>(name, ignoreCase: true, out var rt)
                    || rt == RoomType.Unknown)
                {
                    continue;
                }
                var weight = entry.Value is null ? 0.0 : Convert.ToDouble(entry.Value);
                rows.Add((rt, weight));
            }
            return rows.Count == 0 ? null : rows;
        }
        catch
        {
            return null;
        }
    }

    // ── id-to-wire conversion ─────────────────────────────────────────

    // Resolve the wire string for an id enum value by reverse-walking
    // AllWireNames. Cached lazily per enum-type via the `wireNames`
    // collection the caller supplies. Returns null for Unknown / missing.
    private static string? ToWire<T>(T id, IReadOnlyCollection<string> wireNames) where T : struct, Enum
    {
        // Special-case: Unknown is a sentinel; surface as null.
        var unknown = (T)Enum.ToObject(typeof(T), 0);
        if (Equals(id, unknown)) return null;

        // The generated *IdNames map (wire string → enum value). We need
        // the reverse. Walk the wire names, parse each through FromWire,
        // and pick the one that matches the requested enum.
        var fromWire = typeof(T).Assembly.GetType($"Sts2Headless.Protocol.Methods.{typeof(T).Name}Names")
            ?.GetMethod("FromWire", BindingFlags.Public | BindingFlags.Static);
        if (fromWire is null) return null;
        foreach (var wire in wireNames)
        {
            var parsed = fromWire.Invoke(null, new object?[] { wire });
            if (parsed is T t && Equals(t, id)) return wire;
        }
        return null;
    }

    // ── per-model reads ────────────────────────────────────────────────

    public string ReadString(object? model, string propName)
    {
        if (model is null) return string.Empty;
        var prop = model.GetType().GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (prop is null) return string.Empty;
        try
        {
            return prop.GetValue(model)?.ToString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    public int? ReadInt(object? model, string propName)
    {
        if (model is null) return null;
        var prop = model.GetType().GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (prop is null) return null;
        try
        {
            var v = prop.GetValue(model);
            return v is null ? null : Convert.ToInt32(v);
        }
        catch
        {
            return null;
        }
    }

    public bool ReadBool(object? model, string propName)
    {
        if (model is null) return false;
        var prop = model.GetType().GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (prop is null) return false;
        try
        {
            return prop.GetValue(model) is bool b && b;
        }
        catch
        {
            return false;
        }
    }

    // Return the wire id of a model (its Id.Entry string), or null.
    public string? ReadEntryId(object? model)
    {
        if (model is null) return null;
        var idProp = model.GetType().GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
        if (idProp is null) return null;
        try
        {
            var idObj = idProp.GetValue(model);
            if (idObj is null) return null;
            var entryProp = idObj.GetType().GetProperty("Entry", BindingFlags.Public | BindingFlags.Instance);
            if (entryProp is null) return idObj.ToString();
            return entryProp.GetValue(idObj)?.ToString();
        }
        catch
        {
            return null;
        }
    }

    // Walk a model's IEnumerable property and pull each entry's wire id.
    // Used by per-act encounter / event pool surfacing.
    public IReadOnlyList<string> ReadIdList(object? model, string propName)
    {
        if (model is null) return Array.Empty<string>();
        var prop = model.GetType().GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (prop is null) return Array.Empty<string>();
        try
        {
            if (prop.GetValue(model) is not IEnumerable enumerable) return Array.Empty<string>();
            var result = new List<string>();
            foreach (var entry in enumerable)
            {
                var id = ReadEntryId(entry);
                if (id is not null) result.Add(id);
            }
            return result;
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    // Encounter -> monster id list. Encounters reference monsters via
    // `Slots` (each slot has a MonsterModel reference) or via an
    // `AllMonsters`-style property. Try both and dedupe.
    public IReadOnlyList<string> ReadEncounterMonsterIds(object? encounter)
    {
        if (encounter is null) return Array.Empty<string>();
        var slotsIds = ReadIdList(encounter, "Slots")
            .Concat(ReadIdList(encounter, "MonsterSlots"))
            .Concat(ReadIdList(encounter, "AllMonsters"))
            .ToList();
        // Slot entries may carry a wrapper (Slot.Monster.Id) rather than
        // a direct Id — try the property chain too.
        if (slotsIds.Count == 0)
        {
            slotsIds.AddRange(ReadEncounterMonsterSlots(encounter));
        }
        // Dedupe preserving order.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var ordered = new List<string>();
        foreach (var id in slotsIds)
        {
            if (id is null || !seen.Add(id)) continue;
            ordered.Add(id);
        }
        return ordered;
    }

    private IEnumerable<string> ReadEncounterMonsterSlots(object encounter)
    {
        var slotsProp = encounter.GetType().GetProperty("Slots", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (slotsProp is null) yield break;
        if (slotsProp.GetValue(encounter) is not IEnumerable slots) yield break;
        foreach (var slot in slots)
        {
            if (slot is null) continue;
            var monsterProp = slot.GetType().GetProperty("Monster", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var monster = monsterProp?.GetValue(slot);
            var id = ReadEntryId(monster);
            if (id is not null) yield return id;
        }
    }

    // ── helpers ───────────────────────────────────────────────────────

    private object? FindByWireId(IReadOnlyList<object> list, string? wireId)
    {
        if (string.IsNullOrEmpty(wireId)) return null;
        foreach (var entry in list)
        {
            if (string.Equals(ReadEntryId(entry), wireId, StringComparison.Ordinal))
                return entry;
        }
        return null;
    }

    // Monsters aren't a top-level ModelDb collection — they live as
    // referenced models on encounters. Walk every encounter's slot list
    // and surface the first match.
    private object? FindMonsterByWire(string? wireId)
    {
        if (string.IsNullOrEmpty(wireId)) return null;
        foreach (var encounter in _allEncounters.Value)
        {
            var slotsProp = encounter.GetType().GetProperty("Slots", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (slotsProp?.GetValue(encounter) is not IEnumerable slots) continue;
            foreach (var slot in slots)
            {
                if (slot is null) continue;
                var monsterProp = slot.GetType().GetProperty("Monster", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var monster = monsterProp?.GetValue(slot);
                if (monster is null) continue;
                if (string.Equals(ReadEntryId(monster), wireId, StringComparison.Ordinal))
                    return monster;
            }
        }
        return null;
    }
}
