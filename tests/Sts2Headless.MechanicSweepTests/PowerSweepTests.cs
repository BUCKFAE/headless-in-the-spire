using Sts2Headless.IntegrationTests;
using Sts2Headless.MechanicSweep;
using Sts2Headless.MechanicSweep.Sweeps;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.MechanicSweepTests;

// Per-id smoke sweep for every PowerId in PowerIdNames.AllWireNames.
// Same opt-in / sampling shape as the other sweeps. Fixture:
// start_combat → apply_power (Player; fallback Enemy:0) → 2 end_turns
// → kill_all_enemies, draining TriggeredSincePrev between calls.
//
// Report (gitignored, regenerate locally):
//   documentation/coverage/sweep-powers.md
//   documentation/coverage/sweep-powers.json
public class PowerSweepTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;
    private readonly ITestOutputHelper _output;

    public PowerSweepTests(HostSubprocess host, ITestOutputHelper output)
    {
        _host = host;
        _output = output;
    }

    [Fact]
    [Trait("Category", "MechanicSweep")]
    public async Task AllPowers_NoCrash()
    {
        if (!SweepGate.ShouldRun("POWER"))
        {
            _output.WriteLine(
                "PowerSweepTests: skipping — set RUN_POWER_SWEEP=1 (or RUN_MECHANIC_SWEEP=1) to opt in. "
                + "Use `just sweep-powers` for the full pass or `just sweep-sample <N>` for a fast subset.");
            return;
        }

        var transport = new TransportAdapter(_host);
        var sample = SweepGate.TrySampleIds(PowerIdNames.AllWireNames);
        var version = SweepGate.ReadGameVersion();

        var rowIndex = 0;
        var totalIds = sample?.Count ?? PowerIdNames.AllWireNames.Count;
        _output.WriteLine($"=== power sweep starting — {totalIds} ids, game={version} ===");

        var report = await new PowerSweep().RunAsync(
            transport,
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
            });

        var (md, _) = SweepReportIo.Write(report);
        _output.WriteLine("=== power sweep complete ===");
        _output.WriteLine($"  elapsed:     {report.TotalElapsed.TotalMinutes:0.0} min");
        _output.WriteLine($"  crashed:     {report.Crashes}");
        _output.WriteLine($"  timeouts:    {report.Timeouts}");
        _output.WriteLine($"  triggered:   {report.Triggered}");
        _output.WriteLine($"  played:      {report.Played}");
        _output.WriteLine($"  unplayable:  {report.Unplayable}");
        _output.WriteLine($"  report:      {Path.GetRelativePath(Directory.GetCurrentDirectory(), md)}");

        Assert.True(
            report.Crashes == 0 && report.Timeouts == 0,
            $"{report.Crashes} crash(es), {report.Timeouts} timeout(s) — see documentation/coverage/sweep-powers.md");
    }
}
