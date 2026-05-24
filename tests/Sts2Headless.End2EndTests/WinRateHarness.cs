using System.Diagnostics;
using System.Text;
using Sts2Headless.Agents.Driving;
using Sts2Headless.Agents.Hosting;
using Sts2Headless.BattleAgent;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.End2EndTests;

// Reusable parallel win-rate measurement harness for IroncladAgent. The
// existing IroncladAgentA0Tests.MeasureWinRate runs serially (foreach
// host = new HostSubprocess()) which paid the sts2.dll bootstrap cost
// once per seed and burned ~20+ minutes on a 50-seed sweep. This harness
// spreads the same workload across an HostPool of W workers (each pays
// the dll-load cost once at startup, then reuses the worker for many
// runs), and reports both metrics we actually care about:
//
//   * cleared A0  — IsVictory true at run termination (beat Act 3 boss)
//   * beat Act 1  — CurrentActIndex >= 1 at termination (mid-run gate;
//                   useful for spotting "agent regressed in early game"
//                   even when full-clear rate hasn't moved)
//
// HostPool workers spawn a clean HostProcess each (--enable-debug NOT
// passed, AD-7 compliant), and run/new resets per-worker state, so seeds
// can be dispatched to whichever worker is idle.
public static class WinRateHarness
{
    public sealed record SeedOutcome(
        ulong Seed,
        bool ClearedA0,
        bool BeatAct1Boss,
        int FinalActIndex,
        int FinalActFloor,
        int FinalHp,
        int FinalMaxHp,
        string Termination,
        long ElapsedMs,
        string? Error);

    public sealed record Report(
        string Label,
        int Ascension,
        int WorkerCount,
        IReadOnlyList<SeedOutcome> Seeds,
        TimeSpan Wall)
    {
        public int Clears => Seeds.Count(s => s.ClearedA0);
        public int Act1Wins => Seeds.Count(s => s.BeatAct1Boss);
        public int Errors => Seeds.Count(s => s.Error is not null);
        public double AvgFloor => Seeds.Count == 0 ? 0.0
            : Seeds.Where(s => s.Error is null).DefaultIfEmpty().Average(s => s?.FinalActFloor ?? 0);
    }

    public sealed record MeasurementOptions(
        int Ascension,
        IReadOnlyList<ulong> Seeds,
        int WorkerCount,
        string ReplayRootBase,
        Func<IroncladAgent> AgentFactory,
        string Label = "IroncladAgent (defaults)",
        TimeSpan? PerSeedTimeout = null,
        int MaxStepsPerRun = AgentDriver.DefaultMaxSteps,
        Action<SeedOutcome>? OnSeedDone = null);

    public static async Task<Report> MeasureAsync(
        MeasurementOptions options,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Seeds.Count == 0)
            throw new ArgumentException("Seeds must not be empty.", nameof(options));
        if (options.WorkerCount < 1)
            throw new ArgumentOutOfRangeException(nameof(options), "WorkerCount must be >= 1.");

        var workerCount = Math.Min(options.WorkerCount, options.Seeds.Count);
        var perSeedTimeout = options.PerSeedTimeout ?? TimeSpan.FromMinutes(5);

        var sw = Stopwatch.StartNew();

        await using var pool = new HostPool(new HostPoolOptions(
            WorkerCount: workerCount,
            ReplayRootBase: options.ReplayRootBase,
            // Per-request timeout. Individual SendAsync calls inside a run
            // are sub-second; the only way this trips is if the engine
            // genuinely hangs in a way our patches missed.
            RequestTimeout: TimeSpan.FromSeconds(60)));

        var outcomes = new SeedOutcome[options.Seeds.Count];

