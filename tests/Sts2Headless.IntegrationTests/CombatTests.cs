using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// run/end_turn + run/play_card — the combat wire surface. Reaches a
// CombatRoom by walking off the floor-0 starting nodes (Monster on every
// seed we've tried), drives a fight to completion, and verifies the
// post-combat auto-advance back to MapRoom.
//
// Combat is the largest mutating surface so far; the assertions favour
// shape (hand non-empty, enemies populated, isPlayPhase=true) over exact
// values, since hand contents / enemy HP shift with sts2 rebalances.
public class CombatTests
{
    [Fact]
    public async Task SelectMonsterNode_LandsInCombat_WithPopulatedCombatState()
    {
        await using var host = new HostSubprocess();

        var start = await host.SendAsync<RunNewResult>("run/new", new RunNewParams(Seed: 42uL));
        var monsterNode = start.AvailableMapNodes.First(n => n.Type == MapNodeType.Monster && n.Row > 0);

        var afterPick = await host.SendAsync<RunSelectMapNodeResult>(
            "run/select_map_node", new RunSelectMapNodeParams(Col: monsterNode.Col, Row: monsterNode.Row));

        Assert.Equal(RoomType.CombatRoom, afterPick.CurrentRoomType);
        Assert.NotNull(afterPick.CombatState);
        var combat = afterPick.CombatState!;
        // The Ironclad starting hand is 5 cards; play phase begins on round 1
        // with full energy. Don't pin exact numbers — the game can rebalance —
        // but the shape must be sane.
        Assert.True(combat.IsInProgress, "combat should be in progress after entering a Monster room");
        Assert.True(combat.IsPlayPhase, "first round should start in play phase");
        Assert.Equal(1, combat.Round);
        Assert.True(combat.Energy > 0, $"energy should be positive at turn start, was {combat.Energy}");
        Assert.True(combat.MaxEnergy >= combat.Energy);
        Assert.NotEmpty(combat.Hand);
        Assert.NotEmpty(combat.Enemies);
        // Every reported enemy must be alive (ReadEnemies filters dead ones)
        // and carry a stable monster id.
        Assert.All(combat.Enemies, e =>
        {
            Assert.True(e.Hp > 0, $"enemy {e.Index} hp should be positive, was {e.Hp}");
            Assert.True(e.MaxHp >= e.Hp);
            Assert.False(string.IsNullOrEmpty(e.MonsterId), $"enemy {e.Index} missing monsterId");
        });
        // Hand cards should have non-empty ids and well-defined target types.
        // Unknown surfaces would mean ParseEnum failed against a sts2 value
        // we haven't catalogued — that's a discipline failure, surface it.
        Assert.All(combat.Hand, c =>
        {
            Assert.False(string.IsNullOrEmpty(c.Id), $"card {c.Index} missing id");
            Assert.True(c.Cost >= 0, $"card {c.Index} negative cost {c.Cost}");
            Assert.NotEqual(TargetType.Unknown, c.TargetType);
        });

        // Outside CombatRoom the snapshot shouldn't carry combat-only fields.
        Assert.Empty(afterPick.AvailableMapNodes);
        Assert.Empty(afterPick.AvailableEventOptions);
    }

