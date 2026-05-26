using Sts2Headless.BattleAgent.Core;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.BattleAgent.UnitTests;

// Pins the planner / model invariant that keeps an Ironclad run from
// surfacing engine NREs on unmodelled cards: cards the catalog doesn't
// know about are NOT returned as LegalActions (conservative fallback).
// Catches the regression where a new card lands in CardId.g.cs but we
// forget to model it, the planner happily issues a play_card for it,
// and the host NREs.
public sealed class HeadlessSafetyTests
{
    private static readonly ICombatModel Model = new CombatModel(IroncladCardCatalog.Instance);

    [Fact]
    public void CardsNotInCatalog_NotInLegalActions()
    {
        // CardId.Unknown is the wire-deserialise fallback; it has no
        // catalog entry by construction. The planner must skip rather
        // than treat it as "spend energy, no effect".
        var state = TestFixtures.State(
            energy: 3,
            hand: new[] { TestFixtures.Card(CardId.Unknown, 0, cost: 1) },
            enemies: new[] { TestFixtures.Enemy(hp: 50) });
        var actions = Model.LegalActions(state);
        Assert.DoesNotContain(actions, a => a is SimPlayCard);
    }

}
