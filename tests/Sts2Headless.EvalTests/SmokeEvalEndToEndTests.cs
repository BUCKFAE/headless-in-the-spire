using System.Text.Json;
using Sts2Headless.Eval;
using Sts2Headless.Eval.Manifests;
using Sts2Headless.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace Sts2Headless.EvalTests;

// End-to-end smoke. Runs Greedy on a 1-seed inline matrix against a real
// host subprocess (Sts2Headless.dll lands next to the test bin/ via the
// project reference). Verifies:
//
//   * config.json / summary.json / summary.md / runs.jsonl all exist.
//   * runs.jsonl is one line per cell.
//   * The cell directory has a `cell.json`.
//   * Eval exit-code logic (HasHarnessError) is false on a clean matrix.
//
// Gated `[Trait("Category", "Integration")]` because it spawns sts2.dll.
// The default `just validation::test` filter excludes Integration tests
// on machines without a populated vendor/, in line with the existing
// IntegrationTests project's posture.
[Trait("Category", "Integration")]
public sealed class SmokeEvalEndToEndTests(ITestOutputHelper log)
{
    [Fact]
    public async Task Greedy_One_Seed_End_To_End()
    {
        // Early-return on machines without a populated vendor/ — the
        // test infrastructure runs hermetically on developer workstations
        // and on the self-hosted CI runner; both are expected to have
        // vendor/ populated when this category is included.
        if (!VendorAvailable())
        {
            log.WriteLine("vendor/sts2.dll not present — skipping.");
            return;
        }

        using var tmp = new TempDir("sts2-eval-smoke");

        var report = await EvaluationHarness.RunAsync(new EvaluationHarnessConfig
        {
            Agents = [BuiltinAgents.Greedy],
            Seeds  = SeedBanks.Inline([42], name: "test-inline"),
            Output = new OutputLayout
            {
                EvalRoot        = tmp.Path,
                EvalIdGenerator = static _ => "eval-test",
            },
            Budgets = new HarnessBudgets
            {
                // Greedy on one seed plays in seconds in practice; cap at
                // 3 minutes so a regression doesn't time out the session.
                PerCell  = TimeSpan.FromMinutes(3),
                MaxSteps = 1500,
            },
            TeeProcessStderr = true,
        },
        onCellComplete: cell => log.WriteLine(
            $"[{cell.Terminus}] {cell.Agent.Name} seed={cell.Seed} floor={cell.FloorReached} {cell.WallClockMs}ms"),
        onLog: line => log.WriteLine(line));

        Assert.False(report.HasHarnessError, "Greedy smoke eval surfaced a HarnessError terminus");

        Assert.True(File.Exists(report.Output.ConfigJson),       "config.json not written");
        Assert.True(File.Exists(report.Output.SummaryJson),      "summary.json not written");
        Assert.True(File.Exists(report.Output.SummaryMarkdown),  "summary.md not written");
        Assert.True(File.Exists(report.Output.RunsJsonl),        "runs.jsonl not written");

        var lines = File.ReadAllLines(report.Output.RunsJsonl);
        Assert.Single(lines);

        var cellResult = JsonSerializer.Deserialize<CellResult>(lines[0], Eval.Json.EvalJson.Wire)
            ?? throw new InvalidOperationException("runs.jsonl line deserialised to null");
        Assert.Equal("greedy", cellResult.Agent.Name);
        Assert.Equal((ulong)42, cellResult.Seed);

        var cellJsonPath = Path.Combine(report.EvalDirectory, cellResult.ReplayPath, "cell.json");
        Assert.True(File.Exists(cellJsonPath), $"cell.json missing at {cellJsonPath}");
    }

    private static bool VendorAvailable()
    {
        var root = Sts2Headless.Utils.Paths.LocateRepoRoot();
        var dll = Path.Combine(Sts2Headless.Utils.Paths.VendorDir(root), Sts2Headless.Utils.Paths.Sts2DllName);
        return File.Exists(dll);
    }
}
