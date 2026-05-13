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
        Assert.Equal(Character.Ironclad, result.Character);
        Assert.Equal(42uL, result.Seed);
        Assert.Contains("Player", result.PlayerType);
        Assert.Equal(RoomType.MapRoom, result.CurrentRoomType);
    }

    [Fact]
    public async Task RunNew_WithNeow_Lands_At_EventRoom_WithFullHp()
    {
        // ExtraFields.StartedWithNeow=true makes EnterAct auto-enter the
        // Neow blessing EventRoom. The path used to silently zero HP via
        // missing GodotStubs (Vector2.Zero / Node2D.Position) — gap closed.
        // No wire method yet exists to dismiss the Neow event, so this only
        // verifies the landing shape; the EventRoom is a dead-end for now.
        await using var host = new HostSubprocess();

        var result = await host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Seed: 1uL, WithNeow: true));
        var state = await host.SendAsync<RunStateResult>("run/state");

        Assert.True(result.Ok);
        Assert.Equal(RoomType.EventRoom, result.CurrentRoomType);
        Assert.Equal(RoomType.EventRoom, state.CurrentRoomType);
        Assert.True(state.Hp > 0, $"HP should survive the Neow entry, was {state.Hp}");
        Assert.Equal(state.MaxHp, state.Hp);
        Assert.False(state.IsGameOver);
    }

    [Fact]
    public async Task RunNew_UnsupportedCharacter_ReturnsInternalError()
    {
        await using var host = new HostSubprocess();

        var error = await host.ExpectErrorAsync(
            "run/new", new RunNewParams(Character: Character.Silent));

        Assert.Equal(-32603, error.Code);
        Assert.Contains("Silent", error.Message);
    }

    [Fact]
    public async Task RunState_AfterRunNew_ReturnsRunSnapshot()
    {
        await using var host = new HostSubprocess();

        await host.SendAsync<RunNewResult>("run/new", new RunNewParams(Seed: 1uL));
        var state = await host.SendAsync<RunStateResult>("run/state");

        Assert.True(state.Ok);
        Assert.Equal(Character.Ironclad, state.Character);
        Assert.Equal(1uL, state.Seed);
        // Ironclad starts at 80/80 — we don't pin the exact number here in
        // case the game rebalances, but the values must be sensible.
        Assert.True(state.Hp > 0);
        Assert.True(state.MaxHp > 0);
        Assert.True(state.Hp <= state.MaxHp);
        Assert.True(state.Gold >= 0);
        Assert.True(state.DeckSize > 0);
        Assert.Equal(RoomType.MapRoom, state.CurrentRoomType);
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
