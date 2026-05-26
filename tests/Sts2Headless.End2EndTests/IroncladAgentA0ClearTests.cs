using Sts2Headless.BattleAgent;
using Sts2Headless.BattleAgent.Core;
using Sts2Headless.TestSupport;
using Xunit;

namespace Sts2Headless.End2EndTests;

// "Clear A0" win-rate measurements — the agent must beat the Act 3 boss
// (IsVictory=true at termination), not just the Act 1 boss the
// IroncladAgentA0Tests measure. The latter test pre-dates the
// multi-character / multi-act work and bakes in a stopWhen that exits
// the run at start of Act 2.
//
// A0 = ascension 0 (lowest difficulty). The game has 3 acts; "clear A0"
// = beat all three. Per the user-stated goal, we want this agent to
// clear A0 most of the time.
//
// All measurements run via WinRateHarness on a HostPool. Workers reuse
// their host subprocesses across seeds, paying the sts2.dll bootstrap
// cost once each instead of once per seed (which is what the old serial
// loop did). Diagnostic-traited so they don't run on `just validation::test`.
public class IroncladAgentA0ClearTests
{
    private readonly ITestOutputHelper _output;
    public IroncladAgentA0ClearTests(ITestOutputHelper output) => _output = output;

    [Fact]
    [Trait("Category", "Diagnostic")]
    public async Task IroncladAgent_ClearA0_50Seeds_Parallel()
    {
        await RunMeasurement(
            label: "IroncladAgent (default Exhaustive planner)",
            seeds: Enumerable.Range(1, 50).Select(i => (ulong)i).ToArray(),
            agentFactory: () => new IroncladAgent(),
            outFile: "/tmp/ironclad-a0-clear/default-exhaustive.md");
    }

    [Fact]
    [Trait("Category", "Diagnostic")]
    public async Task IroncladAgent_ClearA0_200Seeds_Parallel()
    {
        // Bigger sample for tighter confidence intervals. 50-seed runs
        // hover at 11/50 (22%); the standard error is ±~6%, so a real
        // change of 2-3 seeds is hard to distinguish from noise. 200
        // tightens the SE to ~3%.
        await RunMeasurement(
            label: "IroncladAgent (200-seed default Exhaustive)",
            seeds: Enumerable.Range(1, 200).Select(i => (ulong)i).ToArray(),
            agentFactory: () => new IroncladAgent(),
            outFile: "/tmp/ironclad-a0-clear/default-exhaustive-200.md");
    }

    [Fact]
    [Trait("Category", "Diagnostic")]
    public async Task IroncladAgent_ClearA0_50Seeds_MultiTurn2()
    {
        await RunMeasurement(
            label: "IroncladAgent (MultiTurnExhaustivePlanner, lookahead=2)",
            seeds: Enumerable.Range(1, 50).Select(i => (ulong)i).ToArray(),
            agentFactory: () => new IroncladAgent(
                planner: new MultiTurnExhaustivePlanner(lookaheadTurns: 2)),
            outFile: "/tmp/ironclad-a0-clear/multiturn-2.md");
    }

    [Fact]
    [Trait("Category", "Diagnostic")]
    public async Task IroncladAgent_ClearA0_50Seeds_MultiTurn3()
    {
        await RunMeasurement(
            label: "IroncladAgent (MultiTurnExhaustivePlanner, lookahead=3)",
            seeds: Enumerable.Range(1, 50).Select(i => (ulong)i).ToArray(),
            agentFactory: () => new IroncladAgent(
                planner: new MultiTurnExhaustivePlanner(lookaheadTurns: 3)),
            outFile: "/tmp/ironclad-a0-clear/multiturn-3.md");
    }

    // Smoke test for the parallel harness itself — 4 seeds across 4 workers
    // verifies that pool spin-up + dispatch + report formatting all work.
    // Cheap: ~30s for 4 seeds. Asserts the harness produces a well-formed
    // report; does NOT assert wins (the agent's win rate is the subject
    // of separate measurements, not this test).
    [Fact]
    public async Task WinRateHarness_Smoke_FourSeeds_DoesNotCrash()
    {
        using var tmp = new TempDir("sts2-winrate-smoke");
        var report = await WinRateHarness.MeasureAsync(new WinRateHarness.MeasurementOptions(
            Ascension: 0,
            Seeds: new ulong[] { 1, 2, 3, 4 },
            WorkerCount: 4,
            ReplayRootBase: tmp.Path,
            AgentFactory: () => new IroncladAgent(),
            Label: "smoke",
            PerSeedTimeout: TimeSpan.FromMinutes(2)));

        _output.WriteLine(WinRateHarness.FormatMarkdown(report));
        Assert.Equal(4, report.Seeds.Count);
        // Each worker should have produced at least one replay file.
        // (Even on death, the recorder emits a manifest + the .mcr / .run
        // for the combat that killed us.)
        for (var i = 0; i < report.WorkerCount; i++)
        {
            var dir = Path.Combine(tmp.Path, $"worker-{i}");
            Assert.True(Directory.Exists(dir), $"worker-{i} replay dir missing");
        }
    }

    private async Task RunMeasurement(
        string label,
        IReadOnlyList<ulong> seeds,
        Func<IroncladAgent> agentFactory,
        string outFile)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outFile)!);
        // Worker count: one per available core, capped at 8 so we don't
        // create more sts2.dll loads than the box can comfortably hold in
        // RAM. Most workstations have at least 4; the 50-seed sweep gets a
        // 4-8x wall-clock speedup over the old serial loop.
        var workers = Math.Clamp(Environment.ProcessorCount / 2, 2, 8);

        // Replay output lives in /tmp so a full sweep doesn't fill
        // replays/manual. Cleaned up by the TempDir.
        using var tmp = new TempDir("sts2-a0-clear");

        // Cap total wall in case of pathological cases. Per-seed timeout
        // inside the harness limits individual runs.
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(30));

        var report = await WinRateHarness.MeasureAsync(
            new WinRateHarness.MeasurementOptions(
                Ascension: 0,
                Seeds: seeds,
                WorkerCount: workers,
                ReplayRootBase: tmp.Path,
                AgentFactory: agentFactory,
                Label: label,
                PerSeedTimeout: TimeSpan.FromMinutes(5),
                OnSeedDone: r => _output.WriteLine(
                    $"seed={r.Seed} cleared={r.ClearedA0} act={r.FinalActIndex} "
                    + $"floor={r.FinalActFloor} term={r.Termination} ms={r.ElapsedMs}")),
            cts.Token);

        var md = WinRateHarness.FormatMarkdown(report);
        await File.WriteAllTextAsync(outFile, md);
        _output.WriteLine($"--- summary written to {outFile} ---");
        _output.WriteLine(md);

        // This is a measurement, not a regression gate. Assert only that
        // we got results for every seed — the win-rate number itself goes
        // into the markdown for review.
        Assert.Equal(seeds.Count, report.Seeds.Count);
    }
}
