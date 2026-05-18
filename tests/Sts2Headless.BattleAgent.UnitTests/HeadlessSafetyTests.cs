using Sts2Headless.BattleAgent.Core;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.BattleAgent.UnitTests;

// Pins the planner / model invariants that keep an Ironclad run from
// surfacing engine NREs:
//   * Every catalog entry marked IsHeadlessUnsafe is actually excluded
//     from LegalActions when present in hand.
//   * Cards the catalog doesn't know about ARE NOT returned as
//     LegalActions either (conservative fallback). Catches the regression
//     where a new card lands in CardId.g.cs but we forget to model it,
//     the planner happily issues a play_card for it, and the host NREs.
//   * IroncladDraftPolicy never picks a known-unsafe card even when it
//     is the only offered card.
public sealed class HeadlessSafetyTests
{
    private static readonly ICombatModel Model = new CombatModel(IroncladCardCatalog.Instance);

    [Theory]
    [InlineData(CardId.Whirlwind)]      // discovered 2026-05-18 via 10-seed sweep
    [InlineData(CardId.Headbutt)]
    [InlineData(CardId.Armaments)]
    [InlineData(CardId.BurningPact)]
    [InlineData(CardId.DualWield)]
    [InlineData(CardId.InfernalBlade)]
    public void HeadlessUnsafeCards_NotInLegalActions(CardId id)
    {
        var state = TestFixtures.State(
            energy: 3,
            hand: new[] { TestFixtures.Card(id, 0, cost: 1, targetType: TargetType.AnyEnemy) },
            enemies: new[] { TestFixtures.Enemy(hp: 50) });
        var actions = Model.LegalActions(state);
        Assert.DoesNotContain(actions, a => a is SimPlayCard);
    }

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

    [Fact]
    public void HeadlessUnsafeCard_AppliedDirectly_ReturnsInvalidState()
    {
        // Direct Apply (bypassing LegalActions) must also refuse to
        // execute an unsafe card — defense in depth against any planner
        // path that constructs a SimPlayCard without re-checking
        // legality.
        var state = TestFixtures.State(
            energy: 3,
            hand: new[] { TestFixtures.Card(CardId.Whirlwind, 0, cost: -1) },
            enemies: new[] { TestFixtures.Enemy(hp: 50) });
        var next = Model.Apply(state, new SimPlayCard(0, null));
        Assert.True(next.IsInvalid);
    }

    [Fact]
    public void DraftPolicy_NeverPicksWhirlwind_EvenIfOnlyOption()
    {
        var policy = new BattleAgent.IroncladDraftPolicy();
        var state = MakeRewardState(new[]
        {
            new CardRewardOption(Index: 0, Id: CardId.Whirlwind, Cost: -1),
        });
        var action = policy.Choose(state);
        Assert.IsType<Sts2Headless.Agents.SkipReward>(action);
    }

    [Fact]
    public void DraftPolicy_NeverPicksAnyHeadlessUnsafeCard()
    {
        // Pin the set of known-unsafe cards. Each must be skippable —
        // if the rewards screen ever forces a no-skip card pick, this
        // test stays as documentation of the contract (and the agent
        // will need a separate "least-bad" fallback for that case).
        var policy = new BattleAgent.IroncladDraftPolicy();
        var unsafeCards = new[]
        {
            CardId.Whirlwind,
            CardId.Headbutt,
            CardId.Armaments,
            CardId.BurningPact,
            CardId.DualWield,
            CardId.InfernalBlade,
        };
        foreach (var id in unsafeCards)
        {
            var state = MakeRewardState(new[]
            {
                new CardRewardOption(Index: 0, Id: id, Cost: 1),
            });
            var action = policy.Choose(state);
            Assert.IsType<Sts2Headless.Agents.SkipReward>(action);
        }
    }

    private static Sts2Headless.Protocol.Methods.RunStateResult MakeRewardState(
        IReadOnlyList<CardRewardOption> cards) => new(
            Ok: true,
            Character: Character.Ironclad,
            Seed: 1uL,
            Hp: 80,
            MaxHp: 80,
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
            CombatState: null,
            RewardsState: new RewardsState(new[]
            {
                new RewardOption(Index: 0, Kind: RewardKind.Card, CanSkip: true, Cards: cards),
            }),
            Relics: Array.Empty<Relic>(),
            OwnedPotions: Array.Empty<OwnedPotion>(),
            TriggeredSincePrev: Array.Empty<TriggerEvent>(),
            TriggeredDropped: 0L);
}
