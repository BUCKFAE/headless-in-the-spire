using Sts2Headless.Agents.Contracts;
using Sts2Headless.Agents.Examples;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.UnitTests;

// PotionDrinkingAgent wraps GreedyAgent's combat decision so the first
// usable potion is drunk on combat round 1. Beyond round 1 the agent
// must behave exactly like GreedyAgent — the wrapper exists to surface
// potion content for the coverage sweep, not to change baseline play.
public class PotionDrinkingAgentTests
{
    [Fact]
    public void Round1_WithUsablePotion_DrinksIt()
    {
        var agent = new PotionDrinkingAgent();
        var state = CombatState(
            round: 1,
            potions: new[]
            {
                new OwnedPotion(Index: 0, Id: "BLOCK_POTION", TargetType: TargetType.None, CanUse: true),
            });

        var action = agent.Decide(state);

        var use = Assert.IsType<UsePotion>(action);
        Assert.Equal(0, use.PotionIndex);
        Assert.Null(use.TargetIndex);
    }

    [Fact]
    public void Round1_AnyEnemyPotion_TargetsEnemyZero()
    {
        var agent = new PotionDrinkingAgent();
        var state = CombatState(
            round: 1,
            potions: new[]
            {
                new OwnedPotion(Index: 0, Id: "FIRE_POTION", TargetType: TargetType.AnyEnemy, CanUse: true),
            });

        var use = Assert.IsType<UsePotion>(agent.Decide(state));
        Assert.Equal(0, use.TargetIndex);
    }

    [Fact]
    public void Round1_SkipsUnusablePotions()
    {
        var agent = new PotionDrinkingAgent();
        var state = CombatState(
            round: 1,
            potions: new[]
            {
                new OwnedPotion(Index: 0, Id: "BLOCKED_POTION", TargetType: TargetType.None, CanUse: false),
                new OwnedPotion(Index: 1, Id: "BLOCK_POTION", TargetType: TargetType.None, CanUse: true),
            });

        var use = Assert.IsType<UsePotion>(agent.Decide(state));
        Assert.Equal(1, use.PotionIndex);
    }

    [Fact]
    public void Round1_NoPotions_FallsThroughToGreedyEndTurn()
    {
        // GreedyAgent's DecideCombat returns EndTurn when the hand has no
        // playable cards — empty hand here is the simplest reproduction.
        var agent = new PotionDrinkingAgent();
        var state = CombatState(round: 1, potions: Array.Empty<OwnedPotion>());

        Assert.IsType<EndTurn>(agent.Decide(state));
    }

    [Fact]
    public void Round2_DoesNotDrinkEvenWithUsablePotion()
    {
        // The wrapper is round-1-only by design — saves potion variety
        // for the next combat instead of chain-drinking the whole belt.
        var agent = new PotionDrinkingAgent();
        var state = CombatState(
            round: 2,
            potions: new[]
            {
                new OwnedPotion(Index: 0, Id: "BLOCK_POTION", TargetType: TargetType.None, CanUse: true),
            });

        Assert.IsType<EndTurn>(agent.Decide(state));
    }

    // ── helpers ──────────────────────────────────────────────────────────

    private static RunStateResult CombatState(int round, IReadOnlyList<OwnedPotion> potions) => new(
        Ok: true,
        Character: Character.Ironclad,
        Seed: 1uL,
        Hp: 80, MaxHp: 80,
        Gold: 99,
        DeckSize: 12,
        CurrentRoomType: RoomType.CombatRoom,
        ActFloor: 1,
        CurrentActIndex: 0,
        IsGameOver: false,
        IsVictory: false,
        IsDead: false,
        AvailableMapNodes: Array.Empty<MapNode>(),
        AvailableEventOptions: Array.Empty<EventOption>(),
        AvailableRestSiteOptions: Array.Empty<RestSiteOption>(),
        AvailableMerchantItems: Array.Empty<MerchantItem>(),
        AvailableTreasureRelics: Array.Empty<TreasureRelic>(),
        CombatState: new Sts2Headless.Protocol.Methods.CombatState(
            Round: round,
            Energy: 3, MaxEnergy: 3,
            PlayerBlock: 0,
            IsPlayPhase: true,
            IsInProgress: true,
            DrawPileCount: 5, DiscardPileCount: 0,
            Hand: Array.Empty<Card>(),
            Enemies: new[]
            {
                new Enemy(0, "JAW_WORM", Hp: 40, MaxHp: 40, Block: 0, IntendsAttack: false,
                    Intents: Array.Empty<Intent>(), Powers: Array.Empty<Power>()),
            },
            PlayerPowers: Array.Empty<Power>()),
        RewardsState: null,
        Relics: Array.Empty<Relic>(),
        OwnedPotions: potions,
        TriggeredSincePrev: Array.Empty<TriggerEvent>(),
        TriggeredDropped: 0);
}
