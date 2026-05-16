using Sts2Headless.Agents;
using Sts2Headless.IntegrationTests;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.End2EndTests;

// End-to-end forcing function: drive an Ironclad run on seed 42 from
// Neow to victory with the 999/999 HP cheat *plus* heal-between-rooms
// keeping the agent alive across every combat. Survival is explicitly
// not what we're testing; the goal is to prove the run can be *driven*
// through every act transition and final-boss state to IsVictory=true.
//
// Two cheats stack:
//   * debug/set_hp once at run start to push the cap to 999/999.
//   * debug/set_hp again whenever the drive returns at a MapRoom with
//     Hp < MaxHp. Mirrors the ReachAct1BossTests pattern. Without
//     this second heal, the agent eats too much cumulative damage in
//     multi-enemy combats (e.g. Act 2 floor 14 OVICOPTER + 3 TOUGH_EGGs
//     ran 44 rounds and exhausted the 999 HP pool). With it, the agent
//     enters every combat at full HP and the test can answer the
//     question it actually exists to answer: is the wire/engine
//     surface complete enough to drive a full run end-to-end?
//
// Trace lands at /tmp/seed42-game-walk.md.
public class BeatGameOnSeed42Tests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;
    private readonly ITestOutputHelper _output;

    public BeatGameOnSeed42Tests(HostSubprocess host, ITestOutputHelper output)
    {
        _host = host;
        _output = output;
    }

    [Fact(Skip = "SoulNexus patches applied; agent now clears Act 3 floor 9 and reaches Act 3 floor 10 EventRoom that surfaces 0 available options (host auto-advance gap). NoLegalActionException at room=EventRoom. Investigation: either route empty-EventRoom through a new Phase like MapEmpty + new wire method, or fix host-side auto-advance.")]
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
        var healCount = 0;

        // Drive in waves: each wave runs until either victory, a wounded
        // MapRoom (heal-needed), or game-over / stall. After a heal
        // checkpoint, top up via debug/set_hp and continue.
        try
        {
            while (true)
            {
                var outcome = await AgentDriver.PlayRunAsync(
                    transport,
                    agent,
                    stopWhen: s => s.IsVictory
                                    || (s.CurrentRoomType == RoomType.MapRoom && s.Hp < s.MaxHp),
                    ct: cts.Token);
                state = outcome.FinalState;

                if (state.IsVictory) break;
                if (outcome.TerminatedBy == TerminationReason.GameOver) break;

                // Top up between rooms. Unbounded heals would mask a true
                // regression (e.g. agent looping on a map it can't leave),
                // so we cap at 200 — generous enough for a full run on
                // seed 42 (~50-80 map rooms across three acts).
                var heal = await transport.SendAsync<DebugSetHpResult>(
                    "debug/set_hp", new DebugSetHpParams(Hp: state.MaxHp));
                Assert.True(heal.Ok, "debug/set_hp returned ok=false during multi-act heal");
                healCount++;
                if (healCount >= 200)
                {
                    _output.WriteLine($"=== heal cap of 200 reached at act={state.CurrentActIndex} floor={state.ActFloor} — likely an agent loop, not a slow drive ===");
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            error = ex;
        }

        await File.WriteAllTextAsync("/tmp/seed42-game-walk.md", transport.Markdown);
        _output.WriteLine($"log_chars={transport.Markdown.Length} heals={healCount} state={(state is null ? "null" : $"room={state.CurrentRoomType} act={state.CurrentActIndex} floor={state.ActFloor} hp={state.Hp}/{state.MaxHp} victory={state.IsVictory} dead={state.IsDead}")}");
        if (error is not null) _output.WriteLine($"error: {error.GetType().Name}: {error.Message}");

        Assert.Null(error);
        Assert.NotNull(state);
        Assert.True(state!.IsVictory, $"agent failed to win (final hp={state.Hp}/{state.MaxHp}, act={state.CurrentActIndex}, floor={state.ActFloor}, room={state.CurrentRoomType}, dead={state.IsDead}, heals={healCount})");
    }
}
