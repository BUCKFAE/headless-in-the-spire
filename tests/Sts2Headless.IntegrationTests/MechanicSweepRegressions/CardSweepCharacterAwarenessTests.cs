using Sts2Headless.Cheats;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests.MechanicSweepRegressions;

// Regression pin for the CardSweep "wrong character" fix.
//
// Before the fix CardSweep always ran with Character.Ironclad and
// fed the test card into an Ironclad deck. Class-bound cards (Regent
// / Defect / Necrobinder) returned CanPlay=false because their cost
// is in a resource the wrong character doesn't have (Regent's Stars,
// Defect's orbs, etc.) and surfaced as ~27 Unplayable rows that
// looked like fixture-staging gaps.
//
// The fix: CardSweep now looks up each card's owning character via
// CardOriginPools.OwningCharacter (generated from ModelDb's
// per-character card pools) and starts a run with that character.
// Per-character filler (STRIKE_REGENT / DEFEND_REGENT / etc.) keeps
// the deck-replace round-trip clean — the engine refuses Ironclad
// starter cards in a Regent deck.
//
// These tests pin the happy path for one card from each character so
// a regression in the character-aware fixture surfaces here, ahead of
// the slow full sweep.
public class CardSweepCharacterAwarenessTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;
    public CardSweepCharacterAwarenessTests(HostSubprocess host) => _host = host;

    // One representative card per non-Ironclad character that the sweep
    // confirmed plays cleanly under the fix. ASTRAL_PULSE is Regent's
    // low-cost Stars card, STRIKE_DEFECT is Defect's basic Attack,
    // STRIKE_NECROBINDER + STRIKE_SILENT are starter Attacks for their
    // characters. The Ironclad row is a control — the fix should NOT
    // regress Ironclad cards.
    [Theory]
    [InlineData("STRIKE_IRONCLAD",    "Ironclad")]
    [InlineData("STRIKE_SILENT",      "Silent")]
    [InlineData("STRIKE_DEFECT",      "Defect")]
    [InlineData("ASTRAL_PULSE",       "Regent")]
    [InlineData("STRIKE_NECROBINDER", "Necrobinder")]
    public async Task ClassBoundCard_PlaysUnderItsOwningCharacter(
        string cardId, string expectedPool)
    {
        // The lookup is what the sweep uses. Asserting it here too so
        // a stale map (post-game-bump without regen) surfaces this
        // test red before the slower sweep.
        var pool = CardOriginPools.OfCard(cardId);
        Assert.Equal(expectedPool, pool.ToString());

        var character = CardOriginPools.OwningCharacter(cardId);
        Assert.NotNull(character);

        var transport = new HostSubprocessTransportAdapter(_host);
        await transport.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: character!.Value, Seed: 42uL));
        await transport.SetHpAsync(999, 999);

        // Same shape as CardSweep's FillerDeckFor: per-character
        // starter pair so replace_deck accepts every card.
        var (strikeId, defendId) = character.Value switch
        {
            Character.Ironclad    => ("STRIKE_IRONCLAD",    "DEFEND_IRONCLAD"),
            Character.Silent      => ("STRIKE_SILENT",      "DEFEND_SILENT"),
            Character.Defect      => ("STRIKE_DEFECT",      "DEFEND_DEFECT"),
            Character.Regent      => ("STRIKE_REGENT",      "DEFEND_REGENT"),
            Character.Necrobinder => ("STRIKE_NECROBINDER", "DEFEND_NECROBINDER"),
            _ => throw new System.InvalidOperationException($"unmapped character {character}"),
        };
        var deck = new[]
        {
            (cardId,   0),
            (strikeId, 0), (strikeId, 0),
            (defendId, 0), (defendId, 0),
        };
        await transport.ReplaceDeckAsync(deck);
        await transport.StartCombatAsync("SLIMES_NORMAL");

        // Card lands on turn 1 in a 5-card deck. Confirm it's in hand,
        // then play it at target=0. The fix is verified when play_card
        // succeeds (it used to throw CanPlay=false on non-Ironclad
        // cards in an Ironclad run).
        var state = await transport.SendAsync<RunStateResult>("run/state");
        Assert.NotNull(state.CombatState);
        var pascal = ToPascalCase(cardId);
        var handIdx = state.CombatState!.Hand
            .ToList()
            .FindIndex(c => string.Equals(c.Id.ToString(), pascal, StringComparison.Ordinal));
        Assert.True(handIdx >= 0, $"{cardId} not drawn into hand on turn 1");

        await transport.SendAsync<RunPlayCardResult>(
            "run/play_card", new RunPlayCardParams(CardIndex: handIdx, TargetIndex: 0));
    }

    [Fact]
    public void CardOriginPool_CoversEveryShippedCard()
    {
        // Sanity check on the generated manifest: every wire card id
        // has a non-Unknown pool entry. Catches the case where the
        // generator dropped a pool category mid-bump.
        var missing = new List<string>();
        foreach (var cardId in CardIdNames.AllWireNames)
        {
            if (CardOriginPools.OfCard(cardId) == CardOriginPool.Unknown)
                missing.Add(cardId);
        }
        Assert.True(missing.Count == 0,
            $"CardOriginPool missing {missing.Count} ids: "
            + $"[{string.Join(", ", missing.Take(8))}{(missing.Count > 8 ? ", ..." : "")}]. "
            + "Re-run `just generate-content-ids`.");
    }

    private static string ToPascalCase(string snake)
    {
        var sb = new System.Text.StringBuilder(snake.Length);
        var atWordStart = true;
        foreach (var ch in snake)
        {
            if (ch == '_') { atWordStart = true; continue; }
            sb.Append(atWordStart ? char.ToUpperInvariant(ch) : char.ToLowerInvariant(ch));
            atWordStart = false;
        }
        return sb.ToString();
    }
}
