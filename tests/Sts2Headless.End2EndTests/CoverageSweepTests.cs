using Sts2Headless.Agents;
using Sts2Headless.BattleAgent;
using Sts2Headless.Cheats;
using Sts2Headless.IntegrationTests;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.End2EndTests;

// First slice of the content-coverage sweep:
//   * Drive a fixed set of (character, seed, agent) tuples with a
//     999/999 HP cheat (same approach as BeatAct1BossOnSeed42Tests —
//     keep the agent alive long enough to surface late-game content;
//     mid-run death cuts coverage short).
//   * CoverageRecorder hooks the AgentDriver and accumulates seen/played/
//     used/faced sets per run.
//   * After every run, CoverageAggregator unions the report into a
//     cross-run total.
//   * At the end, JSON + markdown reports go to documentation/coverage/
//     (gitignored — proprietary content same as the *Id.g.cs manifests).
//
// OFF BY DEFAULT. The full sweep is ~30s per seed × N seeds, way too
// slow for `just test-end2end`. `just coverage` sets RUN_COVERAGE_SWEEP=1
// to opt in; without that env var, the test exits early as a no-op so
// the run-from-IDE story still works without an explicit Skip attribute
// (which `dotnet test --filter` can't override).
//
// EXTENDING the sweep:
//   * Add to s_runs below — each row is (character, seed, agentLabel,
//     agentFactory). The label appears in the per-run output and the
//     aggregator's `runLabels`; the factory runs once per row so each
//     iteration gets a fresh agent.
//   * Per-run HP/cheat tweaks go in the inner loop; if you find a
//     character that needs a different cheat shape (Silent's discard
//     pile, Defect's orbs interfering with greedy targeting), branch
//     here rather than in the agent.
//   * Specialised agents that expose new content (e.g. PotionDrinkingAgent
//     for the potion / *_POWER surface that the never-drinking
//     GreedyAgent can't reach) earn their seed on the matrix.
public class CoverageSweepTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;
    private readonly ITestOutputHelper _output;

    public CoverageSweepTests(HostSubprocess host, ITestOutputHelper output)
    {
        _host = host;
        _output = output;
    }

    // The sweep matrix. Three agent tiers:
    //   * `greedy` — five seeds. Fast, low-fidelity baseline. Never
    //     wins fights but exercises the run-flow + first-room content.
    //   * `potions` — one seed. Surfaces the ~40-entry potion /
    //     *_POWER surface the never-drinking GreedyAgent can't reach.
    //   * `ironclad` — one seed. The production combat-planning agent
    //     (Sts2Headless.BattleAgent.IroncladAgent). Wins fights, so
    //     reaches mid-Act 2 / Act 3 content that the greedy baseline
    //     never sees. The 999/999 HP cheat in the inner loop still
    //     applies, so it won't tilt on a single bad hand.
    //
    // Adding rows multiplies wall-time linearly; sustain ~5 minutes
    // total or split into tiers.
    private static readonly (Character Character, ulong Seed, string AgentLabel, Func<IAgent> AgentFactory)[] s_runs =
    [
        (Character.Ironclad, 42uL,  "greedy",   () => new GreedyAgent()),
        (Character.Ironclad, 1uL,   "greedy",   () => new GreedyAgent()),
        (Character.Ironclad, 2uL,   "greedy",   () => new GreedyAgent()),
        (Character.Ironclad, 3uL,   "greedy",   () => new GreedyAgent()),
        (Character.Ironclad, 100uL, "greedy",   () => new GreedyAgent()),
        (Character.Ironclad, 42uL,  "potions",  () => new PotionDrinkingAgent()),
        (Character.Ironclad, 42uL,  "ironclad", () => new IroncladAgent()),
    ];

    [Fact]
    [Trait("Category", "CoverageSweep")]
    public async Task GreedyAgent_Sweep_DumpsCoverageReport()
    {
        // Off-by-default gate. Set via `just coverage`; absence keeps
        // this test out of the default end-to-end run.
        if (Environment.GetEnvironmentVariable("RUN_COVERAGE_SWEEP") != "1")
        {
            _output.WriteLine("CoverageSweepTests: skipping — set RUN_COVERAGE_SWEEP=1 (or run `just coverage`) to opt in.");
            return;
        }

        var aggregator = new CoverageAggregator();
        var transport = new HostSubprocessTransport(_host);

        // 3-minute per-run wall-clock cap × N seeds. A single run on the
        // greedy + 999 HP cheat path usually terminates in <30s either by
        // reaching the run's natural endpoint (game-over from a scripted
        // event, or stall-detector tripping on an unhandled hang) or the
        // 4000-step driver budget. Capping prevents a regression from
        // spinning the whole sweep into a multi-hour hang.
        var perRunBudget = TimeSpan.FromMinutes(3);

        foreach (var (character, seed, agentLabel, agentFactory) in s_runs)
        {
            await _host.SendAsync<RunNewResult>("run/new",
                new RunNewParams(Character: character, Seed: seed));

            // Pump HP so the agent survives long enough to surface
            // mid-and-late-Act content; the agent's combat play is unmodified.
            await _host.SendAsync<DebugSetHpResult>(
                "debug/set_hp", new DebugSetHpParams(Hp: 999, MaxHp: 999));

            var recorder = new CoverageRecorder();
            var agent = agentFactory();
            using var cts = new CancellationTokenSource(perRunBudget);

            RunOutcome outcome;
            try
            {
                outcome = await AgentDriver.PlayRunAsync(
                    transport,
                    agent,
                    coverageRecorder: recorder,
                    ct: cts.Token);
            }
            catch (Exception ex) when (ex is StallDetectedException or CombatBudgetExceededException or OperationCanceledException)
            {
                // Hangs / timeouts are *coverage signals* (the recorder
                // already accumulated everything seen so far); don't fail
                // the sweep — record what we got and move on. A real
                // regression in stall handling would surface as a
                // dedicated test elsewhere, not here.
                _output.WriteLine($"[{character} seed={seed}] terminated early: {ex.GetType().Name}: {ex.Message}");
                outcome = new RunOutcome(default!, -1, TerminationReason.StepLimit, AgentStopReason: ex.GetType().Name);
            }

            var report = recorder.Snapshot();
            aggregator.Add(report, runLabel: $"{character}-seed-{seed}-{agentLabel}");
            _output.WriteLine(
                $"[{character} seed={seed} agent={agentLabel}] " +
                $"terminated={outcome.TerminatedBy} steps={outcome.Steps} " +
                $"cards_seen={report.CardsSeen.Count} cards_played={report.CardsPlayed.Count} " +
                $"relics_seen={report.RelicsSeen.Count} potions_seen={report.PotionsSeen.Count} " +
                $"potions_used={report.PotionsUsed.Count} " +
                $"monsters_faced={report.MonstersFaced.Count} powers_seen={report.PowersSeen.Count}");
        }

        // Drop the report under documentation/coverage/ — gitignored. The
        // human running `just coverage` follows the printed paths to the
        // markdown to read the gaps; the JSON exists for tool consumption
        // (a future "coverage diff between commits" tool).
        var repoRoot = RepoRoot();
        var coverageDir = Path.Combine(repoRoot, "documentation", "coverage");
        Directory.CreateDirectory(coverageDir);
        var mdPath = Path.Combine(coverageDir, "latest.md");
        var jsonPath = Path.Combine(coverageDir, "latest.json");
        await File.WriteAllTextAsync(mdPath, aggregator.RenderMarkdown());
        await File.WriteAllTextAsync(jsonPath, aggregator.RenderJson());

        _output.WriteLine($"=== coverage report written ===");
        _output.WriteLine($"  markdown: {Path.GetRelativePath(repoRoot, mdPath)}");
        _output.WriteLine($"  json:     {Path.GetRelativePath(repoRoot, jsonPath)}");
    }

    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8; i++)
        {
            if (File.Exists(Path.Combine(dir, "Sts2Headless.slnx"))) return dir;
            var p = Directory.GetParent(dir);
            if (p is null) break;
            dir = p.FullName;
        }
        throw new InvalidOperationException("repo root not found");
    }
}
