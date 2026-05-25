using System.Collections;
using System.Reflection;

namespace Sts2Headless.Runtime.Bindings;

// Seed-deterministic peek surface (AD-7 debug counterpart to Reveal).
// These methods leak info that *would be* knowable from the run's seed
// if we could clone-and-fast-forward the engine, but the round-trip
// infrastructure (SerializableRunState save/restore) isn't in place
// in this slice. The peek shape ships scoped down to read-only paths:
//
//   - PeekCardReward: returns the candidate POOL from
//     CardCreationOptions.ForRoom(player, RoomType.CombatRoom).GetPossibleCards(player).
//     This is the *filtered candidate set* the engine samples from, NOT the
//     specific 3-card triplet a real reward roll would produce. Non-mutating.
//
//   - PeekEventOutcome: returns the declarative metadata visible on the
//     EventOption without invoking Chosen() (text key + IsLocked). Full
//     outcome simulation (HP / gold / relics / cards deltas) requires the
//     clone-and-restore path; the returned `Notes` documents the scope.
//
// All reflection here is best-effort: a missing surface returns
// PeekResult.Failure(...) so the caller can soft-fail rather than tear
// down the host. Mirrors the soft-fail posture of Reveal.cs.
public sealed partial class Sts2Bindings
{
    // Wire-shape projection of one entry in the reward pool.
    public sealed record PeekCardEntry(
        string CardId,
        int Cost,
        string Rarity);

    public sealed record PeekCardRewardSnapshot(
        bool Ok,
        string EncounterId,
        IReadOnlyList<PeekCardEntry> Cards,
        string Notes);

    // Drive CardCreationOptions.ForRoom(player, CombatRoom).GetPossibleCards(player)
    // off the live RunState — non-mutating, so no clone needed. The
    // encounterId is informational only (it doesn't influence the card
    // pool, which keys off the player's character + the room type); we
    // echo it back so callers can correlate with the schedule.
    public PeekCardRewardSnapshot PeekCardReward(RunHandle handle, string? encounterId)
    {
        var resolvedEncounterId = encounterId;
        var notes = "Pool-only: lists every card the engine would consider for this room, not a specific 3-card roll.";

        if (string.IsNullOrWhiteSpace(resolvedEncounterId))
        {
            // Best-effort default: peek the next pending normal encounter
            // off the schedule. If no schedule is bound, leave the id blank
            // — the pool itself doesn't depend on the encounter.
            try
            {
                var schedule = ReadActSchedule(handle);
                if (schedule.NormalEncountersVisited < schedule.NormalEncounterIds.Count)
                {
                    resolvedEncounterId = schedule.NormalEncounterIds[schedule.NormalEncountersVisited];
                }
            }
            catch
            {
                // Soft-fail — encounterId stays null/empty, pool still computes.
            }
        }
        resolvedEncounterId ??= string.Empty;

        IEnumerable? candidates;
        try
        {
            candidates = InvokeGetPossibleCardsForCombat(handle);
        }
        catch (Exception ex)
        {
            return new PeekCardRewardSnapshot(
                Ok: false,
                EncounterId: resolvedEncounterId,
                Cards: Array.Empty<PeekCardEntry>(),
                Notes: $"CardCreationOptions invocation failed: {ex.GetType().Name}: {ex.Message}");
        }

        if (candidates is null)
        {
            return new PeekCardRewardSnapshot(
                Ok: false,
                EncounterId: resolvedEncounterId,
                Cards: Array.Empty<PeekCardEntry>(),
                Notes: "CardCreationOptions.ForRoom returned no candidates (engine surface missing or pool empty).");
        }

        var entries = new List<PeekCardEntry>();
        foreach (var card in candidates)
        {
            if (card is null) continue;
            try
            {
                var entry = ProjectCardEntry(card);
                if (entry is not null) entries.Add(entry);
            }
            catch
            {
                // Per-card soft-fail — skip and keep walking the pool.
                continue;
            }
        }

        return new PeekCardRewardSnapshot(
            Ok: true,
            EncounterId: resolvedEncounterId,
            Cards: entries,
            Notes: notes);
    }

