using System.Diagnostics;
using Sts2Headless.Agents.Driving;
using Sts2Headless.BattleAgent;
using Sts2Headless.Cheats;
using Sts2Headless.IntegrationTests;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.End2EndTests;

// Regression for the Doormaker phase-transition fix.
//
// Original failure: patching every Doormaker move method + SwapPhasePower
// as "skip body, return CompletedTask" silently stripped the boss of its
// mechanics. DramaticOpenMove's first-turn body sets the real boss HP
// via CreatureCmd.SetMaxAndCurrentHp(OriginalHp); skipping it left the
// boss at the engine-design sentinel MaxHp ≈ 999999999 — a 10⁹ HP
// statue with no powers. CombatBudgetGuard tripped at round 81 and the
// encounter went into KnownEngineBlocked as a "Timeout".
//
// Proper fix (HangPatches.Async.cs / HangPatches.Monsters.cs):
//   * Patch Cmd.CustomScaledWait (the animation pause inside each
//     move) so it returns Task.CompletedTask. Same shape as Cmd.Wait.
//   * Patch Doormaker.UpdateVisual (the Godot sprite swap on phase
//     transitions) so it no-ops.
//   * Stop patching the move bodies — they now run their gameplay
//     logic (HP setter, PowerCmd.Apply/Remove, DamageCmd.Attack)
//     normally because the helpers they depend on are stubbed at the
//     leaf instead of the root.
//   * Stop patching SwapPhasePower<T>. Its body is pure
//     PowerCmd.Remove/Apply, no UI — never needed patching.
//
// Three assertions, each catches a different regression shape:
public class DoormakerPhaseTransitionTests
{
    private static readonly (string CardId, int UpgradeLevel)[] FightDeck =
    [
        ("BLUDGEON", 1),
        ("BLUDGEON", 1),
        ("UPPERCUT", 1),
        ("DEFEND_IRONCLAD", 0),
        ("DEFEND_IRONCLAD", 0),
    ];

    [Fact]
    public async Task Doormaker_BossHp_DropsBelowSentinelAfterFirstTurn()
    {
        // Cheapest regression: by the end of the player's first turn,
        // DramaticOpenMove has fired and CreatureCmd.SetMaxAndCurrentHp
        // has overwritten the 999999999 placeholder with OriginalHp.
        // If we ever regress to skipping DramaticOpenMove's body, the
        // HP stays at the sentinel — this catches it without driving a
        // full combat.
        await using var host = new HostSubprocess();
        var transport = new HostSubprocessTransport(host);

        await host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));
        await transport.SetHpAsync(999, 999);

        var start = await transport.StartCombatAsync("DOORMAKER_BOSS");
        Assert.True(start.InProgress);

        await host.SendAsync<RunEndTurnResult>("run/end_turn");

        var state = await host.SendAsync<RunStateResult>("run/state");
        var boss = state.CombatState?.Enemies.FirstOrDefault();
        Assert.NotNull(boss);
        // Doormaker's OriginalHp is ~489 per the engine's design. Anything
        // under 10k means the HP setter fired (sentinel was 999999999).
        Assert.True(boss!.Hp < 10_000 && boss.MaxHp < 10_000,
            $"Doormaker HP after first round is {boss.Hp}/{boss.MaxHp} — that's the " +
            $"phase-transition sentinel. DramaticOpenMove's body didn't run; check " +
            $"HangPatches.Monsters.cs (Doormaker should NOT have move methods in its " +
            $"patch set) and HangPatches.Async.cs (Cmd.CustomScaledWait must be patched).");
    }

    [Fact]
    public async Task Doormaker_HasHungerPower_AfterFirstTurn()
    {
        // Structural assertion: after DramaticOpenMove fires, the boss
        // is in Hunger phase — HungerPower is the active phase power.
        // If we regress to skipping DramaticOpenMove or SwapPhasePower's
        // gameplay logic, the boss has zero powers.
        await using var host = new HostSubprocess();
        var transport = new HostSubprocessTransport(host);

        await host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));
        await transport.SetHpAsync(999, 999);
        var start = await transport.StartCombatAsync("DOORMAKER_BOSS");
        Assert.True(start.InProgress);

        await host.SendAsync<RunEndTurnResult>("run/end_turn");

        var state = await host.SendAsync<RunStateResult>("run/state");
        var boss = state.CombatState?.Enemies.FirstOrDefault();
        Assert.NotNull(boss);
        Assert.True(
            boss!.Powers.Any(p => p.Id.ToString().Contains("HUNGER", StringComparison.OrdinalIgnoreCase)),
            $"Doormaker should carry HUNGER_POWER (or HUNGER_OF_THE_VOID_POWER) after " +
            $"DramaticOpenMove's phase setup. Observed powers: " +
            $"[{string.Join(",", boss.Powers.Select(p => $"{p.Id}:{p.Amount}"))}]");
    }

    [Fact]
    public async Task Doormaker_CanBeKilled_WithIroncladAgentAndRealDeck()
    {
        // End-to-end win contract: a real Ironclad agent with a damage
        // deck should beat Doormaker within a reasonable step budget.
        // 23 rounds is the empirical kill window with BLUDGEON+1×2,
        // UPPERCUT+1, DEFEND×2 against 489-HP Doormaker; we cap at 60.
        await using var host = new HostSubprocess();
        var transport = new HostSubprocessTransport(host);

        await host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));
        await transport.ReplaceDeckAsync(FightDeck);
        await transport.SetHpAsync(999, 999);
        await transport.GiveRelicAsync("TOUGH_BANDAGES");
        var start = await transport.StartCombatAsync("DOORMAKER_BOSS");
        Assert.True(start.InProgress);

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var agent = new IroncladAgent();
        var sw = Stopwatch.StartNew();
        var outcome = await AgentDriver.PlayRunAsync(
            transport,
            agent,
            stopWhen: s => s.CombatState is not { IsInProgress: true },
            maxSteps: 300,
            ct: cts.Token);
        sw.Stop();

        Assert.True(
            outcome.TerminatedBy == TerminationReason.StopRequested,
            $"Doormaker combat did not end cleanly: terminatedBy={outcome.TerminatedBy} " +
            $"steps={outcome.Steps} elapsed={sw.Elapsed.TotalSeconds:0.0}s. " +
            $"StepLimit or Timeout means the move bodies are getting skipped again " +
            $"(check HangPatches.Monsters.cs PatchDoormaker) or a new UI helper started " +
            $"NRE'ing inside the move bodies (look for the stack trace in stderr).");
        Assert.False(outcome.FinalState.IsDead || outcome.FinalState.IsGameOver,
            $"Doormaker killed the agent (deck/policy issue, not the headless fix). " +
            $"hp={outcome.FinalState.Hp}");
    }
}
