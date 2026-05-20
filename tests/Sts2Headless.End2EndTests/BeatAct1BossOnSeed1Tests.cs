using Sts2Headless.Agents;
using Sts2Headless.BattleAgent;
using Sts2Headless.IntegrationTests;
using Sts2Headless.Cheats;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.End2EndTests;

// Regression test for the CEREMONIAL_BEAST_BOSS stall first observed in
// a /play-claude session on seed 1, Ironclad. The symptom was: every
// run/end_turn returned ok=true with isPlayPhase=false, round counter
// frozen at the end-of-turn snapshot, hand empty — because the boss's
// stun-self move (SetStunned / StunnedMove) walks UI animation state
// (_stunTrigger, _stunAnim, _stunSfx) that doesn't exist headless,
// NREs, and the exception is swallowed by TaskHelper.LogTaskExceptions.
// HangPatches.PatchCeremonialBeast (modelled on PatchTestSubject)
// neutralises the move bodies via Task.CompletedTask prefixes; without
// that patch this test fails with a StallDetectedException citing the
// frozen round=N, hp=999, hand=0, intent=Stun fingerprint.
//
// Mirrors BeatAct1BossOnSeed42Tests for shape; differences are:
//   * seed=1 (the formerly failing seed) vs. 42
//   * IroncladAgent (production planner) vs. the seed-42-specific
//     CheatingHellRaisingSeed42Agent
//   * Trace lands in /tmp/seed1-boss-walk.md for post-mortem
public class BeatAct1BossOnSeed1Tests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;
    private readonly ITestOutputHelper _output;

    public BeatAct1BossOnSeed1Tests(HostSubprocess host, ITestOutputHelper output)
    {
        _host = host;
        _output = output;
    }

    [Fact]
    public async Task IroncladAgent_BeatsCeremonialBeast_OnSeed1()
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 1uL));

        // Cheat: 999 maxHP keeps the agent above zero through every
        // pre-boss fight so we test the boss-combat slice, not Act 1
        // attrition. The agent's combat play is unmodified.
        var cheat = await _host.SendAsync<DebugSetHpResult>(
            "debug/set_hp", new DebugSetHpParams(Hp: 999, MaxHp: 999));
        Assert.True(cheat.Ok);

        var inner = new HostSubprocessTransport(_host);
        var transport = new ReconTransport(inner);
        var agent = new IroncladAgent();

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

        Exception? error = null;
        RunStateResult? state = null;

        try
        {
            // Drive until either:
            //  - we cross out of floor 17 into floor 18+ (boss beaten),
            //  - we land back on the act-1 map at floor 17 (boss beaten
            //    via the post-combat reward → MapRoom auto-advance),
            //  - the run reports GameOver, or
            //  - the 5-minute cancellation fires.
            var outcome = await AgentDriver.PlayRunAsync(
                transport,
                agent,
                stopWhen: s => s.ActFloor >= 18
                                || (s.ActFloor == 17 && s.CurrentRoomType == RoomType.MapRoom),
                ct: cts.Token);
            state = outcome.FinalState;
        }
        catch (Exception ex)
        {
            error = ex;
        }

        await File.WriteAllTextAsync("/tmp/seed1-boss-walk.md", transport.Markdown);
        _output.WriteLine(
            $"log_chars={transport.Markdown.Length} "
            + $"state={(state is null ? "null" : $"room={state.CurrentRoomType} floor={state.ActFloor} hp={state.Hp}/{state.MaxHp} gameOver={state.IsGameOver}")}");
        if (error is not null)
            _output.WriteLine($"error: {error.GetType().Name}: {error.Message}");

        Assert.Null(error);
        Assert.NotNull(state);
        Assert.False(state!.IsGameOver,
            $"agent died (final hp={state.Hp}/{state.MaxHp}, floor={state.ActFloor}, room={state.CurrentRoomType})");
        Assert.True(state.ActFloor >= 17,
            $"agent stopped before reaching the boss (floor={state.ActFloor})");
        Assert.True(state.Hp > 0, $"agent at boss with hp={state.Hp}");
    }
}
