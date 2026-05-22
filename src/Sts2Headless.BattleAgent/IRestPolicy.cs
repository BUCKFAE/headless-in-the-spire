using Sts2Headless.Agents.Contracts;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.BattleAgent;

// Picks HEAL vs SMITH at a rest site. Pluggable so a future policy
// that values particular upgrades over heals (e.g. Demon Form +
// upgrades early) can replace the heuristic.
public interface IRestPolicy
{
    AgentAction Choose(RunStateResult state);
}
