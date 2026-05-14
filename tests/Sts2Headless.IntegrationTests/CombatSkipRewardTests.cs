using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Skip-side reward behaviour: happy path (skipping a card reward does NOT
// grow the deck) plus three error probes pinning the run/skip_reward
// contract:
//   - no pending rewards               → -32603
//   - rewardIndex out of range         → -32603
//   - skip on a non-card reward kind   → -32603
//
// Seed 42 is the canonical fixture seed across the combat suite; the first
// reachable monster combat reliably surfaces both a skippable card reward
// and at least one non-card reward (typically gold), so the error probes
// don't need their own seed search.
//
// Shares one HostSubprocess across the class via IClassFixture: every test
// starts with run/new, which resets the prior RunManager via Sts2Bindings.
public class CombatSkipRewardTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;

    public CombatSkipRewardTests(HostSubprocess host) => _host = host;

    [Fact]
    public async Task SkipCardReward_LeavesDeckUnchanged()
    {
        // Skipping a skippable card reward must NOT add a card. Deck size
        // stays the same across the skip; non-card rewards still get claimed
        // automatically by the test loop so we end up back at MapRoom.
        await _host.SendAsync<RunNewResult>("run/new", new RunNewParams(Seed: 42uL));
        var beforeCombat = await _host.SendAsync<RunStateResult>("run/state");
        var deckBefore = beforeCombat.DeckSize;

        var rewards = await CombatHelpers.DriveCombatToRewards(_host);
        var cardReward = rewards.Available.FirstOrDefault(r => r.Kind == RewardKind.Card && r.CanSkip);
        Assert.NotNull(cardReward);

        var afterSkip = await _host.SendAsync<RunSkipRewardResult>(
            "run/skip_reward", new RunSkipRewardParams(RewardIndex: cardReward!.Index));
        Assert.True(afterSkip.Ok);

        // Drain remaining rewards; assert deck is unchanged at the end.
        var rs = afterSkip.RewardsState;
        for (var safety = 0; safety < 10 && rs is not null && rs.Available.Count > 0; safety++)
        {
            var resp = await _host.SendAsync<RunSelectRewardResult>(
                "run/select_reward", new RunSelectRewardParams(RewardIndex: 0, CardIndex: null));
            rs = resp.RewardsState;
        }

        var afterCombat = await _host.SendAsync<RunStateResult>("run/state");
        Assert.Equal(RoomType.MapRoom, afterCombat.CurrentRoomType);
        Assert.Equal(deckBefore, afterCombat.DeckSize);
    }

    [Fact]
    public async Task SkipReward_NoPendingRewards_ReturnsInternalError()
    {
        // At MapRoom there are no pending rewards. Sts2Bindings.SkipReward
        // throws InvalidOperationException("no pending rewards to skip");
        // the host wire surfaces that as -32603 so callers can't drift state.
        await _host.SendAsync<RunNewResult>("run/new", new RunNewParams(Seed: 42uL));

        var error = await _host.ExpectErrorAsync(
            "run/skip_reward", new RunSkipRewardParams(RewardIndex: 0));

        Assert.Equal(-32603, error.Code);
        Assert.Contains("no pending rewards", error.Message);
    }

    [Fact]
    public async Task SkipReward_OutOfRangeIndex_ReturnsInternalError()
    {
        // With rewards pending, an index outside [0, count) raises
        // ArgumentOutOfRangeException in the bindings → -32603. The error
        // path is non-mutating: the pending list survives the failed call.
        await _host.SendAsync<RunNewResult>("run/new", new RunNewParams(Seed: 42uL));
        var rewards = await CombatHelpers.DriveCombatToRewards(_host);
        Assert.NotEmpty(rewards.Available);

        var error = await _host.ExpectErrorAsync(
            "run/skip_reward", new RunSkipRewardParams(RewardIndex: 99));

        Assert.Equal(-32603, error.Code);
        Assert.Contains("out of range", error.Message);
    }

    [Fact]
    public async Task SkipReward_NonCardReward_ReturnsInternalError()
    {
        // Only card rewards are skippable. Pointing skip at a non-card
        // reward (gold / relic / potion) raises InvalidOperationException
        // ("only card rewards are skippable") → -32603. Seed 42's first
        // combat always offers at least one non-card reward alongside the
        // card pick, so we don't have to seed-shop.
        await _host.SendAsync<RunNewResult>("run/new", new RunNewParams(Seed: 42uL));
        var rewards = await CombatHelpers.DriveCombatToRewards(_host);
        var nonCard = rewards.Available.FirstOrDefault(r => r.Kind != RewardKind.Card);
        Assert.NotNull(nonCard);

        var error = await _host.ExpectErrorAsync(
            "run/skip_reward", new RunSkipRewardParams(RewardIndex: nonCard!.Index));

        Assert.Equal(-32603, error.Code);
        Assert.Contains("only card rewards", error.Message);
    }
}
