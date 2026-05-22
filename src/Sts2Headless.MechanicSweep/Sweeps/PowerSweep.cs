using System.Diagnostics;
using Sts2Headless.Agents.Contracts;
using Sts2Headless.Cheats;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.MechanicSweep.Sweeps;

// Per-PowerId smoke sweep. For each id in PowerIdNames.AllWireNames
// (~270 powers):
//
//   1. run/new(Ironclad, seed=42)
//   2. debug/set_hp(999, 999)
//   3. debug/start_combat(SLIMES_NORMAL)    — powers need combat
//   4. debug/apply_power(powerId, amount=2)
//      → defaults to Player target; if the wire surfaces an "invalid
//      params" refusal (some powers are monster-only and the engine
//      may reject them on the player), fall back to enemy target.
//   5. drain triggers via run/state
//   6. run/end_turn  — many powers tick on turn end (Poison, Block-
//                      reset, duration powers); fires AfterTurnEnd /
//                      AfterPlayerTurnStartEarly hooks
//   7. drain triggers
//   8. run/end_turn  — second turn so duration powers can resolve
//   9. drain triggers
//  10. debug/kill_all_enemies — combat-end hooks
//  11. final drain
//
// Outcomes:
//   * Triggered  — at least one TriggerKind.Power hook attributed to
//                  this power's id fired
//   * Played     — power applied without crash but didn't fire a hook
//                  attributed to itself (the power's listeners may be
//                  passive; hook attribution depends on the power's
//                  own AbstractModel overrides)
//   * Unplayable — wire-level refusal from apply_power on both
//                  Player AND first-Enemy targets
//   * Crashed    — host or runtime threw
//   * Timeout    — per-id budget elapsed
public sealed class PowerSweep
{
    public static readonly System.TimeSpan PerPowerBudget = System.TimeSpan.FromSeconds(30);
    public const string BenignEncounter = "SLIMES_NORMAL";

    public async System.Threading.Tasks.Task<SweepReport> RunAsync(
        ITransport transport,
        System.Collections.Generic.IReadOnlyList<string>? sampleIds = null,
        string gameVersion = "unknown",
        System.Action<SweepRow>? onRow = null,
        System.Threading.CancellationToken ct = default)
    {
        var universe = SweepInternals.FilterReachable(PowerIdNames.AllWireNames);
        var ids = sampleIds is { Count: > 0 } ? sampleIds : universe;
        var sampled = sampleIds is { Count: > 0 };

        var rows = new System.Collections.Generic.List<SweepRow>(ids.Count);
        var totalSw = Stopwatch.StartNew();
        foreach (var powerId in ids)
        {
            ct.ThrowIfCancellationRequested();
            var row = await RunOneAsync(transport, powerId, ct);
            rows.Add(row);
            onRow?.Invoke(row);
        }
        totalSw.Stop();
        return new SweepReport(
            Kind: "powers",
            Rows: rows,
            TotalElapsed: totalSw.Elapsed,
            GameVersion: gameVersion,
            Sampled: sampled,
            UniverseSize: universe.Count);
    }