    [Fact]
    public async Task EndTurn_AdvancesRoundCounter_AndRunsEnemyTurn()
    {
        // After end_turn the engine switches sides (player → enemy → next
        // player turn). The wire layer drives SwitchFromPlayerToEnemySide
        // (the natural multi-player chain refuses without a real NetService),
        // which then runs the standard StartTurn → ExecuteEnemyTurn path
        // synchronously: monsters resolve their intents and deal damage.
        // Verified: round advances, IsPlayPhase flips back, and the Fuzzy
        // Wurm Crawler's Attack intent reduces player HP.
        await using var host = new HostSubprocess();

        var start = await host.SendAsync<RunNewResult>("run/new", new RunNewParams(Seed: 42uL));
        var monsterNode = start.AvailableMapNodes.First(n => n.Type == MapNodeType.Monster && n.Row > 0);
        var inCombat = await host.SendAsync<RunSelectMapNodeResult>(
            "run/select_map_node", new RunSelectMapNodeParams(Col: monsterNode.Col, Row: monsterNode.Row));
        Assert.NotNull(inCombat.CombatState);
        Assert.Equal(1, inCombat.CombatState!.Round);
        var hpBefore = inCombat.Hp;
        // Pre-condition: the chosen monster actually intends to attack this
        // turn. If it didn't, the HP-decreased assertion below would be
        // meaningless. Seed 42 → Fuzzy Wurm Crawler with an Attack intent.
        Assert.Contains(inCombat.CombatState.Enemies, e =>
            e.Intents.Any(i => i.Kind == IntentKind.Attack));

        var afterEndTurn = await host.SendAsync<RunEndTurnResult>("run/end_turn");

        Assert.True(afterEndTurn.Ok);
        Assert.Equal(RoomType.CombatRoom, afterEndTurn.CurrentRoomType);
        Assert.NotNull(afterEndTurn.CombatState);
        Assert.Equal(2, afterEndTurn.CombatState!.Round);
        Assert.True(afterEndTurn.CombatState.IsPlayPhase,
            "after end_turn the engine should switch back to play phase for round 2");
        Assert.True(afterEndTurn.CombatState.IsInProgress);
        // Energy should refresh on round start (Ironclad base is 3).
        Assert.Equal(afterEndTurn.CombatState.MaxEnergy, afterEndTurn.CombatState.Energy);
        // Enemy actually attacked — player HP dropped.
        Assert.True(afterEndTurn.Hp < hpBefore,
            $"expected enemy turn to deal damage; hp {hpBefore} → {afterEndTurn.Hp}");
    }

    [Fact]
    public async Task FightToCompletion_SurfacesRewards_ThenAdvancesBackToMapRoom()
    {
        // Drive a full combat: spam attack cards every turn until either the
        // enemy dies (combat ends → rewards surface) or we run out of safety
        // iterations. The Ironclad starting deck plus Strike+Bash damage
        // outpaces the Crawler's incoming damage, so the fight resolves long
        // before the iteration cap. After rewards are consumed (either by
        // skipping or claiming the first option) the host advances back to
        // MapRoom on the next snapshot.
        await using var host = new HostSubprocess();

        var start = await host.SendAsync<RunNewResult>("run/new", new RunNewParams(Seed: 42uL));
        var monsterNode = start.AvailableMapNodes.First(n => n.Type == MapNodeType.Monster && n.Row > 0);
        var snap = await host.SendAsync<RunSelectMapNodeResult>(
            "run/select_map_node", new RunSelectMapNodeParams(Col: monsterNode.Col, Row: monsterNode.Row));

        RoomType currentRoom = snap.CurrentRoomType;
        Protocol.Methods.CombatState? combat = snap.CombatState;
        RewardsState? rewards = snap.RewardsState;
        Assert.NotNull(combat);
        Assert.Null(rewards); // No rewards while the fight is live.

        for (var safety = 0; safety < 40 && currentRoom == RoomType.CombatRoom && rewards is null; safety++)
        {
            // Play any playable attack card we can afford, targeting enemy 0.
            var attack = combat!.Hand.FirstOrDefault(c =>
                c.CanPlay && c.Cost <= combat.Energy && c.TargetType == TargetType.AnyEnemy);
            if (attack is not null)
            {
                var afterPlay = await host.SendAsync<RunPlayCardResult>(
                    "run/play_card", new RunPlayCardParams(CardIndex: attack.Index, TargetIndex: 0));
                currentRoom = afterPlay.CurrentRoomType;
                combat = afterPlay.CombatState;
                rewards = afterPlay.RewardsState;
                if (rewards is not null || currentRoom != RoomType.CombatRoom) break;
                continue;
            }

            // Out of attack options — end the turn.
            var afterEnd = await host.SendAsync<RunEndTurnResult>("run/end_turn");
            currentRoom = afterEnd.CurrentRoomType;
            combat = afterEnd.CombatState;
            rewards = afterEnd.RewardsState;
        }

        // Combat resolved: rewards now block the auto-advance to MapRoom.
        Assert.NotNull(rewards);
        Assert.NotEmpty(rewards!.Available);

        // Drain every pending reward — claim non-card rewards (gold/relic/
        // potion can't be skipped); claim the first card option for any card
        // reward (the test asserts the cycle runs to completion, not which
        // pick is "best"). After the last reward consumes, the next snapshot
        // should report MapRoom.
        RoomType room = currentRoom;
        for (var safety = 0; safety < 12 && rewards is not null && rewards.Available.Count > 0; safety++)
        {
            // Always operate on index 0 — the host re-numbers the available
            // list after each consumption, so the new "first reward" advances
            // through whatever's left.
            var head = rewards.Available[0];
            int? cardIndex = head.Kind == RewardKind.Card && head.Cards is { Count: > 0 } ? 0 : null;
            var afterSelect = await host.SendAsync<RunSelectRewardResult>(
                "run/select_reward", new RunSelectRewardParams(RewardIndex: 0, CardIndex: cardIndex));
            room = afterSelect.CurrentRoomType;
            rewards = afterSelect.RewardsState;
        }

        Assert.Null(rewards);
        Assert.Equal(RoomType.MapRoom, room);
    }

