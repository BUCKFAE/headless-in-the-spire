using Sts2Headless.Agents.Driving;
using Sts2Headless.Agents.Examples;
using Sts2Headless.Cheats;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Coverage for the SMITH branch of run/select_rest_site_option. The wire
// surface routes a CardSelectIndices hint through the host's installed
// ICardSelector (HeadlessCardSelector), which the engine consults inside
// SmithRestSiteOption.OnSelect → CardSelectCmd.FromDeckForUpgrade. The
// upgrade is observable via debug/read_deck — a card the test pinned with
// debug/replace_deck moves from upgradeLevel=0 to upgradeLevel=1.
//
// This pins:
//   1. CardSelectIndices: [[0]] reaches the engine via the selector queue.
//   2. SmithRestSiteOption upgrades the FIRST upgradable card the engine
//      offers (FromDeckForUpgrade pre-filters non-upgradable cards before
//      our selector sees them).
//   3. The post-SMITH snapshot reports MapRoom — the engine empties Options
//      after SMITH and the host's auto-advance fires (single-pick default).
//   4. debug/read_deck round-trips faithfully (cardId + upgradeLevel).
public class RestSiteSmithTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;

    public RestSiteSmithTests(HostSubprocess host) => _host = host;

    [Fact]
    public async Task SelectSmith_WithCardIndex_UpgradesFirstUpgradableCard()
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

        // Walk to a rest site so the SMITH option is live and enabled.
        // GreedyAgent's DecideRestSite would now pick SMITH itself, so we
        // stop the agent on rest-site entry and drive the SMITH call by
        // hand to keep the assertion explicit.
        var transport = new HostSubprocessAgentTransport(_host);
        var agent = new GreedyAgent();
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        var outcome = await AgentDriver.PlayRunAsync(
            transport,
            agent,
            stopWhen: s => s.CurrentRoomType == RoomType.RestSiteRoom
                            && s.AvailableRestSiteOptions.Any(o => o.IsEnabled
                                && o.OptionId.Equals("SMITH", StringComparison.OrdinalIgnoreCase)),
            ct: cts.Token);
        var entry = outcome.FinalState;

        // Pin the deck to a tiny, fully-upgradable list so the assertion
        // is deterministic regardless of what the agent did to the
        // starter deck on the way here.
        var replace = await transport.ReplaceDeckAsync(new[]
        {
            ("POMMEL_STRIKE", 0),
            ("DEFEND_IRONCLAD", 0),
        });
        Assert.True(replace.Ok);
        Assert.Equal(2, replace.DeckSize);

        var deckBefore = await transport.ReadDeckAsync();
        Assert.Equal(2, deckBefore.DeckSize);
        Assert.All(deckBefore.Cards, c => Assert.Equal(0, c.UpgradeLevel));

        // SMITH is index N in AvailableRestSiteOptions; pick the first
        // enabled SMITH option (OptionId comparison defends against a
        // re-ordering of HEAL vs SMITH).
        var smith = entry.AvailableRestSiteOptions.First(o =>
            o.IsEnabled
            && o.OptionId.Equals("SMITH", StringComparison.OrdinalIgnoreCase));

        var afterPick = await _host.SendAsync<RunSelectRestSiteOptionResult>(
            "run/select_rest_site_option",
            new RunSelectRestSiteOptionParams(
                OptionIndex: smith.Index,
                CardSelectIndices: new[] { new[] { 0 } }));
        Assert.True(afterPick.Ok);

        // SMITH empties Options → host auto-advance flips us to MapRoom.
        // Accept either the immediate response or a follow-up run/state
        // reporting MapRoom; the engine's exact transition timing isn't
        // a wire contract worth pinning here (mirrors HEAL test).
        var finalRoom = afterPick.CurrentRoomType == RoomType.MapRoom
            ? afterPick.CurrentRoomType
            : (await _host.SendAsync<RunStateResult>("run/state")).CurrentRoomType;
        Assert.Equal(RoomType.MapRoom, finalRoom);

        // The actual SMITH assertion: exactly one card got upgraded.
        var deckAfter = await transport.ReadDeckAsync();
        Assert.Equal(2, deckAfter.DeckSize);
        var upgradedCount = deckAfter.Cards.Count(c => c.UpgradeLevel > 0);
        Assert.Equal(1, upgradedCount);
        // SmithCount defaults to 1, so the upgrade lands on the first
        // card index the selector saw (which is the first upgradable
        // card in deck order). With both cards upgradable, that's index 0.
        Assert.Equal(1, deckAfter.Cards[0].UpgradeLevel);
        Assert.Equal(0, deckAfter.Cards[1].UpgradeLevel);
    }
}