    private static async System.Threading.Tasks.Task<SweepRow> RunOneAsync(
        ITransport transport, string powerId, System.Threading.CancellationToken outerCt)
    {
        var sw = Stopwatch.StartNew();
        using var cts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(outerCt);
        cts.CancelAfter(PerPowerBudget);
        var ct = cts.Token;

        var firedHooks = new HashSet<string>(StringComparer.Ordinal);
        var steps = 0;

        try
        {
            await transport.SendAsync<RunNewResult>(
                "run/new",
                new RunNewParams(Character: Character.Ironclad, Seed: 42uL));
            await transport.SetHpAsync(999, 999);
            var combat = await transport.StartCombatAsync(BenignEncounter);
            if (!combat.InProgress)
            {
                sw.Stop();
                return new SweepRow(
                    powerId, SweepOutcome.Crashed, Steps: 0, sw.Elapsed,
                    Detail: $"start_combat returned InProgress=false (enemyCount={combat.EnemyCount})");
            }
            DrainTriggers(await transport.SendAsync<RunStateResult>("run/state"), powerId, firedHooks);

            // Apply: try Player target first, fall back to first Enemy
            // on a wire-level refusal. The "applied to player but should
            // be monster-only" case is one of the few classes the engine
            // is opinionated about; trying enemy after gives the broadest
            // coverage.
            var appliedTarget = "?";
            var appliedAmount = -1;
            try
            {
                var resp = await transport.ApplyPowerAsync(powerId, amount: 2, enemyIndex: null);
                appliedTarget = resp.TargetDescription;
                appliedAmount = resp.AppliedAmount;
            }
            catch (System.Exception wx) when (SweepInternals.IsWireError(wx))
            {
                if (SweepInternals.IsInternalError(wx))
                {
                    sw.Stop();
                    return new SweepRow(
                        powerId, SweepOutcome.Crashed, Steps: 0, sw.Elapsed,
                        Detail: $"apply_power(Player): {wx.GetType().Name}: {SweepInternals.Truncate(wx.Message)}");
                }
                // Player-side refused. Try Enemy:0.
                try
                {
                    var resp = await transport.ApplyPowerAsync(powerId, amount: 2, enemyIndex: 0);
                    appliedTarget = resp.TargetDescription;
                    appliedAmount = resp.AppliedAmount;
                }
                catch (System.Exception wx2) when (SweepInternals.IsWireError(wx2))
                {
                    if (SweepInternals.IsInternalError(wx2))
                    {
                        sw.Stop();
                        return new SweepRow(
                            powerId, SweepOutcome.Crashed, Steps: 0, sw.Elapsed,
                            Detail: $"apply_power(Enemy:0): {wx2.GetType().Name}: {SweepInternals.Truncate(wx2.Message)}");
                    }
                    // Both sides refused. Power may need a different
                    // creature kind (Osty-only, summoned-only, ...).
                    sw.Stop();
                    return new SweepRow(
                        powerId, SweepOutcome.Unplayable, Steps: 0, sw.Elapsed,
                        Detail: $"apply_power refused on both Player and Enemy:0 — {SweepInternals.Truncate(wx2.Message)}");
                }
            }
            DrainTriggers(await transport.SendAsync<RunStateResult>("run/state"), powerId, firedHooks);

            // Two end_turns so duration-based powers can decrement,
            // turn-start/end hooks can fire, etc.
            for (var turn = 0; turn < 2; turn++)
            {
                ct.ThrowIfCancellationRequested();
                var state = await transport.SendAsync<RunStateResult>("run/state");
                if (state.CombatState is not { IsInProgress: true }) break;

                try
                {
                    _ = await transport.SendAsync<RunEndTurnResult>("run/end_turn");
                    steps++;
                }
                catch (System.Exception wx) when (SweepInternals.IsWireError(wx))
                {
                    if (SweepInternals.IsInternalError(wx))
                    {
                        sw.Stop();
                        return new SweepRow(
                            powerId, SweepOutcome.Crashed, Steps: steps, sw.Elapsed,
                            Detail: $"end_turn (target={appliedTarget}): {wx.GetType().Name}: {SweepInternals.Truncate(wx.Message)}");
                    }
                    break;
                }
                DrainTriggers(await transport.SendAsync<RunStateResult>("run/state"), powerId, firedHooks);
            }

            // Combat end captures AfterCombatVictory / AfterCombatEnd for
            // any power that listens (DemonForm-shape on-combat-end).
            try
            {
                _ = await transport.KillAllEnemiesAsync();
                DrainTriggers(await transport.SendAsync<RunStateResult>("run/state"), powerId, firedHooks);
            }
            catch (System.Exception wx) when (SweepInternals.IsWireError(wx) && !SweepInternals.IsInternalError(wx))
            {
                // benign — combat may have ended naturally
            }

            sw.Stop();
            var outcome = firedHooks.Count > 0 ? SweepOutcome.Triggered : SweepOutcome.Played;
            var detail = $"target={appliedTarget}"
                + (appliedAmount >= 0 ? $",amount={appliedAmount}" : "")
                + (firedHooks.Count > 0 ? $",hooks: {string.Join(",", firedHooks.OrderBy(h => h, StringComparer.Ordinal))}" : "");
            return new SweepRow(powerId, outcome, Steps: steps, sw.Elapsed, detail);
        }
        catch (System.OperationCanceledException) when (cts.Token.IsCancellationRequested && !outerCt.IsCancellationRequested)
        {
            sw.Stop();
            return new SweepRow(
                powerId, SweepOutcome.Timeout, Steps: steps, sw.Elapsed,
                Detail: $"per-power budget {PerPowerBudget.TotalSeconds:0}s exceeded");
        }
        catch (System.Exception ex)
        {
            sw.Stop();
            return new SweepRow(
                powerId, SweepOutcome.Crashed, Steps: steps, sw.Elapsed,
                Detail: $"{ex.GetType().Name}: {SweepInternals.Truncate(ex.Message)}");
        }
    }

    private static void DrainTriggers(
        RunStateResult state,
        string powerId,
        HashSet<string> sink)
    {
        foreach (var ev in state.TriggeredSincePrev)
        {
            if (ev.Kind != TriggerKind.Power) continue;
            if (!string.Equals(ev.Source, powerId, StringComparison.Ordinal)) continue;
            if (!string.IsNullOrEmpty(ev.Hook)) sink.Add(ev.Hook);
        }
    }
}
