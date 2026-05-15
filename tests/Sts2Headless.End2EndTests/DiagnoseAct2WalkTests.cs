using Sts2Headless.Agents;
using Sts2Headless.IntegrationTests;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.End2EndTests;

// Slice (a) of "beat all acts": find out what the wire/engine actually do
// after VANTOM falls on seed 42. Today we stop the green test at floor 17
// (post-boss MapRoom), so the post-act-1 surface is unobserved territory —
// before committing to a multi-act drive we need to know:
//
//   * does ActFloor keep counting (18, 19, …) or reset to a new act?
//   * does a new MapRoom land naturally, or does the engine wait for
//     something explicit (a `run/enter_next_act` we haven't built)?
//   * does any unhandled RoomType surface (a NeowRoom-style transition
//     screen, a victory screen, an unknown room name)?
//   * does IsGameOver flip — and if so, is it victory or a crash?
//   * does the bound combat / map / event surface keep working for the
//     act-2 enemies, or does an unbound code path NRE?
//
// The probe shares the existing setup verbatim (Seed42Agent + cheat HP +
// ReconTransport) and only differs in two ways:
//   1. the stop condition never matches (we drive until something stops us),
//   2. the markdown trace lands in /tmp/seed42-postact1-walk.md.
//
// [Trait("category", "diagnostic")] keeps this out of the default run.
public class DiagnoseAct2WalkTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;
    private readonly ITestOutputHelper _output;

    public DiagnoseAct2WalkTests(HostSubprocess host, ITestOutputHelper output)
    {
        _host = host;
        _output = output;
    }

    [Fact]
    [Trait("category", "diagnostic")]
    public async Task Seed42Agent_DriveBeyondAct1Boss_DumpsTrace()
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

        var cheat = await _host.SendAsync<DebugSetHpResult>(
            "debug/set_hp", new DebugSetHpParams(Hp: 999, MaxHp: 999));
        Assert.True(cheat.Ok);

        var inner = new HostSubprocessTransport(_host);
        var transport = new ReconTransport(inner);
        var agent = new Seed42Agent();

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(8));

        Exception? error = null;
        RunStateResult? state = null;

        try
        {
            // Stop condition that never fires — we want to see how far the
            // agent gets before something throws (stall detection, unhandled
            // RoomType, step-budget overflow, cancellation) or terminates
            // naturally (IsGameOver returns via RunOutcome).
            var outcome = await AgentDriver.PlayRunAsync(
                transport,
                agent,
                stopWhen: _ => false,
                ct: cts.Token);
            state = outcome.FinalState;
        }
        catch (Exception ex)
        {
            error = ex;
        }

        var path = "/tmp/seed42-postact1-walk.md";
        await File.WriteAllTextAsync(path, transport.Markdown);

        // Read back the final live state — if `DriveUntilAsync` threw, the
        // last `state` we captured is null; ask the host directly so the
        // digest still tells us where we ended up.
        RunStateResult? final = state;
        try { final ??= await _host.SendAsync<RunStateResult>("run/state"); }
        catch { /* host may have died too; the trace file still has the tail */ }

        _output.WriteLine($"=== trace: {path} ({transport.Markdown.Length} chars) ===");
        if (final is not null)
        {
            _output.WriteLine($"=== final: room={final.CurrentRoomType} floor={final.ActFloor} hp={final.Hp}/{final.MaxHp} gameOver={final.IsGameOver} ===");
        }
        else
        {
            _output.WriteLine("=== final: <unavailable> ===");
        }
        if (error is not null)
        {
            _output.WriteLine($"=== error: {error.GetType().Name}: {error.Message} ===");
        }
        else
        {
            _output.WriteLine("=== error: <none — stop condition was unreachable, so the drive loop exited some other way> ===");
        }
    }
}
