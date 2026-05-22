using System.Diagnostics;
using Sts2Headless.Agents.Contracts;
using Sts2Headless.Cheats;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.MechanicSweep.Sweeps;

// Per-AfflictionId smoke sweep. Afflictions attach to cards via
// CardCmd.Afflict, so the fixture has to put a card in hand and apply
// the affliction to it:
//
//   1. run/new(Ironclad, seed=42)
//   2. debug/set_hp(999, 999)
//   3. debug/replace_deck(STARTER_DECK)
//   4. debug/start_combat(SLIMES_NORMAL)
//   5. debug/afflict_card(afflictionId, handIndex=0)
//   6. drain triggers
//   7. run/end_turn  — afflicted card may auto-trigger on turn cycle
//   8. drain triggers
//   9. debug/kill_all_enemies
//
// AfflictionModel exposes few AbstractModel hook overrides (only Hexed
// surfaces in the listener-dispatch probe), so Triggered counts will be
// low across the kind. The sweep's primary signal is "afflict_card →
// 2 turns → no crash."
public sealed class AfflictionSweep
{
    public static readonly System.TimeSpan PerAfflictionBudget = System.TimeSpan.FromSeconds(30);
    public const string BenignEncounter = "SLIMES_NORMAL";

    private static readonly (string CardId, int UpgradeLevel)[] FixedDeck =
    [
        ("STRIKE_IRONCLAD", 0),
        ("STRIKE_IRONCLAD", 0),
        ("DEFEND_IRONCLAD", 0),
        ("DEFEND_IRONCLAD", 0),
    ];

    public async System.Threading.Tasks.Task<SweepReport> RunAsync(
        ITransport transport,
        System.Collections.Generic.IReadOnlyList<string>? sampleIds = null,
        string gameVersion = "unknown",
        System.Action<SweepRow>? onRow = null,
        System.Threading.CancellationToken ct = default)
    {
        var universe = SweepInternals.FilterReachable(AfflictionIdNames.AllWireNames);
        var ids = sampleIds is { Count: > 0 } ? sampleIds : universe;
        var sampled = sampleIds is { Count: > 0 };

        var rows = new System.Collections.Generic.List<SweepRow>(ids.Count);
        var totalSw = Stopwatch.StartNew();
        foreach (var afflictionId in ids)
        {
            ct.ThrowIfCancellationRequested();
            var row = await RunOneAsync(transport, afflictionId, ct);
            rows.Add(row);
            onRow?.Invoke(row);
        }
        totalSw.Stop();
        return new SweepReport(
            Kind: "afflictions",
            Rows: rows,
            TotalElapsed: totalSw.Elapsed,
            GameVersion: gameVersion,
            Sampled: sampled,
            UniverseSize: universe.Count);
    }

    private static async System.Threading.Tasks.Task<SweepRow> RunOneAsync(
        ITransport transport, string afflictionId, System.Threading.CancellationToken outerCt)
    {
        var sw = Stopwatch.StartNew();
        using var cts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(outerCt);
        cts.CancelAfter(PerAfflictionBudget);
        var ct = cts.Token;

        var firedHooks = new HashSet<string>(StringComparer.Ordinal);
        var steps = 0;

        try
        {
            await transport.SendAsync<RunNewResult>(
                "run/new",
                new RunNewParams(Character: Character.Ironclad, Seed: 42uL));
            await transport.SetHpAsync(999, 999);
            await transport.ReplaceDeckAsync(FixedDeck);
            var combat = await transport.StartCombatAsync(BenignEncounter);
            if (!combat.InProgress)
            {
                sw.Stop();
                return new SweepRow(
                    afflictionId, SweepOutcome.Crashed, Steps: 0, sw.Elapsed,
                    Detail: $"start_combat returned InProgress=false (enemyCount={combat.EnemyCount})");
            }
            DrainTriggers(await transport.SendAsync<RunStateResult>("run/state"), afflictionId, firedHooks);

            string targetCard;
            try
            {
                var resp = await transport.AfflictCardAsync(afflictionId, handIndex: 0, amount: 1);
                targetCard = resp.CardId;
            }
            catch (System.Exception wx) when (SweepInternals.IsWireError(wx))
            {
                sw.Stop();
                var c = SweepInternals.ClassifyWireError("affliction", afflictionId, wx);
                return new SweepRow(
                    afflictionId, c.Outcome, Steps: 0, sw.Elapsed,
                    Detail: $"afflict_card: {c.Detail}");
            }
            DrainTriggers(await transport.SendAsync<RunStateResult>("run/state"), afflictionId, firedHooks);

            // One end_turn so afflicted-card on-tick paths fire.
            try
            {
                _ = await transport.SendAsync<RunEndTurnResult>("run/end_turn");
                steps++;
                DrainTriggers(await transport.SendAsync<RunStateResult>("run/state"), afflictionId, firedHooks);
            }
            catch (System.Exception wx) when (SweepInternals.IsWireError(wx))
            {
                if (SweepInternals.IsInternalError(wx))
                {
                    sw.Stop();
                    var c = SweepInternals.ClassifyWireError("affliction", afflictionId, wx);
                    return new SweepRow(
                        afflictionId, c.Outcome, Steps: steps, sw.Elapsed,
                        Detail: $"end_turn: {c.Detail}");
                }
            }

            // Force end so AfterCombatVictory / AfterCombatEnd fire.
            try
            {
                _ = await transport.KillAllEnemiesAsync();
                DrainTriggers(await transport.SendAsync<RunStateResult>("run/state"), afflictionId, firedHooks);
            }
            catch (System.Exception wx) when (SweepInternals.IsWireError(wx) && !SweepInternals.IsInternalError(wx)) { }

            sw.Stop();
            var outcome2 = firedHooks.Count > 0 ? SweepOutcome.Triggered : SweepOutcome.Played;
            var detail = $"target={targetCard}"
                + (firedHooks.Count > 0 ? $",hooks: {string.Join(",", firedHooks.OrderBy(h => h, StringComparer.Ordinal))}" : "");
            return new SweepRow(afflictionId, outcome2, Steps: steps, sw.Elapsed, detail);
        }
        catch (System.OperationCanceledException) when (cts.Token.IsCancellationRequested && !outerCt.IsCancellationRequested)
        {
            sw.Stop();
            return new SweepRow(
                afflictionId, SweepOutcome.Timeout, Steps: steps, sw.Elapsed,
                Detail: $"per-affliction budget {PerAfflictionBudget.TotalSeconds:0}s exceeded");
        }
        catch (System.Exception ex)
        {
            sw.Stop();
            return new SweepRow(
                afflictionId, SweepOutcome.Crashed, Steps: steps, sw.Elapsed,
                Detail: $"{ex.GetType().Name}: {SweepInternals.Truncate(ex.Message)}");
        }
    }

    private static void DrainTriggers(RunStateResult state, string id, HashSet<string> sink)
    {
        foreach (var ev in state.TriggeredSincePrev)
        {
            if (ev.Kind != TriggerKind.Affliction) continue;
            if (!string.Equals(ev.Source, id, StringComparison.Ordinal)) continue;
            if (!string.IsNullOrEmpty(ev.Hook)) sink.Add(ev.Hook);
        }
    }
}
