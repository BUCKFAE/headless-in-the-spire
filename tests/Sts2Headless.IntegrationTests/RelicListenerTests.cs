using Sts2Headless.Cheats;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Phase-4 verifiable post-condition: card-obtain rewards must route through
// the engine's CardPileCmd.Add path (not direct deck-list mutation), because
// only the engine path fires the on-card-obtain listener pipeline that relics
// hook via AbstractModel.AfterCardChangedPiles.
//
// LuckyFysh is the regression fixture: an Uncommon, character-agnostic relic
// whose AfterCardChangedPiles override calls PlayerCmd.GainGold(15, Owner)
// every time a card lands in the holder's deck. If a future change reverts
// ClaimCardReward to direct Deck.Cards mutation, the engine hook never
// fires, gold delta stays at zero, and this test fails loudly. Conversely,
// if the engine pipeline starts double-firing the hook, the assertion's
// strict `== 15` traps that too.
//
// Injection mechanism: debug/give_relic uses RelicCmd.Obtain — the same
// engine path RelicReward.OnSelectWrapper takes — so the relic's own
// subscription wiring is exactly what the engine sets up for treasure-room
// relics. No backing-field mutation, no test-only special case.
public class RelicListenerTests
{
    [Fact]
    public async Task SelectCardReward_FiresLuckyFyshOnObtain_GrantsFifteenGold()
    {
        await using var host = new HostSubprocess();
        await RunFixtures.StartFreshRunAtMap(host, seed: 42uL);

        // Inject LuckyFysh BEFORE the first combat so its subscriptions are
        // wired up by the time we land on the rewards screen. Capture deck
        // size + gold immediately after the inject (RelicCmd.Obtain itself
        // mutates neither — both deltas come from the post-combat reward).
        var afterInject = await host.SendAsync<DebugGiveRelicResult>(
            "debug/give_relic", new DebugGiveRelicParams(RelicId: "LUCKY_FYSH"));
        Assert.True(afterInject.Ok);
        Assert.Equal("LUCKY_FYSH", afterInject.RelicId);
        var deckBefore = afterInject.DeckSize;
        var goldBefore = afterInject.Gold;

        var rewards = await CombatHelpers.DriveCombatToRewards(host);
        var cardReward = rewards.Available.FirstOrDefault(r => r.Kind == RewardKind.Card);
        Assert.NotNull(cardReward);
        Assert.NotEmpty(cardReward!.Cards!);

        var afterClaim = await host.SendAsync<RunSelectRewardResult>(
            "run/select_reward", new RunSelectRewardParams(RewardIndex: cardReward.Index, CardIndex: 0));
        Assert.True(afterClaim.Ok);

        // Drain remaining (non-card) rewards so we land back on the map and
        // can read a clean post-combat snapshot. Non-card OnSelectWrappers
        // can credit gold too (the gold reward in particular), so capture
        // gold *before* draining and assert the +15 there.
        var rs = afterClaim.RewardsState;
        var goldAfterCard = (await host.SendAsync<RunStateResult>("run/state")).Gold;
        for (var safety = 0; safety < 10 && rs is not null && rs.Available.Count > 0; safety++)
        {
            var resp = await host.SendAsync<RunSelectRewardResult>(
                "run/select_reward", new RunSelectRewardParams(RewardIndex: 0, CardIndex: null));
            rs = resp.RewardsState;
        }

        var afterCombat = await host.SendAsync<RunStateResult>("run/state");
        Assert.Equal(RoomType.MapRoom, afterCombat.CurrentRoomType);

        // Engine pipeline post-condition: deck grew by 1 AND LuckyFysh's
        // AfterCardChangedPiles fired (gold delta == 15). A direct deck-list
        // mutation would still grow the deck but skip the hook → gold delta
        // would be 0, failing this assertion.
        Assert.Equal(deckBefore + 1, afterCombat.DeckSize);
        Assert.Equal(goldBefore + 15, goldAfterCard);
    }
}
