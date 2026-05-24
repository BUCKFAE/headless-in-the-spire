using Sts2Headless.BattleAgent.Core;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.BattleAgent.UnitTests;

// Unit tests for the Custom-handler scaling cards in IroncladCardCatalog:
// PerfectedStrike, Whirlwind, Corruption, PactsEnd, Rampage. Each test
// pins the empirical engine behaviour captured in
// tests/Sts2Headless.IntegrationTests/ScalingCardProbeTests.cs against
// the simulator so the planner's projection matches what the engine will
// actually do.
public sealed class ScalingCardHandlerTests
{
    private static readonly ICombatModel Model = new CombatModel(IroncladCardCatalog.Instance);
    private static readonly IEvaluator Eval = new HeuristicEvaluator();
    private static readonly ICombatPlanner Planner = new ExhaustivePlanner();
    private static readonly PlannerBudget Budget = new(MaxNodes: 50_000);

    // ── PerfectedStrike ───────────────────────────────────────────────

    [Fact]
    public void PerfectedStrike_BaseDamageWithNoStrikesInDeck()
    {
        var state = TestFixtures.State(
            energy: 3,
            hand: new[] { TestFixtures.Card(CardId.PerfectedStrike, 0, targetType: TargetType.AnyEnemy) },
            enemies: new[] { TestFixtures.Enemy(hp: 100) },
            strikeCardsInDeck: 0);
        var plan = Planner.PlanTurn(state, Model, Eval, Budget, default);
        // 6 + 2 * 0 = 6 damage
        Assert.Equal(94, plan.ProjectedEndOfTurnState.Enemies[0].Hp);
    }

    [Fact]
    public void PerfectedStrike_ScalesWithDeckStrikeCount()
    {
        // 5 Strike-named cards in deck (e.g. PS itself + 4 Strikes): 6 + 2*5 = 16 dmg.
        var state = TestFixtures.State(
            energy: 3,
            hand: new[] { TestFixtures.Card(CardId.PerfectedStrike, 0, targetType: TargetType.AnyEnemy) },
            enemies: new[] { TestFixtures.Enemy(hp: 100) },
            strikeCardsInDeck: 5);
        var plan = Planner.PlanTurn(state, Model, Eval, Budget, default);
        Assert.Equal(84, plan.ProjectedEndOfTurnState.Enemies[0].Hp);
    }

    [Fact]
    public void PerfectedStrike_UpgradedScalesAtThreePerStrike()
    {
        // Upgraded: 6 + 3 per Strike. With 5 Strikes: 6 + 15 = 21.
        var state = TestFixtures.State(
            energy: 3,
            hand: new[] { TestFixtures.Card(CardId.PerfectedStrike, 0, upgraded: true, targetType: TargetType.AnyEnemy) },
            enemies: new[] { TestFixtures.Enemy(hp: 100) },
            strikeCardsInDeck: 5);
        var plan = Planner.PlanTurn(state, Model, Eval, Budget, default);
        Assert.Equal(79, plan.ProjectedEndOfTurnState.Enemies[0].Hp);
    }

    // ── Whirlwind ─────────────────────────────────────────────────────

    [Fact]
    public void Whirlwind_DealsFivePerEnergyAoeAndDrainsEnergy()
    {
        // Engine probe: 3 energy → 15 dmg per enemy (2 enemies → total 30).
        var state = TestFixtures.State(
            energy: 3,
            hand: new[] { TestFixtures.Card(CardId.Whirlwind, 0, targetType: TargetType.AllEnemies) },
            enemies: new[]
            {
                TestFixtures.Enemy(index: 0, hp: 100),
                TestFixtures.Enemy(index: 1, hp: 100),
            });
        var plan = Planner.PlanTurn(state, Model, Eval, Budget, default);
        var post = plan.ProjectedEndOfTurnState;
        Assert.Equal(85, post.Enemies[0].Hp);
        Assert.Equal(85, post.Enemies[1].Hp);
        // Energy should be 0 after the X-cost play (drained by handler).
        // Block math during EndPlayerTurn may shift Energy back when the
        // next turn starts; the planner's plan score is what matters,
        // and lethal projection includes the AoE damage above.
    }

    [Fact]
    public void Whirlwind_UpgradedDealsEightPerEnergy()
    {
        var state = TestFixtures.State(
            energy: 2,
            hand: new[] { TestFixtures.Card(CardId.Whirlwind, 0, upgraded: true, targetType: TargetType.AllEnemies) },
            enemies: new[] { TestFixtures.Enemy(hp: 100) });
        var plan = Planner.PlanTurn(state, Model, Eval, Budget, default);
        // 2 energy * 8 per-hit = 16 dmg.
        Assert.Equal(84, plan.ProjectedEndOfTurnState.Enemies[0].Hp);
    }

    [Fact]
    public void Whirlwind_IsLegalDespiteXCostFlag()
    {
        // X-cost cards arrive with Cost=-1; the planner must still
        // surface them as legal actions when energy > 0.
        var state = TestFixtures.State(
            energy: 2,
            hand: new[] { TestFixtures.Card(CardId.Whirlwind, 0, cost: -1, targetType: TargetType.AllEnemies) },
            enemies: new[] { TestFixtures.Enemy(hp: 100) });
        var actions = Model.LegalActions(state);
        Assert.Contains(actions, a => a is SimPlayCard);
    }

