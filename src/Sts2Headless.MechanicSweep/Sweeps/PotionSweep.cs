using System.Diagnostics;
using Sts2Headless.Agents.Contracts;
using Sts2Headless.Cheats;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.MechanicSweep.Sweeps;

// Per-PotionId smoke sweep. For each id in PotionIdNames.AllWireNames:
//
//   1. run/new(Ironclad, seed=42)
//   2. debug/set_hp(999, 999)        — survive incidental damage
//   3. debug/give_potion(P)          — adds to PotionSlots; on-procure
//                                      hooks fire (BeforePotionProcured,
//                                      AfterPotionProcured)
//   4. debug/start_combat(BENIGN)    — potions are usable in-combat;
//                                      switching room also fires room hooks
//   5. drain triggers via run/state
//   6. run/use_potion(slotIdx, target=0)
//                                    — fires BeforePotionUsed,
//                                      AfterPotionUsed, plus any
//                                      potion-specific damage / heal /
//                                      block / power-apply hooks
//   7. debug/kill_all_enemies        — captures combat-end hooks
//
// Drain state.TriggeredSincePrev after every action; accumulate hook
// firings attributed to (Kind=Potion, Source=this-potion).
//
// Tolerated outcomes:
//   * Triggered  — potion fired ≥1 hook attributed to its id
//   * Played     — potion was given and (attempted to) use without crash
//                  but didn't fire a hook attributed to it (rare for
//                  potions; the procure/use hooks are very general)
//   * Unplayable — wire refused at give-time (character-locked, slots
//                  full, etc.) or at use-time (wrong target)
//
// Failure:
//   * Crashed    — engine threw an unhandled exception
//   * Timeout    — per-id budget elapsed
public sealed class PotionSweep
{
    public static readonly System.TimeSpan PerPotionBudget = System.TimeSpan.FromSeconds(30);
    public const string BenignEncounter = "SLIMES_NORMAL";

    public async System.Threading.Tasks.Task<SweepReport> RunAsync(
        ITransport transport,
        System.Collections.Generic.IReadOnlyList<string>? sampleIds = null,
        string gameVersion = "unknown",
        System.Action<SweepRow>? onRow = null,
        System.Threading.CancellationToken ct = default)
    {
        var universe = PotionIdNames.AllWireNames
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();
        var ids = sampleIds is { Count: > 0 } ? sampleIds : universe;
        var sampled = sampleIds is { Count: > 0 };

        var rows = new System.Collections.Generic.List<SweepRow>(ids.Count);
        var totalSw = Stopwatch.StartNew();
        foreach (var potionId in ids)
        {
            ct.ThrowIfCancellationRequested();
            var row = await RunOneAsync(transport, potionId, ct);
            rows.Add(row);
            onRow?.Invoke(row);
        }
        totalSw.Stop();
        return new SweepReport(
            Kind: "potions",
            Rows: rows,
            TotalElapsed: totalSw.Elapsed,
            GameVersion: gameVersion,
            Sampled: sampled,
            UniverseSize: universe.Count);
    }

