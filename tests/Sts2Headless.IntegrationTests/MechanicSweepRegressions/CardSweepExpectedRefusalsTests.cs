using Sts2Headless.MechanicSweep;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests.MechanicSweepRegressions;

// Regression pin for the CardExpectedRefusals catalog.
//
// Cards listed in SweepKnownIssues.CardExpectedRefusals are Unplayable
// in the smoke CardSweep fixture even with the fixture's debug/set_energy
// + debug/gain_stars resource boost. Two empirical buckets:
//
//   * TargetType=AnyAlly (bitflag=64): CardModel.CanPlay refuses because
//     CombatState.PlayerCreatures.Where(IsAlive).Count() > 1 is
//     structurally false in single-player — these are co-op-multiplayer
//     cards (BELIEVE_IN_YOU, COORDINATE, DEMONIC_SHIELD, IGNITION,
//     INTERCEPT, LARGESSE, LIFT, MIMIC). Out of scope per requirements
//     /01-initial-goals.md.
//
//   * IsPlayable override (bitflag=8): PACTS_END's PactsEnd.get_IsPlayable
//     requires Exhaust pile count >= DynamicVars.Cards (~3); the fixture
//     starts combat with an empty Exhaust pile.
//
// The sweep marks each row Detail with "expected-refusal: <reason>" so
// reports stay self-explanatory.
//
// These tests catch three drift scenarios:
//   (1) An id in the catalog no longer exists in the manifest (rename
//       or removal after a GAME_VERSION bump) — flag it so the
//       catalog stays clean.
//   (2) Pool annotation wiring loses the prefix path
//       (ClassifyWireError → SweepKnownIssues.TryGetExpectedRefusal).
//   (3) The catalog grows stale: a card that used to refuse now
//       plays cleanly because the engine relaxed its CanPlay or our
//       fixture grew the staging — remove the entry.
//
// (3) is best surfaced by the slow sweep flipping the row to Played;
// we don't pin it here because doing so would freeze the catalog
// against legitimate engine improvements.
public class CardSweepExpectedRefusalsTests
{
    [Fact]
    public void EveryExpectedRefusalRefersToARealCardId()
    {
        var manifest = new HashSet<string>(
            CardIdNames.AllWireNames, StringComparer.Ordinal);
        var stale = new List<string>();
        foreach (var (kind, id, _) in SweepKnownIssues.AllExpectedRefusals())
        {
            if (!string.Equals(kind, "card", StringComparison.Ordinal)) continue;
            if (!manifest.Contains(id)) stale.Add(id);
        }
        Assert.True(stale.Count == 0,
            $"SweepKnownIssues.CardExpectedRefusals has stale ids: "
            + $"[{string.Join(", ", stale)}]. "
            + "Either re-run `just generate-content-ids` (the id was renamed) "
            + "or remove the row (the card was deleted).");
    }

    [Fact]
    public void ExpectedRefusal_LookupReturnsTheCatalogedReason()
    {
        // Pick one entry as a representative — the lookup itself is
        // exercised by the sweep on every row, so a smoke check here
        // is enough.
        Assert.True(
            SweepKnownIssues.TryGetExpectedRefusal("card", "PACTS_END", out var reason),
            "PACTS_END should be in CardExpectedRefusals");
        Assert.NotNull(reason);
        // Reason cites the IsPlayable override + the Exhaust-pile predicate
        // it gates on. A drift here (e.g. the engine removes the override
        // or our reason loses the IL citation) is worth surfacing.
        Assert.Contains("IsPlayable", reason!, StringComparison.Ordinal);
        Assert.Contains("Exhaust", reason!, StringComparison.Ordinal);
    }

    [Fact]
    public void ExpectedRefusal_LookupReturnsFalseForPlainCards()
    {
        // Negative case: STRIKE_IRONCLAD plays cleanly, so it must
        // NOT be on the expected-refusals list. Catches accidental
        // copy-paste growth of the catalog.
        Assert.False(
            SweepKnownIssues.TryGetExpectedRefusal("card", "STRIKE_IRONCLAD", out _),
            "STRIKE_IRONCLAD should never be on CardExpectedRefusals");
    }
}
