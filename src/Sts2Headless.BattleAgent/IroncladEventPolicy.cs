using Sts2Headless.Agents;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.BattleAgent;

// Conservative default: take the last unlocked option ("Leave" by
// engine convention). Same shape HeuristicAgent uses by default; we
// keep it explicit here so a future per-event lookup table can replace
// it as a single class swap. Many event rewards route through the
// CardSelectCmd sub-flow which NREs in headless — leaving sidesteps
// that whole class of crashes at the cost of skipping every event
// reward.
public sealed class IroncladEventPolicy : IEventPolicy
{
    public AgentAction Choose(RunStateResult state)
    {
        for (var i = state.AvailableEventOptions.Count - 1; i >= 0; i--)
        {
            if (!state.AvailableEventOptions[i].IsLocked)
                return new SelectEventOption(state.AvailableEventOptions[i].Index);
        }
        throw new InvalidOperationException(
            "IroncladEventPolicy: event phase with no unlocked options");
    }
}
