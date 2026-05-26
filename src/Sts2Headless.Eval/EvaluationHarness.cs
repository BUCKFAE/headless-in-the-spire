using System.Diagnostics;
using Sts2Headless.Eval.Execution;
using Sts2Headless.Eval.Scoring;
using Sts2Headless.Utils;

namespace Sts2Headless.Eval;

// The library entrypoint. One call site (`RunAsync(config)`) takes a
// matrix and returns a typed report. No globals, no implicit env vars,
// no required JSON-on-disk to get started.
//
// Lifecycle:
//   1. Expand the matrix into Cell instances (capability filter folded
//      into expansion).
//   2. Resolve the eval root: `<config.Output.EvalRoot>/<eval-id>/`.
//      Write `config.json` immediately so an interrupted eval still
//      has a reproducible spec on disk.
//   3. Fan out cells across the worker pool. Per-cell CellExecutor
//      owns one host subprocess + one agent subprocess, runs the loop
//      to terminus, returns CellResult. Each result is appended to
//      `runs.jsonl` and mirrored into the cell's `cell.json` as it
//      finishes — a kill -9 leaves a partial-but-readable directory.
//   4. After all cells, run the scoring function over the collected
//      results to produce an AgentRanking list. Emit `summary.json`
//      and `summary.md`.
//
// The harness is host-pool-free intentionally for v1: a fresh host
// subprocess per cell gives perfect isolation (no carry-over state)
// and natural replay layout (STS2_REPLAY_OUT can be set to exactly
// where we want the bytes to land). The bootstrap cost is real;
// optimising to a pooled host with per-cell run/new resets is a
// follow-up tracked in this method's invariants — anything that
// depended on cells sharing state would have broken the eval anyway.
public static class EvaluationHarness
{
    public static async Task<EvaluationReport> RunAsync(
        EvaluationHarnessConfig                 config,
        CancellationToken                       cancellationToken = default,
        Action<CellResult>?                     onCellComplete    = null,
        Action<MatrixSkip>?                     onSkip            = null,
        Action<string>?                         onLog             = null)
    {
        ArgumentNullException.ThrowIfNull(config);

        // ── Expand matrix ────────────────────────────────────────────────
        var cells = MatrixExpander.Expand(config, onSkip);
        if (cells.Count == 0)
            throw new InvalidOperationException(
                "EvaluationHarness: matrix expansion produced zero cells. "
                + "Check capability filters — every agent declared zero supported (character, ascension) overlaps with the config.");

        // ── Resolve output paths ─────────────────────────────────────────
        var repoRoot = Paths.LocateRepoRoot();
        var evalRootAbsolute = Path.IsPathRooted(config.Output.EvalRoot)
            ? config.Output.EvalRoot
            : Path.Combine(repoRoot, config.Output.EvalRoot);
        var now = DateTimeOffset.UtcNow;
        var evalId = config.Output.EvalIdGenerator(now);
        var evalDirAbsolute = Path.Combine(evalRootAbsolute, evalId);
        Directory.CreateDirectory(evalDirAbsolute);

        var output = new EvaluationOutputPaths(
            EvalDirectory:   evalDirAbsolute,
            ConfigJson:      Path.Combine(evalDirAbsolute, "config.json"),
            SummaryJson:     Path.Combine(evalDirAbsolute, "summary.json"),
            SummaryMarkdown: Path.Combine(evalDirAbsolute, "summary.md"),
            RunsJsonl:       Path.Combine(evalDirAbsolute, "runs.jsonl"),
            CellsDirectory:  Path.Combine(evalDirAbsolute, "cells"));
        Directory.CreateDirectory(output.CellsDirectory);

        // Capture config eagerly so an interrupted eval still has its
        // canonical reproducer on disk.
        var gameVersion = GameVersionPin.Read(repoRoot);
        EvaluationReportIo.WriteConfig(output.ConfigJson, config, evalId, gameVersion);

        // ── Run cells ────────────────────────────────────────────────────
        using var writer = new CellWriter(output.RunsJsonl);
        var workers = ResolveWorkerCap(config, cells.Count);
        var sw = Stopwatch.StartNew();

        var results = new List<CellResult>(cells.Count);
        var sem = new SemaphoreSlim(workers, workers);
        var tasks = new List<Task<CellResult>>(cells.Count);
        foreach (var cell in cells)
        {
            await sem.WaitAsync(cancellationToken);
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var result = await CellExecutor.ExecuteAsync(
                        config:           config,
                        cell:             cell,
                        evalId:           evalId,
                        evalRootAbsolute: evalDirAbsolute,
                        onLog:            onLog,
                        outerCt:          cancellationToken);
                    var cellDirAbs = Path.Combine(evalDirAbsolute, cell.RelativeReplayDir);
                    writer.Append(result, cellDirAbs);
                    onCellComplete?.Invoke(result);
                    return result;
                }
                finally
                {
                    sem.Release();
                }
            }, cancellationToken));
        }

        foreach (var task in tasks)
            results.Add(await task);

        sw.Stop();

        // ── Score + emit summary ─────────────────────────────────────────
        var ranking = config.Scoring.Rank(results);
        var rankedResults = AssignRankScores(results, ranking);

        var summary = new EvaluationSummary(
            EvalId:        evalId,
            GameVersion:   gameVersion?.Version ?? "",
            Sts2DllSha256: gameVersion?.Sha256 ?? "",
            SeedBank:      new SeedBankReference(config.Seeds.Name, config.Seeds.Version, config.Seeds.Seeds.Count),
            Characters:    config.Characters,
            Ascensions:    config.Ascensions,
            Modifiers:     config.Modifiers,
            Scoring:       new ScoringFunctionReference(config.Scoring.Name, config.Scoring.Version),
            ElapsedMs:     sw.ElapsedMilliseconds,
            CellCount:     results.Count,
            Workers:       workers,
            Ranking:       ranking,
            NotableCells:  rankedResults
                .Where(r => r.Terminus is CellTerminus.EngineCrash or CellTerminus.HostCrash
                                       or CellTerminus.AgentCrash  or CellTerminus.HarnessError)
                .Select(r => new NotableCell(
                    Agent:      r.Agent.Name,
                    Seed:       r.Seed,
                    Terminus:   r.Terminus,
                    Floor:      r.FloorReached,
                    ReplayPath: r.ReplayPath,
                    Error:      r.Error))
                .ToList());

        EvaluationReportIo.WriteSummary(output, summary);

        return new EvaluationReport(
            EvalId:        evalId,
            EvalDirectory: evalDirAbsolute,
            Output:        output,
            Summary:       summary,
            Cells:         rankedResults,
            Config:        config);
    }

    private static int ResolveWorkerCap(EvaluationHarnessConfig config, int matrixSize)
    {
        if (config.Workers is int explicitCap && explicitCap > 0)
            return Math.Min(explicitCap, matrixSize);
        // AD-9 default: ⌊cores / 2⌋, clamped to [1, matrixSize].
        return Math.Max(1, Math.Min(matrixSize, Environment.ProcessorCount / 2));
    }

    // Cells go into runs.jsonl with a placeholder Score (0.0) — we don't
    // know the score until after the scoring function runs over the full
    // result set. After ranking, we copy the per-agent score back into
    // each row so the returned `Cells` collection is self-consistent.
    // (`runs.jsonl` lines are already on disk by then; we accept that
    // they carry the placeholder. The canonical aggregate is summary.json
    // anyway — runs.jsonl is the per-cell evidence trail.)
    private static List<CellResult> AssignRankScores(
        IReadOnlyList<CellResult>   cells,
        IReadOnlyList<AgentRanking> ranking)
    {
        var scoreByAgent = ranking.ToDictionary(r => r.Agent.Name, r => r.Score, StringComparer.Ordinal);
        return cells
            .Select(c => scoreByAgent.TryGetValue(c.Agent.Name, out var s)
                ? c with { Scoring = new ScoringMetrics(s) }
                : c)
            .ToList();
    }
}
