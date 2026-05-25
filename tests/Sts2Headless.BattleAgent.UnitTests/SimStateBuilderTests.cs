using Sts2Headless.BattleAgent.Core;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.BattleAgent.UnitTests;

// Wire CombatState → SimState mapping. Catches regressions in
// PlayerStatus power-id translation and in the Card / Enemy field
// projections.
public sealed class SimStateBuilderTests
{
    private static CombatState SampleCombat(
        IReadOnlyList<Card>? hand = null,
        IReadOnlyList<Enemy>? enemies = null,
        IReadOnlyList<Power>? playerPowers = null,
        int playerBlock = 0,
        int energy = 3) => new(
            Round: 1,
            Energy: energy,
            MaxEnergy: 3,
            PlayerBlock: playerBlock,
            IsPlayPhase: true,
            IsInProgress: true,
            DrawPileCount: 10,
            DiscardPileCount: 0,
            Hand: hand ?? Array.Empty<Card>(),
            Enemies: enemies ?? Array.Empty<Enemy>(),
            PlayerPowers: playerPowers ?? Array.Empty<Power>());

    [Fact]
    public void HandMapsCardIndexAsOriginalHandIndex()
    {
        var combat = SampleCombat(hand: new[]
        {
            new Card(Index: 7, Id: CardId.StrikeIronclad, Cost: 1, CanPlay: true, TargetType: TargetType.AnyEnemy, Upgraded: false),
        });
        var sim = SimStateBuilder.FromWire(combat, currentHp: 80, maxHp: 80);
        Assert.Single(sim.Hand);
        Assert.Equal(7, sim.Hand[0].OriginalHandIndex);
        Assert.Equal(CardId.StrikeIronclad, sim.Hand[0].Id);
    }

    [Fact]
    public void EnergyAndBlockComeFromWire()
    {
        var combat = SampleCombat(energy: 2, playerBlock: 7);
        var sim = SimStateBuilder.FromWire(combat, 80, 80);
        Assert.Equal(2, sim.Energy);
        Assert.Equal(7, sim.Block);
    }

    [Fact]
    public void HpFieldsMirrorRunStateNotCombatState()
    {
        // HP / MaxHP live on RunStateResult, not CombatState, so
        // SimStateBuilder takes them as explicit args.
        var combat = SampleCombat();
        var sim = SimStateBuilder.FromWire(combat, currentHp: 42, maxHp: 80);
        Assert.Equal(42, sim.Hp);
        Assert.Equal(80, sim.MaxHp);
    }

    [Fact]
    public void StrengthPowerMapsToStatusStrength()
    {
        var combat = SampleCombat(playerPowers: new[]
        {
            new Power(PowerId.StrengthPower, 4),
        });
        var sim = SimStateBuilder.FromWire(combat, 80, 80);
        Assert.Equal(4, sim.Status.Strength);
    }

    [Fact]
    public void KnownPowersAllMap()
    {
        var combat = SampleCombat(playerPowers: new[]
        {
            new Power(PowerId.StrengthPower, 2),
            new Power(PowerId.DexterityPower, 1),
            new Power(PowerId.VulnerablePower, 1),
            new Power(PowerId.WeakPower, 1),
            new Power(PowerId.FrailPower, 1),
            new Power(PowerId.DemonFormPower, 3),
            new Power(PowerId.RupturePower, 2),
            new Power(PowerId.BarricadePower, 1),
        });
        var sim = SimStateBuilder.FromWire(combat, 80, 80);
        Assert.Equal(2, sim.Status.Strength);
        Assert.Equal(1, sim.Status.Dexterity);
        Assert.Equal(1, sim.Status.Vulnerable);
        Assert.Equal(1, sim.Status.Weak);
        Assert.Equal(1, sim.Status.Frail);
        Assert.Equal(3, sim.Status.DemonForm);
        Assert.Equal(2, sim.Status.Rupture);
        Assert.Equal(1, sim.Status.Barricade);
    }

    [Fact]
    public void UnknownPowersDontCrashTheBuilder()
    {
        // An unknown power should leave the status untouched; the
        // builder should still produce a usable SimState.
        var combat = SampleCombat(playerPowers: new[]
        {
            new Power(PowerId.Unknown, 7),
        });
        var sim = SimStateBuilder.FromWire(combat, 80, 80);
        Assert.Equal(0, sim.Status.Strength);
    }

    [Fact]
    public void EnemyIntentMapsFirstIntent()
    {
        var combat = SampleCombat(enemies: new[]
        {
            new Enemy(
                Index: 0,
                MonsterId: MonsterIdNames.FromWire("LOUSE_RED"),
                Hp: 11,
                MaxHp: 11,
                Block: 0,
                IntendsAttack: true,
                Intents: new[]
                {
                    new Intent(IntentKind.Attack, Damage: 5, Hits: 1, Block: 0),
                },
                Powers: Array.Empty<Power>()),
        });
        var sim = SimStateBuilder.FromWire(combat, 80, 80);
        Assert.NotNull(sim.Enemies[0].Intent);
        Assert.Equal(5, sim.Enemies[0].Intent!.Damage);
    }

    [Fact]
    public void EnemyVulnerableAndWeakMap()
    {
        var combat = SampleCombat(enemies: new[]
        {
            new Enemy(
                Index: 0,
                MonsterId: MonsterIdNames.FromWire("CULTIST"),
                Hp: 50,
                MaxHp: 50,
                Block: 0,
                IntendsAttack: false,
                Intents: Array.Empty<Intent>(),
                Powers: new[]
                {
                    new Power(PowerId.VulnerablePower, 3),
                    new Power(PowerId.WeakPower, 2),
                }),
        });
        var sim = SimStateBuilder.FromWire(combat, 80, 80);
        Assert.Equal(3, sim.Enemies[0].Vulnerable);
        Assert.Equal(2, sim.Enemies[0].Weak);
    }
}
