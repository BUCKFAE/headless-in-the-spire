using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Claim-side reward behaviour: card reward → deck grows. Owns its own host
// so it runs in parallel with the shape / skip tests.
public class CombatSelectRewardTests
{
    [Fact]
    public async Task SelectCardReward_AddsCardToDeck()
    {
        // After combat ends with a card reward in the offered set, claiming
        // that card grows the deck by one. Pin the deck-size delta to 1 so a
        // future regression that double-adds (or silently drops) is caught.
        await using var host = new HostSubprocess();
        await RunFixtures.StartFreshRunAtMap(host, seed: 42uL);

        // Capture deck size while still on the map (combat enters mutate it
        // through draw piles, not the source deck — but we want the canonical
        // baseline before any post-combat additions).
        var beforeCombat = await host.SendAsync<RunStateResult>("run/state");
        var deckBefore = beforeCombat.DeckSize;

        var rewards = await CombatHelpers.DriveCombatToRewards(host);
        var cardReward = rewards.Available.FirstOrDefault(r => r.Kind == RewardKind.Card);
        Assert.NotNull(cardReward);
        Assert.NotEmpty(cardReward!.Cards!);

        var afterClaim = await host.SendAsync<RunSelectRewardResult>(
            "run/select_reward", new RunSelectRewardParams(RewardIndex: cardReward.Index, CardIndex: 0));
        Assert.True(afterClaim.Ok);

        // Drain any remaining non-card rewards so we land back on the map and
        // can read a clean post-combat deck size.
        var rs = afterClaim.RewardsState;
        for (var safety = 0; safety < 10 && rs is not null && rs.Available.Count > 0; safety++)
        {
            var resp = await host.SendAsync<RunSelectRewardResult>(
                "run/select_reward", new RunSelectRewardParams(RewardIndex: 0, CardIndex: null));
            rs = resp.RewardsState;
        }

        var afterCombat = await host.SendAsync<RunStateResult>("run/state");
        Assert.Equal(RoomType.MapRoom, afterCombat.CurrentRoomType);
        Assert.Equal(deckBefore + 1, afterCombat.DeckSize);
    }
}
