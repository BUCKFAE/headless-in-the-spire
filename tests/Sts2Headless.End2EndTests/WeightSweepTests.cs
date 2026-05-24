using Sts2Headless.BattleAgent;
using Sts2Headless.BattleAgent.Core;
using Sts2Headless.TestSupport;
using Xunit;

namespace Sts2Headless.End2EndTests;

// Quick sweep harness for autonomous tuning. Each [Fact] is one
// HeuristicWeights variant measured against the 50-seed corpus and
// written to /tmp/ironclad-a0-clear/sweep-<name>.md. Marked Diagnostic
// so they stay out of `just test`.
public class WeightSweepTests
{
    private readonly ITestOutputHelper _output;
    public WeightSweepTests(ITestOutputHelper output) => _output = output;

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_AggressiveEnemyHp() => Measure(
        "aggressive-enemy-hp",
        HeuristicWeights.Default with { EnemyHp = -4.0, IncomingDamage = -3.0 });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_VeryAggressiveEnemyHp() => Measure(
        "very-aggressive-enemy-hp",
        HeuristicWeights.Default with { EnemyHp = -6.0, IncomingDamage = -2.5 });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_DefensiveLeaning() => Measure(
        "defensive",
        HeuristicWeights.Default with { EnemyHp = -2.0, IncomingDamage = -4.5, PlayerBlock = 1.0 });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_BiggerVuln() => Measure(
        "bigger-vuln",
        HeuristicWeights.Default with { EnemyVulnerable = 6.0, EnemyWeak = 5.0 });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_BurstAndVuln() => Measure(
        "burst-and-vuln",
        HeuristicWeights.Default with { EnemyHp = -4.0, EnemyVulnerable = 6.0, EnemyWeak = 5.0, IncomingDamage = -3.0 });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_StrengthHeavy() => Measure(
        "strength-heavy",
        HeuristicWeights.Default with { PlayerStrength = 10.0, EnemyHp = -4.0 });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_NotDying() => Measure(
        "not-dying",
        HeuristicWeights.Default with { IncomingDamage = -6.0, PlayerBlock = 1.5 });

    // Second round (after path + event + merchant policies improved):
    // try the same evaluator shapes that previously didn't help, plus a
    // few new combinations.
    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V2_AggressiveR2() => Measure(
        "v2-aggressive-r2",
        HeuristicWeights.Default with { EnemyHp = -4.0, IncomingDamage = -2.5, EnemyVulnerable = 5.0 });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V2_StrongPowers() => Measure(
        "v2-strong-powers",
        HeuristicWeights.Default with {
            DemonForm = 60.0,
            FeelNoPain = 14.0,
            Metallicize = 18.0,
            DarkEmbrace = 16.0,
            EnemyHp = -3.0
        });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V2_BalancedTune() => Measure(
        "v2-balanced",
        HeuristicWeights.Default with { EnemyHp = -3.0, IncomingDamage = -3.0, PlayerHp = 4.0 });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V2_VeryAggressive() => Measure(
        "v2-very-aggressive",
        HeuristicWeights.Default with { EnemyHp = -5.0, IncomingDamage = -2.0 });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V2_BlockHeavy() => Measure(
        "v2-block-heavy",
        HeuristicWeights.Default with { PlayerBlock = 1.5, IncomingDamage = -5.0, EnemyHp = -2.5 });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V2_BiggerNodes() => MeasureWithPlanner(
        "v2-bigger-nodes",
        HeuristicWeights.Default,
        new PlannerBudget(MaxNodes: 250_000));

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V2_MultiTurn2NewEval() => MeasureWithPlannerVariant(
        "v2-multiturn2",
        HeuristicWeights.Default with { EnemyHp = -4.0 },
        () => new MultiTurnExhaustivePlanner(lookaheadTurns: 2));

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V2_DurationBoost() => Measure(
        "v2-duration-boost",
        HeuristicWeights.Default with {
            EnemyHp = -3.0,
            PlayerStrength = 10.0,
            EnemyVulnerable = 5.0,
            EnemyWeak = 5.0,
            DemonForm = 60.0
        });

    private async Task MeasureWithPlanner(string name, HeuristicWeights weights, PlannerBudget budget)
        => await DoMeasure(name, () => new IroncladAgent(
            evaluator: new HeuristicEvaluator(weights),
            budget: budget));

    private async Task MeasureWithPlannerVariant(string name, HeuristicWeights weights, Func<ICombatPlanner> plannerFactory)
        => await DoMeasure(name, () => new IroncladAgent(
            evaluator: new HeuristicEvaluator(weights),
            planner: plannerFactory()));

    private async Task Measure(string name, HeuristicWeights weights)
        => await DoMeasure(name, () => new IroncladAgent(
            evaluator: new HeuristicEvaluator(weights)));

    private async Task DoMeasure(string name, Func<IroncladAgent> factory)
    {
        var seeds = Enumerable.Range(1, 50).Select(i => (ulong)i).ToArray();
        var workers = Math.Clamp(Environment.ProcessorCount / 2, 2, 8);
        using var tmp = new TempDir("sts2-a0-sweep");
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(15));

        var report = await WinRateHarness.MeasureAsync(
            new WinRateHarness.MeasurementOptions(
                Ascension: 0,
                Seeds: seeds,
                WorkerCount: workers,
                ReplayRootBase: tmp.Path,
                AgentFactory: factory,
                Label: $"sweep:{name}",
                PerSeedTimeout: TimeSpan.FromMinutes(5)),
            cts.Token);

        var outFile = $"/tmp/ironclad-a0-clear/sweep-{name}.md";
        Directory.CreateDirectory(Path.GetDirectoryName(outFile)!);
        var md = WinRateHarness.FormatMarkdown(report);
        await File.WriteAllTextAsync(outFile, md);
        _output.WriteLine(md);
        Assert.Equal(seeds.Length, report.Seeds.Count);
    }
}
