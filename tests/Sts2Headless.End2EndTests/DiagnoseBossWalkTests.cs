using Sts2Headless.Agents.Driving;
using Sts2Headless.Agents.Examples;
using Sts2Headless.IntegrationTests;
using Sts2Headless.Cheats;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.End2EndTests;

// Temporary diagnostic harness around ReachAct1BossTests. Runs the same
// boss-walk but wraps the transport in LoggingTransport so a stall or
// timeout surfaces the exact wire call sequence — answers "which combat
// is the agent stuck in?" rather than "the agent is stuck somewhere."
//
// [Trait("Category", "Diagnostic")] keeps it out of the default test run;
// invoke explicitly via `--filter "Category=Diagnostic"` or by name.
public class DiagnoseBossWalkTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;
    private readonly ITestOutputHelper _output;

    public DiagnoseBossWalkTests(HostSubprocess host, ITestOutputHelper output)
    {
        _host = host;
        _output = output;
    }

    [Fact]
    [Trait("Category", "Diagnostic")]
    public async Task DriveBossWalk_WithFullLogging()
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

        var inner = new HostSubprocessTransport(_host);
        var transport = new LoggingTransport(inner);
        var agent = new GreedyAgent();

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));

        RunStateResult? state = null;
        var healCount = 0;
        Exception? walkError = null;
        try
        {
            while (true)
            {
                state = (await AgentDriver.PlayRunAsync(
                    transport,
                    agent,
                    stopWhen: s => s.CurrentRoomType == RoomType.BossRoom
                                    || (s.CurrentRoomType == RoomType.MapRoom && s.Hp < s.MaxHp),
                    ct: cts.Token)).FinalState;

                if (state.CurrentRoomType == RoomType.BossRoom) break;

                var heal = await transport.SendAsync<DebugSetHpResult>(
                    "debug/set_hp", new DebugSetHpParams(Hp: state.MaxHp));
                Assert.True(heal.Ok);
                healCount++;
                if (healCount >= 50) break;
            }
        }
        catch (Exception ex)
        {
            walkError = ex;
        }

        // Dump the full call log to /tmp for grep/analysis; emit a digest
        // to xUnit output so failing CI still surfaces the headline.
        var logPath = "/tmp/diagnose-boss-walk.log";
        await File.WriteAllLinesAsync(logPath, transport.Log);
        _output.WriteLine($"=== walk done. calls={transport.Log.Count} heals={healCount} error={(walkError?.GetType().Name ?? "none")} ===");
        if (state is not null)
        {
            _output.WriteLine($"=== final state: room={state.CurrentRoomType} floor={state.ActFloor} hp={state.Hp}/{state.MaxHp} gameOver={state.IsGameOver} ===");
        }
        _output.WriteLine($"=== full log written to {logPath} ===");
        if (walkError is not null)
        {
            _output.WriteLine($"=== walk error: {walkError.GetType().Name}: {walkError.Message} ===");
        }

        // Don't assert success — this is a diagnostic. Just emit the log.
    }
}
