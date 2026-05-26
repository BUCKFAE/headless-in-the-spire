using Sts2Headless.BattleAgent.Core;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.BattleAgent.UnitTests;

// Tests the reference CombatModel's Apply / EndPlayerTurn semantics
// against synthetic SimStates. These are pure-C# tests — no host, no
// sts2.dll. Engine-parity is a separate suite (IntegrationTests).
public sealed class CombatModelTests
{
    private static readonly ICombatModel Model = new CombatModel(IroncladCardCatalog.Instance);

    // ── Damage ────────────────────────────────────────────────────────

    [Fact]
    public void StrikeDealsSixDamageToTarget()
    {
        var state = TestFixtures.State(
            hand: new[] { TestFixtures.Card(CardId.StrikeIronclad, 0, targetType: TargetType.AnyEnemy) },
            enemies: new[] { TestFixtures.Enemy(hp: 30) });
        var next = Model.Apply(state, new SimPlayCard(0, 0));
        Assert.Equal(24, next.Enemies[0].Hp);
        Assert.Equal(2, next.Energy); // strike costs 1
        Assert.Empty(next.Hand);
    }

    [Fact]
    public void EnemyBlockAbsorbsDamage()
    {
        var state = TestFixtures.State(
            hand: new[] { TestFixtures.Card(CardId.StrikeIronclad, 0, targetType: TargetType.AnyEnemy) },
            enemies: new[] { TestFixtures.Enemy(hp: 30, block: 4) });
        var next = Model.Apply(state, new SimPlayCard(0, 0));
        Assert.Equal(28, next.Enemies[0].Hp);     // 6 dmg, 4 absorbed, 2 to HP
        Assert.Equal(0, next.Enemies[0].Block);   // block consumed
    }

    [Fact]
    public void VulnerableAddsFiftyPercentDamage()
    {
        var state = TestFixtures.State(
            hand: new[] { TestFixtures.Card(CardId.StrikeIronclad, 0, targetType: TargetType.AnyEnemy) },
            enemies: new[] { TestFixtures.Enemy(hp: 30, vulnerable: 1) });
        var next = Model.Apply(state, new SimPlayCard(0, 0));
        Assert.Equal(30 - 9, next.Enemies[0].Hp); // 6 * 1.5 = 9
    }

    [Fact]
    public void StrengthAddsFlatDamage()
    {
        var status = PlayerStatus.Empty with { Strength = 3 };
        var state = TestFixtures.State(
            status: status,
            hand: new[] { TestFixtures.Card(CardId.StrikeIronclad, 0, targetType: TargetType.AnyEnemy) },
            enemies: new[] { TestFixtures.Enemy(hp: 30) });
        var next = Model.Apply(state, new SimPlayCard(0, 0));
        Assert.Equal(21, next.Enemies[0].Hp); // 6 + 3 = 9
    }

    [Fact]
    public void WeakReducesDamageByTwentyFivePercent()
    {
        var status = PlayerStatus.Empty with { Weak = 1 };
        var state = TestFixtures.State(
            status: status,
            hand: new[] { TestFixtures.Card(CardId.StrikeIronclad, 0, targetType: TargetType.AnyEnemy) },
            enemies: new[] { TestFixtures.Enemy(hp: 30) });
        var next = Model.Apply(state, new SimPlayCard(0, 0));
        Assert.Equal(26, next.Enemies[0].Hp); // floor(6 * 0.75) = 4
    }

    [Fact]
    public void TwinStrikeHitsTwice()
    {
        var state = TestFixtures.State(
            hand: new[] { TestFixtures.Card(CardId.TwinStrike, 0, targetType: TargetType.AnyEnemy) },
            enemies: new[] { TestFixtures.Enemy(hp: 30) });
        var next = Model.Apply(state, new SimPlayCard(0, 0));
        Assert.Equal(20, next.Enemies[0].Hp); // 5 * 2
    }

    [Fact]
    public void ThunderclapHitsAllEnemies()
    {
        var state = TestFixtures.State(
            hand: new[] { TestFixtures.Card(CardId.Thunderclap, 0) },
            enemies: new[]
            {
                TestFixtures.Enemy(index: 0, hp: 20),
                TestFixtures.Enemy(index: 1, hp: 20),
            });
        var next = Model.Apply(state, new SimPlayCard(0, null));
        Assert.Equal(16, next.Enemies[0].Hp);
        Assert.Equal(16, next.Enemies[1].Hp);
        Assert.Equal(1, next.Enemies[0].Vulnerable);
        Assert.Equal(1, next.Enemies[1].Vulnerable);
    }