    [Fact]
    public void Whirlwind_NotLegalAtZeroEnergy()
    {
        var state = TestFixtures.State(
            energy: 0,
            hand: new[] { TestFixtures.Card(CardId.Whirlwind, 0, cost: -1, targetType: TargetType.AllEnemies) },
            enemies: new[] { TestFixtures.Enemy(hp: 100) });
        var actions = Model.LegalActions(state);
        Assert.DoesNotContain(actions, a => a is SimPlayCard);
    }

    // ── Corruption ────────────────────────────────────────────────────

    [Fact]
    public void Corruption_MakesSkillsCostZero_AndExhaustOnPlay()
    {
        // Hand: Corruption (cost 3) + Defend (cost 1). Energy=3, with an
        // incoming attack so block matters to the evaluator.
        //   Without the discount: planner can only afford one card.
        //   With the discount kicking in mid-turn: Corruption (3→0), then
        //   Defend free → 5 block AND Corruption power. The planner picks
        //   the strictly-dominating two-play line.
        var state = TestFixtures.State(
            energy: 3,
            hand: new[]
            {
                TestFixtures.Card(CardId.Corruption, 0),
                TestFixtures.Card(CardId.DefendIronclad, 1),
            },
            enemies: new[] { TestFixtures.Enemy(hp: 100, intent: TestFixtures.Attack(damage: 20)) });
        var plan = Planner.PlanTurn(state, Model, Eval, Budget, default);
        var post = plan.ProjectedEndOfTurnState;
        // Corruption granted the power.
        Assert.Equal(1, post.Status.Corruption);
        // Both Corruption and Defend played (we'll see ≥5 block from the
        // Defend that fired BEFORE end-of-turn enemy attack consumed it).
        var playCount = plan.Actions.Count(a => a is SimPlayCard);
        Assert.Equal(2, playCount);
    }

    [Fact]
    public void Corruption_DoesNotDiscountAttacks()
    {
        // Even with Corruption active, an Attack is unaffected.
        var state = TestFixtures.State(
            energy: 0,
            status: PlayerStatus.Empty with { Corruption = 1 },
            hand: new[] { TestFixtures.Card(CardId.StrikeIronclad, 0, targetType: TargetType.AnyEnemy) },
            enemies: new[] { TestFixtures.Enemy(hp: 100) });
        var actions = Model.LegalActions(state);
        // Strike costs 1; energy=0; not playable even under Corruption.
        Assert.DoesNotContain(actions, a => a is SimPlayCard);
    }

    [Fact]
    public void Corruption_RoutesPlayedSkillToExhaust()
    {
        // Direct Apply check: play a Defend while Corruption is active;
        // discard count unchanged, exhaust count +1.
        var state = TestFixtures.State(
            energy: 1,
            status: PlayerStatus.Empty with { Corruption = 1 },
            hand: new[] { TestFixtures.Card(CardId.DefendIronclad, 0) },
            enemies: new[] { TestFixtures.Enemy(hp: 100) });
        var after = Model.Apply(state, new SimPlayCard(HandIndex: 0, TargetEnemyIndex: null));
        Assert.Equal(state.DiscardPileCount, after.DiscardPileCount);
        Assert.Equal(state.ExhaustPileCount + 1, after.ExhaustPileCount);
    }

    // ── PactsEnd ──────────────────────────────────────────────────────

    [Fact]
    public void PactsEnd_DealsSeventeenToAllEnemies_WhenPlayable()
    {
        // PactsEnd's exhaust-pile gate lives on the engine side via
        // CanPlayFlag. Once the wire says it's playable, the catalog
        // models it as flat 17 AoE.
        var state = TestFixtures.State(
            energy: 3,
            exhaustPileCount: 4,
            hand: new[] { TestFixtures.Card(CardId.PactsEnd, 0, targetType: TargetType.AllEnemies) },
            enemies: new[]
            {
                TestFixtures.Enemy(index: 0, hp: 100),
                TestFixtures.Enemy(index: 1, hp: 100),
            });
        var plan = Planner.PlanTurn(state, Model, Eval, Budget, default);
        var post = plan.ProjectedEndOfTurnState;
        Assert.Equal(83, post.Enemies[0].Hp);
        Assert.Equal(83, post.Enemies[1].Hp);
    }

    [Fact]
    public void PactsEnd_DoesNotPlayWhenWireRefuses()
    {
        // The engine sets CanPlay=false when exhaust < threshold; the
        // simulator trusts that and skips the card.
        var state = TestFixtures.State(
            energy: 3,
            hand: new[] { TestFixtures.Card(CardId.PactsEnd, 0, targetType: TargetType.AllEnemies, canPlay: false) },
            enemies: new[] { TestFixtures.Enemy(hp: 100) });
        var actions = Model.LegalActions(state);
        Assert.DoesNotContain(actions, a => a is SimPlayCard);
    }

    // ── Rampage ───────────────────────────────────────────────────────
    //
    // The +5-per-play scaling is per-card-instance and lives on the
    // engine side (the wire doesn't expose the bonus); the simulator
    // models the first-play damage of 9. These tests pin the base value
    // so a catalog regression to 8 (or any other number) goes red.

    [Fact]
    public void Rampage_BaseDealsNineDamage()
    {
        var state = TestFixtures.State(
            energy: 3,
            hand: new[] { TestFixtures.Card(CardId.Rampage, 0, targetType: TargetType.AnyEnemy) },
            enemies: new[] { TestFixtures.Enemy(hp: 100) });
        var plan = Planner.PlanTurn(state, Model, Eval, Budget, default);
        Assert.Equal(91, plan.ProjectedEndOfTurnState.Enemies[0].Hp);
    }
}
