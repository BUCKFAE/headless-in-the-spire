using Sts2Headless.IntegrationTests;
using Sts2Headless.MechanicSweep;
using Sts2Headless.MechanicSweep.Sweeps;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.MechanicSweepTests;

// Per-id smoke sweep for every RelicId in RelicIdNames.AllWireNames.
// Same opt-in / sampling shape as CardSweepTests; the per-relic
// fixture is broader (give_relic + 4-card deck + 2 turns of play +
// kill_all_enemies) so passive listener relics get a chance to fire.
//
// Report (gitignored, regenerate locally):
//   documentation/coverage/sweep-relics.md
//   documentation/coverage/sweep-relics.json
public class RelicSweepTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;
    private readonly ITestOutputHelper _output;

    public RelicSweepTests(HostSubprocess host, ITestOutputHelper output)
    {
        _host = host;
        _output = output;
    }

    [Fact]
    [Trait("Category", "MechanicSweep")]
    public async Task AllRelics_NoCrash()
    {
        if (!SweepGate.ShouldRun("RELIC"))
        {
            _output.WriteLine(
                "RelicSweepTests: skipping — set RUN_RELIC_SWEEP=1 (or RUN_MECHANIC_SWEEP=1) to opt in. "
                + "Use `just validation::dotnet::sweep::relics` for the full pass or `just validation::dotnet::sweep::sample <N>` for a fast subset.");
            return;
        }

        var transport = new TransportAdapter(_host);
        var sample = SweepGate.TrySampleIds(RelicIdNames.AllWireNames);
        var version = SweepGate.ReadGameVersion();

        var rowIndex = 0;
        var totalIds = sample?.Count ?? RelicIdNames.AllWireNames.Count;
        _output.WriteLine($"=== relic sweep starting — {totalIds} ids, game={version} ===");

        var report = await new RelicSweep().RunAsync(
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
                        $"  [{rowIndex,4}/{totalIds}] [{row.Outcome,-12}] {row.Id,-32} "
                        + $"steps={row.Steps} {row.Elapsed.TotalSeconds:0.0}s "
                        + (row.Detail ?? ""));
                }
            });

        var (md, _) = SweepReportIo.Write(report);
        _output.WriteLine("=== relic sweep complete ===");
        _output.WriteLine($"  elapsed:     {report.TotalElapsed.TotalMinutes:0.0} min");
        _output.WriteLine($"  crashed:     {report.Crashes}");
        _output.WriteLine($"  timeouts:    {report.Timeouts}");
        _output.WriteLine($"  triggered:   {report.Triggered}");
        _output.WriteLine($"  played:      {report.Played}");
        _output.WriteLine($"  unplayable:  {report.Unplayable}");
        _output.WriteLine($"  report:      {Path.GetRelativePath(Directory.GetCurrentDirectory(), md)}");

        Assert.True(
            report.Crashes == 0 && report.Timeouts == 0,
            $"{report.Crashes} crash(es), {report.Timeouts} timeout(s) — see documentation/coverage/sweep-relics.md");
    }
}
