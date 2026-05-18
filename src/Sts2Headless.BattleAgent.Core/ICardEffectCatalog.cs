using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.BattleAgent.Core;

// Pluggable card-effect knowledge. The CombatModel asks the catalog
// "what does this card do, in this upgrade state?" and applies the
// returned effect. Different catalogs let us:
//   - extend coverage incrementally (IroncladCardCatalog ships first;
//     SilentCardCatalog and a CompositeCatalog land later)
//   - shadow the production catalog in tests (TestCardCatalog returns
//     synthetic effects with known stats)
//   - inject experimental balance changes for what-if analysis
public interface ICardEffectCatalog
{
    // Returns the effect for the card in the given upgrade state.
    // Null means the catalog has no knowledge of this card — the model
    // treats it as a no-op (energy is still spent) so the agent never
    // crashes on an unknown card.
    CardEffect? GetEffect(CardId cardId, bool upgraded);

    // Convenience: ids the catalog has explicit modelling for. Used by
    // coverage tests and by the draft policy ("treat unknown cards as
    // 'no model' so don't draft").
    IReadOnlyCollection<CardId> ModelledIds { get; }
}
