using Sts2Headless.Agents.Contracts;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.BattleAgent;

// Picks an option at an event room. Pluggable so a future per-event
// lookup table can replace the safe-default policy used here.
public interface IEventPolicy
{
    AgentAction Choose(RunStateResult state);
}
