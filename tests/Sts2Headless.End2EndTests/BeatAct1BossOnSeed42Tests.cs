using Sts2Headless.Agents;
using Sts2Headless.IntegrationTests;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.End2EndTests;

// The forcing function for Seed42Agent: drive an Ironclad run on seed 42
// from run/new through the act-1 boss combat (VANTOM, 173 HP, SLIPPERY:9)
// and assert the agent kills the boss without dying. No debug/set_hp
// resurrection — the agent must survive on its own from end to end.
//
// When this test goes green, the seed-42 win is locked in as a regression.
// Failures during iteration write a full snapshot trace to
// /tmp/seed42-boss-walk.log so the next iteration can see exactly where
// the agent broke.
//
// Diagnostic category for now — flip off the trait once stable.
public class BeatAct1BossOnSeed42Tests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;
    private readonly ITestOutputHelper _output;

    public BeatAct1BossOnSeed42Tests(HostSubprocess host, ITestOutputHelper output)
    {
        _host = host;
        _output = output;
    }

    // Skipped pending agent iteration: agent reaches floor 9 (post-elite)
    // but dies vs Mawler at hp=0, then hits an engine-side NRE on
    // select_reward (card pick) when the player is at hp=0. The latter
    // is a known engine gap (no patch yet); the former is an agent-skill
    // gap — needs better defence stacking through the floor-8 elite so
    // we enter floor 9 with more HP. Iteration is ongoing.
    [Fact(Skip = "Agent dies at floor 9 Mawler; iteration ongoing — see agent-survival-gaps.md")]
    [Trait("category", "diagnostic")]
    public async Task Seed42Agent_Ironclad_BeatsVantom_NoDebugHeals()
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

        var inner = new HostSubprocessTransport(_host);
        var transport = new ReconTransport(inner);
        var agent = new Seed42Agent();

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        Exception? error = null;
        RunStateResult? state = null;

        try
        {
            // Drive until either:
            //  - HP > 0 in a non-combat room past floor 17 (boss-beaten),
            //  - the run reports game-over (throws inside DriveUntilAsync),
            //  - 3-minute cancellation fires.
            state = await agent.DriveUntilAsync(
                transport,
                stopWhen: s => s.ActFloor >= 18
                                || (s.ActFloor == 17 && s.CurrentRoomType == RoomType.MapRoom),
                ct: cts.Token);
        }
        catch (Exception ex)
        {
            error = ex;
        }

        await File.WriteAllTextAsync("/tmp/seed42-boss-walk.md", transport.Markdown);
        _output.WriteLine($"log_chars={transport.Markdown.Length} state={(state is null ? "null" : $"room={state.CurrentRoomType} floor={state.ActFloor} hp={state.Hp}/{state.MaxHp} gameOver={state.IsGameOver}")}");
        if (error is not null) _output.WriteLine($"error: {error.GetType().Name}: {error.Message}");

        Assert.Null(error);
        Assert.NotNull(state);
        Assert.False(state!.IsGameOver, $"agent died (final hp={state.Hp}/{state.MaxHp}, floor={state.ActFloor}, room={state.CurrentRoomType})");
        Assert.True(state.ActFloor >= 17, $"agent stopped before reaching the boss (floor={state.ActFloor})");
        Assert.True(state.Hp > 0, $"agent at boss with hp={state.Hp}");
    }
}
