using Sts2Headless.BattleAgent.Core;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.BattleAgent.UnitTests;

public sealed class MctsPlannerTests
{
    private static readonly ICombatModel Model = new CombatModel(IroncladCardCatalog.Instance);
    private static readonly IEvaluator Eval = new HeuristicEvaluator();

    [Fact]
    public void EmptyHandPlansEndTurnOnly()
    {
        var state = TestFixtures.State(hand: Array.Empty<SimCard>());
        var planner = new MctsPlanner();
        var plan = planner.PlanTurn(state, Model, Eval, new PlannerBudget(MaxNodes: 1000), default);
        Assert.NotEmpty(plan.Actions);
        Assert.IsType<SimEndTurn>(plan.Actions[^1]);
    }

    [Fact]
    public void FindsLethalSingleStrike()
    {
        var state = TestFixtures.State(
            hand: new[] { TestFixtures.Card(CardId.StrikeIronclad, 0, targetType: TargetType.AnyEnemy) },
            enemies: new[] { TestFixtures.Enemy(hp: 6) });
        var planner = new MctsPlanner();
        var plan = planner.PlanTurn(state, Model, Eval, new PlannerBudget(MaxNodes: 5000), default);
        Assert.True(plan.IsLethal);
        Assert.Contains(plan.Actions, a => a is SimPlayCard);
    }

    [Fact]
    public void FindsLethalWithEnoughSimulations()
    {
        // 18 HP enemy + 3 Strikes (18 dmg total at 3 energy) — needs
        // all three strikes to lethal. With enough simulations MCTS
        // should converge on the lethal line.
        var state = TestFixtures.State(
            energy: 3,
            hand: new[]
            {
                TestFixtures.Card(CardId.StrikeIronclad, 0, targetType: TargetType.AnyEnemy),
                TestFixtures.Card(CardId.StrikeIronclad, 1, targetType: TargetType.AnyEnemy),
                TestFixtures.Card(CardId.StrikeIronclad, 2, targetType: TargetType.AnyEnemy),
            },
            enemies: new[] { TestFixtures.Enemy(hp: 18) });
        var planner = new MctsPlanner();
        var plan = planner.PlanTurn(state, Model, Eval, new PlannerBudget(MaxNodes: 20_000), default);
        Assert.True(plan.IsLethal,
            $"MCTS missed lethal: actions={string.Join(',', plan.Actions.Select(a => a.GetType().Name))} "
            + $"finalEnemyHp={plan.ProjectedEndOfTurnState.Enemies[0].Hp}");
    }

    [Fact]
    public void RespectsCancellation()
    {
        var state = TestFixtures.State(
            hand: new[] { TestFixtures.Card(CardId.StrikeIronclad, 0, targetType: TargetType.AnyEnemy) },
            enemies: new[] { TestFixtures.Enemy(hp: 100) });
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var planner = new MctsPlanner();
        var plan = planner.PlanTurn(state, Model, Eval, new PlannerBudget(MaxNodes: 50_000), cts.Token);
        // Even pre-cancelled, the seed plan must come back.
        Assert.NotEmpty(plan.Actions);
        Assert.IsType<SimEndTurn>(plan.Actions[^1]);
    }

    [Fact]
    public void RespectsBudget()
    {
        var state = TestFixtures.State(
            energy: 3,
            hand: new[]
            {
                TestFixtures.Card(CardId.StrikeIronclad, 0, targetType: TargetType.AnyEnemy),
                TestFixtures.Card(CardId.StrikeIronclad, 1, targetType: TargetType.AnyEnemy),
            },
            enemies: new[] { TestFixtures.Enemy(hp: 100) });
        var budget = new PlannerBudget(MaxNodes: 10);
        var planner = new MctsPlanner();
        var plan = planner.PlanTurn(state, Model, Eval, budget, default);
        Assert.True(plan.NodesExplored <= 15,
            $"explored {plan.NodesExplored} nodes with budget 10");
    }
}
