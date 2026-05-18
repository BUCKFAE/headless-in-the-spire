using Sts2Headless.Agents;
using Sts2Headless.BattleAgent;
using Sts2Headless.IntegrationTests;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.End2EndTests;

// End-to-end win-rate validation for the simulator-driven IroncladAgent
// at Ascension 0 (the wire surface currently hardcodes ascension to 0;
// A1 lands once the BLOCKED Ascension-on-RunNewParams entry resolves).
//
// "Won" here means "beat Act 1 boss" — the agent reached currentActIndex
// >= 1 at termination, regardless of how deep into Act 2 it went after.
// Beating Act 1 is the canonical proof that the combat planner +
// drafting policy compose into something better than a coin flip; it's
// the same bar bottled_ai / scumthespire used as their first
// regression target on STS1.
//
// Two tests:
//   * Smoke (single seed 42): "the agent does not crash, returns a
//     terminal outcome". Floor reached is reported but not asserted —
//     fragility belongs in diagnostic traits, not in the green-build
//     gate.
//   * Win rate (10 seeds, diagnostic-traited): the actual measurement.
//     Threshold set conservatively at first; raised as the agent gains
//     better drafts / smarter rest decisions / etc. Marked diagnostic so
//     it doesn't block `just test` while the agent is still being tuned.
public class IroncladAgentA0Tests
{
    private const int WinFloorThreshold = 18; // first floor of Act 2 (post-A1-boss)

    private readonly ITestOutputHelper _output;

    public IroncladAgentA0Tests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task IroncladAgent_RunsToTermination_Seed42()
    {
        await using var host = new HostSubprocess();
        await host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

        var transport = new HostSubprocessTransport(host);
        var agent = new IroncladAgent();

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var outcome = await AgentDriver.PlayRunAsync(
            transport,
            agent,
            // Stop the moment we either die or beat the Act 1 boss. Lets
            // us observe outcome without burning CI time on Act 2+.
            stopWhen: s => s.ActFloor >= WinFloorThreshold || s.CurrentActIndex >= 1,
            ct: cts.Token);

        _output.WriteLine(
            $"termination={outcome.TerminatedBy} steps={outcome.Steps} "
            + $"floor={outcome.FinalState.ActFloor} act={outcome.FinalState.CurrentActIndex} "
            + $"hp={outcome.FinalState.Hp}/{outcome.FinalState.MaxHp} "
            + $"gameOver={outcome.FinalState.IsGameOver} room={outcome.FinalState.CurrentRoomType}");

        Assert.NotEqual(TerminationReason.StepLimit, outcome.TerminatedBy);
        Assert.True(outcome.Steps > 5,
            $"agent terminated after only {outcome.Steps} steps — likely crashed before doing anything meaningful");
    }

    [Fact]
    [Trait("category", "diagnostic")]
    public async Task IroncladAgent_WinsAtLeastThreeOfTenSeeds_A0()
    {
        // ten consecutive integer seeds — small enough to keep CI
        // tractable, large enough to surface non-deterministic regressions
        // in the simulator/planner.
        var seeds = Enumerable.Range(1, 10).Select(i => (ulong)i).ToArray();
        var results = new List<(ulong seed, bool won, int floor, string termination, string detail)>();

        Directory.CreateDirectory("/tmp/ironclad-a0");
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(20));
        foreach (var seed in seeds)
        {
            await using var host = new HostSubprocess();
            var newRun = await host.SendAsync<RunNewResult>(
                "run/new", new RunNewParams(Character: Character.Ironclad, Seed: seed));

            // Anchor the win predicate on the *initial* CurrentActIndex
            // observed at run/new so a fresh state with index 1 doesn't
            // count as "beat Act 1 boss". A real win is strictly more
            // acts than where we started.
            var initialState = await host.SendAsync<RunStateResult>("run/state");
            var startAct = initialState.CurrentActIndex;
            var startFloor = initialState.ActFloor;

            var transport = new CrashTracingTransport(new HostSubprocessTransport(host));
            var agent = new IroncladAgent();

            try
            {
                var outcome = await AgentDriver.PlayRunAsync(
                    transport,
                    agent,
                    stopWhen: s => s.CurrentActIndex > startAct
                                   || s.ActFloor >= WinFloorThreshold,
                    ct: cts.Token);
                var won = !outcome.FinalState.IsGameOver
                    && (outcome.FinalState.CurrentActIndex > startAct
                        || outcome.FinalState.ActFloor >= WinFloorThreshold);
                results.Add((
                    seed,
                    won,
                    outcome.FinalState.ActFloor,
                    outcome.TerminatedBy.ToString(),
                    $"startAct={startAct} startFloor={startFloor} steps={outcome.Steps} "
                    + $"finalRoom={outcome.FinalState.CurrentRoomType} "
                    + $"hp={outcome.FinalState.Hp}/{outcome.FinalState.MaxHp}"));
            }
            catch (Exception ex)
            {
                // Write the full stack to a per-seed file so the run-rate
                // line stays readable while still preserving the failure
                // detail for follow-up.
                await File.WriteAllTextAsync(
                    $"/tmp/ironclad-a0/seed-{seed}-crash.txt",
                    $"{ex.GetType().FullName}: {ex.Message}\n\n{ex}\n\n"
                    + $"inner: {ex.InnerException?.GetType().FullName}: "
                    + $"{ex.InnerException?.Message}\n{ex.InnerException}");
                results.Add((seed, false, -1, ex.GetType().Name,
                    $"see /tmp/ironclad-a0/seed-{seed}-crash.txt: {ex.Message}"));
            }
        }

        var wins = results.Count(r => r.won);
        var summary = string.Join("\n",
            results.Select(r => $"seed={r.seed} won={r.won} floor={r.floor} term={r.termination} | {r.detail}"));
        await File.WriteAllTextAsync("/tmp/ironclad-a0/summary.txt", summary);
        // Current threshold is 3/10 — the floor we last verified on
        // 2026-05-18. The stretch goal is 5/10 (half wins) but getting
        // there needs deeper combat-planner work than v1 ships with.
        // Don't lower this threshold to make a regression pass — fix
        // the regression instead. See /tmp/ironclad-a0-tuning.md for
        // the per-iteration history.
        Assert.True(wins >= 3,
            $"expected at least 3/10 wins, got {wins}/10\n{summary}");
    }
}
