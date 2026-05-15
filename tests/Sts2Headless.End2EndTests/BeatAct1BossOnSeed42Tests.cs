using Sts2Headless.Agents;
using Sts2Headless.IntegrationTests;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.End2EndTests;

// Cheat-mode forcing function: drive an Ironclad run on seed 42 through
// the act-1 boss combat (VANTOM, 173 HP, SLIPPERY:9) with the player
// pumped to 999/999 HP at the start. The agent itself plays normally;
// only the HP cap is artificial — a deliberate workaround for two
// engine gaps (Phrog+wriggler elite damage budget; hp=0 select_reward
// NRE) so the boss-combat slice can ship while a fair-start agent is
// the next iteration.
//
// Full trace lands in /tmp/seed42-boss-walk.md on every run.
public class BeatAct1BossOnSeed42Tests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;
    private readonly ITestOutputHelper _output;

    public BeatAct1BossOnSeed42Tests(HostSubprocess host, ITestOutputHelper output)
    {
        _host = host;
        _output = output;
    }

    [Fact]
    [Trait("category", "diagnostic")]
    public async Task Seed42Agent_Ironclad_BeatsVantom_WithMaxHpCheat()
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

        // Cheat: 999 maxHP keeps the agent above zero through every
        // pre-boss fight. The agent's combat play is unmodified.
        var cheat = await _host.SendAsync<DebugSetHpResult>(
            "debug/set_hp", new DebugSetHpParams(Hp: 999, MaxHp: 999));
        Assert.True(cheat.Ok);

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
            //  - the run reports game-over (RunOutcome with TerminationReason.GameOver),
            //  - 3-minute cancellation fires.
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