    [Fact]
    public void BashAppliesVulnerableAndDamage()
    {
        var state = TestFixtures.State(
            hand: new[] { TestFixtures.Card(CardId.Bash, 0, targetType: TargetType.AnyEnemy) },
            enemies: new[] { TestFixtures.Enemy(hp: 30) });
        var next = Model.Apply(state, new SimPlayCard(0, 0));
        Assert.Equal(22, next.Enemies[0].Hp);
        Assert.Equal(2, next.Enemies[0].Vulnerable);
        Assert.Equal(1, next.Energy);
    }

    // ── Block ────────────────────────────────────────────────────────

    [Fact]
    public void DefendGivesFiveBlock()
    {
        var state = TestFixtures.State(
            hand: new[] { TestFixtures.Card(CardId.DefendIronclad, 0) });
        var next = Model.Apply(state, new SimPlayCard(0, null));
        Assert.Equal(5, next.Block);
    }

    [Fact]
    public void FrailReducesBlockByTwentyFivePercent()
    {
        var status = PlayerStatus.Empty with { Frail = 1 };
        var state = TestFixtures.State(
            status: status,
            hand: new[] { TestFixtures.Card(CardId.DefendIronclad, 0) });
        var next = Model.Apply(state, new SimPlayCard(0, null));
        Assert.Equal(3, next.Block); // floor(5 * 0.75) = 3
    }

    [Fact]
    public void DexterityAddsFlatBlock()
    {
        var status = PlayerStatus.Empty with { Dexterity = 2 };
        var state = TestFixtures.State(
            status: status,
            hand: new[] { TestFixtures.Card(CardId.DefendIronclad, 0) });
        var next = Model.Apply(state, new SimPlayCard(0, null));
        Assert.Equal(7, next.Block);
    }

    [Fact]
    public void BodySlamDamageEqualsCurrentBlock()
    {
        var state = TestFixtures.State(
            block: 12,
            hand: new[] { TestFixtures.Card(CardId.BodySlam, 0, targetType: TargetType.AnyEnemy) },
            enemies: new[] { TestFixtures.Enemy(hp: 30) });
        var next = Model.Apply(state, new SimPlayCard(0, 0));
        Assert.Equal(18, next.Enemies[0].Hp); // 30 - 12
        Assert.Equal(12, next.Block); // block not consumed by Body Slam
    }

    // ── Powers ───────────────────────────────────────────────────────

    [Fact]
    public void InflameGivesTwoStrength()
    {
        var state = TestFixtures.State(
            hand: new[] { TestFixtures.Card(CardId.Inflame, 0) });
        var next = Model.Apply(state, new SimPlayCard(0, null));
        Assert.Equal(2, next.Status.Strength);
    }

    [Fact]
    public void DemonFormAppliesPower()
    {
        var state = TestFixtures.State(
            hand: new[] { TestFixtures.Card(CardId.DemonForm, 0) });
        var next = Model.Apply(state, new SimPlayCard(0, null));
        Assert.Equal(2, next.Status.DemonForm);
    }

    // ── Energy / self-damage / draw ──────────────────────────────────

    [Fact]
    public void BloodlettingTradesHpForEnergy()
    {
        var state = TestFixtures.State(
            hp: 50,
            energy: 3,
            hand: new[] { TestFixtures.Card(CardId.Bloodletting, 0) });
        var next = Model.Apply(state, new SimPlayCard(0, null));
        Assert.Equal(47, next.Hp);
        Assert.Equal(5, next.Energy);     // -0 cost, +2 energy
        Assert.Equal(1, next.CardsDrawnThisTurn);
    }

    [Fact]
    public void PommelStrikeDamagesAndDraws()
    {
        var state = TestFixtures.State(
            hand: new[] { TestFixtures.Card(CardId.PommelStrike, 0, targetType: TargetType.AnyEnemy) },
            enemies: new[] { TestFixtures.Enemy(hp: 30) });
        var next = Model.Apply(state, new SimPlayCard(0, 0));
        Assert.Equal(21, next.Enemies[0].Hp);
        Assert.Equal(1, next.CardsDrawnThisTurn);
    }

    // ── End turn ─────────────────────────────────────────────────────

    [Fact]
    public void EnemyAttackHitsHpAfterBlock()
    {
        var state = TestFixtures.State(
            hp: 50,
            block: 4,
            enemies: new[] { TestFixtures.Enemy(intent: TestFixtures.Attack(damage: 10)) });
        var next = Model.EndPlayerTurn(state);
        Assert.Equal(44, next.Hp); // 10 dmg, 4 block, 6 to HP
        Assert.Equal(0, next.Block); // block cleared at start of next turn
    }

