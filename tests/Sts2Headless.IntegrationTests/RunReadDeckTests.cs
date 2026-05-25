using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Cover run/read_deck end-to-end. The wire method is deliberately NOT
// debug-gated — the player's own deck is information they legitimately
// own, and surfacing it without --enable-debug lets a production agent
// inspect its deck at decision points (drafting, smithing, removal).
//
// Two pins:
//   1. After run/new (Ironclad), run/read_deck returns the 10-card
//      starter deck with non-empty displayNames. The wire shape
//      (DeckCard records) must round-trip through the dispatch table.
//   2. The same method is callable WITHOUT --enable-debug — a regression
//      net for the gating posture. If a future refactor moves
//      run/read_deck under the cheat catalog by accident, this test goes
//      red.
public class RunReadDeckTests
{
    [Fact]
    public async Task RunReadDeck_OnFreshIroncladRun_ReturnsTenStarterCardsWithDisplayNames()
    {
        await using var host = NoDebugHost.Start();

        await host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 1uL));

        var deck = await host.SendAsync<RunReadDeckResult>(
            "run/read_deck", new RunReadDeckParams());

        Assert.True(deck.Ok);
        // Ironclad's starter deck is 10 cards (5 Strike, 4 Defend, 1 Bash)
        // at the v0.103.2 pin. A drift here is a sts2.dll content change
        // — the test calls that out explicitly rather than hiding it as
        // "deck has cards".
        Assert.Equal(10, deck.DeckSize);
        Assert.Equal(10, deck.Cards.Count);

        // Every card surfaces a non-empty displayName via the
        // NameLookup-fed SnapshotEnricher (run/read_deck mirrors the
        // snapshot path: each DeckCard runs through names.Card()).
        Assert.All(deck.Cards, c => Assert.False(string.IsNullOrEmpty(c.DisplayName),
            $"DeckCard {c.CardId} has empty displayName — NameLookup likely missed it"));

        // Sanity: the deck includes at least one Strike and one Defend
        // (the starter pool). We don't pin the count of each kind because
        // the wire walk reports Deck.Cards order (insertion), which can
        // shuffle without changing identity.
        Assert.Contains(deck.Cards, c => c.CardId == CardId.StrikeIronclad);
        Assert.Contains(deck.Cards, c => c.CardId == CardId.DefendIronclad);
    }

    [Fact]
    public async Task RunReadDeck_RequiresAnActiveRun()
    {
        await using var host = NoDebugHost.Start();

        // No run/new — every stateful method should reject with a
        // structured error rather than NRE-crashing the host.
        var err = await host.ExpectErrorAsync(
            "run/read_deck", new RunReadDeckParams());

        Assert.Contains("run/new", err.Message);
    }
}