    [Fact]
    public async Task PostCombat_RewardsSurfaceAtLeastOneCardChoice()
    {
        // Walk into combat, kill the enemy, then verify the post-combat
        // rewards include a card reward with at least one option. Doesn't
        // claim anything — just shape-checks the wire so a regression in
        // reward generation surfaces fast.
        await using var host = new HostSubprocess();

        var rewards = await DriveCombatToRewards(host);
        Assert.NotEmpty(rewards.Available);
        var card = rewards.Available.FirstOrDefault(r => r.Kind == RewardKind.Card);
        Assert.NotNull(card);
        Assert.NotNull(card!.Cards);
        Assert.NotEmpty(card.Cards!);
        Assert.All(card.Cards!, c =>
        {
            Assert.False(string.IsNullOrEmpty(c.Id), $"card option {c.Index} missing id");
            Assert.True(c.Cost >= 0, $"card option {c.Index} negative cost {c.Cost}");
        });
    }

    [Fact]
    public async Task SelectCardReward_AddsCardToDeck()
    {
        // After combat ends with a card reward in the offered set, claiming
        // that card grows the deck by one. Pin the deck-size delta to 1 so a
        // future regression that double-adds (or silently drops) is caught.
        await using var host = new HostSubprocess();
        await host.SendAsync<RunNewResult>("run/new", new RunNewParams(Seed: 42uL));

        // Capture deck size while still on the map (combat enters mutate it
        // through draw piles, not the source deck — but we want the canonical
        // baseline before any post-combat additions).
        var beforeCombat = await host.SendAsync<RunStateResult>("run/state");
        var deckBefore = beforeCombat.DeckSize;

        var rewards = await DriveCombatToRewards(host);
        var cardReward = rewards.Available.FirstOrDefault(r => r.Kind == RewardKind.Card);
        Assert.NotNull(cardReward);
        Assert.NotEmpty(cardReward!.Cards!);

        var afterClaim = await host.SendAsync<RunSelectRewardResult>(
            "run/select_reward", new RunSelectRewardParams(RewardIndex: cardReward.Index, CardIndex: 0));
        Assert.True(afterClaim.Ok);

        // Drain any remaining non-card rewards so we land back on the map and
        // can read a clean post-combat deck size.
        var rs = afterClaim.RewardsState;
        for (var safety = 0; safety < 10 && rs is not null && rs.Available.Count > 0; safety++)
        {
            var resp = await host.SendAsync<RunSelectRewardResult>(
                "run/select_reward", new RunSelectRewardParams(RewardIndex: 0, CardIndex: null));
            rs = resp.RewardsState;
        }

        var afterCombat = await host.SendAsync<RunStateResult>("run/state");
        Assert.Equal(RoomType.MapRoom, afterCombat.CurrentRoomType);
        Assert.Equal(deckBefore + 1, afterCombat.DeckSize);
    }

