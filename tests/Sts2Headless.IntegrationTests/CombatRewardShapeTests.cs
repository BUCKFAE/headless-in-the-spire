using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Read-only assertions on the shape of the rewards surfaced post-combat —
// no claim/skip side effects. Split into its own class so it runs in
// parallel with the claim/skip-side reward tests.
public class CombatRewardShapeTests
{
    [Fact]
    public async Task PostCombat_RewardsSurfaceAtLeastOneCardChoice()
    {
        // Walk into combat, kill the enemy, then verify the post-combat
        // rewards include a card reward with at least one option. Doesn't
        // claim anything — just shape-checks the wire so a regression in
        // reward generation surfaces fast.
        await using var host = new HostSubprocess();

        var rewards = await CombatHelpers.DriveCombatToRewards(host);
        Assert.NotEmpty(rewards.Available);
        var card = rewards.Available.FirstOrDefault(r => r.Kind == RewardKind.Card);
        Assert.NotNull(card);
        Assert.NotNull(card!.Cards);
        Assert.NotEmpty(card.Cards!);
        Assert.All(card.Cards!, c =>
        {
            Assert.NotEqual(CardId.Unknown, c.Id);
            Assert.True(c.Cost >= 0, $"card option {c.Index} negative cost {c.Cost}");
        });
    }
}
