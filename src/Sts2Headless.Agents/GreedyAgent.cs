using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Agents;

// "Play whatever's in front of you" agent. Picks the first reasonable
// option at every decision point, never looks ahead, never plans energy.
// Purpose is forward progress through a run so end-to-end tests can
// drive multi-room arcs — not to actually win.
//
// Inherits HeuristicAgent and overrides the three phases where it
// matters (map node priority, combat play, reward skip). Every other
// phase uses the HeuristicAgent default, which is itself "do the
// dumbest thing that keeps the run moving" — perfect for the greedy
// posture.
public sealed class GreedyAgent : HeuristicAgent
{
    // Map: greedy priority. Lower number = preferred. Merchant/Treasure
    // are deprioritised because the greedy agent doesn't buy and doesn't
    // value a chest detour — both are still routable (the HeuristicAgent
    // defaults handle the rooms themselves), so the agent will take them
    // rather than throw if a row has nothing else on offer.
    protected override AgentAction DecideMap(RunStateResult state)
    {
        static int Priority(MapNodeType t) => t switch
        {
            MapNodeType.Monster => 0,
            MapNodeType.Elite => 1,
            MapNodeType.Event => 2,
            MapNodeType.Unknown => 3,
            MapNodeType.Boss => 4,
            MapNodeType.RestSite => 5,
            MapNodeType.Merchant => 100,
            MapNodeType.Treasure => 100,
            _ => 200,
        };
        var pick = state.AvailableMapNodes
            .OrderBy(n => Priority(n.Type))
            .ThenBy(n => n.Col)
            .First();
        return new SelectMapNode(pick.Col, pick.Row);
    }

    // Combat: find any affordable card we can legally play. The wire's
    // `canPlay` already encodes engine rules (X-cost, perma-disabled,
    // retain, …), so we trust it and only re-check Cost as a defence
    // in depth.
    protected override AgentAction DecideCombat(RunStateResult state)
    {
        var combat = state.CombatState
            ?? throw new InvalidOperationException(
                $"GreedyAgent: in {state.CurrentRoomType} but combatState is null.");

        var playable = combat.Hand.FirstOrDefault(c => c.CanPlay && c.Cost >= 0 && c.Cost <= combat.Energy);
        if (playable is not null)
        {
            // AnyEnemy is the only target mode that requires a caller-supplied
            // targetIndex; for everything else the wire ignores it. Targeting
            // enemy 0 isn't smart but is always legal so long as at least one
            // enemy is alive — which is guaranteed by IsInProgress.
            var target = playable.TargetType == TargetType.AnyEnemy ? (int?)0 : null;
            return new PlayCard(playable.Index, target);
        }
        return new EndTurn();
    }

    // Event: pick the LAST unlocked option. sts2 by convention puts the
    // "Leave / Decline" choice last; earlier options often route through
    // CardSelectCmd factories that NRE in headless. Picking last
    // sidesteps the broken UI chain at the cost of skipping every event's
    // positive reward — acceptable for a "forward progress" agent.
    //
    // This is the same default as HeuristicAgent provides, so we don't
    // need to override. Kept here as a comment in case someone adds a
    // smarter default upstream.

    // Rewards: skip every skippable card; claim everything else. Same as
    // HeuristicAgent's default (which encodes the same "no model for
    // card quality" stance), so no override needed.
}
