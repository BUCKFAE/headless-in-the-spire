using Sts2Headless.Agents;
using Sts2Headless.Cheats;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Coverage for the merchant slice of the snapshot wire surface. The host
// surfaces AvailableMerchantItems on every snapshot-bearing result, gated
// to MerchantRoom (analogous to AvailableRestSiteOptions ↔ RestSiteRoom).
//
// Seed picked from the diagnostic merchant-scan
// (DiagnoseMerchantSeedScanTests, run after the 2026-05-15 combat-stall
// fix): seed 13 lands a merchant on floor 3 after a single heal, the
// shortest of the 5 surfacing seeds. If sts2 versions change the map
// generator's PRNG and seed 13 no longer hits a merchant fast, re-run the
// scan and pick a new short-path seed — that's a normal version-bump
// chore, not a regression.
//
// Discipline:
//   * Don't pin specific item ids (CardId / RelicId / PotionId) — sts2's
//     merchant rolls are seed-derived and may change without us breaking.
//     Assert shape (non-empty, every item has Cost ≥ 0, every item has a
//     classified Kind) instead.
//   * Don't assert IsAffordable values either — the player's gold at the
//     time of arrival depends on the upstream walk's combat rolls.
//   * The leave-exits test confirms the room-transition contract the
//     agent depends on. If sts2 changes merchants to auto-advance the way
//     rest-sites do for HEAL, this test surfaces the change rather than
//     silently passing.
public class MerchantRoomTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;

    public MerchantRoomTests(HostSubprocess host) => _host = host;

    // Walk until standing on a merchant with items surfaced. Biases the
    // map walk toward Merchant nodes — on every MapRoom step we pick a
    // merchant if one is on the current row, otherwise delegate to the
    // GreedyAgent (which handles combat / events / etc). Heals between
    // rooms via debug/set_hp so the agent doesn't starve.
    private async Task<RunStateResult> WalkToMerchantEntry()
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 13uL));

        var transport = new HostSubprocessAgentTransport(_host);
        var agent = new GreedyAgent();
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        var state = await _host.SendAsync<RunStateResult>("run/state");
        var healCount = 0;
        var lastFloor = state.ActFloor;

        for (var step = 0; step < 2000; step++)
        {
            if (state.CurrentRoomType == RoomType.MerchantRoom
                && state.AvailableMerchantItems.Count > 0)
            {
                return state;
            }

            if (state.IsGameOver)
            {
                Assert.Fail(
                    $"merchant-walk: game over at floor {state.ActFloor} before reaching a merchant. " +
                    $"Heals so far: {healCount}. " +
                    "The greedy agent can't reliably reach merchants in Act 1 without survival tuning — " +
                    "either bump max HP, find a short-path seed, or pre-buff the player.");
            }

            if (state.CurrentRoomType == RoomType.MapRoom && state.Hp < state.MaxHp)
            {
                var heal = await _host.SendAsync<DebugSetHpResult>(
                    "debug/set_hp", new DebugSetHpParams(Hp: state.MaxHp));
                Assert.True(heal.Ok, "debug/set_hp returned ok=false during merchant-walk heal");
                healCount++;
                Assert.True(healCount < 60,
                    $"healed {healCount} times without reaching a merchant — seed has no merchant in the agent's path.");
                state = await _host.SendAsync<RunStateResult>("run/state");
                continue;
            }

            if (state.CurrentRoomType == RoomType.MapRoom)
            {
                var merchant = state.AvailableMapNodes.FirstOrDefault(n => n.Type == MapNodeType.Merchant);
                if (merchant is not null)
                {
                    await _host.SendAsync<RunSelectMapNodeResult>(
                        "run/select_map_node",
                        new RunSelectMapNodeParams(Col: merchant.Col, Row: merchant.Row));
                    state = await _host.SendAsync<RunStateResult>("run/state");
                    continue;
                }
            }

            state = (await AgentDriver.PlayRunAsync(
                transport,
                agent,
                stopWhen: s => s.CurrentRoomType == RoomType.MerchantRoom
                                || (s.CurrentRoomType == RoomType.MapRoom && s.ActFloor != lastFloor)
                                || (s.CurrentRoomType == RoomType.MapRoom && s.Hp < s.MaxHp),
                ct: cts.Token)).FinalState;
            lastFloor = state.ActFloor;
        }

        Assert.Fail($"merchant-walk exceeded 2000 steps without reaching a merchant.");
        return state; // unreachable
    }

    [Fact]
    public async Task WalkToMerchant_SurfacesItems_WithStableShape()
    {
        var state = await WalkToMerchantEntry();

        Assert.Equal(RoomType.MerchantRoom, state.CurrentRoomType);
        Assert.NotEmpty(state.AvailableMerchantItems);

        Assert.All(state.AvailableMerchantItems, item =>
        {
            Assert.True(item.Cost >= 0,
                $"merchant item index={item.Index} has negative cost={item.Cost}");
            Assert.NotEqual(MerchantKind.Unknown, item.Kind);
        });

        for (var i = 0; i < state.AvailableMerchantItems.Count; i++)
        {
            Assert.Equal(i, state.AvailableMerchantItems[i].Index);
        }

        Assert.All(state.AvailableMerchantItems, item =>
        {
            switch (item.Kind)
            {
                case MerchantKind.Card:
                    Assert.False(string.IsNullOrEmpty(item.CardId),
                        $"card item index={item.Index} has empty cardId");
                    Assert.Null(item.RelicId);
                    Assert.Null(item.PotionId);
                    break;
                case MerchantKind.Relic:
                    Assert.False(string.IsNullOrEmpty(item.RelicId),
                        $"relic item index={item.Index} has empty relicId");
                    Assert.Null(item.CardId);
                    Assert.Null(item.PotionId);
                    break;
                case MerchantKind.Potion:
                    Assert.False(string.IsNullOrEmpty(item.PotionId),
                        $"potion item index={item.Index} has empty potionId");
                    Assert.Null(item.CardId);
                    Assert.Null(item.RelicId);
                    break;
                case MerchantKind.CardRemoval:
                    Assert.Null(item.CardId);
                    Assert.Null(item.RelicId);
                    Assert.Null(item.PotionId);
                    break;
                default:
                    Assert.Fail($"unhandled merchant kind {item.Kind} at index {item.Index}");
                    break;
            }
        });

        var kinds = state.AvailableMerchantItems.Select(i => i.Kind).ToHashSet();
        Assert.Contains(MerchantKind.Card, kinds);
        Assert.Contains(MerchantKind.Relic, kinds);
        Assert.Contains(MerchantKind.Potion, kinds);
        Assert.Contains(MerchantKind.CardRemoval, kinds);
    }

    [Fact]
    public async Task BuyMerchantItem_DeductsGold_FlipsStocked_GrantsItem()
    {
        var entry = await WalkToMerchantEntry();
        Assert.Equal(RoomType.MerchantRoom, entry.CurrentRoomType);

        // Pick the cheapest affordable item with a known kind. IsAffordable
        // is already the engine's "Player.Gold >= Cost" computation; if no
        // items are affordable on this seed the test surfaces that as a
        // signal to either re-pick the seed or add a gold pre-buff cheat,
        // rather than passing-but-untested.
        var pre = await _host.SendAsync<RunStateResult>("run/state");
        var pick = pre.AvailableMerchantItems
                       .Where(i => i.IsAffordable && i.IsStocked && i.Kind != MerchantKind.Unknown)
                       .OrderBy(i => i.Cost)
                       .FirstOrDefault();
        Assert.NotNull(pick);
        Assert.True(pre.Gold >= pick.Cost,
            $"chosen item index={pick.Index} cost={pick.Cost} but player has gold={pre.Gold}");

        var preDeck = pre.DeckSize;
        var preRelicIds = pre.Relics.Select(r => r.Id).ToHashSet();
        var prePotionIds = pre.OwnedPotions.Select(p => p.Id).ToList();

        var buy = await _host.SendAsync<RunBuyMerchantItemResult>(
            "run/buy_merchant_item", new RunBuyMerchantItemParams(ItemIndex: pick.Index));
        Assert.True(buy.Ok);
        Assert.Equal(pick.Index, buy.ItemIndex);
        Assert.Equal(RoomType.MerchantRoom, buy.CurrentRoomType);

        // The result envelope carries availableMerchantItems but no Gold —
        // re-read run/state for the gold delta and any kind-specific
        // mutation (deck size for cards, relics/potions for the others).
        var post = await _host.SendAsync<RunStateResult>("run/state");
        Assert.Equal(pre.Gold - pick.Cost, post.Gold);

        var postItem = post.AvailableMerchantItems.First(i => i.Index == pick.Index);
        Assert.False(postItem.IsStocked,
            $"merchant item index={pick.Index} kind={pick.Kind} still IsStocked=true after purchase");

        switch (pick.Kind)
        {
            case MerchantKind.Card:
                Assert.Equal(preDeck + 1, post.DeckSize);
                break;
            case MerchantKind.Relic:
                Assert.NotNull(pick.RelicId);
                Assert.Contains(pick.RelicId, post.Relics.Select(r => r.Id));
                Assert.DoesNotContain(pick.RelicId, preRelicIds);
                break;
            case MerchantKind.Potion:
                Assert.NotNull(pick.PotionId);
                // Potions stack — count must rise by exactly one for the
                // bought id (a fresh slot or +1 on an existing slot).
                var preCount = prePotionIds.Count(p => p == pick.PotionId);
                var postCount = post.OwnedPotions.Count(p => p.Id == pick.PotionId);
                Assert.Equal(preCount + 1, postCount);
                break;
            case MerchantKind.CardRemoval:
                // Card-removal is a service, not an item — verify the
                // deck shrank by exactly one and the merchant rolled the
                // service off the wire (subsequent buys cost more / are
                // unavailable depending on engine policy).
                Assert.Equal(preDeck - 1, post.DeckSize);
                break;
            default:
                Assert.Fail($"unhandled MerchantKind={pick.Kind}");
                break;
        }
    }

    [Fact]
    public async Task LeaveMerchant_ExitsTo_MapRoom()
    {
        var entry = await WalkToMerchantEntry();
        Assert.Equal(RoomType.MerchantRoom, entry.CurrentRoomType);

        var afterLeave = await _host.SendAsync<RunLeaveMerchantRoomResult>(
            "run/leave_merchant_room");

        var finalRoom = afterLeave.CurrentRoomType == RoomType.MapRoom
            ? afterLeave.CurrentRoomType
            : (await _host.SendAsync<RunStateResult>("run/state")).CurrentRoomType;
        Assert.Equal(RoomType.MapRoom, finalRoom);

        var post = await _host.SendAsync<RunStateResult>("run/state");
        Assert.Equal(RoomType.MapRoom, post.CurrentRoomType);
        Assert.Empty(post.AvailableMerchantItems);
    }
}
