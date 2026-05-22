using Sts2Headless.IntegrationTests;
using Sts2Headless.MechanicSweep;
using Sts2Headless.MechanicSweep.Sweeps;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.MechanicSweepTests;

// Per-id smoke sweep for every AfflictionId in AfflictionIdNames.AllWireNames.
// Off-by-default. Fixture: start_combat → afflict_card(handIndex=0) →
// end_turn → kill_all_enemies, draining TriggeredSincePrev between calls.
public class AfflictionSweepTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;
    private readonly ITestOutputHelper _output;

    public AfflictionSweepTests(HostSubprocess host, ITestOutputHelper output)
    {
        _host = host;
        _output = output;
    }

    [Fact]
    [Trait("Category", "MechanicSweep")]
    public async Task AllAfflictions_NoCrash()
    {
        if (!SweepGate.ShouldRun("AFFLICTION"))
        {
            _output.WriteLine(
                "AfflictionSweepTests: skipping — set RUN_AFFLICTION_SWEEP=1 (or RUN_MECHANIC_SWEEP=1) to opt in. "
                + "Use `just sweep-afflictions` for the full pass.");
            return;
        }

        var transport = new TransportAdapter(_host);
        var sample = SweepGate.TrySampleIds(AfflictionIdNames.AllWireNames);
        var version = SweepGate.ReadGameVersion();

        var rowIndex = 0;
        var totalIds = sample?.Count ?? AfflictionIdNames.AllWireNames.Count;
        _output.WriteLine($"=== affliction sweep starting — {totalIds} ids, game={version} ===");

        var report = await new AfflictionSweep().RunAsync(
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
        _output.WriteLine("=== affliction sweep complete ===");
        _output.WriteLine($"  elapsed:     {report.TotalElapsed.TotalMinutes:0.0} min");
        _output.WriteLine($"  crashed:     {report.Crashes}");
        _output.WriteLine($"  timeouts:    {report.Timeouts}");
        _output.WriteLine($"  triggered:   {report.Triggered}");
        _output.WriteLine($"  played:      {report.Played}");
        _output.WriteLine($"  unplayable:  {report.Unplayable}");
        _output.WriteLine($"  report:      {Path.GetRelativePath(Directory.GetCurrentDirectory(), md)}");

        Assert.True(
            report.Crashes == 0 && report.Timeouts == 0,
            $"{report.Crashes} crash(es), {report.Timeouts} timeout(s) — see documentation/coverage/sweep-afflictions.md");
    }
}
