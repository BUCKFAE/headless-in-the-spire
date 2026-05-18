using Sts2Headless.BattleAgent.Core;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.BattleAgent.UnitTests;

public sealed class MultiTurnExhaustivePlannerTests
{
    private static readonly ICombatModel Model = new CombatModel(IroncladCardCatalog.Instance);
    private static readonly IEvaluator Eval = new HeuristicEvaluator();
    private static readonly PlannerBudget Budget = new(MaxNodes: 50_000);

    [Fact]
    public void LookaheadOfOneEqualsExhaustivePlanner()
    {
        // With LookaheadTurns=1, MultiTurnExhaustivePlanner should
        // behave the same as ExhaustivePlanner on a simple state.
        var state = TestFixtures.State(
            hand: new[] { TestFixtures.Card(CardId.StrikeIronclad, 0, targetType: TargetType.AnyEnemy) },
            enemies: new[] { TestFixtures.Enemy(hp: 6) });

        var single = new ExhaustivePlanner().PlanTurn(state, Model, Eval, Budget, default);
        var multi = new MultiTurnExhaustivePlanner(lookaheadTurns: 1).PlanTurn(state, Model, Eval, Budget, default);

        Assert.Equal(single.IsLethal, multi.IsLethal);
        Assert.Equal(single.Actions.Count, multi.Actions.Count);
    }

    [Fact]
    public void RejectsZeroOrNegativeLookahead()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MultiTurnExhaustivePlanner(lookaheadTurns: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MultiTurnExhaustivePlanner(lookaheadTurns: -1));
    }

    [Fact]
    public void PrefersBlockingOverAttackingWhenSecondTurnWouldKill()
    {
        // 8 HP player, 1 enemy intending Attack 6 (one-turn lookahead
        // says "fine, you'll have 2 HP"). One-turn planner has no idea
        // turn 2 also hits for 6, killing us. Multi-turn planner sees
        // it. Hand: Defend (5 block) + Strike (6 dmg).
        //
        // One-turn-optimal: play Strike for 6 dmg, take 6 → end at 2 HP.
        //   But next turn enemy attacks again → 2 - 6 = -4, dead.
        // Multi-turn-optimal: play Defend for 5 block, take 1 dmg →
        //   end at 7 HP. Survives turn 2 (1 unblocked dmg → 6 HP).
        var state = TestFixtures.State(
            hp: 8,
            maxHp: 80,
            energy: 1,
            hand: new[]
            {
                TestFixtures.Card(CardId.DefendIronclad, 0),
                TestFixtures.Card(CardId.StrikeIronclad, 1, targetType: TargetType.AnyEnemy),
            },
            enemies: new[]
            {
                TestFixtures.Enemy(hp: 100, intent: TestFixtures.Attack(damage: 6)),
            });

        var planner = new MultiTurnExhaustivePlanner(lookaheadTurns: 2);
        var plan = planner.PlanTurn(state, Model, Eval, Budget, default);

        // Player should still be alive in the projection.
        Assert.True(plan.ProjectedEndOfTurnState.Hp > 0,
            $"player projected dead with hp={plan.ProjectedEndOfTurnState.Hp}");
        // And the chosen first action should be a play (Defend), not
        // an immediate end-turn.
        Assert.IsType<SimPlayCard>(plan.Actions[0]);
    }

    [Fact]
    public void RewardsKillingBufferOverDamagingTank()
    {
        // Two enemies: a low-HP "buffer" intending Buff (no damage this
        // turn) and a higher-HP "tank" intending Attack 10. With one
        // turn of lookahead, killing the buffer looks irrelevant
        // (it isn't attacking now). With two turns of lookahead, the
        // buffer's attack potential matters.
        //
        // We set up: 2 Strikes (12 dmg) targeting either. Buffer = 6 HP
        // (one Strike kills); Tank = 100 HP (lots of damage to chew
        // through). The planner should kill the buffer.
        var state = TestFixtures.State(
            hp: 80,
            energy: 2,
            hand: new[]
            {
                TestFixtures.Card(CardId.StrikeIronclad, 0, targetType: TargetType.AnyEnemy),
                TestFixtures.Card(CardId.StrikeIronclad, 1, targetType: TargetType.AnyEnemy),
            },
            enemies: new[]
            {
                TestFixtures.Enemy(index: 0, hp: 6, intent: null), // buffer, no damage intent
                TestFixtures.Enemy(index: 1, hp: 100, intent: TestFixtures.Attack(damage: 10)),
            });

        var planner = new MultiTurnExhaustivePlanner(lookaheadTurns: 2);
        var plan = planner.PlanTurn(state, Model, Eval, Budget, default);

        // After plan execution, buffer should be dead.
        Assert.True(plan.ProjectedEndOfTurnState.Enemies[0].IsDead,
            $"buffer survived (hp={plan.ProjectedEndOfTurnState.Enemies[0].Hp}); the planner missed it");
    }

    [Fact]
    public void LethalShortCircuitStillFiresWithLookahead()
    {
        // Single 5-HP enemy + one Strike. Should still hit lethal
        // immediately and return rather than waste budget on projection.
        var state = TestFixtures.State(
            hand: new[] { TestFixtures.Card(CardId.StrikeIronclad, 0, targetType: TargetType.AnyEnemy) },
            enemies: new[] { TestFixtures.Enemy(hp: 5) });

        var planner = new MultiTurnExhaustivePlanner(lookaheadTurns: 3);
        var plan = planner.PlanTurn(state, Model, Eval, Budget, default);

        Assert.True(plan.IsLethal);
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
                TestFixtures.Card(CardId.StrikeIronclad, 2, targetType: TargetType.AnyEnemy),
            },
            enemies: new[]
            {
                TestFixtures.Enemy(index: 0, hp: 100),
                TestFixtures.Enemy(index: 1, hp: 100),
            });

        var budget = new PlannerBudget(MaxNodes: 5);
        var planner = new MultiTurnExhaustivePlanner(lookaheadTurns: 2);
        var plan = planner.PlanTurn(state, Model, Eval, budget, default);
        Assert.True(plan.NodesExplored <= 10);
    }

    [Fact]
    public void NoCardsPlansEndTurnImmediately()
    {
        var state = TestFixtures.State(hand: Array.Empty<SimCard>());
        var planner = new MultiTurnExhaustivePlanner(lookaheadTurns: 2);
        var plan = planner.PlanTurn(state, Model, Eval, Budget, default);
        Assert.Single(plan.Actions);
        Assert.IsType<SimEndTurn>(plan.Actions[0]);
    }
}
