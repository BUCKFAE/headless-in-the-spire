using Sts2Headless.Agents;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.BattleAgent;

// Picks the next map node. Pluggable so a future "look ahead at the
// whole map" planner can replace the row-by-row heuristic.
public interface IPathPolicy
{
    AgentAction Choose(RunStateResult state);
}
