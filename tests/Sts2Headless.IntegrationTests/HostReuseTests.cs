using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Idempotency probe for host reuse across tests.
//
// Today every integration test spawns a fresh HostSubprocess, paying the
// ~1s sts2.dll bootstrap per test. The proposed speedup is IClassFixture<
// HostSubprocess> so a class's worth of tests shares one host, with each
// test calling run/new to reset the session. That only works if a second
// run/new on a reused host produces the same snapshot as the first — if
// the game's global singletons (RunManager, ModelDb, RNG) leak state
// across runs, reuse silently corrupts later tests.
//
// This test pins the invariant: same seed, two runs, same snapshot.
public class HostReuseTests
{
    [Fact]
    public async Task RunNew_Twice_SameSeed_ProducesIdenticalSnapshot()
    {
        await using var host = new HostSubprocess();

        var first = await host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Seed: 42uL));
        var firstState = await host.SendAsync<RunStateResult>("run/state");

        var second = await host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Seed: 42uL));
        var secondState = await host.SendAsync<RunStateResult>("run/state");

        // run/new echo
        Assert.Equal(first.Character, second.Character);
        Assert.Equal(first.Seed, second.Seed);
        Assert.Equal(first.PlayerType, second.PlayerType);
        Assert.Equal(first.CurrentRoomType, second.CurrentRoomType);

        // The deterministic shape of the post-boot map: same legal moves in
        // the same order. If RNG or map gen leaks between runs, this is
        // where divergence shows up.
        Assert.Equal(firstState.AvailableMapNodes.Count, secondState.AvailableMapNodes.Count);
        for (var i = 0; i < firstState.AvailableMapNodes.Count; i++)
        {
            Assert.Equal(firstState.AvailableMapNodes[i], secondState.AvailableMapNodes[i]);
        }

        // Scalar state — HP/MaxHp/Gold/DeckSize/room/floor — must match.
        Assert.Equal(firstState.Character, secondState.Character);
        Assert.Equal(firstState.Seed, secondState.Seed);
        Assert.Equal(firstState.Hp, secondState.Hp);
        Assert.Equal(firstState.MaxHp, secondState.MaxHp);
        Assert.Equal(firstState.Gold, secondState.Gold);
        Assert.Equal(firstState.DeckSize, secondState.DeckSize);
        Assert.Equal(firstState.CurrentRoomType, secondState.CurrentRoomType);
        Assert.Equal(firstState.ActFloor, secondState.ActFloor);
        Assert.Equal(firstState.IsGameOver, secondState.IsGameOver);
    }

    [Fact]
    public async Task RunNew_Twice_SameSeed_ProducesIdenticalNeowEventOptions()
    {
        // The Neow path exercises EventRoom + the option-generation RNG,
        // which is a denser leak surface than the bare MapRoom landing.
        // Every run starts at Neow, so the two run/new calls should
        // produce byte-identical event options for the same seed.
        await using var host = new HostSubprocess();

        await host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Seed: 7uL));
        var firstState = await host.SendAsync<RunStateResult>("run/state");

        await host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Seed: 7uL));
        var secondState = await host.SendAsync<RunStateResult>("run/state");

        Assert.Equal(RoomType.EventRoom, firstState.CurrentRoomType);
        Assert.Equal(RoomType.EventRoom, secondState.CurrentRoomType);
        Assert.Equal(firstState.AvailableEventOptions.Count, secondState.AvailableEventOptions.Count);
        for (var i = 0; i < firstState.AvailableEventOptions.Count; i++)
        {
            Assert.Equal(firstState.AvailableEventOptions[i], secondState.AvailableEventOptions[i]);
        }
        Assert.Equal(firstState.Hp, secondState.Hp);
        Assert.Equal(firstState.MaxHp, secondState.MaxHp);
        Assert.Equal(firstState.DeckSize, secondState.DeckSize);
    }
}
