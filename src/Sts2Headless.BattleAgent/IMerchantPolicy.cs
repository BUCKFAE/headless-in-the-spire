using Sts2Headless.Agents.Contracts;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.BattleAgent;

// Picks a Buy or Leave action when the agent is in a merchant room.
// The default HeuristicAgent just leaves; an Ironclad agent that wants
// to use its gold needs an explicit policy.
public interface IMerchantPolicy
{
    AgentAction Choose(RunStateResult state);
}
