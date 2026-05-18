using Sts2Headless.BattleAgent.Core;
using Sts2Headless.Cheats;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Parity tests: drive the real engine through a fixed card sequence and
// drive our simulator through the same sequence, then assert player HP /
// block / energy + each enemy's HP / block agree.
//
// Why this matters: the simulator's card-effect data (IroncladCardCatalog)
// is hand-authored from STS1 + the existing CardMechanics.cs baseline.
// Anywhere our numbers drift from sts2's actual values, the planner will
// pick suboptimal plays in production. These tests are the canary that
// catches stat drift before E2E win-rate degrades.
//
// Setup pattern: replace the deck with N copies of one (or a few) card so
// the opening hand is deterministic regardless of shuffle order, then
// enter a known monster fight. The first turn's state is reproducible
// even when seed-driven shuffling differs.
//
// Failure interpretation: a red parity test usually means
//   (a) STS2's stat for the card differs from what we encoded (update
//       IroncladCardCatalog),
//   (b) the simulator's status-math diverges from the engine
//       (Vulnerable, Weak, Strength ordering),
//   (c) the card has a side effect we don't model (update CardEffect /
//       Custom handler).
// Never silently relax the assertion — match the engine.
public class CombatParityTests
{
    private static SimState BuildSim(RunSelectMapNodeResult snap, int hp, int maxHp) =>
        SimStateBuilder.FromWire(
            snap.CombatState ?? throw new InvalidOperationException("expected CombatState"),
            currentHp: hp,
            maxHp: maxHp);

    private static SimState BuildSim(RunPlayCardResult snap, int hp, int maxHp) =>
        SimStateBuilder.FromWire(
            snap.CombatState ?? throw new InvalidOperationException("expected CombatState"),
            currentHp: hp,
            maxHp: maxHp);

    [Fact]
    public async Task SingleStrike_DamageMatchesEngine()
    {
        await using var host = new HostSubprocess();
        var start = await host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

        // Deterministic deck: 10 Strikes. Opening hand is 5 Strikes
        // regardless of shuffle order.
        await host.SendAsync<DebugReplaceDeckResult>(
            "debug/replace_deck",
            new DebugReplaceDeckParams(Enumerable.Range(0, 10)
                .Select(_ => new CardSpec("STRIKE_IRONCLAD")).ToArray()));

        var monsterNode = start.AvailableMapNodes.First(n => n.Type == MapNodeType.Monster && n.Row > 0);
        var enterCombat = await host.SendAsync<RunSelectMapNodeResult>(
            "run/select_map_node",
            new RunSelectMapNodeParams(Col: monsterNode.Col, Row: monsterNode.Row));
        Assert.NotNull(enterCombat.CombatState);

        var state0 = await host.SendAsync<RunStateResult>("run/state");
        var sim0 = BuildSim(enterCombat, state0.Hp, state0.MaxHp);
        Assert.NotEmpty(sim0.Hand);
        Assert.Equal(CardId.StrikeIronclad, sim0.Hand[0].Id);
        Assert.NotEmpty(sim0.Enemies);
        var enemyIdx = 0;
        var preTargetHp = sim0.Enemies[enemyIdx].Hp;
        var preTargetBlock = sim0.Enemies[enemyIdx].Block;

        // Predict: Strike does 6 damage, multiplied by 1.5 if target is
        // Vulnerable, minus enemy block.
        var model = new CombatModel(IroncladCardCatalog.Instance);
        var simAfter = model.Apply(sim0, new SimPlayCard(0, enemyIdx));

        // Drive engine through same action.
        var engineAfter = await host.SendAsync<RunPlayCardResult>(
            "run/play_card",
            new RunPlayCardParams(CardIndex: sim0.Hand[0].OriginalHandIndex!.Value, TargetIndex: enemyIdx));

        var engineSim = BuildSim(engineAfter, state0.Hp, state0.MaxHp);
        Assert.Equal(simAfter.Enemies[enemyIdx].Hp, engineSim.Enemies[enemyIdx].Hp);
        Assert.Equal(simAfter.Enemies[enemyIdx].Block, engineSim.Enemies[enemyIdx].Block);
        Assert.Equal(simAfter.Energy, engineSim.Energy);
    }