    private static async System.Threading.Tasks.Task<SweepRow> RunOneAsync(
        ITransport transport, string potionId, System.Threading.CancellationToken outerCt)
    {
        var sw = Stopwatch.StartNew();
        using var cts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(outerCt);
        cts.CancelAfter(PerPotionBudget);
        var ct = cts.Token;

        var firedHooks = new HashSet<string>(StringComparer.Ordinal);
        var steps = 0;

        try
        {
            // 1. Fresh run.
            await transport.SendAsync<RunNewResult>(
                "run/new",
                new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

            // 2. HP cheat first so a damage-dealing potion that misfires
            // doesn't kill the player mid-fixture.
            await transport.SetHpAsync(999, 999);

            // 3. Grant the potion. Slot=-1 → first empty; SlotIndex in
            // the result names where it landed.
            DebugGivePotionResult give;
            try
            {
                give = await transport.GivePotionAsync(potionId);
            }
            catch (System.Exception wx) when (SweepInternals.IsWireError(wx))
            {
                sw.Stop();
                var outcome = SweepInternals.IsInternalError(wx) ? SweepOutcome.Crashed : SweepOutcome.Unplayable;
                return new SweepRow(
                    potionId, outcome, Steps: 0, sw.Elapsed,
                    Detail: $"give_potion: {wx.GetType().Name}: {SweepInternals.Truncate(wx.Message)}");
            }
            DrainTriggers(await transport.SendAsync<RunStateResult>("run/state"), potionId, firedHooks);

            // 4. Force benign combat so use_potion's "must be in combat"
            // gate is open. SLIMES_NORMAL is the same minimal fixture
            // CardSweep / RelicSweep use.
            var combat = await transport.StartCombatAsync(BenignEncounter);
            if (!combat.InProgress)
            {
                sw.Stop();
                return new SweepRow(
                    potionId, SweepOutcome.Crashed, Steps: steps, sw.Elapsed,
                    Detail: $"start_combat returned InProgress=false (enemyCount={combat.EnemyCount})");
            }
            DrainTriggers(await transport.SendAsync<RunStateResult>("run/state"), potionId, firedHooks);

            // 5. Use the potion. OwnedPotions is filtered to non-null
            // PotionSlots — match by id to find its OwnedPotions index
            // (which is what run/use_potion takes, NOT the underlying
            // PotionSlots index — see Sts2Bindings.Potion.cs's comment).
            var preUse = await transport.SendAsync<RunStateResult>("run/state");
            DrainTriggers(preUse, potionId, firedHooks);
            var potionIdx = -1;
            for (var i = 0; i < preUse.OwnedPotions.Count; i++)
            {
                if (string.Equals(preUse.OwnedPotions[i].Id, potionId, StringComparison.Ordinal))
                {
                    potionIdx = i;
                    break;
                }
            }
            if (potionIdx >= 0)
            {
                try
                {
                    _ = await transport.SendAsync<RunUsePotionResult>(
                        "run/use_potion",
                        new RunUsePotionParams(PotionIndex: potionIdx, TargetIndex: 0));
                    steps++;
                }
                catch (System.Exception wx) when (SweepInternals.IsWireError(wx))
                {
                    if (SweepInternals.IsInternalError(wx))
                    {
                        sw.Stop();
                        return new SweepRow(
                            potionId, SweepOutcome.Crashed, Steps: steps, sw.Elapsed,
                            Detail: $"use_potion: {wx.GetType().Name}: {SweepInternals.Truncate(wx.Message)}");
                    }
                    // benign refusal (wrong target type, can't use this
                    // potion in current state) — record as Played, the
                    // potion still went through procure hooks.
                }
                DrainTriggers(await transport.SendAsync<RunStateResult>("run/state"), potionId, firedHooks);
            }

            // 6. Force combat end so combat-tied hooks (rare for potions
            // but possible — e.g. Lucky Tonic firing AfterCombatVictory).
            try
            {
                _ = await transport.KillAllEnemiesAsync();
                DrainTriggers(await transport.SendAsync<RunStateResult>("run/state"), potionId, firedHooks);
            }
            catch (System.Exception wx) when (SweepInternals.IsWireError(wx) && !SweepInternals.IsInternalError(wx))
            {
                // Benign — combat may already be over.
            }

            sw.Stop();
            var outcome2 = firedHooks.Count > 0 ? SweepOutcome.Triggered : SweepOutcome.Played;
            var detail = firedHooks.Count > 0
                ? $"hooks: {string.Join(",", firedHooks.OrderBy(h => h, StringComparer.Ordinal))}"
                : null;
            return new SweepRow(potionId, outcome2, Steps: steps, sw.Elapsed, detail);
        }
        catch (System.OperationCanceledException) when (cts.Token.IsCancellationRequested && !outerCt.IsCancellationRequested)
        {
            sw.Stop();
            return new SweepRow(
                potionId, SweepOutcome.Timeout, Steps: steps, sw.Elapsed,
                Detail: $"per-potion budget {PerPotionBudget.TotalSeconds:0}s exceeded");
        }
        catch (System.Exception ex)
        {
            sw.Stop();
            return new SweepRow(
                potionId, SweepOutcome.Crashed, Steps: steps, sw.Elapsed,
                Detail: $"{ex.GetType().Name}: {SweepInternals.Truncate(ex.Message)}");
        }
    }

    private static void DrainTriggers(
        RunStateResult state,
        string potionId,
        HashSet<string> sink)
    {
        foreach (var ev in state.TriggeredSincePrev)
        {
            if (ev.Kind != TriggerKind.Potion) continue;
            if (!string.Equals(ev.Source, potionId, StringComparison.Ordinal)) continue;
            if (!string.IsNullOrEmpty(ev.Hook)) sink.Add(ev.Hook);
        }
    }
}