    [Fact]
    public void DebuffsDecayAtEndOfTurn()
    {
        var state = TestFixtures.State(
            status: PlayerStatus.Empty with { Vulnerable = 2, Weak = 1, Frail = 1 },
            enemies: new[] { TestFixtures.Enemy(intent: null) });
        var next = Model.EndPlayerTurn(state);
        Assert.Equal(1, next.Status.Vulnerable);
        Assert.Equal(0, next.Status.Weak);
        Assert.Equal(0, next.Status.Frail);
    }

    [Fact]
    public void EnergyRefillsOnNextTurn()
    {
        var state = TestFixtures.State(
            energy: 0,
            maxEnergyPerTurn: 3,
            enemies: new[] { TestFixtures.Enemy(intent: null) });
        var next = Model.EndPlayerTurn(state);
        Assert.Equal(3, next.Energy);
    }

    [Fact]
    public void HandClearsOnNextTurn()
    {
        // The simulator doesn't know the deck so it can't draw — Hand
        // is just emptied. Planners that look multi-turn must respect
        // this; v1's exhaustive planner is one-turn only.
        var state = TestFixtures.State(
            hand: new[] { TestFixtures.Card(CardId.StrikeIronclad, 0) },
            enemies: new[] { TestFixtures.Enemy(intent: null) });
        var next = Model.EndPlayerTurn(state);
        Assert.Empty(next.Hand);
    }

    [Fact]
    public void DemonFormBuildsStrengthOnTurnStart()
    {
        var state = TestFixtures.State(
            status: PlayerStatus.Empty with { DemonForm = 2 },
            enemies: new[] { TestFixtures.Enemy(intent: null) });
        var next = Model.EndPlayerTurn(state);
        Assert.Equal(2, next.Status.Strength);
        Assert.Equal(2, next.Status.DemonForm); // power persists
    }

    [Fact]
    public void BarricadeRetainsBlockAcrossTurns()
    {
        var state = TestFixtures.State(
            block: 10,
            status: PlayerStatus.Empty with { Barricade = 1 },
            enemies: new[] { TestFixtures.Enemy(intent: null) });
        var next = Model.EndPlayerTurn(state);
        Assert.Equal(10, next.Block);
    }

    // ── Termination ──────────────────────────────────────────────────

    [Fact]
    public void AllEnemiesDeadIsLethal()
    {
        var state = TestFixtures.State(
            enemies: new[] { TestFixtures.Enemy(hp: 0) });
        Assert.True(Model.AllEnemiesDead(state));
        Assert.True(Model.IsCombatOver(state));
    }

    [Fact]
    public void PlayerDeadIsCombatOver()
    {
        var state = TestFixtures.State(hp: 0);
        Assert.True(Model.IsPlayerDead(state));
        Assert.True(Model.IsCombatOver(state));
    }

    // ── LegalActions ─────────────────────────────────────────────────

    [Fact]
    public void LegalActionsAlwaysIncludesEndTurn()
    {
        var state = TestFixtures.State(hand: Array.Empty<SimCard>());
        var actions = Model.LegalActions(state);
        Assert.Contains(actions, a => a is SimEndTurn);
    }

    [Fact]
    public void LegalActionsSkipsUnaffordableCards()
    {
        var state = TestFixtures.State(
            energy: 1,
            hand: new[] { TestFixtures.Card(CardId.Bash, 0, cost: 2, targetType: TargetType.AnyEnemy) },
            enemies: new[] { TestFixtures.Enemy() });
        var actions = Model.LegalActions(state);
        Assert.DoesNotContain(actions, a => a is SimPlayCard);
    }

    [Fact]
    public void LegalActionsEnumeratesAllLivingEnemiesForAnyEnemyCards()
    {
        var state = TestFixtures.State(
            hand: new[] { TestFixtures.Card(CardId.StrikeIronclad, 0, targetType: TargetType.AnyEnemy) },
            enemies: new[]
            {
                TestFixtures.Enemy(index: 0, hp: 20),
                TestFixtures.Enemy(index: 1, hp: 0),  // dead — skip
                TestFixtures.Enemy(index: 2, hp: 10),
            });
        var actions = Model.LegalActions(state).OfType<SimPlayCard>().ToList();
        Assert.Equal(2, actions.Count);
        Assert.Contains(actions, p => p.TargetEnemyIndex == 0);
        Assert.Contains(actions, p => p.TargetEnemyIndex == 2);
    }
}