    private IEnumerable? InvokeGetPossibleCardsForCombat(RunHandle handle)
    {
        // Resolve CardCreationOptions + RoomType.CombatRoom on demand —
        // peek calls are rare, so we don't pay bind-time cost for them.
        var ccoType = Sts2.GetType("MegaCrit.Sts2.Core.Runs.CardCreationOptions")
            ?? throw new InvalidOperationException("CardCreationOptions type not found");
        var roomTypeEnum = Sts2.GetType("MegaCrit.Sts2.Core.Rooms.RoomType")
            ?? throw new InvalidOperationException("RoomType enum not found");
        // The engine's RoomType enum partitions combat by tier
        // ("Monster" / "Elite" / "Boss") rather than carrying a
        // unified "Combat" / "CombatRoom" member like our wire enum.
        // For the pool query we default to Monster (the most common
        // post-combat reward shape); a future signature could accept
        // a tier hint when the caller knows they just finished an
        // elite / boss fight.
        var combatRoomValue =
            TryEnumValue(roomTypeEnum, "Monster")
            ?? TryEnumValue(roomTypeEnum, "Combat")
            ?? TryEnumValue(roomTypeEnum, "CombatRoom")
            ?? throw new InvalidOperationException(
                $"RoomType enum has no Monster/Combat-flavored member. " +
                $"Known: [{string.Join(", ", Enum.GetNames(roomTypeEnum))}].");

        var forRoom = ccoType.GetMethod(
            "ForRoom",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { _playerType, roomTypeEnum },
            modifiers: null)
            ?? throw new InvalidOperationException("CardCreationOptions.ForRoom(Player, RoomType) not found");

        var options = forRoom.Invoke(null, new[] { handle.Player, combatRoomValue })
            ?? throw new InvalidOperationException("CardCreationOptions.ForRoom returned null");

        var getPossible = ccoType.GetMethod(
            "GetPossibleCards",
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: new[] { _playerType },
            modifiers: null)
            ?? throw new InvalidOperationException("CardCreationOptions.GetPossibleCards(Player) not found");

        var result = getPossible.Invoke(options, new[] { handle.Player });
        return result as IEnumerable;
    }

    // Soft enum lookup by member name. Returns null when the name
    // isn't defined; lets the caller `??`-chain a few candidates
    // without exception-handling.
    private static object? TryEnumValue(Type enumType, string memberName)
    {
        foreach (var name in Enum.GetNames(enumType))
        {
            if (string.Equals(name, memberName, StringComparison.Ordinal))
                return Enum.Parse(enumType, memberName);
        }
        return null;
    }

    private static PeekCardEntry? ProjectCardEntry(object card)
    {
        var cardType = card.GetType();
        var idProp = cardType.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
        if (idProp is null) return null;
        var modelId = idProp.GetValue(card);
        if (modelId is null) return null;
        var entryProp = modelId.GetType().GetProperty("Entry", BindingFlags.Public | BindingFlags.Instance);
        if (entryProp?.GetValue(modelId) is not string wireId) return null;

        // EnergyCost has shape EnergyCost { Base } on CardModel; the engine
        // exposes GetResolved() for the post-modifier cost. Both routes can
        // return Decimal (Stars resource) or int — coerce to int with a
        // sensible default. Unplayable cards return Decimal.MinValue; we
        // surface that as -1.
        var cost = -1;
        var energyProp = cardType.GetProperty("EnergyCost", BindingFlags.Public | BindingFlags.Instance);
        var energyValue = energyProp?.GetValue(card);
        if (energyValue is not null)
        {
            var resolved = energyValue.GetType()
                .GetMethod("GetResolved", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
            var raw = resolved?.Invoke(energyValue, null) ?? energyValue;
            cost = CoerceToInt(raw, fallback: -1);
        }

        var rarity = "Unknown";
        var rarityProp = cardType.GetProperty("Rarity", BindingFlags.Public | BindingFlags.Instance);
        var rarityValue = rarityProp?.GetValue(card);
        if (rarityValue is not null) rarity = rarityValue.ToString() ?? "Unknown";

        return new PeekCardEntry(wireId, cost, rarity);
    }

    private static int CoerceToInt(object value, int fallback)
    {
        try
        {
            return value switch
            {
                int i => i,
                long l => (int)l,
                decimal d => d == decimal.MinValue ? -1 : (int)d,
                double dd => (int)dd,
                float f => (int)f,
                _ => Convert.ToInt32(value),
            };
        }
        catch
        {
            return fallback;
        }
    }

    // ── PeekEventOutcome ─────────────────────────────────────────────────

    public sealed record PeekEventOutcomeSnapshot(
        bool Ok,
        string EventId,
        int OptionIndex,
        int HpDelta,
        int GoldDelta,
        IReadOnlyList<string> RelicsGained,
        IReadOnlyList<string> RelicsLost,
        IReadOnlyList<string> CardsAdded,
        IReadOnlyList<string> CardsRemoved,
        string Notes);

    // Scoped-down "peek": without a SerializableRunState clone surface in
    // place, we can't safely invoke EventOption.Chosen() and roll back.
    // Surfacing the declarative metadata (text key + IsLocked) for the
    // requested option lets callers at least sanity-check option presence
    // and document the simulation gap. All deltas are 0 and lists are
    // empty until the clone path lands.
    public PeekEventOutcomeSnapshot PeekEventOutcome(RunHandle handle, string eventId, int optionIndex)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return new PeekEventOutcomeSnapshot(
                Ok: false,
                EventId: eventId ?? string.Empty,
                OptionIndex: optionIndex,
                HpDelta: 0, GoldDelta: 0,
                RelicsGained: Array.Empty<string>(),
                RelicsLost: Array.Empty<string>(),
                CardsAdded: Array.Empty<string>(),
                CardsRemoved: Array.Empty<string>(),
                Notes: "eventId is empty.");
        }
        if (optionIndex < 0)
        {
            return new PeekEventOutcomeSnapshot(
                Ok: false,
                EventId: eventId,
                OptionIndex: optionIndex,
                HpDelta: 0, GoldDelta: 0,
                RelicsGained: Array.Empty<string>(),
                RelicsLost: Array.Empty<string>(),
                CardsAdded: Array.Empty<string>(),
                CardsRemoved: Array.Empty<string>(),
                Notes: "optionIndex must be >= 0.");
        }

