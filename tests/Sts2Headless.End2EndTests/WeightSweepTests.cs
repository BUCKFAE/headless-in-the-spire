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

    // Third round of sweeps after the v2 baseline locked in 8/50. Try
    // variants that build on the new defaults.
    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V3_StrongStr() => Measure(
        "v3-strong-str",
        HeuristicWeights.Default with { PlayerStrength = 10.0, PlayerDexterity = 4.0 });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V3_BiggerBuffs() => Measure(
        "v3-bigger-buffs",
        HeuristicWeights.Default with {
            DemonForm = 70.0, Combust = 18.0, Metallicize = 18.0,
            FeelNoPain = 12.0, DarkEmbrace = 14.0, Juggernaut = 12.0,
            Barricade = 40.0
        });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V3_LessIncoming() => Measure(
        "v3-less-incoming",
        HeuristicWeights.Default with { IncomingDamage = -2.0, PlayerBlock = 0.3 });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V3_MoreIncoming() => Measure(
        "v3-more-incoming",
        HeuristicWeights.Default with { IncomingDamage = -4.5, PlayerBlock = 1.0 });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V3_StrongHp() => Measure(
        "v3-strong-hp",
        HeuristicWeights.Default with { PlayerHp = 6.0, IncomingDamage = -3.5 });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V3_HighDuration() => Measure(
        "v3-high-duration",
        HeuristicWeights.Default with {
            DemonForm = 80.0,
            PlayerStrength = 12.0,
            EnemyVulnerable = 5.0
        });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V3_BlockMore() => Measure(
        "v3-block-more",
        HeuristicWeights.Default with { PlayerBlock = 1.2, IncomingDamage = -3.5 });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V3_BiggerVulnAndWeak() => Measure(
        "v3-bigger-vuln-weak",
        HeuristicWeights.Default with { EnemyVulnerable = 6.0, EnemyWeak = 6.0 });

    // V4: refine around the v3-less-incoming winning point (10/50).
    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V4_VeryLowIncoming() => Measure(
        "v4-very-low-incoming",
        HeuristicWeights.Default with { IncomingDamage = -1.0 });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V4_NoBlock() => Measure(
        "v4-no-block",
        HeuristicWeights.Default with { IncomingDamage = -2.0, PlayerBlock = 0.0 });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V4_AggrLowIncomingBigEnemy() => Measure(
        "v4-aggr-big-enemy",
        HeuristicWeights.Default with { IncomingDamage = -2.0, EnemyHp = -4.0 });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V4_LessIncomingHigherHp() => Measure(
        "v4-low-incoming-high-hp",
        HeuristicWeights.Default with { IncomingDamage = -2.0, PlayerHp = 5.0 });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V4_Combined1() => Measure(
        "v4-combined-1",
        HeuristicWeights.Default with {
            IncomingDamage = -2.0,
            PlayerBlock = 0.3,
            EnemyHp = -4.0,
            PlayerHp = 5.0,
        });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V4_Combined2() => Measure(
        "v4-combined-2",
        HeuristicWeights.Default with {
            IncomingDamage = -1.5,
            PlayerBlock = 0.2,
            EnemyVulnerable = 5.0,
            EnemyWeak = 5.0,
        });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V4_BoostStr() => Measure(
        "v4-boost-str",
        HeuristicWeights.Default with {
            IncomingDamage = -2.0,
            PlayerBlock = 0.3,
            PlayerStrength = 12.0,
            DemonForm = 60.0,
        });

    // V5: try combining best v4 winners + MCTS planner.
    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V5_NoBlockBoostStr() => Measure(
        "v5-no-block-boost-str",
        HeuristicWeights.Default with {
            IncomingDamage = -2.0,
            PlayerBlock = 0.0,
            PlayerStrength = 12.0,
            DemonForm = 60.0,
        });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V5_MctsBudget() => MeasureWithPlannerVariant(
        "v5-mcts",
        HeuristicWeights.Default,
        () => new MctsPlanner());

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V5_StrongPlayerHp() => Measure(
        "v5-strong-player-hp",
        HeuristicWeights.Default with { PlayerHp = 6.0, PlayerBlock = 0.0 });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V5_MoreEnemyHp() => Measure(
        "v5-more-enemy-hp",
        HeuristicWeights.Default with { EnemyHp = -3.5, IncomingDamage = -2.0 });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V5_HugeBudget() => MeasureWithPlanner(
        "v5-huge-budget",
        HeuristicWeights.Default,
        new PlannerBudget(MaxNodes: 1_000_000));

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V5_LowEnemyHp() => Measure(
        "v5-low-enemy-hp",
        HeuristicWeights.Default with { EnemyHp = -2.5, IncomingDamage = -2.0 });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V5_BiggerLethal() => Measure(
        "v5-bigger-lethal",
        HeuristicWeights.Default with { LethalBonus = 500_000 });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V5_DurationBuffs() => Measure(
        "v5-duration-buffs",
        HeuristicWeights.Default with {
            DemonForm = 70.0,
            FeelNoPain = 14.0,
            DarkEmbrace = 16.0,
            Barricade = 50.0,
            Combust = 18.0,
            Juggernaut = 12.0,
        });

    // V6 — combo around the 10/50 plateau.
    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V6_LowVeryEnemyHp() => Measure(
        "v6-low-very-enemy-hp",
        HeuristicWeights.Default with { EnemyHp = -2.0, IncomingDamage = -1.5 });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V6_BiggerVuln() => Measure(
        "v6-bigger-vuln",
        HeuristicWeights.Default with { EnemyVulnerable = 8.0 });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V6_NoVulnNoWeak() => Measure(
        "v6-no-vuln-no-weak",
        HeuristicWeights.Default with { EnemyVulnerable = 0.0, EnemyWeak = 0.0 });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V6_HpFiveOnly() => Measure(
        "v6-hp-five-only",
        HeuristicWeights.Default with { PlayerHp = 5.0 });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V6_BiggerStrAndVuln() => Measure(
        "v6-bigger-str-and-vuln",
        HeuristicWeights.Default with {
            PlayerStrength = 12.0,
            EnemyVulnerable = 5.0,
            EnemyStrength = -5.0,
        });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V6_LowBlockVeryAggr() => Measure(
        "v6-low-block-very-aggr",
        HeuristicWeights.Default with {
            PlayerBlock = 0.0,
            IncomingDamage = -1.5,
        });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V6_LethalSeek() => Measure(
        "v6-lethal-seek",
        HeuristicWeights.Default with {
            EnemyHp = -3.5,
            IncomingDamage = -2.0,
            PlayerBlock = 0.2,
        });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V6_StrengthEmphasis() => Measure(
        "v6-strength-emphasis",
        HeuristicWeights.Default with {
            PlayerStrength = 14.0,
            DemonForm = 80.0,
            PlayerDexterity = 5.0,
        });

    // V7 — Hail-Mary variants and combinations.
    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V7_FlatEnemyHpPlus() => Measure(
        "v7-flat-enemy-hp-plus",
        HeuristicWeights.Default with { EnemyHp = -2.5, IncomingDamage = -2.0, PlayerBlock = 0.2 });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V7_NegPlayerBlock() => Measure(
        "v7-neg-player-block",
        HeuristicWeights.Default with { PlayerBlock = -0.2, IncomingDamage = -2.0 });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V7_DurationMultBoost() => Measure(
        "v7-duration-mult-boost",
        HeuristicWeights.Default with {
            DemonForm = 90.0,
            FeelNoPain = 16.0,
            DarkEmbrace = 18.0,
            Combust = 20.0,
            Metallicize = 18.0,
            Juggernaut = 14.0,
        });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V7_AggrPower() => Measure(
        "v7-aggr-power",
        HeuristicWeights.Default with {
            DemonForm = 100.0,
            PlayerStrength = 14.0,
            PlayerHp = 5.0,
            IncomingDamage = -2.0,
            PlayerBlock = 0.3,
        });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V7_BigStrengthPenalty() => Measure(
        "v7-big-str-penalty",
        HeuristicWeights.Default with {
            EnemyStrength = -8.0,
            EnemyVulnerable = 5.0,
        });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V7_BigCardsDrawn() => Measure(
        "v7-big-cards-drawn",
        HeuristicWeights.Default with { CardsDrawn = 3.0 });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V7_LowEverything() => Measure(
        "v7-low-everything",
        HeuristicWeights.Default with {
            PlayerHp = 2.0,
            EnemyHp = -1.5,
            PlayerBlock = 0.1,
            IncomingDamage = -1.0,
        });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V7_DefendIsBad() => Measure(
        "v7-defend-is-bad",
        HeuristicWeights.Default with { PlayerBlock = -1.0, IncomingDamage = -1.5 });

    // V8 — deck-tracker active; retry winning v3/v4 variants and combinations.
    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V8_Default() => Measure("v8-default", HeuristicWeights.Default);

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V8_Stronger() => Measure(
        "v8-stronger",
        HeuristicWeights.Default with { PlayerHp = 5.0, EnemyHp = -3.5 });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V8_AggrAttack() => Measure(
        "v8-aggr-attack",
        HeuristicWeights.Default with { EnemyHp = -3.5, IncomingDamage = -2.0 });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V8_DefendBlock() => Measure(
        "v8-defend-block",
        HeuristicWeights.Default with { PlayerBlock = 0.7, IncomingDamage = -2.5 });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V8_StrengthBig() => Measure(
        "v8-strength-big",
        HeuristicWeights.Default with { PlayerStrength = 11.0, DemonForm = 55.0 });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V8_NoCardsDrawn() => Measure(
        "v8-no-cards-drawn",
        HeuristicWeights.Default with { CardsDrawn = 0.0 });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V8_HugeCardsDrawn() => Measure(
        "v8-huge-cards-drawn",
        HeuristicWeights.Default with { CardsDrawn = 5.0 });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V8_MoreLethal() => Measure(
        "v8-more-lethal",
        HeuristicWeights.Default with { LethalBonus = 10_000_000 });

    // V9 — re-sweep on 200-seed corpus since the 50-seed numbers we
    // tuned against earlier turned out to be noisily favourable.
    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V9_200Default() => Measure200("v9-200-default", HeuristicWeights.Default);

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V9_200LessIncoming() => Measure200(
        "v9-200-less-incoming",
        HeuristicWeights.Default with { IncomingDamage = -1.0 });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V9_200MoreIncoming() => Measure200(
        "v9-200-more-incoming",
        HeuristicWeights.Default with { IncomingDamage = -3.0 });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V9_200MoreBlock() => Measure200(
        "v9-200-more-block",
        HeuristicWeights.Default with { PlayerBlock = 0.6 });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V9_200AggressiveEnemy() => Measure200(
        "v9-200-aggressive-enemy",
        HeuristicWeights.Default with { EnemyHp = -4.0 });

    [Fact]
    [Trait("Category", "Diagnostic")]
    public Task Sweep_V9_200StrengthBig() => Measure200(
        "v9-200-strength-big",
        HeuristicWeights.Default with { PlayerStrength = 10.0 });

    private async Task Measure200(string name, HeuristicWeights weights)
    {
        var seeds = Enumerable.Range(1, 200).Select(i => (ulong)i).ToArray();
        var workers = Math.Clamp(Environment.ProcessorCount / 2, 2, 8);
        using var tmp = new TempDir("sts2-a0-sweep");
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(15));
        var report = await WinRateHarness.MeasureAsync(
            new WinRateHarness.MeasurementOptions(
                Ascension: 0,
                Seeds: seeds,
                WorkerCount: workers,
                ReplayRootBase: tmp.Path,
                AgentFactory: () => new IroncladAgent(
                    evaluator: new HeuristicEvaluator(weights)),
                Label: $"sweep200:{name}",
                PerSeedTimeout: TimeSpan.FromMinutes(5)),
            cts.Token);
        var outFile = $"/tmp/ironclad-a0-clear/sweep-{name}.md";
        Directory.CreateDirectory(Path.GetDirectoryName(outFile)!);
        await File.WriteAllTextAsync(outFile, WinRateHarness.FormatMarkdown(report));
        Assert.Equal(seeds.Length, report.Seeds.Count);
    }

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
