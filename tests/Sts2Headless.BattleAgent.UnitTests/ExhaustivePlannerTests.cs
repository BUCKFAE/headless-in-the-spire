using Sts2Headless.BattleAgent.Core;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.BattleAgent.UnitTests;

public sealed class ExhaustivePlannerTests
{
    private static readonly ICombatModel Model = new CombatModel(IroncladCardCatalog.Instance);
    private static readonly IEvaluator Eval = new HeuristicEvaluator();
    private static readonly ICombatPlanner Planner = new ExhaustivePlanner();
    private static readonly PlannerBudget Budget = new(MaxNodes: 50_000);

    [Fact]
    public void EmptyHandPlansEndTurnOnly()
    {
        var state = TestFixtures.State(hand: Array.Empty<SimCard>());
        var plan = Planner.PlanTurn(state, Model, Eval, Budget, default);
        Assert.Single(plan.Actions);
        Assert.IsType<SimEndTurn>(plan.Actions[0]);
    }

    [Fact]
    public void FindsLethalSingleStrike()
    {
        // 6 HP enemy + 1 Strike in hand → planner finds lethal.
        var state = TestFixtures.State(
            hand: new[] { TestFixtures.Card(CardId.StrikeIronclad, 0, targetType: TargetType.AnyEnemy) },
            enemies: new[] { TestFixtures.Enemy(hp: 6) });
        var plan = Planner.PlanTurn(state, Model, Eval, Budget, default);
        Assert.True(plan.IsLethal);
        Assert.Equal(2, plan.Actions.Count); // play + end-turn
        Assert.IsType<SimPlayCard>(plan.Actions[0]);
    }

    [Fact]
    public void FindsLethalTwoStrikes()
    {
        // 12 HP enemy + 2 Strikes + 3 energy → planner finds lethal
        // (6+6=12).
        var state = TestFixtures.State(
            energy: 3,
            hand: new[]
            {
                TestFixtures.Card(CardId.StrikeIronclad, 0, targetType: TargetType.AnyEnemy),
                TestFixtures.Card(CardId.StrikeIronclad, 1, targetType: TargetType.AnyEnemy),
            },
            enemies: new[] { TestFixtures.Enemy(hp: 12) });
        var plan = Planner.PlanTurn(state, Model, Eval, Budget, default);
        Assert.True(plan.IsLethal);
        Assert.Equal(3, plan.Actions.Count); // play + play + end-turn
    }

    [Fact]
    public void PrefersVulnerableBeforeAttack()
    {
        // Bash (8 dmg + 2 vuln) + Strike at a tough enemy. The optimal
        // sequence applies Bash first so Strike benefits from
        // Vulnerable. The planner is allowed to find Bash → Strike OR
        // Strike → Bash, but the projected end-of-turn enemy HP must
        // reflect the Vulnerable-amplified strike.
        var state = TestFixtures.State(
            energy: 3,
            hand: new[]
            {
                TestFixtures.Card(CardId.Bash, 0, targetType: TargetType.AnyEnemy),
                TestFixtures.Card(CardId.StrikeIronclad, 1, targetType: TargetType.AnyEnemy),
            },
            enemies: new[] { TestFixtures.Enemy(hp: 100) });
        var plan = Planner.PlanTurn(state, Model, Eval, Budget, default);
        // Best path: Bash(8) → enemy=92 vuln=2 → Strike(6 * 1.5 = 9) → enemy=83.
        // Worst path: Strike(6) → enemy=94 → Bash(8) → enemy=86 vuln=2.
        Assert.Equal(83, plan.ProjectedEndOfTurnState.Enemies[0].Hp);
    }

    [Fact]
    public void PowerBeforeDamage()
    {
        // Inflame (+2 STR) + Strike at single enemy. Optimal:
        // Inflame first → Strike does 6+2=8 dmg.
        var state = TestFixtures.State(
            energy: 3,
            hand: new[]
            {
                TestFixtures.Card(CardId.Inflame, 0),
                TestFixtures.Card(CardId.StrikeIronclad, 1, targetType: TargetType.AnyEnemy),
            },
            enemies: new[] { TestFixtures.Enemy(hp: 100) });
        var plan = Planner.PlanTurn(state, Model, Eval, Budget, default);
        Assert.Equal(92, plan.ProjectedEndOfTurnState.Enemies[0].Hp);
        Assert.Equal(2, plan.ProjectedEndOfTurnState.Status.Strength);
    }

    [Fact]
    public void RespectsNodeBudget()
    {
        // Tiny budget — planner returns a non-null but shallow plan.
        var state = TestFixtures.State(
            energy: 3,
            hand: new[]
            {
                TestFixtures.Card(CardId.StrikeIronclad, 0, targetType: TargetType.AnyEnemy),
                TestFixtures.Card(CardId.StrikeIronclad, 1, targetType: TargetType.AnyEnemy),
                TestFixtures.Card(CardId.StrikeIronclad, 2, targetType: TargetType.AnyEnemy),
            },
            enemies: new[]
            {
                TestFixtures.Enemy(index: 0, hp: 100),
                TestFixtures.Enemy(index: 1, hp: 100),
            });
        var budget = new PlannerBudget(MaxNodes: 5);
        var plan = Planner.PlanTurn(state, Model, Eval, budget, default);
        Assert.True(plan.NodesExplored <= 10, $"explored {plan.NodesExplored} nodes with budget 5");
        Assert.NotEmpty(plan.Actions);
    }

    [Fact]
    public void RespectsCancellationToken()
    {
        var state = TestFixtures.State(
            hand: new[] { TestFixtures.Card(CardId.StrikeIronclad, 0, targetType: TargetType.AnyEnemy) },
            enemies: new[] { TestFixtures.Enemy(hp: 100) });
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var plan = Planner.PlanTurn(state, Model, Eval, Budget, cts.Token);
        // Even pre-cancelled we still return the seeded EndTurn plan.
        Assert.NotEmpty(plan.Actions);
        Assert.IsType<SimEndTurn>(plan.Actions[^1]);
    }

    [Fact]
    public void SkipsHeadlessUnsafeCards()
    {
        // Headbutt (unsafe) + Strike — planner only plays Strike.
        var state = TestFixtures.State(
            energy: 3,
            hand: new[]
            {
                TestFixtures.Card(CardId.Headbutt, 0, targetType: TargetType.AnyEnemy),
                TestFixtures.Card(CardId.StrikeIronclad, 1, targetType: TargetType.AnyEnemy),
            },
            enemies: new[] { TestFixtures.Enemy(hp: 100) });
        var plan = Planner.PlanTurn(state, Model, Eval, Budget, default);
        var played = plan.Actions.OfType<SimPlayCard>().ToList();
        Assert.All(played, p =>
            Assert.NotEqual(CardId.Headbutt, state.Hand[p.HandIndex].Id));
    }

    [Fact]
    public void DefendsWhenIncomingDamageIsLarge()
    {
        // Big incoming attack + Defend available — planner blocks
        // rather than ending turn unblocked.
        var state = TestFixtures.State(
            hp: 40,
            energy: 1,
            hand: new[] { TestFixtures.Card(CardId.DefendIronclad, 0) },
            enemies: new[] { TestFixtures.Enemy(intent: TestFixtures.Attack(damage: 25)) });
        var plan = Planner.PlanTurn(state, Model, Eval, Budget, default);
        Assert.Equal(2, plan.Actions.Count); // Defend + EndTurn
        Assert.IsType<SimPlayCard>(plan.Actions[0]);
    }
}
