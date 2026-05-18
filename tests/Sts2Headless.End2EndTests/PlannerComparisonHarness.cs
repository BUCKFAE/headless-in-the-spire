using System.Diagnostics;
using System.Text;
using Sts2Headless.Agents;
using Sts2Headless.BattleAgent;
using Sts2Headless.BattleAgent.Core;
using Sts2Headless.IntegrationTests;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.End2EndTests;

// Head-to-head benchmarks for ICombatPlanner implementations on the
// same fixed-seed corpus. Writes a markdown report to
// /tmp/planner-comparison.md so we can answer "which planner wins
// more, reaches deeper floors, and how fast does each plan a turn?"
// with measured data instead of theory.
//
// Diagnostic-traited so it doesn't run on `just test` — each row
// drives a full A0 Ironclad run through the host, which takes 15-30s
// per (planner, seed) pair.
public class PlannerComparisonHarness
{
    private const int WinFloorThreshold = 18;
    private static readonly ulong[] Seeds = Enumerable.Range(1, 10).Select(i => (ulong)i).ToArray();

    private static readonly (string Name, Func<ICombatPlanner> Factory)[] Planners =
    {
        ("ExhaustivePlanner",          () => new ExhaustivePlanner()),
        ("MultiTurnExhaustivePlanner", () => new MultiTurnExhaustivePlanner(lookaheadTurns: 2)),
        ("MctsPlanner",                () => new MctsPlanner()),
    };

    [Fact]
    [Trait("category", "diagnostic")]
    public async Task CompareAllPlanners_A0_IroncladSeeds1To10()
    {
        Directory.CreateDirectory("/tmp/ironclad-a0");
        var report = new StringBuilder();
        report.AppendLine("# Planner comparison — A0 Ironclad, seeds 1-10");
        report.AppendLine();
        report.AppendLine($"Run at {DateTime.UtcNow:O}.");
        report.AppendLine();

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(45));
        var perPlanner = new List<PlannerRow>();

        foreach (var (name, factory) in Planners)
        {
            var rows = new List<SeedResult>();
            foreach (var seed in Seeds)
            {
                rows.Add(await RunSeed(seed, factory, cts.Token));
            }
            var wins = rows.Count(r => r.Won);
            var deepest = rows.Max(r => r.Floor);
            var deathsAtBoss = rows.Count(r => r.Floor >= 17 && !r.Won);
            var avgFloor = rows.Average(r => r.Floor);
            perPlanner.Add(new PlannerRow(name, wins, deepest, deathsAtBoss, avgFloor, rows));

            report.AppendLine($"## {name}");
            report.AppendLine();
            report.AppendLine($"- wins: {wins}/10");
            report.AppendLine($"- deepest floor: {deepest}");
            report.AppendLine($"- reached boss but lost: {deathsAtBoss}");
            report.AppendLine($"- avg floor: {avgFloor:F1}");
            report.AppendLine();
            report.AppendLine("| seed | won | floor | hp | term |");
            report.AppendLine("|------|-----|-------|----|------|");
            foreach (var r in rows)
            {
                report.AppendLine($"| {r.Seed} | {r.Won} | {r.Floor} | {r.Hp} | {r.Termination} |");
            }
            report.AppendLine();
        }

        report.AppendLine("## summary");
        report.AppendLine();
        report.AppendLine("| planner | wins | deepest | boss-deaths | avg floor |");
        report.AppendLine("|---------|------|---------|-------------|-----------|");
        foreach (var p in perPlanner.OrderByDescending(p => p.Wins).ThenByDescending(p => p.AvgFloor))
        {
            report.AppendLine($"| {p.Name} | {p.Wins} | {p.Deepest} | {p.DeathsAtBoss} | {p.AvgFloor:F1} |");
        }

        await File.WriteAllTextAsync("/tmp/planner-comparison.md", report.ToString());

        // No assertion on which planner wins — this is a measurement
        // test, not a regression gate. Look at /tmp/planner-comparison.md.
        Assert.True(perPlanner.Count > 0);
    }

    private static async Task<SeedResult> RunSeed(ulong seed, Func<ICombatPlanner> factory, CancellationToken ct)
    {
        await using var host = new HostSubprocess();
        await host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: seed));
        var init = await host.SendAsync<RunStateResult>("run/state");
        var startAct = init.CurrentActIndex;

        var transport = new HostSubprocessTransport(host);
        var agent = new IroncladAgent(planner: factory());

        var sw = Stopwatch.StartNew();
        try
        {
            var outcome = await AgentDriver.PlayRunAsync(
                transport,
                agent,
                stopWhen: s => s.CurrentActIndex > startAct || s.ActFloor >= WinFloorThreshold,
                ct: ct);
            sw.Stop();
            var won = !outcome.FinalState.IsGameOver
                && (outcome.FinalState.CurrentActIndex > startAct
                    || outcome.FinalState.ActFloor >= WinFloorThreshold);
            return new SeedResult(
                Seed: seed,
                Won: won,
                Floor: outcome.FinalState.ActFloor,
                Hp: $"{outcome.FinalState.Hp}/{outcome.FinalState.MaxHp}",
                Termination: outcome.TerminatedBy.ToString(),
                ElapsedMs: sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new SeedResult(
                Seed: seed,
                Won: false,
                Floor: -1,
                Hp: "-",
                Termination: ex.GetType().Name,
                ElapsedMs: sw.ElapsedMilliseconds);
        }
    }

    private sealed record SeedResult(ulong Seed, bool Won, int Floor, string Hp, string Termination, long ElapsedMs);
    private sealed record PlannerRow(string Name, int Wins, int Deepest, int DeathsAtBoss, double AvgFloor, List<SeedResult> Rows);
}
