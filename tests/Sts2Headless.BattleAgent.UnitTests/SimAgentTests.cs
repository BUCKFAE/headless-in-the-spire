using Sts2Headless.Agents.Contracts;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.BattleAgent.UnitTests;

public sealed class SimAgentTests
{
    private static RunStateResult InCombat(CombatState combat, int hp = 80, int maxHp = 80) =>
        new(
            Ok: true,
            Character: Character.Ironclad,
            Seed: 1uL,
            Hp: hp,
            MaxHp: maxHp,
            Gold: 99,
            DeckSize: 10,
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
            CombatState: combat,
            RewardsState: null,
            Relics: Array.Empty<Relic>(),
            OwnedPotions: Array.Empty<OwnedPotion>(),
            TriggeredSincePrev: Array.Empty<TriggerEvent>(),
            TriggeredDropped: 0L);

    [Fact]
    public void TranslatesPlanIntoPlayCardWithEngineHandIndex()
    {
        // Engine sees an Ironclad with one Strike at engine hand index
        // 4 (deliberately non-zero so we can tell SimAgent isn't
        // hard-wiring 0). The wire's Card.Index → AgentAction.PlayCard.cardIndex.
        var combat = new CombatState(
            Round: 1,
            Energy: 3,
            MaxEnergy: 3,
            PlayerBlock: 0,
            IsPlayPhase: true,
            IsInProgress: true,
            DrawPileCount: 5,
            DiscardPileCount: 0,
            Hand: new[]
            {
                new Card(Index: 4, Id: CardId.StrikeIronclad, Cost: 1, CanPlay: true, TargetType: TargetType.AnyEnemy, Upgraded: false),
            },
            Enemies: new[]
            {
                new Enemy(
                    Index: 0,
                    MonsterId: "TESTONE_HP_MONSTER",
                    Hp: 6,
                    MaxHp: 6,
                    Block: 0,
                    IntendsAttack: false,
                    Intents: Array.Empty<Intent>(),
                    Powers: Array.Empty<Power>()),
            },
            PlayerPowers: Array.Empty<Power>());

        var agent = new SimAgent();
        var action = agent.Decide(InCombat(combat));

        var play = Assert.IsType<PlayCard>(action);
        Assert.Equal(4, play.CardIndex);
        Assert.Equal(0, play.TargetIndex);
    }

    [Fact]
    public void EndsTurnOnEmptyHand()
    {
        var combat = new CombatState(
            Round: 1,
            Energy: 3,
            MaxEnergy: 3,
            PlayerBlock: 0,
            IsPlayPhase: true,
            IsInProgress: true,
            DrawPileCount: 0,
            DiscardPileCount: 0,
            Hand: Array.Empty<Card>(),
            Enemies: new[]
            {
                new Enemy(
                    Index: 0,
                    MonsterId: "M",
                    Hp: 20,
                    MaxHp: 20,
                    Block: 0,
                    IntendsAttack: true,
                    Intents: new[] { new Intent(IntentKind.Attack, 5, 1, 0) },
                    Powers: Array.Empty<Power>()),
            },
            PlayerPowers: Array.Empty<Power>());

        var agent = new SimAgent();
        var action = agent.Decide(InCombat(combat));
        Assert.IsType<EndTurn>(action);
    }
}
