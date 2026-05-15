using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Agents;

// Convenience base for rule-based agents. Splits the single Decide()
// call into one hook per Phase, with defaults that complete a run
// without crashing — every hook makes a forward-progress decision
// even when the subclass hasn't overridden it.
//
// Override what you care about. A subclass that only customises
// DecideCombat still completes runs with reasonable map / event /
// rest-site / merchant / reward behaviour from the defaults.
//
// Mirrors Python's `HeuristicAgent`. Phase priorities (rewards >
// combat > room) come from `PhaseDetector.CurrentPhase`.
public abstract class HeuristicAgent : IAgent
{
    public virtual AgentAction Decide(RunStateResult state) =>
        PhaseDetector.CurrentPhase(state) switch
        {
            Phase.Combat    => DecideCombat(state),
            Phase.Rewards   => DecideRewards(state),
            Phase.Map       => DecideMap(state),
            Phase.MapEmpty  => DecideMapEmpty(state),
            Phase.Event     => DecideEvent(state),
            Phase.RestSite  => DecideRestSite(state),
            Phase.Treasure  => DecideTreasure(state),
            Phase.Merchant  => DecideMerchant(state),
            Phase.Terminal  => throw new NoLegalActionException(
                                "game over — no action available", state),
            Phase.Unknown   => throw new NoLegalActionException(
                                $"no legal action: room={state.CurrentRoomType}, "
                                + $"combatInProgress={state.CombatState?.IsInProgress}, "
                                + $"rewards={state.RewardsState?.Available.Count ?? 0}",
                                state),
            _               => throw new NoLegalActionException(
                                "unhandled phase (compiler should have caught this)", state),
        };

    // ── Per-phase hooks. Defaults below; override what you need. ──────────

    // Default: end the turn. Always legal in the play phase and never
    // voids a run; a subclass that doesn't override this still completes
    // runs, just slowly.
    protected virtual AgentAction DecideCombat(RunStateResult state) => new EndTurn();

    // Default: pick the first node. Smarter agents override to bias by
    // node type (e.g. avoid Merchant if no gold, prefer RestSite when wounded).
    protected virtual AgentAction DecideMap(RunStateResult state)
    {
        var node = state.AvailableMapNodes[0];
        return new SelectMapNode(node.Col, node.Row);
    }

    // Default: advance to the next act. Reached when the engine flipped
    // past the boss and the current act's map has no nodes left.
    protected virtual AgentAction DecideMapEmpty(RunStateResult state) => new EnterNextAct();

    // Default: pick the last unlocked option. sts2 events by convention
    // put the "Leave / Decline" choice last; smarter agents inspect
    // text keys and engage selectively.
    protected virtual AgentAction DecideEvent(RunStateResult state)
    {
        for (var i = state.AvailableEventOptions.Count - 1; i >= 0; i--)
        {
            if (!state.AvailableEventOptions[i].IsLocked)
                return new SelectEventOption(state.AvailableEventOptions[i].Index);
        }
        throw new NoLegalActionException("event phase with no unlocked options", state);
    }

    // Default: prefer HEAL → any enabled non-SMITH → first enabled.
    // SMITH branches into a card-select sub-flow we haven't wired up.
    protected virtual AgentAction DecideRestSite(RunStateResult state)
    {
        var pick = state.AvailableRestSiteOptions.FirstOrDefault(o =>
                       o.IsEnabled
                       && string.Equals(o.OptionId, "HEAL", StringComparison.OrdinalIgnoreCase))
                   ?? state.AvailableRestSiteOptions.FirstOrDefault(o =>
                       o.IsEnabled
                       && !string.Equals(o.OptionId, "SMITH", StringComparison.OrdinalIgnoreCase))
                   ?? state.AvailableRestSiteOptions.FirstOrDefault(o => o.IsEnabled);
        if (pick is null)
            throw new NoLegalActionException("rest site with no enabled options", state);
        return new SelectRestSiteOption(pick.Index);
    }

    // Default: claim the chest. No real player decision today.
    protected virtual AgentAction DecideTreasure(RunStateResult state) => new LeaveTreasureRoom();

    // Default: leave without buying. The greedy posture is "don't spend
    // gold on speculative purchases"; a smarter agent that values its
    // gold overrides to pick BuyMerchantItem for the right items.
    protected virtual AgentAction DecideMerchant(RunStateResult state) => new LeaveMerchantRoom();

    // Default: skip card rewards when allowed (the picker has no model
    // for "is this card good for me"); claim everything else (gold,
    // relics, potions). Smarter agents override with a DraftScore table.
    protected virtual AgentAction DecideRewards(RunStateResult state)
    {
        // PhaseDetector guarantees Available is non-empty when we get here.
        var head = state.RewardsState!.Available[0];
        if (head.Kind == RewardKind.Card)
        {
            if (head.CanSkip)
                return new SkipReward(head.Index);
            // Forced (non-skippable) card pick — take the first option.
            var cardIdx = (head.Cards?.Count ?? 0) > 0 ? head.Cards![0].Index : 0;
            return new SelectReward(head.Index, cardIdx);
        }
        return new SelectReward(head.Index);
    }
}
