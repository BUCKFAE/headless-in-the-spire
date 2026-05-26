using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// run/new + run/state happy and error paths. run/new walks the full
// sts2-cli StartRun chain and lands the player at the Neow blessing
// EventRoom with StartedWithNeow=true — every STS2 run starts there,
// no opt-out. run/state surfaces the post-boot snapshot;
// RunFixtures.DismissNeow advances past the blessing to floor 1
// MapRoom for tests that need to inspect map state.
//
// Shares one HostSubprocess across the class via IClassFixture: every test
// starts with run/new, and Sts2Bindings.StartRun resets the prior
// RunManager on each call — so previous-test session state does not leak
// forward. Per-character coverage lives in MultiCharacterStartRunTests;
// this class pins the Ironclad lifecycle specifically.
public class RunLifecycleTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;

    public RunLifecycleTests(HostSubprocess host) => _host = host;

    [Fact]
    public async Task RunNew_Ironclad_Lands_At_Neow_EventRoom_WithFullHp()
    {
        // ExtraFields.StartedWithNeow=true is the only path: EnterAct
        // auto-enters the Neow blessing EventRoom. The path used to
        // silently zero HP via missing GodotStubs (Vector2.Zero /
        // Node2D.Position) — gap closed. Event-choice flow lives in
        // EventChoiceTests; this test pins the landing shape: in
        // EventRoom, full HP, run not over.
        var result = await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Seed: 42uL));
        var state = await _host.SendAsync<RunStateResult>("run/state");

        Assert.True(result.Ok);
        Assert.Equal(Character.Ironclad, result.Character);
        Assert.Equal(42uL, result.Seed);
        Assert.Contains("Player", result.PlayerType);
        Assert.Equal(RoomType.EventRoom, result.CurrentRoomType);
        Assert.Equal(RoomType.EventRoom, state.CurrentRoomType);
        Assert.True(state.Hp > 0, $"HP should survive the Neow entry, was {state.Hp}");
        Assert.Equal(state.MaxHp, state.Hp);
        Assert.False(state.IsGameOver);
    }

    [Fact]
    public async Task DismissNeow_AdvancesTo_MapRoom_OnFloorOne()
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Seed: 1uL));
        var state = await RunFixtures.DismissNeow(_host);

        Assert.Equal(RoomType.MapRoom, state.CurrentRoomType);
        Assert.Equal(1, state.ActFloor);
        Assert.NotEmpty(state.AvailableMapNodes);
    }


    [Fact]
    public async Task RunState_AfterRunNew_ReturnsRunSnapshot()
    {
        await _host.SendAsync<RunNewResult>("run/new", new RunNewParams(Seed: 1uL));
        var state = await _host.SendAsync<RunStateResult>("run/state");

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
        Assert.Equal(RoomType.EventRoom, state.CurrentRoomType);
        // Neow lives at engine ActFloor=1 — that's where every run lands.
        Assert.Equal(1, state.ActFloor);
        Assert.False(state.IsGameOver);
    }
}
