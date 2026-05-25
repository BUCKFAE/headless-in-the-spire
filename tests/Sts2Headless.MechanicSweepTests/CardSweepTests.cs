using Sts2Headless.IntegrationTests;
using Sts2Headless.MechanicSweep;
using Sts2Headless.MechanicSweep.Sweeps;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.MechanicSweepTests;

// Per-CardId smoke sweep — drives every id in CardIdNames.AllWireNames
// through CardSweep's "single-card deck + benign combat" fixture and
// surfaces Crashed / Timeout outcomes as test failures.
//
// Opt in with `just validation::dotnet::sweep::cards` (sets RUN_CARD_SWEEP=1) or
// `just validation::dotnet::sweep::sample <N>` (sets MECHANIC_SWEEP_SAMPLE=N for a fast pass
// across every kind). The sweep is off by default — a full run takes
// ~hours.
//
// Report (gitignored, regenerate locally):
//   documentation/coverage/sweep-cards.md
//   documentation/coverage/sweep-cards.json
public class CardSweepTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;
    private readonly ITestOutputHelper _output;

    public CardSweepTests(HostSubprocess host, ITestOutputHelper output)
    {
        _host = host;
        _output = output;
    }

    [Fact]
    [Trait("Category", "MechanicSweep")]
    public async Task AllCards_NoCrash()
    {
        if (!SweepGate.ShouldRun("CARD"))
        {
            _output.WriteLine(
                "CardSweepTests: skipping — set RUN_CARD_SWEEP=1 (or RUN_MECHANIC_SWEEP=1) to opt in. "
                + "Use `just validation::dotnet::sweep::cards` for the full pass or `just validation::dotnet::sweep::sample <N>` for a fast subset.");
            return;
        }

        var transport = new TransportAdapter(_host);
        var sample = SweepGate.TrySampleIds(CardIdNames.AllWireNames);
        var version = SweepGate.ReadGameVersion();

        var rowIndex = 0;
        var totalIds = sample?.Count ?? CardIdNames.AllWireNames.Count;
        _output.WriteLine($"=== card sweep starting — {totalIds} ids, game={version} ===");

        var report = await new CardSweep().RunAsync(
            transport,
            sampleIds: sample,
            gameVersion: version,
            onRow: row =>
            {
                rowIndex++;
                // Stream every failure outcome live so a long sweep is
                // diagnosable mid-run; informational outcomes only emit
                // every 25 rows to keep the log readable.
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
        _output.WriteLine("=== card sweep complete ===");
        _output.WriteLine($"  elapsed:     {report.TotalElapsed.TotalMinutes:0.0} min");
        _output.WriteLine($"  crashed:     {report.Crashes}");
        _output.WriteLine($"  timeouts:    {report.Timeouts}");
        _output.WriteLine($"  played:      {report.Played}");
        _output.WriteLine($"  unreachable: {report.Unreachable}");
        _output.WriteLine($"  unplayable:  {report.Unplayable}");
        _output.WriteLine($"  report:      {Path.GetRelativePath(Directory.GetCurrentDirectory(), md)}");

        Assert.True(
            report.Crashes == 0 && report.Timeouts == 0,
            $"{report.Crashes} crash(es), {report.Timeouts} timeout(s) — see documentation/coverage/sweep-cards.md");
    }
}
