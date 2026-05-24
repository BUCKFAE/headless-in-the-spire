using Sts2Headless.BattleAgent.Core;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.BattleAgent.UnitTests;

// Builders that other test files reuse to assemble synthetic SimStates
// without ceremony. Sole purpose is keeping per-test setup
// readable — every state is "an Ironclad at full HP with these
// specific cards in hand, fighting this specific enemy".
internal static class TestFixtures
{
    public static SimEnemy Enemy(
        int index = 0,
        int hp = 30,
        int maxHp = 30,
        int block = 0,
        int strength = 0,
        int vulnerable = 0,
        int weak = 0,
        EnemyIntent? intent = null,
        string? monsterId = null) => new(
            Index: index,
            MonsterId: monsterId,
            Hp: hp,
            MaxHp: maxHp,
            Block: block,
            Strength: strength,
            Vulnerable: vulnerable,
            Weak: weak,
            Intent: intent);

    public static EnemyIntent Attack(int damage, int hits = 1) =>
        new(IntentKind.Attack, damage, hits, 0);

    public static EnemyIntent Defend(int block) =>
        new(IntentKind.Defend, 0, 0, block);

    public static SimCard Card(
        CardId id,
        int handIndex,
        int? cost = null,
        bool upgraded = false,
        TargetType targetType = TargetType.None,
        bool canPlay = true) => new(
            Id: id,
            Cost: cost ?? DefaultCost(id),
            Upgraded: upgraded,
            TargetType: targetType,
            CanPlayFlag: canPlay,
            OriginalHandIndex: handIndex);

    public static SimState State(
        int hp = 80,
        int maxHp = 80,
        int energy = 3,
        int maxEnergyPerTurn = 3,
        int block = 0,
        PlayerStatus? status = null,
        IReadOnlyList<SimCard>? hand = null,
        IReadOnlyList<SimEnemy>? enemies = null,
        int drawPileCount = 10,
        int discardPileCount = 0,
        int exhaustPileCount = 0,
        int turn = 1,
        int cardsDrawnThisTurn = 0,
        int strikeCardsInDeck = 0) => new(
            Hp: hp,
            MaxHp: maxHp,
            Energy: energy,
            MaxEnergyPerTurn: maxEnergyPerTurn,
            Block: block,
            Turn: turn,
            Status: status ?? PlayerStatus.Empty,
            Hand: hand ?? Array.Empty<SimCard>(),
            DrawPileCount: drawPileCount,
            DiscardPileCount: discardPileCount,
            ExhaustPileCount: exhaustPileCount,
            Enemies: enemies ?? new[] { Enemy() },
            CardsDrawnThisTurn: cardsDrawnThisTurn,
            IsInvalid: false,
            StrikeCardsInDeck: strikeCardsInDeck);

    // A test-only catalog that overlays a single CardId as
    // IsHeadlessUnsafe on top of the production catalog. Used by the
    // unsafe-card-filter tests so the IsHeadlessUnsafe mechanism keeps
    // regression coverage even though no production card currently
    // carries the flag.
    public static ICardEffectCatalog CatalogWithUnsafeOverride(CardId unsafeId) =>
        new SyntheticUnsafeCatalog(IroncladCardCatalog.Instance, unsafeId);

    private sealed class SyntheticUnsafeCatalog(ICardEffectCatalog inner, CardId unsafeId) : ICardEffectCatalog
    {
        public CardEffect? GetEffect(CardId cardId, bool upgraded)
        {
            var baseEffect = inner.GetEffect(cardId, upgraded);
            if (cardId == unsafeId)
            {
                return (baseEffect ?? new CardEffect(IsAttack: true, Damage: 1))
                    with { IsHeadlessUnsafe = true };
            }
            return baseEffect;
        }

        public IReadOnlyCollection<CardId> ModelledIds => inner.ModelledIds;
    }

    // Conventional default costs so test setup reads naturally. Mirrors
    // the actual game (Strike=1, Defend=1, Bash=2, Inflame=1, …) so the
    // tests don't drift from intuition. Anything unspecified defaults
    // to 1.
    private static int DefaultCost(CardId id) => id switch
    {
        CardId.StrikeIronclad => 1,
        CardId.DefendIronclad => 1,
        CardId.Bash => 2,
        CardId.Inflame => 1,
        CardId.BodySlam => 1,
        CardId.PommelStrike => 1,
        CardId.ShrugItOff => 1,
        CardId.TwinStrike => 1,
        CardId.Bludgeon => 3,
        CardId.Uppercut => 2,
        CardId.Bloodletting => 0,
        CardId.Hemokinesis => 1,
        CardId.Whirlwind => -1,    // X-cost
        CardId.PactsEnd => 2,
        CardId.Rampage => 1,
        CardId.PerfectedStrike => 2,
        CardId.DemonForm => 3,
        CardId.Offering => 0,
        CardId.FeelNoPain => 1,
        CardId.Barricade => 3,
        CardId.Corruption => 3,
        CardId.Impervious => 2,
        _ => 1,
    };
}
