using Sts2Headless.Agents;
using Sts2Headless.IntegrationTests;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.End2EndTests;

// End-to-end forcing function: drive an Ironclad run on seed 42 from Neow
// to victory with the 999/999 HP cheat keeping the agent alive through
// any combat. The cheat is the same workaround
// `BeatAct1BossOnSeed42Tests` uses — survival is not what we're testing;
// the goal here is to prove the run can be *driven* through every act
// transition and final-boss state to IsVictory=true.
//
// Status: blocked on at least one new engine hang — THIEVING_HOPPER's
// ESCAPE_ARTIST_POWER turn stalls in an infinite end-turn loop in Act 2
// (observed at seed-42 floor 3, recorded in
// /tmp/seed42-postact1-walk.md). The same shape as the VANTOM/SLIPPERY
// hang we patched for Act 1 (HangPatches.PatchVantomDismemberMove); a
// per-monster Harmony patch is the likely fix. Until those patches
// land, this test stays [Skip]ped so the goal assertion lives in the
// codebase without permanently failing CI.
//
// To run locally: drop the SkipAttribute below and `just test-cs` (or
// invoke by filter). Trace lands at /tmp/seed42-game-walk.md regardless.
public class BeatGameOnSeed42Tests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;
    private readonly ITestOutputHelper _output;

    public BeatGameOnSeed42Tests(HostSubprocess host, ITestOutputHelper output)
    {
        _host = host;
        _output = output;
    }

    [Fact(Skip = "Still chasing Act 2/3 monster-move hangs. The StallDetector catches each new one in ~10 seconds (see fingerprint in the failure message), then we add a Harmony patch in HangPatches.cs. Currently stops at Act 2 floor 12 on BOWLBUG_ROCK's move body. Lift the skip once IsVictory is reachable.")]
    [Trait("category", "diagnostic")]
    public async Task Seed42Agent_Ironclad_WinsTheGame_WithMaxHpCheat()
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

        var cheat = await _host.SendAsync<DebugSetHpResult>(
            "debug/set_hp", new DebugSetHpParams(Hp: 999, MaxHp: 999));
        Assert.True(cheat.Ok);

        var inner = new HostSubprocessTransport(_host);
        var transport = new ReconTransport(inner);
        var agent = new Seed42Agent();

        // 30 minutes is a comfortable upper bound for a full Ironclad run
        // at this agent's decision pace. If we hit it, the test should
        // fail loud rather than silently pass.
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(30));

        Exception? error = null;
        RunStateResult? state = null;

        try
        {
            var outcome = await AgentDriver.PlayRunAsync(
                transport,
                agent,
                stopWhen: s => s.IsVictory,
                ct: cts.Token);
            state = outcome.FinalState;
        }
        catch (Exception ex)
        {
            error = ex;
        }

        await File.WriteAllTextAsync("/tmp/seed42-game-walk.md", transport.Markdown);
        _output.WriteLine($"log_chars={transport.Markdown.Length} state={(state is null ? "null" : $"room={state.CurrentRoomType} act={state.CurrentActIndex} floor={state.ActFloor} hp={state.Hp}/{state.MaxHp} victory={state.IsVictory} dead={state.IsDead}")}");
        if (error is not null) _output.WriteLine($"error: {error.GetType().Name}: {error.Message}");

        Assert.Null(error);
        Assert.NotNull(state);
        Assert.True(state!.IsVictory, $"agent failed to win (final hp={state.Hp}/{state.MaxHp}, act={state.CurrentActIndex}, floor={state.ActFloor}, room={state.CurrentRoomType}, dead={state.IsDead})");
    }
}
