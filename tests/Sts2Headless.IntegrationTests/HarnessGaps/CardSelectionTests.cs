using Sts2Headless.Cheats;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests.HarnessGaps;

// Gap: cards that prompt the player to choose another card crash the host.
//
// Trigger
//   Headbutt's description ("Deal X damage. Put a card from your Discard
//   Pile on top of your Draw Pile.") drives its OnPlay through one of the
//   `MegaCrit.Sts2.Core.Commands.CardSelectCmd.From*` factories to surface
//   the card-selection prompt. Same shape applies to Armaments and any
//   other "choose a card" effect.
//
// Why it's broken
//   `Sts2Headless.Runtime.HangPatches.PatchCardSelectCmdFactories`
//   (src/Sts2Headless.Runtime/HangPatches.cs, search for the method name)
//   replaces every `CardSelectCmd.From*` body with a prefix that returns
//   `Task.FromResult<TInner>(default)`. That patch was added as a band-aid
//   for *event*-side card-select screens (event handlers NRE on a null
//   CardSelectCmd, but the agent can route around them by picking
//   Leave/Decline). It is the wrong answer for combat cards — an agent
//   can't "decline" a card it drew. The awaited null CardSelectCmd is
//   then dereferenced inside `Headbutt.OnPlay`, surfacing as
//   `System.ArgumentNullException: source` inside
//   `Enumerable.FirstOrDefault[TSource](IEnumerable<TSource>)`.
//
// Fix sketch
//   Install a `MegaCrit.Sts2.Core.TestSupport.ICardSelector` implementation
//   in `Sts2Headless.Runtime` (the reference impl is in
//   `external-tools/sts2-cli/src/Sts2Headless/RunSimulator.cs`,
//   class `HeadlessCardSelector`). Auto-pick first card is a reasonable
//   v1 policy; surface a `run/select_cards` wire method when an agent
//   wants to choose. With the selector in place, narrow
//   `PatchCardSelectCmdFactories` to the event/reward-UI factories only
//   (or drop it entirely if the selector covers them too).
//
// Lifecycle
//   * Born red. Today the `run/play_card` call below throws
//     a JsonRpcError(InternalError) and `SendAsync` re-throws it.
//   * The day the selector lands, the test goes green. Drop the
//     `Gap` trait and move this file to its feature home (probably
//     `CombatCardSelectionTests.cs` next to `CombatTests.cs`).
public class CardSelectionTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;

    public CardSelectionTests(HostSubprocess host) => _host = host;

    [Fact, Trait("Category", "Gap")]
    public async Task Headbutt_PlaysToCompletion_WithoutHostError()
    {
        // run/new — fresh ironclad on a fixed seed so the first-row map
        // shape is reproducible. Seed choice is arbitrary; the bug fires
        // regardless of seed because it's about the card's behaviour, not
        // the run path.
        var start = await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 1uL));
        Assert.True(start.Ok);

        // Pin the deck so Headbutt is guaranteed in the opening hand and
        // playable on turn 1. Five-card deck = whole deck draws, so we
        // don't depend on shuffle order; one Headbutt + filler strikes
        // keep total energy demand low. Card ids are the wire form (see
        // `CardId.g.cs` and `DebugReplaceDeckTests` for the convention).
        var deck = await _host.SendAsync<DebugReplaceDeckResult>(
            "debug/replace_deck",
            new DebugReplaceDeckParams(new[]
            {
                new CardSpec("HEADBUTT"),
                new CardSpec("STRIKE_IRONCLAD"),
                new CardSpec("STRIKE_IRONCLAD"),
                new CardSpec("STRIKE_IRONCLAD"),
                new CardSpec("STRIKE_IRONCLAD"),
            }));
        Assert.True(deck.Ok);

        // Step into the first reachable combat. MapHelpers/CombatHelpers'
        // existing drivers do too much (they end combat); inline a minimal
        // walk so this test focuses on the single play_card call that
        // surfaces the gap.
        var atMap = await _host.SendAsync<RunStateResult>("run/state");
        Assert.Equal(RoomType.MapRoom, atMap.CurrentRoomType);
        var monsterNode = atMap.AvailableMapNodes.First(
            n => n.Type == MapNodeType.Monster && n.Row > 0);
        var inCombat = await _host.SendAsync<RunSelectMapNodeResult>(
            "run/select_map_node",
            new RunSelectMapNodeParams(Col: monsterNode.Col, Row: monsterNode.Row));
        var combat = inCombat.CombatState;
        Assert.NotNull(combat);

        // Headbutt should be in the opening hand (whole-deck draw on a
        // 5-card deck). If draw rules change and it isn't, the test fails
        // here with a clearer message than a downstream play_card error.
        var headbutt = combat.Hand.FirstOrDefault(c => c.Id == CardId.Headbutt);
        Assert.NotNull(headbutt);
        Assert.True(headbutt.CanPlay,
            $"Headbutt drawn but CanPlay=false (cost={headbutt.Cost}, energy={combat.Energy}).");

        // The gap fires here. Today the host returns
        //   JsonRpcError(code=InternalError, message="internal error:
        //     ArgumentNullException: Value cannot be null. (Parameter 'source')")
        // because Headbutt.OnPlay awaits a null CardSelectCmd from our
        // patched factory and then enumerates a null collection derived
        // from it. SendAsync<T> re-throws the JsonRpcError, so the test
        // fails inside this call.
        var afterPlay = await _host.SendAsync<RunPlayCardResult>(
            "run/play_card",
            new RunPlayCardParams(CardIndex: headbutt.Index, TargetIndex: 0));

        // Post-fix expectations (asserted so the test stays useful as a
        // regression after the selector lands):
        //   * Call returns ok=true.
        //   * Combat is still in progress (Headbutt doesn't end combat).
        //   * Headbutt left the hand (moved to discard, per the card's
        //     normal flow).
        //   * One enemy took damage (Headbutt deals damage as well as
        //     moving a discard onto the draw pile).
        Assert.True(afterPlay.Ok);
        Assert.NotNull(afterPlay.CombatState);
        Assert.True(afterPlay.CombatState.IsInProgress);
        Assert.DoesNotContain(afterPlay.CombatState.Hand, c => c.Id == CardId.Headbutt);

        // Damage check: at least one alive enemy lost HP vs the pre-play
        // snapshot, OR an enemy died. Either is a valid post-state.
        var preHpById = combat.Enemies.ToDictionary(e => e.Index, e => e.Hp);
        var damaged = afterPlay.CombatState.Enemies.Any(
            e => preHpById.TryGetValue(e.Index, out var pre) && e.Hp < pre);
        var killed = combat.Enemies.Count > afterPlay.CombatState.Enemies.Count;
        Assert.True(damaged || killed,
            "Headbutt played but no enemy took damage or died.");
    }
}
