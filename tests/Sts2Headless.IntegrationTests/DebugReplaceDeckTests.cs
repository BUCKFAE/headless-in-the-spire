using Sts2Headless.Cheats;
using Sts2Headless.Protocol;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Coverage for the debug/replace_deck wire surface. Pins:
//   * Deck size after replacement matches the requested card count.
//   * Result.cardIds round-trips the requested ids in order.
//   * Unknown card ids surface InvalidParams (not InternalError).
//   * Upgraded variants are accepted (we don't inspect the upgrade level
//     on the snapshot here — that would require a deck-cards probe we
//     haven't surfaced; the smoke test is "no crash, deck size matches").
//
// Behavior in combat is exercised end-to-end by BeatGameOnSeed42Tests
// where the deck is replaced before the run is driven.
public class DebugReplaceDeckTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;

    public DebugReplaceDeckTests(HostSubprocess host) => _host = host;

    [Fact]
    public async Task ReplaceDeck_ShrinksDeckToRequestedCardCount()
    {
        var start = await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));
        Assert.True(start.Ok);
        var before = await _host.SendAsync<RunStateResult>("run/state");
        Assert.True(before.DeckSize > 4, $"starter deck unexpectedly small ({before.DeckSize})");

        var resp = await _host.SendAsync<DebugReplaceDeckResult>(
            "debug/replace_deck",
            new DebugReplaceDeckParams(new[]
            {
                new CardSpec("STRIKE_IRONCLAD"),
                new CardSpec("STRIKE_IRONCLAD"),
                new CardSpec("DEFEND_IRONCLAD"),
                new CardSpec("BASH"),
            }));

        Assert.True(resp.Ok);
        Assert.Equal(4, resp.DeckSize);
        Assert.Equal(new[] { "STRIKE_IRONCLAD", "STRIKE_IRONCLAD", "DEFEND_IRONCLAD", "BASH" }, resp.CardIds);

        var after = await _host.SendAsync<RunStateResult>("run/state");
        Assert.Equal(4, after.DeckSize);
    }

    [Fact]
    public async Task ReplaceDeck_AcceptsUpgradedCards()
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

        var resp = await _host.SendAsync<DebugReplaceDeckResult>(
            "debug/replace_deck",
            new DebugReplaceDeckParams(new[]
            {
                new CardSpec("POMMEL_STRIKE", UpgradeLevel: 1),
                new CardSpec("POMMEL_STRIKE", UpgradeLevel: 1),
                new CardSpec("HELLRAISER"),
            }));

        Assert.True(resp.Ok);
        Assert.Equal(3, resp.DeckSize);
    }

    [Fact]
    public async Task ReplaceDeck_UnknownCardId_ReturnsInvalidParams()
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

        var err = await _host.ExpectErrorAsync(
            "debug/replace_deck",
            new DebugReplaceDeckParams(new[] { new CardSpec("NOT_A_REAL_CARD") }));
        Assert.Equal(WireErrorCode.InvalidParams, err.Code);
        Assert.Contains("NOT_A_REAL_CARD", err.Message);
    }

    [Fact]
    public async Task ReplaceDeck_EmptyList_ReturnsInvalidParams()
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

        var err = await _host.ExpectErrorAsync(
            "debug/replace_deck",
            new DebugReplaceDeckParams(Array.Empty<CardSpec>()));
        Assert.Equal(WireErrorCode.InvalidParams, err.Code);
        Assert.Contains("non-empty", err.Message);
    }

    [Fact]
    public async Task ReplaceDeck_NegativeUpgradeLevel_ReturnsInvalidParams()
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

        var err = await _host.ExpectErrorAsync(
            "debug/replace_deck",
            new DebugReplaceDeckParams(new[] { new CardSpec("STRIKE_IRONCLAD", UpgradeLevel: -1) }));
        Assert.Equal(WireErrorCode.InvalidParams, err.Code);
        Assert.Contains("upgradeLevel", err.Message);
    }
}
