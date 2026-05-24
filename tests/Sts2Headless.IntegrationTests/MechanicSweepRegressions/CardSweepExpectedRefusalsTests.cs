using Sts2Headless.MechanicSweep;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests.MechanicSweepRegressions;

// Regression pin for the CardExpectedRefusals catalog.
//
// Cards listed in SweepKnownIssues.CardExpectedRefusals are Unplayable
// in the smoke CardSweep fixture by design — their CanPlay predicate
// hinges on runtime state our fixture doesn't stage (a non-empty Pact
// stack for PACTS_END, a 'last card played' for MIMIC, an active Orb
// for the Defect cards, multi-Star accumulation for late-game Regent
// cards, etc.). The sweep marks their Detail with
// "expected-refusal: <reason>" so reports stay self-explanatory.
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
        Assert.Contains("Pact", reason!, StringComparison.OrdinalIgnoreCase);
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
