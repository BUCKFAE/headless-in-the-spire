using Sts2Headless.IntegrationTests;
using Sts2Headless.MechanicSweep;
using Sts2Headless.MechanicSweep.Sweeps;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.MechanicSweepTests;

// Per-id smoke sweep for every PotionId in PotionIdNames.AllWireNames.
// Same opt-in / sampling shape as Card / Relic sweeps. Fixture:
// give_potion + start_combat(SLIMES_NORMAL) + use_potion +
// kill_all_enemies, draining TriggeredSincePrev between calls.
//
// Report (gitignored, regenerate locally):
//   documentation/coverage/sweep-potions.md
//   documentation/coverage/sweep-potions.json
public class PotionSweepTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;
    private readonly ITestOutputHelper _output;

    public PotionSweepTests(HostSubprocess host, ITestOutputHelper output)
    {
        _host = host;
        _output = output;
    }

    [Fact]
    [Trait("Category", "MechanicSweep")]
    public async Task AllPotions_NoCrash()
    {
        if (!SweepGate.ShouldRun("POTION"))
        {
            _output.WriteLine(
                "PotionSweepTests: skipping — set RUN_POTION_SWEEP=1 (or RUN_MECHANIC_SWEEP=1) to opt in. "
                + "Use `just validation::dotnet::sweep::potions` for the full pass or `just validation::dotnet::sweep::sample <N>` for a fast subset.");
            return;
        }

        var transport = new TransportAdapter(_host);
        var sample = SweepGate.TrySampleIds(PotionIdNames.AllWireNames);
        var version = SweepGate.ReadGameVersion();

        var rowIndex = 0;
        var totalIds = sample?.Count ?? PotionIdNames.AllWireNames.Count;
        _output.WriteLine($"=== potion sweep starting — {totalIds} ids, game={version} ===");

        var report = await new PotionSweep().RunAsync(
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
        _output.WriteLine("=== potion sweep complete ===");
        _output.WriteLine($"  elapsed:     {report.TotalElapsed.TotalMinutes:0.0} min");
        _output.WriteLine($"  crashed:     {report.Crashes}");
        _output.WriteLine($"  timeouts:    {report.Timeouts}");
        _output.WriteLine($"  triggered:   {report.Triggered}");
        _output.WriteLine($"  played:      {report.Played}");
        _output.WriteLine($"  unplayable:  {report.Unplayable}");
        _output.WriteLine($"  report:      {Path.GetRelativePath(Directory.GetCurrentDirectory(), md)}");

        Assert.True(
            report.Crashes == 0 && report.Timeouts == 0,
            $"{report.Crashes} crash(es), {report.Timeouts} timeout(s) — see documentation/coverage/sweep-potions.md");
    }
}
