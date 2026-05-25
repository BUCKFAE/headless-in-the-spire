using System.Diagnostics;
using Sts2Headless.Agents.Driving;
using Sts2Headless.Agents.Examples;
using Sts2Headless.Agents.Hosting;
using Sts2Headless.Protocol.Methods;
using Sts2Headless.TestSupport;
using Xunit;

namespace Sts2Headless.End2EndTests;

// Goal #3 throughput probe: how many full Ironclad runs/sec / runs/day
// can HostPool drive on this workstation? Traited `Category=Benchmark`
// so it's excluded from `just validation::dotnet::test-end2end` (it's slow and the goal is
// measurement, not a regression gate).
//
// Configuration via env vars so the operator can dial it without
// recompiling:
//
//   STS2_BENCH_WORKERS   parallel HostPool workers (default 4)
//   STS2_BENCH_RUNS      total Ironclad runs to drive (default 8)
//   STS2_BENCH_MAX_STEPS per-run AgentDriver step cap (default 4000)
//
// The benchmark uses GreedyAgent, not IroncladAgent — IroncladAgent's
// simulator-driven planner spends most of its wall-clock in C# code,
// not the wire, which would measure the planner instead of the host.
// GreedyAgent is "wire-bound work" by construction: every step is a
// round-trip through the host, so runs/sec here is a fair proxy for
// the host's throughput ceiling.
public class ParallelHostThroughputBenchmark
{
    private readonly ITestOutputHelper _output;

    public ParallelHostThroughputBenchmark(ITestOutputHelper output) => _output = output;

    [Fact]
    [Trait("Category", "Benchmark")]
    public async Task GreedyIronclad_ConcurrentRunsThroughput()
    {
        var workers = ReadIntEnv("STS2_BENCH_WORKERS", defaultValue: 4);
        var totalRuns = ReadIntEnv("STS2_BENCH_RUNS", defaultValue: 8);
        var maxSteps = ReadIntEnv("STS2_BENCH_MAX_STEPS", defaultValue: 4000);

        Assert.True(workers >= 1, "STS2_BENCH_WORKERS must be >= 1");
        Assert.True(totalRuns >= 1, "STS2_BENCH_RUNS must be >= 1");

        // TempDir self-deletes on Dispose, replacing the old try/finally
        // that hand-rolled the temp path + cleanup.
        using var tmpDir = new TempDir("sts2-bench");
        var tmpRoot = tmpDir.Path;

        _output.WriteLine($"workers={workers} totalRuns={totalRuns} maxSteps={maxSteps}");
        _output.WriteLine($"replayRoot={tmpRoot}");

        var poolStart = Stopwatch.StartNew();
        await using var pool = new HostPool(new HostPoolOptions(
            WorkerCount: workers,
            ReplayRootBase: tmpRoot,
            RequestTimeout: TimeSpan.FromMinutes(5)));
        poolStart.Stop();
        _output.WriteLine($"pool boot: {poolStart.Elapsed.TotalSeconds:F2}s ({workers} workers, eager start)");

        var perRunDurations = new double[totalRuns];
        var perRunOutcomes = new TerminationReason[totalRuns];
        var perRunFloors = new int[totalRuns];

        var runWall = Stopwatch.StartNew();
        var tasks = Enumerable.Range(0, totalRuns)
            .Select(i => pool.RunAsync(async (host, ct) =>
            {
                var seed = (ulong)(1 + i);
                var sw = Stopwatch.StartNew();
                await host.SendAsync<RunNewResult>(
                    "run/new", new RunNewParams(
                        Character: Character.Ironclad,
                        Seed: seed));
                var outcome = await AgentDriver.PlayRunAsync(
                    host,
                    new GreedyAgent(),
                    maxSteps: maxSteps,
                    ct: ct);
                sw.Stop();
                perRunDurations[i] = sw.Elapsed.TotalSeconds;
                perRunOutcomes[i] = outcome.TerminatedBy;
                perRunFloors[i] = outcome.FinalState.ActFloor;
                return outcome;
            }))
            .ToArray();
        await Task.WhenAll(tasks);
        runWall.Stop();

        // Wall-clock throughput: how many runs/sec did we land in
        // the wall window? Multiplied out to runs/hour and
        // runs/day for the goal-doc framing.
        var totalSeconds = runWall.Elapsed.TotalSeconds;
        var runsPerSecond = totalRuns / totalSeconds;
        var runsPerHour = runsPerSecond * 3600;
        var runsPerDay = runsPerSecond * 86400;

        Array.Sort(perRunDurations);
        var p50 = Percentile(perRunDurations, 0.50);
        var p95 = Percentile(perRunDurations, 0.95);
        var p99 = Percentile(perRunDurations, 0.99);
        var mean = perRunDurations.Average();

        _output.WriteLine("");
        _output.WriteLine($"wall-clock total: {totalSeconds:F1}s for {totalRuns} runs");
        _output.WriteLine($"per-run duration: mean={mean:F2}s p50={p50:F2}s p95={p95:F2}s p99={p99:F2}s");
        _output.WriteLine($"throughput:       {runsPerSecond:F3} runs/s = {runsPerHour:F0} runs/h = {runsPerDay:F0} runs/day");
        _output.WriteLine("");
        _output.WriteLine($"termination breakdown: {string.Join(", ", perRunOutcomes.GroupBy(r => r).Select(g => $"{g.Key}={g.Count()}"))}");
        _output.WriteLine($"final floor: min={perRunFloors.Min()} max={perRunFloors.Max()} mean={perRunFloors.Average():F1}");

        // Sanity floor: throughput shouldn't collapse to "I ran one
        // worker at a time, sequentially". With N workers the wall
        // clock should be roughly total_seq / N + boot_cost, not
        // total_seq. Test asserts the workers actually overlapped:
        // wall < 0.9 × (sum of per-run durations) when N >= 2.
        if (workers >= 2)
        {
            var sumPerRun = perRunDurations.Sum();
            Assert.True(
                totalSeconds < 0.9 * sumPerRun,
                $"benchmark wall-clock {totalSeconds:F1}s is suspiciously close to sequential " +
                $"({sumPerRun:F1}s); workers={workers} may not be parallelising work.");
        }
    }

    private static int ReadIntEnv(string name, int defaultValue)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrEmpty(raw) || !int.TryParse(raw, out var parsed)
            ? defaultValue
            : parsed;
    }

    // Nearest-rank percentile on a pre-sorted array. Good enough for
    // small N (this benchmark's totalRuns is typically <100); for big-N
    // benchmarks we'd swap in linear-interpolation.
    private static double Percentile(double[] sortedAsc, double q)
    {
        if (sortedAsc.Length == 0) return 0;
        var idx = (int)Math.Ceiling(q * sortedAsc.Length) - 1;
        idx = Math.Clamp(idx, 0, sortedAsc.Length - 1);
        return sortedAsc[idx];
    }
}
