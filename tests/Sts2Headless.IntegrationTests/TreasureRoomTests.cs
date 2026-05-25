using Sts2Headless.Agents.Driving;
using Sts2Headless.Agents.Examples;
using Sts2Headless.Cheats;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Coverage for the treasure-room slice of the wire surface. Unlike rest
// sites or events, a treasure room has no player decision — there's only
// "open the chest". The wire still requires an explicit ack call
// (run/take_treasure or run/skip_treasure) because the engine does not
// auto-transition out on its own: without the call the room sticks at
// TreasureRoom even though every reward has been granted.
//
// Setup uses the GreedyAgent to walk forward until standing on a
// TreasureRoom. The agent already routes through treasure (treasure
// nodes have the lowest pick priority, but the agent still selects them
// when no other option is available); a seed-and-driver pairing surfaces
// the room without us hand-coding a route.
//
// Discipline:
//   * Don't pin a specific relic id — relic drops are seed/RNG-driven and
//     a content patch may renumber the rolling table. Assert that *some*
//     relic was added.
//   * Don't pin gold delta either — extra rewards can vary by act/path.
//   * Do pin the room transition: TreasureRoom → MapRoom after the call.
public class TreasureRoomTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;

    public TreasureRoomTests(HostSubprocess host) => _host = host;

    // Drive the agent until it enters a TreasureRoom but stop *before* it
    // calls run/take_treasure. The stop condition exits when the
    // snapshot shows TreasureRoom; the agent's own treasure branch would
    // immediately leave, so we cut it off here.
    //
    // Heal-between-rooms via debug/set_hp keeps the dumb-by-design agent
    // alive long enough to reach treasure floors (seed 42's treasure is
    // mid-Act-1 and the unhealed agent dies around floor 8). Same pattern
    // as End2EndTests/ReachAct1BossTests — healing is a test-fixture
    // concern, not an agent one.
    private async Task<RunStateResult> WalkToTreasureRoom()
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

        var transport = new HostSubprocessAgentTransport(_host);
        var agent = new GreedyAgent();
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));

        RunStateResult state;
        var healCount = 0;
        while (true)
        {
            state = (await AgentDriver.PlayRunAsync(
                transport,
                agent,
                stopWhen: s => s.CurrentRoomType == RoomType.TreasureRoom
                                || (s.CurrentRoomType == RoomType.MapRoom && s.Hp < s.MaxHp),
                ct: cts.Token)).FinalState;

            if (state.CurrentRoomType == RoomType.TreasureRoom) return state;

            var heal = await _host.SendAsync<DebugSetHpResult>(
                "debug/set_hp", new DebugSetHpParams(Hp: state.MaxHp));
            Assert.True(heal.Ok, "debug/set_hp returned ok=false during treasure-walk heal");
            Assert.True(healCount++ < 50,
                $"healed {healCount} times without reaching a treasure room. " +
                $"Last state: floor={state.ActFloor}, room={state.CurrentRoomType}.");
        }
    }

    [Fact]
    public async Task WalkToTreasureRoom_LandsOnTreasureRoom_WithOfferingAndEmptyOptions()
    {
        var state = await WalkToTreasureRoom();

        // Treasure rooms have no map/event/rest/merchant choices — only
        // the chest offering. The choices slots stay empty; the offering
        // populates via availableTreasureRelics. Pin both so a future
        // engine change that starts surfacing chest-tier picks (rare /
        // common / uncommon picks, mirror chest, …) trips the test
        // rather than silently widening the shape.
        Assert.Equal(RoomType.TreasureRoom, state.CurrentRoomType);
        Assert.Empty(state.AvailableMapNodes);
        Assert.Empty(state.AvailableEventOptions);
        Assert.Empty(state.AvailableRestSiteOptions);
        Assert.Empty(state.AvailableMerchantItems);
        Assert.Null(state.CombatState);

        // The chest offering is populated by the snapshot itself —
        // callers don't need to invoke any preview method. Today's chests
        // offer exactly one relic; assert at-least-one so a future
        // SilverCrucible-style "empty chest" modifier doesn't immediately
        // red the test, but pin non-zero for the seed-42 baseline.
        Assert.NotEmpty(state.AvailableTreasureRelics);
        foreach (var offered in state.AvailableTreasureRelics)
        {
            Assert.NotEqual(RelicId.Unknown, offered.RelicId);
        }
    }

    [Fact]
    public async Task TakeTreasure_GrantsOfferedRelicAndExitsToMap()
    {
        var entry = await WalkToTreasureRoom();
        var relicsBefore = entry.Relics.Select(r => r.Id).ToHashSet();
        var offeredIds = entry.AvailableTreasureRelics.Select(r => r.RelicId).ToArray();
        Assert.NotEmpty(offeredIds);

        var resp = await _host.SendAsync<RunTakeTreasureResult>("run/take_treasure");

        Assert.True(resp.Ok);
        var finalRoom = resp.CurrentRoomType == RoomType.MapRoom
            ? resp.CurrentRoomType
            : (await _host.SendAsync<RunStateResult>("run/state")).CurrentRoomType;
        Assert.Equal(RoomType.MapRoom, finalRoom);

        // The offered relic id must now be in Player.Relics. This pins
        // the snapshot's preview to the actual grant — a future bug where
        // we previewed one relic but granted a different one (e.g. fresh
        // DoNormalRewards roll on leave) would surface here.
        var post = await _host.SendAsync<RunStateResult>("run/state");
        var newRelics = post.Relics.Select(r => r.Id).Where(id => !relicsBefore.Contains(id)).ToArray();
        Assert.NotEmpty(newRelics);
        foreach (var offeredId in offeredIds)
        {
            Assert.Contains(offeredId, post.Relics.Select(r => r.Id));
        }

        // availableTreasureRelics is gated to TreasureRoom — must be
        // empty after exit so callers don't read a stale offering.
        Assert.Empty(post.AvailableTreasureRelics);
    }

    [Fact]
    public async Task SkipTreasure_DoesNotGrantRelicAndExitsToMap()
    {
        var entry = await WalkToTreasureRoom();
        var relicsBefore = entry.Relics.Select(r => r.Id).ToHashSet();
        var offeredIds = entry.AvailableTreasureRelics.Select(r => r.RelicId).ToArray();
        Assert.NotEmpty(offeredIds);

        var resp = await _host.SendAsync<RunSkipTreasureResult>("run/skip_treasure");

        Assert.True(resp.Ok);
        // Same MapRoom transition as the take path — skipping still
        // closes the synchronizer session, runs DoExtraRewardsIfNeeded,
        // and forces back to the map.
        var finalRoom = resp.CurrentRoomType == RoomType.MapRoom
            ? resp.CurrentRoomType
            : (await _host.SendAsync<RunStateResult>("run/state")).CurrentRoomType;
        Assert.Equal(RoomType.MapRoom, finalRoom);

        // Player.Relics must NOT contain any of the offered ids that
        // weren't already in the bag — that's the contract of skip_treasure.
        var post = await _host.SendAsync<RunStateResult>("run/state");
        var postIds = post.Relics.Select(r => r.Id).ToHashSet();
        foreach (var offeredId in offeredIds)
        {
            if (relicsBefore.Contains(offeredId)) continue;  // already had it
            Assert.DoesNotContain(offeredId, postIds);
        }

        Assert.Empty(post.AvailableTreasureRelics);
    }
}
