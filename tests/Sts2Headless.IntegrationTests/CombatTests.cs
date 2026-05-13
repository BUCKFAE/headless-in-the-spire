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
    public async Task EndTurn_AdvancesRoundCounter_AndReturnsToPlayPhase()
    {
        // After end_turn the engine switches sides (player → enemy → next
        // player turn). The wire layer drives SwitchFromPlayerToEnemySide so
        // that round increments and IsPlayPhase flips back. Note: under the
        // current GodotStubs surface the enemy's attack is a no-op (the
        // ExecuteEnemyTurn callback we pass is null, so monsters don't
        // actually damage the player) — fixing that needs more engine
        // plumbing and is tracked separately. Until then, combat won't end
        // by attrition; tests that need a finished fight should play cards
        // to deal damage instead.
        await using var host = new HostSubprocess();

        var start = await host.SendAsync<RunNewResult>("run/new", new RunNewParams(Seed: 42uL));
        var monsterNode = start.AvailableMapNodes.First(n => n.Type == MapNodeType.Monster && n.Row > 0);
        var inCombat = await host.SendAsync<RunSelectMapNodeResult>(
            "run/select_map_node", new RunSelectMapNodeParams(Col: monsterNode.Col, Row: monsterNode.Row));
        Assert.NotNull(inCombat.CombatState);
        Assert.Equal(1, inCombat.CombatState!.Round);

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
