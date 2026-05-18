using Sts2Headless.Agents;
using Sts2Headless.BattleAgent.Core;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.BattleAgent;

// The production full-run agent. Inherits SimAgent's combat-planning
// brain and overrides every other phase with the corresponding
// IxxxPolicy.
//
// Phase routing comes from HeuristicAgent's PhaseDetector. Each
// override calls into the injected policy so the agent is a
// composition of pluggable parts — swap any policy without subclassing
// IroncladAgent.
public sealed class IroncladAgent : SimAgent
{
    public IDraftPolicy DraftPolicy { get; }
    public IPathPolicy PathPolicy { get; }
    public IRestPolicy RestPolicy { get; }
    public IEventPolicy EventPolicy { get; }

    public IroncladAgent(
        IDraftPolicy? draftPolicy = null,
        IPathPolicy? pathPolicy = null,
        IRestPolicy? restPolicy = null,
        IEventPolicy? eventPolicy = null,
        ICombatModel? model = null,
        IEvaluator? evaluator = null,
        ICombatPlanner? planner = null,
        PlannerBudget? budget = null)
        : base(model, evaluator, planner, budget)
    {
        DraftPolicy = draftPolicy ?? new IroncladDraftPolicy();
        PathPolicy  = pathPolicy ?? new IroncladPathPolicy();
        RestPolicy  = restPolicy ?? new IroncladRestPolicy();
        EventPolicy = eventPolicy ?? new IroncladEventPolicy();
    }

    protected override AgentAction DecideMap(RunStateResult state) =>
        PathPolicy.Choose(state);

    protected override AgentAction DecideRewards(RunStateResult state) =>
        DraftPolicy.Choose(state);

    protected override AgentAction DecideRestSite(RunStateResult state) =>
        RestPolicy.Choose(state);

    protected override AgentAction DecideEvent(RunStateResult state) =>
        EventPolicy.Choose(state);
}
