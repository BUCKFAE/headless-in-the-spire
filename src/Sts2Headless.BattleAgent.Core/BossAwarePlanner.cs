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
        // RolloutMultiTurnPlanner tested with N=1/3/5 samples (single-
        // sample variance fix) and greedy/scripted player turns
        // (mean bias fix). Best result was 30/200 vs 35/200 for plain
        // MultiTurn — the rollout's mean is biased no matter how the
        // turn is scripted (greedy-attack over-projects damage,
        // defend-then-attack under-projects damage to the *attack*
        // side). Bias dominates the variance reduction. Reverted.
        // Kept the class and SimState.DeckCardIds plumbing in tree.
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