    [Fact]
    public async Task SingleDefend_BlockMatchesEngine()
    {
        await using var host = new HostSubprocess();
        var start = await host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

        await host.SendAsync<DebugReplaceDeckResult>(
            "debug/replace_deck",
            new DebugReplaceDeckParams(Enumerable.Range(0, 10)
                .Select(_ => new CardSpec("DEFEND_IRONCLAD")).ToArray()));

        var monsterNode = start.AvailableMapNodes.First(n => n.Type == MapNodeType.Monster && n.Row > 0);
        var enterCombat = await host.SendAsync<RunSelectMapNodeResult>(
            "run/select_map_node",
            new RunSelectMapNodeParams(Col: monsterNode.Col, Row: monsterNode.Row));

        var state0 = await host.SendAsync<RunStateResult>("run/state");
        var sim0 = BuildSim(enterCombat, state0.Hp, state0.MaxHp);
        Assert.Equal(CardId.DefendIronclad, sim0.Hand[0].Id);

        var model = new CombatModel(IroncladCardCatalog.Instance);
        var simAfter = model.Apply(sim0, new SimPlayCard(0, null));

        var engineAfter = await host.SendAsync<RunPlayCardResult>(
            "run/play_card",
            new RunPlayCardParams(CardIndex: sim0.Hand[0].OriginalHandIndex!.Value, TargetIndex: null));
        var engineSim = BuildSim(engineAfter, state0.Hp, state0.MaxHp);

        Assert.Equal(simAfter.Block, engineSim.Block);
        Assert.Equal(simAfter.Energy, engineSim.Energy);
    }

    [Fact]
    public async Task BashAppliesVulnerableAndDamage_MatchesEngine()
    {
        await using var host = new HostSubprocess();
        var start = await host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

        // Deck of 10 Bashes — every hand draw is Bashes only.
        await host.SendAsync<DebugReplaceDeckResult>(
            "debug/replace_deck",
            new DebugReplaceDeckParams(Enumerable.Range(0, 10)
                .Select(_ => new CardSpec("BASH")).ToArray()));

        var monsterNode = start.AvailableMapNodes.First(n => n.Type == MapNodeType.Monster && n.Row > 0);
        var enterCombat = await host.SendAsync<RunSelectMapNodeResult>(
            "run/select_map_node",
            new RunSelectMapNodeParams(Col: monsterNode.Col, Row: monsterNode.Row));

        var state0 = await host.SendAsync<RunStateResult>("run/state");
        var sim0 = BuildSim(enterCombat, state0.Hp, state0.MaxHp);

        var model = new CombatModel(IroncladCardCatalog.Instance);
        var enemyIdx = 0;
        var simAfter = model.Apply(sim0, new SimPlayCard(0, enemyIdx));

        var engineAfter = await host.SendAsync<RunPlayCardResult>(
            "run/play_card",
            new RunPlayCardParams(CardIndex: sim0.Hand[0].OriginalHandIndex!.Value, TargetIndex: enemyIdx));
        var engineSim = BuildSim(engineAfter, state0.Hp, state0.MaxHp);

        Assert.Equal(simAfter.Enemies[enemyIdx].Hp, engineSim.Enemies[enemyIdx].Hp);
        Assert.Equal(simAfter.Enemies[enemyIdx].Vulnerable, engineSim.Enemies[enemyIdx].Vulnerable);
        Assert.Equal(simAfter.Energy, engineSim.Energy);
    }

