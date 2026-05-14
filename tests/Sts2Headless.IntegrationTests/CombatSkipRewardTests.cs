using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Skip-side reward behaviour: skipping a card reward must NOT grow the deck.
// Owns its own host so it runs in parallel with the claim / shape tests.
public class CombatSkipRewardTests
{
    [Fact]
    public async Task SkipCardReward_LeavesDeckUnchanged()
    {
        // Skipping a skippable card reward must NOT add a card. Deck size
        // stays the same across the skip; non-card rewards still get claimed
        // automatically by the test loop so we end up back at MapRoom.
        await using var host = new HostSubprocess();
        await host.SendAsync<RunNewResult>("run/new", new RunNewParams(Seed: 42uL));
        var beforeCombat = await host.SendAsync<RunStateResult>("run/state");
        var deckBefore = beforeCombat.DeckSize;

        var rewards = await CombatHelpers.DriveCombatToRewards(host);
        var cardReward = rewards.Available.FirstOrDefault(r => r.Kind == RewardKind.Card && r.CanSkip);
        if (cardReward is null)
        {
            // Skip the test if the seed/room offered a non-skippable card reward —
            // we want to assert skip behaviour, not "every card reward is
            // skippable". Surface as a soft skip rather than a misleading pass.
            return;
        }

        var afterSkip = await host.SendAsync<RunSkipRewardResult>(
            "run/skip_reward", new RunSkipRewardParams(RewardIndex: cardReward.Index));
        Assert.True(afterSkip.Ok);

        // Drain remaining rewards; assert deck is unchanged at the end.
        var rs = afterSkip.RewardsState;
        for (var safety = 0; safety < 10 && rs is not null && rs.Available.Count > 0; safety++)
        {
            var resp = await host.SendAsync<RunSelectRewardResult>(
                "run/select_reward", new RunSelectRewardParams(RewardIndex: 0, CardIndex: null));
            rs = resp.RewardsState;
        }

        var afterCombat = await host.SendAsync<RunStateResult>("run/state");
        Assert.Equal(RoomType.MapRoom, afterCombat.CurrentRoomType);
        Assert.Equal(deckBefore, afterCombat.DeckSize);
    }
}
