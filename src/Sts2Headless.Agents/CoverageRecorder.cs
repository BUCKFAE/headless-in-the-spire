using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Agents;

// Collects per-run content coverage by observing run/state snapshots and the
// agent's actions. Designed to be wired by AgentDriver alongside StallDetector
// so every IAgent run produces coverage data for free.
//
// AXES (what's recorded per kind):
//
//   * Seen     — id appeared in any snapshot. The widest, cheapest axis.
//                e.g. CardsSeen includes everything in CombatState.Hand at any
//                point + all card-reward options offered + all merchant card
//                items. We never see Draw/Discard *contents* (the wire only
//                surfaces counts), but in any normal run cards get drawn into
//                hand at some point, so CardsSeen converges to "deck contents
//                over time" plus offered cards.
//
//   * Played   — agent actually played the card (PlayCard action). Captured
//                pre-dispatch from prevState.CombatState.Hand[ix].Id so the
//                lookup is exact, not inferred from a hand-size delta.
//
//   * Used     — potion used (UsePotion action). Same pre-dispatch capture.
//
//   * Taken    — event option selected (SelectEventOption action). Captured
//                from prevState.AvailableEventOptions[ix].TextKey.
//
//   * Faced    — enemy encountered in combat (Enemies[].MonsterId across
//                every snapshot with a non-empty CombatState).
//
// AXES we DON'T do here (deferred to next slices):
//
//   * Triggered (relic R fired on event E, monster M used move N, power P
//                applied) — needs Harmony-patched coverage events. The
//                inferential MVP doesn't fake-attribute "saw a +15 gold
//                jump → Lucky Fysh fired".
//
//   * Encounters — no wire id today. We have monster ids and combat-state
//                  but no "this fight is encounter X" attribution. Would
//                  need either an engine-side patch or a fingerprint
//                  reconstruction (sorted monster-id tuple → encounter
//                  catalogue match). Defer until coverage gaps make it
//                  worth implementing.
//
// THREAD-SAFETY: not safe for concurrent observers — a single recorder
// per run is the contract. Aggregation across parallel runs happens at
// the CoverageAggregator level by combining per-run Snapshot() reports.
//
// EXTENDING: adding a new axis means (a) a new HashSet field, (b) the
// matching union/delta in Observe / OnAction, (c) a new property on
// CoverageReport. Don't grow this by reading new wire fields without
// also re-running the gap report so the new axis lands with data.
public sealed class CoverageRecorder
{
    private readonly HashSet<string> _cardsSeen = new(StringComparer.Ordinal);
    private readonly HashSet<string> _cardsPlayed = new(StringComparer.Ordinal);
    private readonly HashSet<string> _relicsSeen = new(StringComparer.Ordinal);
    private readonly HashSet<string> _potionsSeen = new(StringComparer.Ordinal);
    private readonly HashSet<string> _potionsUsed = new(StringComparer.Ordinal);
    private readonly HashSet<string> _monstersFaced = new(StringComparer.Ordinal);
    private readonly HashSet<string> _powersSeen = new(StringComparer.Ordinal);
    private readonly HashSet<string> _eventOptionsSeen = new(StringComparer.Ordinal);
    private readonly HashSet<string> _eventOptionsTaken = new(StringComparer.Ordinal);
    private readonly HashSet<string> _relicsTriggered = new(StringComparer.Ordinal);
    private readonly HashSet<string> _cardsTriggered = new(StringComparer.Ordinal);
    private readonly HashSet<string> _monstersTriggered = new(StringComparer.Ordinal);
    private readonly HashSet<string> _potionsTriggered = new(StringComparer.Ordinal);
    private readonly HashSet<string> _powersTriggered = new(StringComparer.Ordinal);
    private readonly HashSet<string> _hooksFired = new(StringComparer.Ordinal);

