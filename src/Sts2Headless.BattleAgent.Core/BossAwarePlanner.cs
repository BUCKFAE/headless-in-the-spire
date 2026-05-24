namespace Sts2Headless.BattleAgent.Core;

// Picks between two planners based on combat shape:
//   - "regular" fights (max enemy HP < threshold) → ExhaustivePlanner.
//     Single-turn search is enough and fastest.
//   - "boss" fights (max enemy HP >= threshold OR > 1 high-HP enemy)
//     → MultiTurnExhaustivePlanner. Boss-fight projection of "if this
//     thing stays alive for 2 more turns, what's my HP look like?" is
//     where the planner's myopia bites the agent.
public sealed class BossAwarePlanner : ICombatPlanner
{
    public int BossThreshold { get; }
    public int Lookahead { get; }
    public ICombatPlanner Regular { get; }
    public ICombatPlanner Boss { get; }

    public BossAwarePlanner(
        int bossThreshold = 100,
        int lookahead = 2,
        ICombatPlanner? regular = null,
        ICombatPlanner? boss = null)
    {
        BossThreshold = bossThreshold;
        Lookahead = lookahead;
        Regular = regular ?? new ExhaustivePlanner();
        Boss = boss ?? new MultiTurnExhaustivePlanner(lookaheadTurns: lookahead);
    }

    public TurnPlan PlanTurn(
        SimState rootState,
        ICombatModel model,
        IEvaluator evaluator,
        PlannerBudget budget,
        CancellationToken cancellationToken)
    {
        var planner = IsBossFight(rootState) ? Boss : Regular;
        return planner.PlanTurn(rootState, model, evaluator, budget, cancellationToken);
    }

    private bool IsBossFight(SimState state)
    {
        foreach (var e in state.Enemies)
        {
            if (e.IsDead) continue;
            if (e.MaxHp >= BossThreshold) return true;
        }
        return false;
    }
}
