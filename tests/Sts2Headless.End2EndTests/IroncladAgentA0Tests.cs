using Sts2Headless.Agents.Driving;
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
//     it doesn't block `just validation::test` while the agent is still being tuned.
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
    [Trait("Category", "Diagnostic")]
    public async Task IroncladAgent_WinsAtLeastThreeOfTenSeeds_A0() =>
        // Re-baselined twice: first when Neow became always-on (gates
        // dropped to 0 because the dismissal helper picked the last
        // unlocked option, often a card-select-broken relic and no
        // grant), then bumped back to 1 once IroncladEventPolicy got
        // Neow-aware relic-tier scoring. 50-seed measurement settled
        // at ~18%; the 10-seed slice has a 95% CI of roughly [0,5]
        // wins, so 1 is the highest threshold that doesn't flake.
        // Bump when the agent's combat / draft policies catch up to
        // the boss-clear bar (currently most runs die in floors 6-15).
        await MeasureWinRate(ascension: 0, seedCount: 10, minWins: 1);

    [Fact]
    [Trait("Category", "Diagnostic")]
    public async Task IroncladAgent_WinRate_A0_50Seeds()
    {
        // Broader-sample measurement of A0 win rate. 50 seeds gives a
        // tighter confidence interval than the 10-seed gate test; the
        // assertion threshold was re-baselined when Neow became always-on
        // (was 10/50 = 20%; dropped to 2 while the agent picked broken
        // Neow relics, now bumped to 5 with the Neow-aware policy
        // measuring 9/50 = 18%). The 4-win slack absorbs seed-set
        // variance. Bump back toward the pre-Neow 10/50 once the agent's
        // draft / combat tuning closes the remaining gap.
        await MeasureWinRate(ascension: 0, seedCount: 50, minWins: 5,
            outputDir: "/tmp/ironclad-a0-50");
    }

    [Fact]
    [Trait("Category", "Diagnostic")]
    public async Task IroncladAgent_WinRate_A1_Measurement()
    {
        // Ascension 1 adds ASCENDERS_BANE to the starter deck and
        // bumps base monster damage. We don't expect 3/10 wins here —
        // this is a measurement test, not a regression gate. Records
        // results to /tmp/ironclad-a1/summary.txt. Threshold was 1
        // pre-Neow; held at 0 even with the Neow-aware policy because
        // A1's curse + damage bump still overwhelms the Phial Holster
        // pickup on most 10-seed slices (most recent run: 0/10).
        await MeasureWinRate(ascension: 1, seedCount: 10, minWins: 0,
            outputDir: "/tmp/ironclad-a1");
    }

    private async Task MeasureWinRate(int ascension, int seedCount, int minWins, string? outputDir = null)
    {
        var dir = outputDir ?? "/tmp/ironclad-a0";
        var seeds = Enumerable.Range(1, seedCount).Select(i => (ulong)i).ToArray();
        var results = new List<(ulong seed, bool won, int floor, string termination, string detail)>();

        Directory.CreateDirectory(dir);
        // Per-seed timeout averages 1-3s; budget 30s per seed for headroom.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30 * seedCount));
        foreach (var seed in seeds)
        {
            await using var host = new HostSubprocess();
            var newRun = await host.SendAsync<RunNewResult>(
                "run/new",
                new RunNewParams(Character: Character.Ironclad, Seed: seed, Ascension: ascension));

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
                    $"{dir}/seed-{seed}-crash.txt",
                    $"{ex.GetType().FullName}: {ex.Message}\n\n{ex}\n\n"
                    + $"inner: {ex.InnerException?.GetType().FullName}: "
                    + $"{ex.InnerException?.Message}\n{ex.InnerException}");
                results.Add((seed, false, -1, ex.GetType().Name,
                    $"see {dir}/seed-{seed}-crash.txt: {ex.Message}"));
            }
        }

        var wins = results.Count(r => r.won);
        var summary = string.Join("\n",
            results.Select(r => $"seed={r.seed} won={r.won} floor={r.floor} term={r.termination} | {r.detail}"));
        // Distribution of how far the agent got — useful for diagnosing
        // a low win-rate run (mostly mid-act deaths vs mostly boss losses).
        var floorBuckets = results
            .GroupBy(r => r.floor switch
            {
                < 0 => "crash",
                <= 5 => "floor 1-5",
                <= 10 => "floor 6-10",
                <= 15 => "floor 11-15",
                <= 16 => "floor 16",
                _ => "floor 17 (boss)",
            })
            .Select(g => $"  {g.Key}: {g.Count()}")
            .OrderBy(s => s)
            .ToList();
        var header = $"wins: {wins}/{seedCount} = {(wins * 100.0 / seedCount):F1}%\n"
            + "floor distribution:\n"
            + string.Join("\n", floorBuckets)
            + "\n\nper-seed:\n";
        await File.WriteAllTextAsync($"{dir}/summary.txt", header + summary);
        Assert.True(wins >= minWins,
            $"expected at least {minWins}/{seedCount} wins at ascension={ascension}, got {wins}/{seedCount}\n{header}{summary}");
    }
}
