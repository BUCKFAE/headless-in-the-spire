using Sts2Headless.Agents;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.BattleAgent;

// Decides what to do at a post-combat reward screen. Pluggable so a
// future MCTS-on-deck-quality policy can replace the heuristic one
// without changing the agent wiring.
public interface IDraftPolicy
{
    AgentAction Choose(RunStateResult state);
}
