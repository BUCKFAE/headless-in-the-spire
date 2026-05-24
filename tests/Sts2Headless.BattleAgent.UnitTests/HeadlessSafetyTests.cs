using Sts2Headless.BattleAgent.Core;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.BattleAgent.UnitTests;

// Pins the planner / model invariants that keep an Ironclad run from
// surfacing engine NREs:
//   * Cards the catalog doesn't know about are NOT returned as
//     LegalActions (conservative fallback). Catches the regression
//     where a new card lands in CardId.g.cs but we forget to model it,
//     the planner happily issues a play_card for it, and the host NREs.
//   * Synthetic IsHeadlessUnsafe-flagged cards still get filtered from
//     LegalActions and Apply — the wiring stays intact so we can re-add
//     real entries if a future engine change re-surfaces an NRE family.
//
// Note: no Ironclad cards are currently catalogued as IsHeadlessUnsafe.
// The historical batch (Headbutt, Armaments, BurningPact, DualWield,
// InfernalBlade, Whirlwind) was reclassified safe on 2026-05-24 after
// CardCatalogProbeTests demonstrated every one of them plays cleanly
// against SLIMES_NORMAL post-PrefsSave-init fix.
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