    [Fact]
    public async Task EndTurnEnemyAttackDamage_MatchesEngine()
    {
        await using var host = new HostSubprocess();
        var start = await host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

        // Deck of pure Defends so we can compare block math at end of turn.
        await host.SendAsync<DebugReplaceDeckResult>(
            "debug/replace_deck",
            new DebugReplaceDeckParams(Enumerable.Range(0, 10)
                .Select(_ => new CardSpec("DEFEND_IRONCLAD")).ToArray()));

        var monsterNode = start.AvailableMapNodes.First(n => n.Type == MapNodeType.Monster && n.Row > 0);
        var enterCombat = await host.SendAsync<RunSelectMapNodeResult>(
            "run/select_map_node",
            new RunSelectMapNodeParams(Col: monsterNode.Col, Row: monsterNode.Row));

        var state0 = await host.SendAsync<RunStateResult>("run/state");
        var sim0 = BuildSim(enterCombat, state0.Hp, state0.MaxHp);

        var model = new CombatModel(IroncladCardCatalog.Instance);

        // Play one Defend then end turn.
        var simStep1 = model.Apply(sim0, new SimPlayCard(0, null));
        var simAfterEot = model.EndPlayerTurn(simStep1);

        var engineStep1 = await host.SendAsync<RunPlayCardResult>(
            "run/play_card",
            new RunPlayCardParams(CardIndex: sim0.Hand[0].OriginalHandIndex!.Value, TargetIndex: null));
        Assert.NotNull(engineStep1.CombatState);
        var engineAfterEot = await host.SendAsync<RunEndTurnResult>("run/end_turn");

        var stateAfter = await host.SendAsync<RunStateResult>("run/state");
        Assert.NotNull(engineAfterEot.CombatState);
        var engineSimAfterEot = SimStateBuilder.FromWire(
            engineAfterEot.CombatState!, stateAfter.Hp, stateAfter.MaxHp);

        // Compare HP — the only field the simulator predicts about post-EOT
        // that doesn't depend on engine-side draw. Block at start of next
        // turn (PlatedArmor / Barricade aside) is 0 in both.
        Assert.Equal(simAfterEot.Hp, engineSimAfterEot.Hp);
    }

    [Fact]
    public async Task TwoStrikes_DamageMatchesEngine()
    {
        await using var host = new HostSubprocess();
        var start = await host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

        await host.SendAsync<DebugReplaceDeckResult>(
            "debug/replace_deck",
            new DebugReplaceDeckParams(Enumerable.Range(0, 10)
                .Select(_ => new CardSpec("STRIKE_IRONCLAD")).ToArray()));

        var monsterNode = start.AvailableMapNodes.First(n => n.Type == MapNodeType.Monster && n.Row > 0);
        var enterCombat = await host.SendAsync<RunSelectMapNodeResult>(
            "run/select_map_node",
            new RunSelectMapNodeParams(Col: monsterNode.Col, Row: monsterNode.Row));

        var state0 = await host.SendAsync<RunStateResult>("run/state");
        var sim0 = BuildSim(enterCombat, state0.Hp, state0.MaxHp);
        var model = new CombatModel(IroncladCardCatalog.Instance);
        var enemyIdx = 0;

        // Play two Strikes in the simulator. Indices in SimState.Hand
        // refer to the simulator's hand, which shrinks after each play.
        var simStep1 = model.Apply(sim0, new SimPlayCard(0, enemyIdx));
        var simStep2 = model.Apply(simStep1, new SimPlayCard(0, enemyIdx));

        // Engine: the first play returns a new CombatState whose hand
        // contains the remaining 4 strikes. Play another from that hand.
        var card1WireIdx = sim0.Hand[0].OriginalHandIndex!.Value;
        var engineStep1 = await host.SendAsync<RunPlayCardResult>(
            "run/play_card",
            new RunPlayCardParams(CardIndex: card1WireIdx, TargetIndex: enemyIdx));
        Assert.NotNull(engineStep1.CombatState);
        var card2WireIdx = engineStep1.CombatState!.Hand[0].Index;
        var engineStep2 = await host.SendAsync<RunPlayCardResult>(
            "run/play_card",
            new RunPlayCardParams(CardIndex: card2WireIdx, TargetIndex: enemyIdx));
        var engineSim2 = BuildSim(engineStep2, state0.Hp, state0.MaxHp);

        Assert.Equal(simStep2.Enemies[enemyIdx].Hp, engineSim2.Enemies[enemyIdx].Hp);
        Assert.Equal(simStep2.Energy, engineSim2.Energy);
    }
}
