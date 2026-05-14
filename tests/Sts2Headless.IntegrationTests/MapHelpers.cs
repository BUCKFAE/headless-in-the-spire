using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Shared driver for tests that need state past the first map row (e.g.
// reaching an in-run `?`-room, which sts2 only places on row 2+). Walks the
// fixed shape: run/new → first row-1 monster → combat to rewards → drain
// rewards → MapRoom with row-2 children available. Mirrors the pattern in
// CombatHelpers; lives separately so the names match the wire surface each
// helper exercises (CombatHelpers = run/play_card/end_turn; MapHelpers =
// run/select_map_node).
internal static class MapHelpers
{
    // Walk from the current run-new state past the first reachable combat,
    // ending back on the map with the row-2 children of the picked monster
    // node available in `availableMapNodes`. Caller is expected to have just
    // called run/new (so we're at row 0 with row-1 picks visible).
    //
    // Drains rewards by skipping card rewards (they require a cardIndex from
    // the pick-3 sub-list, which most tests don't care about) and selecting
    // every non-card reward. After the drain, the host auto-advances back to
    // MapRoom and the returned RunStateResult reflects that.
    public static async Task<RunStateResult> WalkPastFirstCombat(HostSubprocess host)
    {
        var rewards = await CombatHelpers.DriveCombatToRewards(host);

        RewardsState? rs = rewards;
        for (var safety = 0; safety < 20 && rs is not null && rs.Available.Count > 0; safety++)
        {
            var pick = rs.Available[0];
            if (pick.Kind == RewardKind.Card && pick.CanSkip)
            {
                var resp = await host.SendAsync<RunSkipRewardResult>(
                    "run/skip_reward", new RunSkipRewardParams(RewardIndex: pick.Index));
                rs = resp.RewardsState;
            }
            else
            {
                var resp = await host.SendAsync<RunSelectRewardResult>(
                    "run/select_reward", new RunSelectRewardParams(RewardIndex: pick.Index, CardIndex: null));
                rs = resp.RewardsState;
            }
        }

        var state = await host.SendAsync<RunStateResult>("run/state");
        Assert.Equal(RoomType.MapRoom, state.CurrentRoomType);
        return state;
    }
}
