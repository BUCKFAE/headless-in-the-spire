using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// run/new + run/state happy and error paths. After Pass C, run/new walks
// the full sts2-cli StartRun chain and lands the player at MapRoom with
// StartedWithNeow=false; run/state surfaces the post-boot snapshot.
public class RunLifecycleTests
{
    [Fact]
    public async Task RunNew_Ironclad_Lands_At_MapRoom()
    {
        await using var host = new HostSubprocess();

        var result = await host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Seed: 42uL));

        Assert.True(result.Ok);
        Assert.Equal("ironclad", result.Character);
        Assert.Equal(42uL, result.Seed);
        Assert.Contains("Player", result.PlayerType);
        Assert.Equal("MapRoom", result.CurrentRoomType);
    }

    [Fact]
    public async Task RunNew_UnsupportedCharacter_ReturnsInternalError()
    {
        await using var host = new HostSubprocess();

        var error = await host.ExpectErrorAsync(
            "run/new", new RunNewParams(Character: "silent"));

        Assert.Equal(-32603, error.Code);
        Assert.Contains("silent", error.Message);
    }

    [Fact]
    public async Task RunState_AfterRunNew_ReturnsRunSnapshot()
    {
        await using var host = new HostSubprocess();

        await host.SendAsync<RunNewResult>("run/new", new RunNewParams(Seed: 1uL));
        var state = await host.SendAsync<RunStateResult>("run/state");

        Assert.True(state.Ok);
        Assert.Equal("ironclad", state.Character);
        Assert.Equal(1uL, state.Seed);
        // Ironclad starts at 80/80 — we don't pin the exact number here in
        // case the game rebalances, but the values must be sensible.
        Assert.True(state.Hp > 0);
        Assert.True(state.MaxHp > 0);
        Assert.True(state.Hp <= state.MaxHp);
        Assert.True(state.Gold >= 0);
        Assert.True(state.DeckSize > 0);
        Assert.Equal("MapRoom", state.CurrentRoomType);
        Assert.Equal(0, state.ActFloor);
        Assert.False(state.IsGameOver);
    }

    [Fact]
    public async Task RunState_WithoutRunNew_ReturnsInternalError()
    {
        await using var host = new HostSubprocess();

        var error = await host.ExpectErrorAsync("run/state");

        Assert.Equal(-32603, error.Code);
        Assert.Contains("no active run", error.Message);
    }
}
