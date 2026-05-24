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
    public int BossNodeMultiplier { get; }

    public BossAwarePlanner(
        int bossThreshold = 100,
        int lookahead = 2,
        ICombatPlanner? regular = null,
        ICombatPlanner? boss = null,
        int bossNodeMultiplier = 1)
    {
        BossThreshold = bossThreshold;
        Lookahead = lookahead;
        Regular = regular ?? new ExhaustivePlanner();
        // RolloutMultiTurnPlanner was tested as the boss-branch (200
        // seeds: 17/200 with raw greedy QuickValue, 24/200 after
        // tuning power-card score down). Both regressed vs 33/200 for
        // the fixed-phantom MultiTurn. The rollout's per-turn
        // projection is more accurate on AVERAGE but injects too much
        // variance — the outer planner sees "we'll handle it next
        // turn" projections that didn't match the actual mid-fight
        // hand. Keep the rollout class in tree for re-experimentation
        // with proper averaging / smarter QuickValue, but route the
        // boss branch back to plain MultiTurn.
        Boss = boss ?? new MultiTurnExhaustivePlanner(lookaheadTurns: lookahead);
        BossNodeMultiplier = bossNodeMultiplier;
    }

    public TurnPlan PlanTurn(
        SimState rootState,
        ICombatModel model,
        IEvaluator evaluator,
        PlannerBudget budget,
        CancellationToken cancellationToken)
    {
        if (IsBossFight(rootState))
        {
            var bigger = BossNodeMultiplier == 1
                ? budget
                : budget with { MaxNodes = budget.MaxNodes * BossNodeMultiplier };
            return Boss.PlanTurn(rootState, model, evaluator, bigger, cancellationToken);
        }
        return Regular.PlanTurn(rootState, model, evaluator, budget, cancellationToken);
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