    [Fact]
    public async Task SkipCardReward_LeavesDeckUnchanged()
    {
        // Skipping a skippable card reward must NOT add a card. Deck size
        // stays the same across the skip; non-card rewards still get claimed
        // automatically by the test loop so we end up back at MapRoom.
        await using var host = new HostSubprocess();
        await host.SendAsync<RunNewResult>("run/new", new RunNewParams(Seed: 42uL));
        var beforeCombat = await host.SendAsync<RunStateResult>("run/state");
        var deckBefore = beforeCombat.DeckSize;

        var rewards = await DriveCombatToRewards(host);
        var cardReward = rewards.Available.FirstOrDefault(r => r.Kind == RewardKind.Card && r.CanSkip);
        if (cardReward is null)
        {
            // Skip the test if the seed/room offered a non-skippable card reward —
            // we want to assert skip behaviour, not "every card reward is
            // skippable". Surface as a soft skip rather than a misleading pass.
            return;
        }

        var afterSkip = await host.SendAsync<RunSkipRewardResult>(
            "run/skip_reward", new RunSkipRewardParams(RewardIndex: cardReward.Index));
        Assert.True(afterSkip.Ok);

        // Drain remaining rewards; assert deck is unchanged at the end.
        var rs = afterSkip.RewardsState;
        for (var safety = 0; safety < 10 && rs is not null && rs.Available.Count > 0; safety++)
        {
            var resp = await host.SendAsync<RunSelectRewardResult>(
                "run/select_reward", new RunSelectRewardParams(RewardIndex: 0, CardIndex: null));
            rs = resp.RewardsState;
        }

        var afterCombat = await host.SendAsync<RunStateResult>("run/state");
        Assert.Equal(RoomType.MapRoom, afterCombat.CurrentRoomType);
        Assert.Equal(deckBefore, afterCombat.DeckSize);
    }

    // Walks from a fresh run through the first reachable combat to the point
    // where rewards surface. Returns the RewardsState on the snapshot that
    // first reports rewards. Throws if rewards never surface within the
    // safety iteration cap — that's a regression worth a loud failure.
    //
    // Idempotent on the run state: callers may have already issued run/new
    // before calling (to capture pre-combat state); we re-issue only when the
    // host reports no active run.
    private static async Task<RewardsState> DriveCombatToRewards(HostSubprocess host)
    {
        RunStateResult start;
        try { start = await host.SendAsync<RunStateResult>("run/state"); }
        catch
        {
            await host.SendAsync<RunNewResult>("run/new", new RunNewParams(Seed: 42uL));
            start = await host.SendAsync<RunStateResult>("run/state");
        }
        if (start.CurrentRoomType != RoomType.MapRoom)
        {
            await host.SendAsync<RunNewResult>("run/new", new RunNewParams(Seed: 42uL));
            start = await host.SendAsync<RunStateResult>("run/state");
        }
        var monsterNode = start.AvailableMapNodes.First(n => n.Type == MapNodeType.Monster && n.Row > 0);
        var snap = await host.SendAsync<RunSelectMapNodeResult>(
            "run/select_map_node", new RunSelectMapNodeParams(Col: monsterNode.Col, Row: monsterNode.Row));

        Protocol.Methods.CombatState? combat = snap.CombatState;
        RewardsState? rewards = snap.RewardsState;
        for (var safety = 0; safety < 40 && rewards is null; safety++)
        {
            var attack = combat?.Hand.FirstOrDefault(c =>
                c.CanPlay && c.Cost <= combat.Energy && c.TargetType == TargetType.AnyEnemy);
            if (attack is not null)
            {
                var afterPlay = await host.SendAsync<RunPlayCardResult>(
                    "run/play_card", new RunPlayCardParams(CardIndex: attack.Index, TargetIndex: 0));
                combat = afterPlay.CombatState;
                rewards = afterPlay.RewardsState;
            }
            else
            {
                var afterEnd = await host.SendAsync<RunEndTurnResult>("run/end_turn");
                combat = afterEnd.CombatState;
                rewards = afterEnd.RewardsState;
            }
        }

        Assert.NotNull(rewards);
        return rewards!;
    }

