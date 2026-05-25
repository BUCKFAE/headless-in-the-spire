using Sts2Headless.Agents.Contracts;
using Sts2Headless.IntegrationTests;
using Sts2Headless.MechanicSweep;
using Sts2Headless.MechanicSweep.Sweeps;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.MechanicSweepTests;

// Per-id smoke sweep for every EncounterId in EncounterIdNames.AllWireNames.
// Replaces the historical EveryEncounterSmokeTests (under End2EndTests/,
// deleted in the agent-driven-coverage removal commit). That version
// used IroncladAgent + Hellraiser+Pommel — surfaced MCTS-specific NREs
// (QUEEN_BOSS) instead of engine bugs. This rewrite is purely about
// "encounter loads + drives 2 turns cleanly."
//
// Hosting mode: shared HostSubprocess across iterations, with two-stage
// recovery for state-bleeders. Some boss encounters (multi-phase
// QUEEN_BOSS-shape mechanics) leave the engine in a state that breaks
// the NEXT iteration's run/new with an NRE; the sweep handles this by:
//   * detecting Crashed/Timeout outcomes and tearing down the host,
//   * if a Crashed outcome lands on a reused host, retrying the same
//     encounter on a fresh host — a Played retry proves the prior
//     encounter bled state, and the row reflects the retry's outcome.
// This collapses the per-id ~1.4s host boot to a one-time ~1.4s amortised
// across 80 encounters plus ~1.4s for each state-bleeder (typically 2-4
// bosses), cutting the full sweep from ~2 min to ~30s with zero loss of
// per-row attribution.
//
// Set MECHANIC_SWEEP_FRESH_HOST_PER_ID=1 to opt back into the original
// fresh-subprocess-per-encounter posture — useful when debugging a
// specific encounter and you want zero ambiguity about state-bleed.
//
// Report (gitignored, regenerate locally):
//   documentation/coverage/sweep-encounters.md
//   documentation/coverage/sweep-encounters.json
public class EncounterSweepTests
{
    private readonly ITestOutputHelper _output;

    public EncounterSweepTests(ITestOutputHelper output) => _output = output;

    [Fact]
    [Trait("Category", "MechanicSweep")]
    public async Task AllEncounters_NoCrash()
    {
        if (!SweepGate.ShouldRun("ENCOUNTER"))
        {
            _output.WriteLine(
                "EncounterSweepTests: skipping — set RUN_ENCOUNTER_SWEEP=1 (or RUN_MECHANIC_SWEEP=1) to opt in. "
                + "Use `just validation::dotnet::sweep::encounters` for the full pass or `just validation::dotnet::sweep::sample <N>` for a fast subset.");
            return;
        }

        var sample = SweepGate.TrySampleIds(EncounterIdNames.AllWireNames);
        var version = SweepGate.ReadGameVersion();
        var freshHostPerId = Environment.GetEnvironmentVariable("MECHANIC_SWEEP_FRESH_HOST_PER_ID") == "1";

        var rowIndex = 0;
        var totalIds = sample?.Count ?? EncounterIdNames.AllWireNames.Count;
        var mode = freshHostPerId
            ? "fresh subprocess per encounter (MECHANIC_SWEEP_FRESH_HOST_PER_ID=1)"
            : "shared subprocess with auto-recovery";
        _output.WriteLine($"=== encounter sweep starting — {totalIds} ids, game={version} ({mode}) ===");

        var report = await new EncounterSweep().RunAsync(
            transportFactory: () =>
            {
                var host = new HostSubprocess();
                ITransport transport = new TransportAdapter(host);
                return Task.FromResult((transport, (System.IAsyncDisposable)host));
            },
            sampleIds: sample,
            gameVersion: version,
            onRow: row =>
            {
                rowIndex++;
                var isFailure = row.Outcome is SweepOutcome.Crashed or SweepOutcome.Timeout;
                if (isFailure || rowIndex % 25 == 0 || rowIndex == totalIds)
                {
                    _output.WriteLine(
                        $"  [{rowIndex,4}/{totalIds}] [{row.Outcome,-12}] {row.Id,-40} "
                        + $"steps={row.Steps} {row.Elapsed.TotalSeconds:0.0}s "
                        + (row.Detail ?? ""));
                }
            },
            freshHostPerId: freshHostPerId);

        var (md, _) = SweepReportIo.Write(report);
        _output.WriteLine("=== encounter sweep complete ===");
        _output.WriteLine($"  elapsed:     {report.TotalElapsed.TotalMinutes:0.0} min");
        _output.WriteLine($"  crashed:     {report.Crashes}");
        _output.WriteLine($"  timeouts:    {report.Timeouts}");
        _output.WriteLine($"  played:      {report.Played}");
        _output.WriteLine($"  unplayable:  {report.Unplayable}");
        _output.WriteLine($"  report:      {Path.GetRelativePath(Directory.GetCurrentDirectory(), md)}");

        Assert.True(
            report.Crashes == 0 && report.Timeouts == 0,
            $"{report.Crashes} crash(es), {report.Timeouts} timeout(s) — see documentation/coverage/sweep-encounters.md");
    }
}
