namespace Sts2Headless.BattleAgent.Core;

// Pluggable turn-planner contract. Given a current SimState, decide
// the full sequence of SimActions for this player turn (ending in
// SimEndTurn). Planners may differ by algorithm (exhaustive,
// MCTS, minimax, learned) but share this interface so SimAgent and
// tests are algorithm-agnostic.
public interface ICombatPlanner
{
    // Plan a full turn. The returned TurnPlan ends with a SimEndTurn,
    // but the agent is free to ignore the EndTurn step if the planner's
    // last play killed all enemies (the engine auto-ends the combat).
    TurnPlan PlanTurn(
        SimState state,
        ICombatModel model,
        IEvaluator evaluator,
        PlannerBudget budget,
        CancellationToken cancellationToken);
}

// Search budget. Hard limits on time and nodes so a planner returns
// even when the optimal turn would be too expensive to enumerate.
public sealed record PlannerBudget(
    int MaxNodes = 50_000,
    TimeSpan? MaxTime = null)
{
    public static PlannerBudget Default { get; } = new();
}

// Output of PlanTurn. The Actions list is in order of execution; the
// last entry is always a SimEndTurn even if Actions has only that one
// entry (i.e. "end turn immediately without playing anything"). The
// projected end-of-turn state is what the planner scored — useful for
// telemetry and for SimAgent's "are we about to die?" check.
public sealed record TurnPlan(
    IReadOnlyList<SimAction> Actions,
    SimState ProjectedEndOfTurnState,
    double Score,
    bool IsLethal,        // killed all enemies before EndTurn
    int NodesExplored);
