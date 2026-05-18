using Sts2Headless.Agents;
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
        var pick = state.AvailableMapNodes
            .OrderBy(n => Priority(n.Type, hpRatio))
            .ThenBy(n => n.Col)
            .First();
        return new SelectMapNode(pick.Col, pick.Row);
    }

    // Lower number = preferred.
    private static int Priority(MapNodeType type, double hpRatio)
    {
        if (hpRatio < 0.50)
        {
            return type switch
            {
                MapNodeType.RestSite => 0,
                MapNodeType.Event    => 1,
                MapNodeType.Treasure => 2,
                MapNodeType.Unknown  => 3,
                MapNodeType.Monster  => 4,
                MapNodeType.Merchant => 5,
                MapNodeType.Elite    => 6,
                MapNodeType.Boss     => 7,
                _ => 100,
            };
        }
        if (hpRatio >= 0.80)
        {
            return type switch
            {
                MapNodeType.Elite    => 0,
                MapNodeType.Monster  => 1,
                MapNodeType.Event    => 2,
                MapNodeType.Unknown  => 3,
                MapNodeType.Treasure => 4,
                MapNodeType.Boss     => 5,
                MapNodeType.Merchant => 6,
                MapNodeType.RestSite => 7,
                _ => 100,
            };
        }
        return type switch
        {
            MapNodeType.Monster  => 0,
            MapNodeType.Event    => 1,
            MapNodeType.Unknown  => 2,
            MapNodeType.Elite    => 3,
            MapNodeType.Treasure => 4,
            MapNodeType.Merchant => 5,
            MapNodeType.Boss     => 6,
            MapNodeType.RestSite => 7,
            _ => 100,
        };
    }
}