    public void Observe(RunStateResult state)
    {
        if (state is null) return;

        // Top-level snapshot state — relics + owned potions are visible on
        // every snapshot regardless of room.
        foreach (var r in state.Relics) if (!string.IsNullOrEmpty(r.Id)) _relicsSeen.Add(r.Id);
        foreach (var p in state.OwnedPotions) if (!string.IsNullOrEmpty(p.Id)) _potionsSeen.Add(p.Id);

        // Combat: hand cards, enemies, powers on both sides. CombatState is
        // null outside combat rooms (and during the post-combat reward window).
        if (state.CombatState is CombatState cs)
        {
            // c.Id is a CardId enum value; we store its .ToString() (the
            // PascalCase member name) here and convert to the SCREAMING_SNAKE_CASE
            // wire form once in Snapshot() via NormalizeCardIds. Keeping the
            // hot path string-concat-free matters because Observe runs once
            // per snapshot, and snapshots are 1:1 with agent actions.
            foreach (var c in cs.Hand) _cardsSeen.Add(c.Id.ToString());
            foreach (var e in cs.Enemies)
            {
                if (!string.IsNullOrEmpty(e.MonsterId)) _monstersFaced.Add(e.MonsterId);
                foreach (var pw in e.Powers) if (!string.IsNullOrEmpty(pw.Id)) _powersSeen.Add(pw.Id);
            }
            foreach (var pw in cs.PlayerPowers) if (!string.IsNullOrEmpty(pw.Id)) _powersSeen.Add(pw.Id);
        }

        // Event options visible right now (one entry per option on the
        // current page). The agent doesn't have to pick them for them to
        // count as Seen — they're observable just by being on the page.
        foreach (var eo in state.AvailableEventOptions)
            if (!string.IsNullOrEmpty(eo.TextKey)) _eventOptionsSeen.Add(eo.TextKey);

        // Reward set: card-reward options, relic/potion rewards. Captures
        // content that's offered even if the agent skips it.
        if (state.RewardsState is RewardsState rs)
        {
            foreach (var ro in rs.Available)
            {
                if (!string.IsNullOrEmpty(ro.RelicId)) _relicsSeen.Add(ro.RelicId);
                if (!string.IsNullOrEmpty(ro.PotionId)) _potionsSeen.Add(ro.PotionId);
                if (ro.Cards is { } cards)
                    foreach (var cardOpt in cards) _cardsSeen.Add(cardOpt.Id.ToString());
            }
        }

        // Merchant inventory: same coverage idea — saw it on the shelf,
        // counts as "the game can produce this content here".
        foreach (var mi in state.AvailableMerchantItems)
        {
            if (!string.IsNullOrEmpty(mi.CardId)) _cardsSeen.Add(mi.CardId);
            if (!string.IsNullOrEmpty(mi.RelicId)) _relicsSeen.Add(mi.RelicId);
            if (!string.IsNullOrEmpty(mi.PotionId)) _potionsSeen.Add(mi.PotionId);
        }

        // Trigger window: every model-hook firing since the prev run/state.
        // Per-kind triggered sets (RelicsTriggered, PowersTriggered, …) are
        // strict subsets of the matching Seen sets — a model has to be
        // *active* in a run to fire. HooksFired tracks the distinct hook
        // names observed across all kinds combined; not bound to a manifest
        // universe (the AbstractModel hook surface isn't enumerated as a
        // content kind) but a useful diagnostic for "did this sweep
        // exercise rest-site / merchant / combat-end code paths at all?"
        foreach (var ev in state.TriggeredSincePrev)
        {
            if (!string.IsNullOrEmpty(ev.Hook)) _hooksFired.Add(ev.Hook);
            if (string.IsNullOrEmpty(ev.Source)) continue;
            switch (ev.Kind)
            {
                case TriggerKind.Relic:    _relicsTriggered.Add(ev.Source); break;
                case TriggerKind.Card:     _cardsTriggered.Add(ev.Source); break;
                case TriggerKind.Monster:  _monstersTriggered.Add(ev.Source); break;
                case TriggerKind.Potion:   _potionsTriggered.Add(ev.Source); break;
                case TriggerKind.Power:    _powersTriggered.Add(ev.Source); break;
                // Unknown / future kinds: skip silently — the wire is
                // additive-compatible and an old recorder seeing a new
                // kind should keep working, not crash. The HooksFired
                // set still captures the hook name.
            }
        }
    }

    // Capture content the agent just chose to apply, using the pre-action
    // snapshot the agent saw. We index into the matching list to read the
    // exact id rather than guess from a post-action hand-size delta —
    // mid-turn draws can confound delta inference.
    public void OnAction(RunStateResult prevState, AgentAction action)
    {
        switch (action)
        {
            case PlayCard pc:
                if (prevState.CombatState is CombatState cs
                    && pc.CardIndex >= 0 && pc.CardIndex < cs.Hand.Count)
                {
                    _cardsPlayed.Add(cs.Hand[pc.CardIndex].Id.ToString());
                }
                break;
            case UsePotion up:
                if (up.PotionIndex >= 0 && up.PotionIndex < prevState.OwnedPotions.Count)
                {
                    var id = prevState.OwnedPotions[up.PotionIndex].Id;
                    if (!string.IsNullOrEmpty(id)) _potionsUsed.Add(id);
                }
                break;
            case SelectEventOption eo:
                if (eo.OptionIndex >= 0 && eo.OptionIndex < prevState.AvailableEventOptions.Count)
                {
                    var key = prevState.AvailableEventOptions[eo.OptionIndex].TextKey;
                    if (!string.IsNullOrEmpty(key)) _eventOptionsTaken.Add(key);
                }
                break;
            // No coverage capture for map/rest/reward/merchant-leave/etc. —
            // the resulting state snapshot already pulls the relevant content
            // into the Seen axes (e.g. selected reward shows up as a relic
            // owned next snapshot; rest-site heal doesn't carry content state
            // to attribute).
        }
    }

