using Sts2Headless.Agents;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Coverage for the treasure-room slice of the wire surface. Unlike rest
// sites or events, a treasure room has no player decision — there's only
// "open the chest". The wire still requires an explicit ack call
// (run/leave_treasure_room) because the engine does not auto-transition
// out on its own: without the call the room sticks at TreasureRoom even
// though every reward has been granted.
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
    // calls run/leave_treasure_room. The stop condition exits when the
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
    public async Task WalkToTreasureRoom_LandsOnTreasureRoom_WithEmptyOptions()
    {
        var state = await WalkToTreasureRoom();

        // Treasure rooms have no choices — the wire's options slots stay
        // empty. Pin this so a future engine change that starts surfacing
        // chest-tier picks (rare/common/uncommon, mirror chest, …) trips
        // the test rather than silently widening the shape.
        Assert.Equal(RoomType.TreasureRoom, state.CurrentRoomType);
        Assert.Empty(state.AvailableMapNodes);
        Assert.Empty(state.AvailableEventOptions);
        Assert.Empty(state.AvailableRestSiteOptions);
        Assert.Null(state.CombatState);
    }

    [Fact]
    public async Task LeaveTreasureRoom_GrantsRelicAndExitsToMap()
    {
        var entry = await WalkToTreasureRoom();
        var relicsBefore = entry.Relics.Count;

        var resp = await _host.SendAsync<RunLeaveTreasureRoomResult>(
            "run/leave_treasure_room");

        Assert.True(resp.Ok);
        // Room must transition back to MapRoom — the engine does not flip
        // on its own; our ForceToMap mirror is what drives it. Accept
        // either the immediate response or a follow-up snapshot reporting
        // MapRoom, since the precise transition tick isn't a wire contract
        // worth pinning here.
        var finalRoom = resp.CurrentRoomType == RoomType.MapRoom
            ? resp.CurrentRoomType
            : (await _host.SendAsync<RunStateResult>("run/state")).CurrentRoomType;
        Assert.Equal(RoomType.MapRoom, finalRoom);

        // At least one relic was granted — don't pin the exact id since
        // chest rolls are seed/RNG-driven and content patches may renumber
        // the rolling table.
        var post = await _host.SendAsync<RunStateResult>("run/state");
        Assert.True(post.Relics.Count > relicsBefore,
            $"expected the chest to grant at least one relic. Before={relicsBefore} after={post.Relics.Count}. " +
            $"Relics: [{string.Join(", ", post.Relics.Select(r => r.Id))}].");
    }
}