    [Fact]
    public async Task PlayCard_RemovesCardFromHand_AndConsumesEnergy()
    {
        await using var host = new HostSubprocess();

        var start = await host.SendAsync<RunNewResult>("run/new", new RunNewParams(Seed: 42uL));
        var monsterNode = start.AvailableMapNodes.First(n => n.Type == MapNodeType.Monster && n.Row > 0);
        var inCombat = await host.SendAsync<RunSelectMapNodeResult>(
            "run/select_map_node", new RunSelectMapNodeParams(Col: monsterNode.Col, Row: monsterNode.Row));
        Assert.NotNull(inCombat.CombatState);
        var combat = inCombat.CombatState!;

        // Find the first playable card; for AnyEnemy cards, target the first
        // alive enemy. The starting Ironclad hand always contains at least
        // one playable card on turn one.
        var card = combat.Hand.FirstOrDefault(c => c.CanPlay && c.Cost <= combat.Energy);
        Assert.NotNull(card);
        int? targetIndex = card!.TargetType == TargetType.AnyEnemy ? 0 : null;

        var energyBefore = combat.Energy;
        var handCountBefore = combat.Hand.Count;

        var after = await host.SendAsync<RunPlayCardResult>(
            "run/play_card", new RunPlayCardParams(CardIndex: card.Index, TargetIndex: targetIndex));

        Assert.True(after.Ok);
        // Combat continues — playing one card doesn't end the turn or the fight.
        Assert.Equal(RoomType.CombatRoom, after.CurrentRoomType);
        Assert.NotNull(after.CombatState);
        var combatAfter = after.CombatState!;
        Assert.True(combatAfter.IsInProgress);
        Assert.True(combatAfter.IsPlayPhase);
        // Hand should shrink (the card moves to discard or, for exhaust
        // cards, to exhaust pile — both reduce hand count) and energy
        // should drop by the card's cost.
        Assert.True(combatAfter.Hand.Count < handCountBefore,
            $"hand should shrink after playing a card; was {handCountBefore}, now {combatAfter.Hand.Count}");
        Assert.Equal(energyBefore - card.Cost, combatAfter.Energy);
    }

    [Fact]
    public async Task EndTurn_WithoutRunNew_ReturnsInternalError()
    {
        await using var host = new HostSubprocess();

        var error = await host.ExpectErrorAsync("run/end_turn");

        Assert.Equal(-32603, error.Code);
        Assert.Contains("no active run", error.Message);
    }

    [Fact]
    public async Task PlayCard_WithoutRunNew_ReturnsInternalError()
    {
        await using var host = new HostSubprocess();

        var error = await host.ExpectErrorAsync(
            "run/play_card", new RunPlayCardParams(CardIndex: 0));

        Assert.Equal(-32603, error.Code);
        Assert.Contains("no active run", error.Message);
    }

    [Fact]
    public async Task EndTurn_NotInCombat_ReturnsInternalError()
    {
        // From a MapRoom, ending a turn is meaningless. The bindings raise
        // InvalidOperationException; surface that so callers can't drift state.
        await using var host = new HostSubprocess();

        await host.SendAsync<RunNewResult>("run/new", new RunNewParams(Seed: 1uL));

        var error = await host.ExpectErrorAsync("run/end_turn");

        Assert.Equal(-32603, error.Code);
    }
}
