using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.BattleAgent;

// Per-run deck tracker. The wire never exposes deck contents during
// combat — it gives only DeckSize. To let PerfectedStrike (and
// future-archetype-aware draft logic) read the real Strike-named-card
// count, the agent maintains its own running deck across the run:
// initialise with the Ironclad starter deck, append on each successful
// card pick.
//
// What we model:
//   - Starter deck (5 Strike, 4 Defend, 1 Bash). This is the wire-true
//     start for Ironclad on A0 (no Ascender's Bane).
//   - Card rewards taken via DraftPolicy.
//   - Cards bought at the merchant via MerchantPolicy.
//
// What we DON'T model:
//   - Cards removed via merchant card removal (we don't know which
//     card the engine deletes from the deck).
//   - Event-granted cards / curses (events don't surface deltas to
//     the wire in a way we capture).
//   - Smithed (upgraded) cards — upgrade is a property of the card,
//     not a copy, so the count is unaffected.
//
// Where partial knowledge bites: counts may *understate* deck
// composition for "X cards in deck" formulas (PerfectedStrike) when
// the player has gained cards from events. Conservative under-count
// is acceptable: the planner just under-values the scaling card,
// which never causes a wrong-direction play, only a slightly less
// confident one.
public sealed class RunDeckTracker
{
    private readonly List<CardId> _cards = new();

    public IReadOnlyList<CardId> Cards => _cards;

    public RunDeckTracker(Character character = Character.Ironclad)
    {
        Reset(character);
    }

    public void Reset(Character character)
    {
        _cards.Clear();
        switch (character)
        {
            case Character.Ironclad:
                for (var i = 0; i < 5; i++) _cards.Add(CardId.StrikeIronclad);
                for (var i = 0; i < 4; i++) _cards.Add(CardId.DefendIronclad);
                _cards.Add(CardId.Bash);
                break;
            // Other characters' starting decks: extend when we add their agents.
            default:
                break;
        }
    }

    public void AddCard(CardId id) => _cards.Add(id);

    public int CountStrikeNamed()
    {
        var n = 0;
        foreach (var c in _cards)
        {
            if (IsStrikeNamed(c)) n++;
        }
        return n;
    }

    private static bool IsStrikeNamed(CardId id) => id switch
    {
        CardId.StrikeIronclad
            or CardId.PerfectedStrike
            or CardId.PommelStrike
            or CardId.TwinStrike
            or CardId.AshenStrike => true,
        _ => false,
    };
}