        // Dispatch each seed to the pool — HostPool's RunAsync queues on a
        // Channel<HostProcess>, so the degree of parallelism is workerCount
        // regardless of how many seeds we hand it at once.
        var tasks = options.Seeds.Select((seed, idx) => pool.RunAsync(async (host, innerCt) =>
        {
            var seedSw = Stopwatch.StartNew();
            using var seedCts = CancellationTokenSource.CreateLinkedTokenSource(innerCt);
            seedCts.CancelAfter(perSeedTimeout);

            try
            {
                await host.SendAsync<RunNewResult>(
                    "run/new",
                    new RunNewParams(
                        Character: Character.Ironclad,
                        Seed: seed,
                        Ascension: options.Ascension));

                var initial = await host.SendAsync<RunStateResult>("run/state");
                var startAct = initial.CurrentActIndex;

                var agent = options.AgentFactory();
                // No stopWhen — we want IsGameOver (victory or death) to
                // terminate the loop naturally. AgentDriver.PlayRunAsync
                // returns RunOutcome with FinalState carrying IsVictory.
                var outcome = await AgentDriver.PlayRunAsync(
                    host,
                    agent,
                    maxSteps: options.MaxStepsPerRun,
                    ct: seedCts.Token);

                var s = outcome.FinalState;
                var clearedA0 = s.IsVictory;
                var beatAct1 = s.CurrentActIndex > startAct
                    || s.ActFloor >= 18; // first floor of Act 2
                seedSw.Stop();

                var result = new SeedOutcome(
                    Seed: seed,
                    ClearedA0: clearedA0,
                    BeatAct1Boss: beatAct1,
                    FinalActIndex: s.CurrentActIndex,
                    FinalActFloor: s.ActFloor,
                    FinalHp: s.Hp,
                    FinalMaxHp: s.MaxHp,
                    Termination: outcome.TerminatedBy.ToString(),
                    ElapsedMs: seedSw.ElapsedMilliseconds,
                    Error: null);
                outcomes[idx] = result;
                options.OnSeedDone?.Invoke(result);
                return result;
            }
            catch (Exception ex)
            {
                seedSw.Stop();
                var msg = ex.InnerException is null
                    ? $"{ex.GetType().Name}: {ex.Message}"
                    : $"{ex.GetType().Name}: {ex.Message} | inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}";
                var result = new SeedOutcome(
                    Seed: seed,
                    ClearedA0: false,
                    BeatAct1Boss: false,
                    FinalActIndex: -1,
                    FinalActFloor: -1,
                    FinalHp: -1,
                    FinalMaxHp: -1,
                    Termination: ex.GetType().Name,
                    ElapsedMs: seedSw.ElapsedMilliseconds,
                    Error: msg);
                outcomes[idx] = result;
                options.OnSeedDone?.Invoke(result);
                return result;
            }
        }, ct)).ToArray();

        await Task.WhenAll(tasks);
        sw.Stop();

        return new Report(
            Label: options.Label,
            Ascension: options.Ascension,
            WorkerCount: workerCount,
            Seeds: outcomes,
            Wall: sw.Elapsed);
    }

    // Markdown summary suitable for documentation/coverage/. Per-seed
    // table + headline metrics + floor histogram. Sorted by seed so a
    // diff between two runs is line-by-line readable.
    public static string FormatMarkdown(Report report)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {report.Label}");
        sb.AppendLine();
        sb.AppendLine($"- ascension: {report.Ascension}");
        sb.AppendLine($"- seeds: {report.Seeds.Count}");
        sb.AppendLine($"- workers: {report.WorkerCount}");
        sb.AppendLine($"- wall: {report.Wall.TotalSeconds:F1}s");
        sb.AppendLine($"- **clear A0 (IsVictory):** {report.Clears}/{report.Seeds.Count} = {report.Clears * 100.0 / report.Seeds.Count:F1}%");
        sb.AppendLine($"- beat Act 1 boss: {report.Act1Wins}/{report.Seeds.Count} = {report.Act1Wins * 100.0 / report.Seeds.Count:F1}%");
        sb.AppendLine($"- errors: {report.Errors}/{report.Seeds.Count}");
        sb.AppendLine($"- avg floor (excl. errors): {report.AvgFloor:F1}");
        sb.AppendLine();

        // Floor histogram — clusters where the runs are dying.
        sb.AppendLine("## floor reached");
        sb.AppendLine();
        var buckets = report.Seeds
            .GroupBy(s => s.Error is not null ? "error"
                : s.ClearedA0 ? "cleared"
                : s.FinalActFloor switch
                {
                    < 0 => "unknown",
                    <= 5 => "01-05",
                    <= 10 => "06-10",
                    <= 15 => "11-15",
                    <= 16 => "16 (pre-boss)",
                    17 => "17 (boss)",
                    <= 33 => "act2 (18-33)",
                    _ => "act3 (34+)",
                })
            .OrderBy(g => g.Key)
            .Select(g => $"- {g.Key}: {g.Count()}");
        foreach (var line in buckets) sb.AppendLine(line);
        sb.AppendLine();

        sb.AppendLine("## per-seed");
        sb.AppendLine();
        sb.AppendLine("| seed | cleared | act1 | act | floor | hp | term | ms |");
        sb.AppendLine("|------|---------|------|-----|-------|----|------|----|");
        foreach (var s in report.Seeds.OrderBy(s => s.Seed))
        {
            sb.AppendLine(
                $"| {s.Seed} | {(s.ClearedA0 ? "✓" : "")} | {(s.BeatAct1Boss ? "✓" : "")} | "
                + $"{s.FinalActIndex} | {s.FinalActFloor} | {s.FinalHp}/{s.FinalMaxHp} | "
                + $"{s.Termination} | {s.ElapsedMs} |");
        }

        // Per-seed error detail, if any.
        var errors = report.Seeds.Where(s => s.Error is not null).ToArray();
        if (errors.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## errors");
            sb.AppendLine();
            foreach (var e in errors)
            {
                sb.AppendLine($"- seed {e.Seed}: {e.Error}");
            }
        }

        return sb.ToString();
    }
}
