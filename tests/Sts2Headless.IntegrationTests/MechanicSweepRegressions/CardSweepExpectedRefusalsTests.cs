using Sts2Headless.MechanicSweep;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests.MechanicSweepRegressions;

// Regression pin for the CardExpectedRefusals catalog.
//
// The catalog is currently empty: every card the sweep used to mark as
// "expected to refuse" has either been staged (CLASH / PACTS_END via
// CardSweep.CustomStagingDeckFor + PreStageHandAsync) or filtered out
// at iteration time (the AnyAlly multiplayer-only set via
// CardSweep.AnyAllyMultiplayerOnly).
//
// The catalog plumbing stays around in case a NEW clean-refusal shape
// surfaces in a future engine bump — these tests pin invariants that
// keep the empty state honest and the lookup mechanism live.
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
            + "Either re-run `just build::generate-content-ids` (the id was renamed) "
            + "or remove the row (the card was deleted).");
    }

    [Fact]
    public void CardExpectedRefusalsCatalogIsEmpty()
    {
        // Pins the "every card works" invariant. If a real new clean-
        // refusal shape needs a catalog entry, update this test to
        // explicitly allow it — the explicit edit forces a conversation
        // about whether the entry should instead become a staging recipe
        // in CardSweep.
        Assert.Empty(SweepKnownIssues.CardExpectedRefusals);
    }

    [Fact]
    public void ExpectedRefusal_LookupReturnsFalseForPlainCards()
    {
        // Negative case: STRIKE_IRONCLAD plays cleanly, so it must
        // NOT be on the expected-refusals list. Catches accidental
        // growth of the catalog with cards that don't belong.
        Assert.False(
            SweepKnownIssues.TryGetExpectedRefusal("card", "STRIKE_IRONCLAD", out _),
            "STRIKE_IRONCLAD should never be on CardExpectedRefusals");
    }
}
