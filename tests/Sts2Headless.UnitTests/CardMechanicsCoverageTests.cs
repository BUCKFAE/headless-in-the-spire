using Sts2Headless.Agents;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.UnitTests;

// Sanity checks on the CardMechanics.Catalog ↔ CardId enum relationship.
// The original design here was an explicit `NotYetModelled` HashSet
// covering every card sts2 ships that the agent hasn't modelled — so a
// new card landing in sts2 would surface as a test failure rather than
// silently producing an empty fallback Mechanics(). That design was
// abandoned for a copyright reason: enumerating every card name in
// committed source is the same proprietary content the gitignored
// CardId.g.cs holds, just in a different format. Committing it would
// defeat the gitignore.
//
// What we have instead:
//   * Compile-time guarantee (caught at build, not here): the
//     Mechanics dictionary keys on the CardId enum, so a card removed
//     from sts2 → enum value gone → catalog entry references a missing
//     value → build error. No runtime check needed.
//   * Runtime sanity check (this test): the modelled set is non-empty
//     and every key is a defined, non-Unknown CardId.
//
// What we deliberately do NOT do:
//   * Assert any specific count of total CardId values, modelled
//     cards, or unmodelled cards. Those numbers are facts about the
//     game's proprietary content and we treat them like the DLL bytes
//     they're derived from.
//
// Detecting "sts2 shipped a new card we should consider":
//   * Run `just generate-card-ids` after bumping the game pin
//     (GAME_VERSION).
//   * The generator prints the card count; compare to your local
//     memory or to a private notebook (not a committed file).
//   * Any new CardId.X values surface in the regenerated CardId.g.cs
//     and become available for hand-modelling in CardMechanics.cs.
public class CardMechanicsCoverageTests
{
    [Fact]
    public void ModelledCardIds_AreNonEmpty_AndAllReferenceRealEnumValues()
    {
        Assert.NotEmpty(CardMechanics.ModelledCardIds);
        foreach (var id in CardMechanics.ModelledCardIds)
        {
            Assert.True(Enum.IsDefined(typeof(CardId), id), $"CardMechanics.Catalog references undefined CardId value: {id}");
            Assert.NotEqual(CardId.Unknown, id);
        }
    }
}
