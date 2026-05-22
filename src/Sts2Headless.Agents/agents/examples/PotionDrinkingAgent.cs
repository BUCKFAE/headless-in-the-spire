using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Agents;

// Variant of GreedyAgent that uses one owned potion at the start of
// every combat before falling through to greedy card-play. The default
// GreedyAgent never drinks (coverage sweeps record `potions: used: 0`
// across whole campaigns), so the entire potion-effect surface — power
// applications, energy bursts, block grants, conditional reads — is
// invisible to the coverage sweep. This wrapper is the cheapest way to
// surface that content without changing the greedy agent's existing
// baseline.
//
// Strategy: combat round 1, any potion with CanUse == true → drink it,
// targeting enemy 0 when the potion targets AnyEnemy (everything else
// ignores the target index). The pick is the first usable potion in
// belt order — no model for which potion is "best for this combat", we
// just want exhaustive triggering. On subsequent rounds (or when no
// potion is usable), delegate to the wrapped GreedyAgent.
//
// Composes rather than inherits because GreedyAgent is sealed. Every
// non-combat decision (map, rewards, events, …) goes straight to the
// inner GreedyAgent so the wrapper only changes one phase.
public sealed class PotionDrinkingAgent : IAgent
{
    private readonly GreedyAgent _inner = new();

    public AgentAction Decide(RunStateResult state)
    {
        // Only inject the potion when we're at the very start of a live
        // combat — Round=1 with IsPlayPhase=true is the engine's signal
        // that the player has the floor. Anything else (mid-combat,
        // post-combat rewards, map, events) drops straight through to
        // the inner GreedyAgent so the baseline behaviour is unchanged.
        if (state.CombatState is { Round: 1, IsPlayPhase: true, IsInProgress: true })
        {
            var potion = state.OwnedPotions.FirstOrDefault(p => p.CanUse);
            if (potion is not null)
            {
                var target = potion.TargetType == TargetType.AnyEnemy ? (int?)0 : null;
                return new UsePotion(potion.Index, target);
            }
        }

        return _inner.Decide(state);
    }
}
