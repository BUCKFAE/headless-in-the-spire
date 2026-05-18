using Sts2Headless.BattleAgent.Core;
using Xunit;

namespace Sts2Headless.BattleAgent.UnitTests;

public sealed class HeuristicEvaluatorTests
{
    private static readonly IEvaluator Eval = new HeuristicEvaluator();

    [Fact]
    public void DeadEnemiesIsLethalScore()
    {
        var state = TestFixtures.State(
            enemies: new[] { TestFixtures.Enemy(hp: 0) });
        var score = Eval.Score(state);
        Assert.True(score >= HeuristicWeights.Default.LethalBonus,
            $"lethal score {score} should dominate everything else");
    }

    [Fact]
    public void DeadPlayerIsHugelyNegative()
    {
        var state = TestFixtures.State(hp: 0);
        var score = Eval.Score(state);
        Assert.True(score <= -HeuristicWeights.Default.DeathPenalty);
    }

    [Fact]
    public void MoreHpIsBetter()
    {
        var lo = TestFixtures.State(hp: 40);
        var hi = TestFixtures.State(hp: 60);
        Assert.True(Eval.Score(hi) > Eval.Score(lo));
    }

    [Fact]
    public void MoreEnemyHpIsWorse()
    {
        var lo = TestFixtures.State(enemies: new[] { TestFixtures.Enemy(hp: 10) });
        var hi = TestFixtures.State(enemies: new[] { TestFixtures.Enemy(hp: 30) });
        Assert.True(Eval.Score(lo) > Eval.Score(hi));
    }

    [Fact]
    public void StrengthBuffsAreValued()
    {
        var noBuff = TestFixtures.State();
        var withBuff = TestFixtures.State(
            status: PlayerStatus.Empty with { Strength = 3 });
        Assert.True(Eval.Score(withBuff) > Eval.Score(noBuff));
    }

    [Fact]
    public void VulnerableOnEnemyIsValued()
    {
        var no = TestFixtures.State(enemies: new[] { TestFixtures.Enemy() });
        var yes = TestFixtures.State(enemies: new[] { TestFixtures.Enemy(vulnerable: 2) });
        Assert.True(Eval.Score(yes) > Eval.Score(no));
    }

    [Fact]
    public void IncomingDamageIsAccountedFor()
    {
        var safe = TestFixtures.State(
            block: 0,
            enemies: new[] { TestFixtures.Enemy(intent: null) });
        var threatened = TestFixtures.State(
            block: 0,
            enemies: new[] { TestFixtures.Enemy(intent: TestFixtures.Attack(damage: 20)) });
        Assert.True(Eval.Score(safe) > Eval.Score(threatened),
            "an enemy intending to attack 20 should look worse than the same enemy idle");
    }

    [Fact]
    public void BlockDiscountedVsHp()
    {
        // Equal nominal "defense": 10 block vs 10 hp. HP should be
        // valued higher because block expires at end of turn.
        var blocky = TestFixtures.State(hp: 50, block: 10);
        var healthy = TestFixtures.State(hp: 60, block: 0);
        Assert.True(Eval.Score(healthy) > Eval.Score(blocky));
    }
}