        // Try to resolve the canonical EventModel via ModelDb so we at
        // least confirm the id and surface option count. We do NOT
        // construct an EventRoom or invoke Chosen() — that would mutate
        // the live run.
        string notes;
        int optionsCount;
        bool optionInRange;
        try
        {
            var canonical = ResolveCanonicalEventModel(eventId);
            if (canonical is null)
            {
                return new PeekEventOutcomeSnapshot(
                    Ok: false,
                    EventId: eventId,
                    OptionIndex: optionIndex,
                    HpDelta: 0, GoldDelta: 0,
                    RelicsGained: Array.Empty<string>(),
                    RelicsLost: Array.Empty<string>(),
                    CardsAdded: Array.Empty<string>(),
                    CardsRemoved: Array.Empty<string>(),
                    Notes: $"unknown event id \"{eventId}\".");
            }

            // Canonical events expose option metadata on a static / instance
            // surface that varies between events (some carry a static
            // OptionFactories list, others build options inside Chosen()'s
            // upstream callers). Best-effort probe: look for an Options /
            // OptionFactories enumerable on the canonical model. If we
            // can't find one, fall back to "outcome simulation not
            // available" — same soft-fail posture as the rest of Peek.
            optionsCount = TryReadCanonicalOptionsCount(canonical);
            optionInRange = optionsCount < 0 || optionIndex < optionsCount;
        }
        catch (Exception ex)
        {
            return new PeekEventOutcomeSnapshot(
                Ok: false,
                EventId: eventId,
                OptionIndex: optionIndex,
                HpDelta: 0, GoldDelta: 0,
                RelicsGained: Array.Empty<string>(),
                RelicsLost: Array.Empty<string>(),
                CardsAdded: Array.Empty<string>(),
                CardsRemoved: Array.Empty<string>(),
                Notes: $"event lookup failed: {ex.GetType().Name}: {ex.Message}");
        }

        notes =
            "Outcome simulation not yet implemented — requires SerializableRunState clone/restore, which is not bound. " +
            "Returned zero deltas and empty diff lists. " +
            (optionsCount >= 0
                ? (optionInRange
                    ? $"Event resolved; optionIndex {optionIndex} is in range of {optionsCount} canonical options."
                    : $"Event resolved but optionIndex {optionIndex} is out of range (canonical reports {optionsCount} options).")
                : "Canonical option count is not exposed declaratively; cannot validate optionIndex without invoking Chosen().");

        return new PeekEventOutcomeSnapshot(
            Ok: false,
            EventId: eventId,
            OptionIndex: optionIndex,
            HpDelta: 0, GoldDelta: 0,
            RelicsGained: Array.Empty<string>(),
            RelicsLost: Array.Empty<string>(),
            CardsAdded: Array.Empty<string>(),
            CardsRemoved: Array.Empty<string>(),
            Notes: notes);
    }

    private object? ResolveCanonicalEventModel(string eventId)
    {
        if (_modelIdCtor is null) return null;
        var eventModelType = Sts2.GetType("MegaCrit.Sts2.Core.Models.EventModel");
        var modelDbType = Sts2.GetType("MegaCrit.Sts2.Core.Models.ModelDb");
        if (eventModelType is null || modelDbType is null) return null;

        var getByIdGeneric = modelDbType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "GetById" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1);
        if (getByIdGeneric is null) return null;

        var getByIdEvent = getByIdGeneric.MakeGenericMethod(eventModelType);
        var modelId = _modelIdCtor.Invoke(new object?[] { "EVENT", eventId });

        try
        {
            return getByIdEvent.Invoke(null, new[] { modelId });
        }
        catch
        {
            return null;
        }
    }

    private static int TryReadCanonicalOptionsCount(object canonicalEvent)
    {
        // Best-effort: a canonical EventModel may expose an "Options" or
        // "OptionFactories" enumerable. Walk a small set of candidate
        // property names; return -1 when nothing matches.
        var t = canonicalEvent.GetType();
        string[] candidates = { "Options", "OptionFactories", "OptionsList", "DefaultOptions" };
        foreach (var name in candidates)
        {
            var prop = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var value = prop?.GetValue(canonicalEvent);
            if (value is ICollection coll) return coll.Count;
            if (value is IEnumerable enumerable)
            {
                var count = 0;
                foreach (var _ in enumerable) count++;
                return count;
            }
        }
        return -1;
    }
}
