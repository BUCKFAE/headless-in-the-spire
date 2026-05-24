using Sts2Headless.Agents.Contracts;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.BattleAgent;

// HP-aware row-by-row node selection. Doesn't plan multiple rows
// ahead — STS1 bots that do (scumthespire's full-map search) need
// considerably more engineering than v1 warrants. The priority bias
// improves on GreedyAgent's flat "monster > elite > everything" by
// preferring rest sites when wounded and elites when fresh.
public sealed class IroncladPathPolicy : IPathPolicy
{
    public AgentAction Choose(RunStateResult state)
    {
        if (state.AvailableMapNodes.Count == 0)
            throw new InvalidOperationException("IroncladPathPolicy: no nodes available");

        var hpRatio = state.MaxHp <= 0 ? 0.0 : (double)state.Hp / state.MaxHp;
        var floorsToBoss = FloorsUntilBoss(state);
        var pick = state.AvailableMapNodes
            .OrderBy(n => Priority(n.Type, hpRatio, floorsToBoss))
            .ThenBy(n => n.Col)
            .First();
        return new SelectMapNode(pick.Col, pick.Row);
    }

    // Approximate how many floors are left until the boss. Each act is
    // ~17 floors; we use the current ActFloor as a proxy (ActFloor 17 is
    // the boss). Returns a non-negative count; negative cases (e.g.,
    // mid-act-transition) clamp to 0.
    private static int FloorsUntilBoss(RunStateResult state)
        => Math.Max(0, 17 - state.ActFloor);

    // Lower number = preferred. Ironclad-specific tuning per the
    // 2026-05 STS2 research deliverable:
    //   - 80 HP base + Burning Blood (+6/fight) lets us take more elites
    //     than other characters. Elite floor cost ≈ 10-15 HP for a relic.
    //   - At >=65% HP we should be fighting elites; at <50% rest is
    //     priority (was 50%).
    //   - Always prefer rest if it's the last floor before boss and we're
    //     not full HP.
    //   - Merchant > Elite when we have gold to spend (>=140 gold).
    private static int Priority(MapNodeType type, double hpRatio, int floorsToBoss)
    {
        // Pre-boss: take a rest no matter what HP, if available.
        if (floorsToBoss <= 1 && type == MapNodeType.RestSite)
            return -1;

        if (hpRatio < 0.45)
        {
            return type switch
            {
                MapNodeType.RestSite => 0,
                MapNodeType.Event    => 1,
                MapNodeType.Treasure => 2,
                MapNodeType.Merchant => 3,
                MapNodeType.Unknown  => 4,
                MapNodeType.Monster  => 5,
                MapNodeType.Elite    => 7,
                MapNodeType.Boss     => 8,
                _ => 100,
            };
        }
        if (hpRatio >= 0.65)
        {
            // Elite-seeking band tested at 0.55 (32/200, too eager,
            // takes elites at low HP), 0.65 (36/200 — current),
            // 0.75 (34/200, too cautious, skips elite floors that
            // give the agent relics it needs to reach the boss).
            return type switch
            {
                MapNodeType.Elite    => 0,
                MapNodeType.Monster  => 1,
                MapNodeType.Merchant => 2,
                MapNodeType.Treasure => 3,
                MapNodeType.Event    => 4,
                MapNodeType.Unknown  => 5,
                MapNodeType.Boss     => 6,
                MapNodeType.RestSite => 7,
                _ => 100,
            };
        }
        // 45-65% HP — mid-game pacing, balanced.
        return type switch
        {
            MapNodeType.Monster  => 0,
            MapNodeType.Treasure => 1,
            MapNodeType.Merchant => 2,
            MapNodeType.Event    => 3,
            MapNodeType.Unknown  => 4,
            MapNodeType.Elite    => 5,
            MapNodeType.Boss     => 6,
            MapNodeType.RestSite => 7,
            _ => 100,
        };
    }
}