    // Frozen snapshot of what's been recorded so far. Multiple calls return
    // independent snapshots — the recorder keeps accumulating after.
    public CoverageReport Snapshot()
    {
        // Convert CardId enum-toString'd values back to the canonical wire
        // form (SCREAMING_SNAKE_CASE) so the aggregator can compare against
        // CardIdNames.AllWireNames directly. The runtime stores enum-style
        // names (PascalCase) because we read state.CombatState.Hand[i].Id
        // which is the CardId enum value; converting once here keeps the
        // recorder hot path cheap and the report consumer-friendly.
        return new CoverageReport(
            CardsSeen: NormalizeCardIds(_cardsSeen),
            CardsPlayed: NormalizeCardIds(_cardsPlayed),
            RelicsSeen: Freeze(_relicsSeen),
            PotionsSeen: Freeze(_potionsSeen),
            PotionsUsed: Freeze(_potionsUsed),
            MonstersFaced: Freeze(_monstersFaced),
            PowersSeen: Freeze(_powersSeen),
            EventOptionsSeen: Freeze(_eventOptionsSeen),
            EventOptionsTaken: Freeze(_eventOptionsTaken),
            RelicsTriggered: Freeze(_relicsTriggered),
            CardsTriggered: Freeze(_cardsTriggered),
            MonstersTriggered: Freeze(_monstersTriggered),
            PotionsTriggered: Freeze(_potionsTriggered),
            PowersTriggered: Freeze(_powersTriggered),
            HooksFired: Freeze(_hooksFired));
    }

    private static IReadOnlySet<string> Freeze(HashSet<string> set) =>
        new HashSet<string>(set, StringComparer.Ordinal);

    // CardId enum values surfaced via .ToString() are PascalCase
    // ("StrikeIronclad"), but the wire form is SCREAMING_SNAKE_CASE
    // ("STRIKE_IRONCLAD") and that's what CardIdNames.AllWireNames carries.
    // Rebuild the inverse here so the recorder's report aligns with the
    // manifest's universe set. Unknown enum-name → preserved verbatim so
    // it shows up as a "huh?" in the gap report rather than vanishing.
    private static IReadOnlySet<string> NormalizeCardIds(HashSet<string> pascalNames)
    {
        var enumToWire = BuildEnumNameToWireLookup();
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in pascalNames)
        {
            result.Add(enumToWire.TryGetValue(name, out var wire) ? wire : name);
        }
        return result;
    }

    private static Dictionary<string, string>? _cachedEnumToWire;
    private static Dictionary<string, string> BuildEnumNameToWireLookup()
    {
        if (_cachedEnumToWire is not null) return _cachedEnumToWire;
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var wire in CardIdNames.AllWireNames)
        {
            // SCREAMING_SNAKE_CASE → PascalCase, mirroring GenerateContentIdsCommand.ToPascalCase.
            var pascal = ToPascalCase(wire);
            dict[pascal] = wire;
        }
        _cachedEnumToWire = dict;
        return dict;
    }

    private static string ToPascalCase(string snake)
    {
        var sb = new System.Text.StringBuilder(snake.Length);
        var atWordStart = true;
        foreach (var ch in snake)
        {
            if (ch == '_') { atWordStart = true; continue; }
            sb.Append(atWordStart ? char.ToUpperInvariant(ch) : char.ToLowerInvariant(ch));
            atWordStart = false;
        }
        return sb.ToString();
    }

}

// One run's coverage observations, frozen. Sets are immutable snapshots —
// safe to hand off to a CoverageAggregator for cross-run union.
public sealed record CoverageReport(
    IReadOnlySet<string> CardsSeen,
    IReadOnlySet<string> CardsPlayed,
    IReadOnlySet<string> RelicsSeen,
    IReadOnlySet<string> PotionsSeen,
    IReadOnlySet<string> PotionsUsed,
    IReadOnlySet<string> MonstersFaced,
    IReadOnlySet<string> PowersSeen,
    IReadOnlySet<string> EventOptionsSeen,
    IReadOnlySet<string> EventOptionsTaken,
    // Per-kind triggered sets: models that fired at least one hook during
    // the run (strict subset of the matching seen/owned set, since the
    // model has to be active in a run to fire). Populated from
    // RunStateResult.TriggeredSincePrev — empty if the host's
    // ModelHookPatcher didn't apply.
    IReadOnlySet<string> RelicsTriggered,
    IReadOnlySet<string> CardsTriggered,
    IReadOnlySet<string> MonstersTriggered,
    IReadOnlySet<string> PotionsTriggered,
    IReadOnlySet<string> PowersTriggered,
    // Distinct hook names observed across the run, all kinds combined
    // (AfterCardChangedPiles, AfterDamageReceived, …). Not bound to a
    // manifest universe.
    IReadOnlySet<string> HooksFired);
