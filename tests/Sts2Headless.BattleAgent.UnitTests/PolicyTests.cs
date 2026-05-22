using Sts2Headless.Agents.Contracts;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.BattleAgent.UnitTests;

public sealed class PolicyTests
{
    // ── Helpers ───────────────────────────────────────────────────────

    private static RunStateResult NewState(
        int hp = 80,
        int maxHp = 80,
        RewardsState? rewards = null,
        IReadOnlyList<MapNode>? mapNodes = null,
        IReadOnlyList<RestSiteOption>? restOptions = null,
        IReadOnlyList<EventOption>? eventOptions = null,
        RoomType currentRoomType = RoomType.MapRoom) => new(
            Ok: true,
            Character: Character.Ironclad,
            Seed: 1uL,
            Hp: hp,
            MaxHp: maxHp,
            Gold: 99,
            DeckSize: 10,
            CurrentRoomType: currentRoomType,
            ActFloor: 1,
            CurrentActIndex: 0,
            IsGameOver: false,
            IsVictory: false,
            IsDead: false,
            AvailableMapNodes: mapNodes ?? Array.Empty<MapNode>(),
            AvailableEventOptions: eventOptions ?? Array.Empty<EventOption>(),
            AvailableRestSiteOptions: restOptions ?? Array.Empty<RestSiteOption>(),
            AvailableMerchantItems: Array.Empty<MerchantItem>(),
            CombatState: null,
            RewardsState: rewards,
            Relics: Array.Empty<Relic>(),
            OwnedPotions: Array.Empty<OwnedPotion>(),
            TriggeredSincePrev: Array.Empty<TriggerEvent>(),
            TriggeredDropped: 0L);

    // ── Draft ─────────────────────────────────────────────────────────

    [Fact]
    public void DraftClaimsGoldRewards()
    {
        var state = NewState(rewards: new RewardsState(new[]
        {
            new RewardOption(Index: 0, Kind: RewardKind.Gold, CanSkip: false, GoldAmount: 50),
        }));
        var policy = new IroncladDraftPolicy();
        var action = policy.Choose(state);
        var pick = Assert.IsType<SelectReward>(action);
        Assert.Equal(0, pick.RewardIndex);
    }

    [Fact]
    public void DraftPicksHighestTierCardFromOffer()
    {
        // Demon Form (S) offered alongside Anger (C) — pick Demon Form.
        var cards = new[]
        {
            new CardRewardOption(Index: 0, Id: CardId.Anger, Cost: 0),
            new CardRewardOption(Index: 1, Id: CardId.DemonForm, Cost: 3),
            new CardRewardOption(Index: 2, Id: CardId.IronWave, Cost: 1),
        };
        var state = NewState(rewards: new RewardsState(new[]
        {
            new RewardOption(Index: 0, Kind: RewardKind.Card, CanSkip: true, Cards: cards),
        }));
        var policy = new IroncladDraftPolicy();
        var pick = Assert.IsType<SelectReward>(policy.Choose(state));
        Assert.Equal(1, pick.CardIndex);
    }

    [Fact]
    public void DraftSkipsAllLowTierOffers()
    {
        // All three cards are headless-unsafe (F-tier) — skip.
        var cards = new[]
        {
            new CardRewardOption(Index: 0, Id: CardId.Headbutt, Cost: 1),
            new CardRewardOption(Index: 1, Id: CardId.Armaments, Cost: 1),
            new CardRewardOption(Index: 2, Id: CardId.BurningPact, Cost: 1),
        };
        var state = NewState(rewards: new RewardsState(new[]
        {
            new RewardOption(Index: 0, Kind: RewardKind.Card, CanSkip: true, Cards: cards),
        }));
        var policy = new IroncladDraftPolicy();
        var skip = Assert.IsType<SkipReward>(policy.Choose(state));
        Assert.Equal(0, skip.RewardIndex);
    }

    // ── Path ──────────────────────────────────────────────────────────

    [Fact]
    public void PathPrefersElitesWhenFresh()
    {
        var nodes = new[]
        {
            new MapNode(Col: 0, Row: 1, Type: MapNodeType.Monster),
            new MapNode(Col: 1, Row: 1, Type: MapNodeType.Elite),
        };
        var state = NewState(hp: 80, maxHp: 80, mapNodes: nodes);
        var policy = new IroncladPathPolicy();
        var pick = Assert.IsType<SelectMapNode>(policy.Choose(state));
        Assert.Equal(MapNodeType.Elite, FindType(nodes, pick.Col, pick.Row));
    }

    [Fact]
    public void PathPrefersRestSiteWhenHurt()
    {
        var nodes = new[]
        {
            new MapNode(Col: 0, Row: 1, Type: MapNodeType.Monster),
            new MapNode(Col: 1, Row: 1, Type: MapNodeType.Elite),
            new MapNode(Col: 2, Row: 1, Type: MapNodeType.RestSite),
        };
        var state = NewState(hp: 20, maxHp: 80, mapNodes: nodes);
        var policy = new IroncladPathPolicy();
        var pick = Assert.IsType<SelectMapNode>(policy.Choose(state));
        Assert.Equal(MapNodeType.RestSite, FindType(nodes, pick.Col, pick.Row));
    }

    private static MapNodeType FindType(IReadOnlyList<MapNode> nodes, int col, int row)
    {
        foreach (var n in nodes)
            if (n.Col == col && n.Row == row) return n.Type;
        throw new InvalidOperationException("node not found");
    }

    // ── Rest ──────────────────────────────────────────────────────────

    [Fact]
    public void RestSmithsWhenAtFullHp()
    {
        var options = new[]
        {
            new RestSiteOption(Index: 0, OptionId: "HEAL", IsEnabled: true),
            new RestSiteOption(Index: 1, OptionId: "SMITH", IsEnabled: true),
        };
        var state = NewState(hp: 80, maxHp: 80, restOptions: options,
            currentRoomType: RoomType.RestSiteRoom);
        var policy = new IroncladRestPolicy();
        var pick = Assert.IsType<SelectRestSiteOption>(policy.Choose(state));
        Assert.Equal(1, pick.OptionIndex);
    }

    [Fact]
    public void RestHealsWhenHurt()
    {
        var options = new[]
        {
            new RestSiteOption(Index: 0, OptionId: "HEAL", IsEnabled: true),
            new RestSiteOption(Index: 1, OptionId: "SMITH", IsEnabled: true),
        };
        var state = NewState(hp: 30, maxHp: 80, restOptions: options,
            currentRoomType: RoomType.RestSiteRoom);
        var policy = new IroncladRestPolicy();
        var pick = Assert.IsType<SelectRestSiteOption>(policy.Choose(state));
        Assert.Equal(0, pick.OptionIndex);
    }

    // ── Event ─────────────────────────────────────────────────────────

    [Fact]
    public void EventPicksLastUnlockedOption()
    {
        var options = new[]
        {
            new EventOption(Index: 0, TextKey: "A", IsLocked: false),
            new EventOption(Index: 1, TextKey: "B", IsLocked: false),
            new EventOption(Index: 2, TextKey: "C", IsLocked: true),
        };
        var state = NewState(eventOptions: options, currentRoomType: RoomType.EventRoom);
        var policy = new IroncladEventPolicy();
        var pick = Assert.IsType<SelectEventOption>(policy.Choose(state));
        Assert.Equal(1, pick.OptionIndex); // last unlocked
    }
}
