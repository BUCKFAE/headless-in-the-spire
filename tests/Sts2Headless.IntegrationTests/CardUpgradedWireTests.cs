using Sts2Headless.BattleAgent.Core;
using Sts2Headless.Cheats;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Regression net for the now-closed gap at SimStateBuilder.cs:28 (and the
// "Upgraded not on wire" docstring above it). The wire Card record gained an
// `upgraded` bool that reflects CardModel.IsUpgraded — true when the card
// sits at the max upgrade level for its class. Before this surfaced, the
// BattleAgent's planner read every card as base, so an upgraded Strike
// silently routed through IroncladCardCatalog's base row (6 dmg instead of
// 9), which can swing single-turn lethal decisions.
//
// The shape of this regression:
//   * Pin a deck with one base Strike + one +1 Strike via debug/replace_deck.
//   * Drive into combat. The whole-deck draw on a 2-card deck guarantees
//     both Strikes land in the opening hand.
//   * Assert the wire surface: exactly one of the two hand cards has
//     Upgraded == true.
//   * Assert the consumer chain: SimStateBuilder.FromWire propagates the
//     bit one-to-one, so exactly one SimCard.Upgraded is true.
//
// Not Gap-traited — this is a closed-gap regression net, the same posture
// as CombatCardSelectionTests for the card-selector NRE.
public class CardUpgradedWireTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;

    public CardUpgradedWireTests(HostSubprocess host) => _host = host;

    [Fact]
    public async Task UpgradedStrike_SurfacesOnWire_AndPropagatesToSimState()
    {
        // Dismiss Neow first so debug/replace_deck mutates the live MapRoom
        // deck, not the in-Neow deck (the engine swaps cards around on the
        // Neow-pick transition and wipes pre-Neow replacements).
        await RunFixtures.StartFreshRunAtMap(_host, character: Character.Ironclad, seed: 1uL);

        // Seed the deck with one base Strike + one +1 Strike. UpgradeLevel: 1
        // routes through CardModel.UpgradeInternal one step in debug/replace_deck
        // — for Strike that's the max upgrade level, so IsUpgraded flips true.
        var replace = await _host.SendAsync<DebugReplaceDeckResult>(
            "debug/replace_deck",
            new DebugReplaceDeckParams(new[]
            {
                new CardSpec("STRIKE_IRONCLAD"),
                new CardSpec("STRIKE_IRONCLAD", UpgradeLevel: 1),
            }));
        Assert.True(replace.Ok);
        Assert.Equal(2, replace.DeckSize);

        var combat = await EnterFirstCombat();

        // Whole-deck draw on a 2-card deck — both Strikes are in hand.
        Assert.Equal(2, combat.Hand.Count);
        Assert.All(combat.Hand, c => Assert.Equal(CardId.StrikeIronclad, c.Id));

        var upgradedCount = combat.Hand.Count(c => c.Upgraded);
        Assert.Equal(1, upgradedCount);

        // SimStateBuilder consumer parity: the bit threads through one-to-one.
        // hp/maxHp don't matter for the upgraded-card assertion; pass dummy
        // values rather than fetching run/state.
        var sim = SimStateBuilder.FromWire(combat, currentHp: 80, maxHp: 80);
        Assert.Equal(2, sim.Hand.Count);
        Assert.Equal(1, sim.Hand.Count(c => c.Upgraded));
    }

    private async Task<CombatState> EnterFirstCombat()
    {
        var atMap = await _host.SendAsync<RunStateResult>("run/state");
        Assert.Equal(RoomType.MapRoom, atMap.CurrentRoomType);
        var monsterNode = atMap.AvailableMapNodes.First(
            n => n.Type == MapNodeType.Monster && n.Row > 0);
        var inCombat = await _host.SendAsync<RunSelectMapNodeResult>(
            "run/select_map_node",
            new RunSelectMapNodeParams(Col: monsterNode.Col, Row: monsterNode.Row));
        Assert.NotNull(inCombat.CombatState);
        return inCombat.CombatState;
    }
}
