using System.Diagnostics;
using Sts2Headless.Agents.Contracts;
using Sts2Headless.Cheats;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.MechanicSweep.Sweeps;

// Per-EnchantmentId smoke sweep. Same shape as AfflictionSweep but via
// CardCmd.Enchant. Enchantments attach to cards in hand and modify
// their behavior (Adroit / Clone / Corrupted / ...).
public sealed class EnchantmentSweep
{
    public static readonly System.TimeSpan PerEnchantmentBudget = System.TimeSpan.FromSeconds(30);
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
        var universe = SweepInternals.FilterReachable(EnchantmentIdNames.AllWireNames);
        var ids = sampleIds is { Count: > 0 } ? sampleIds : universe;
        var sampled = sampleIds is { Count: > 0 };

        var rows = new System.Collections.Generic.List<SweepRow>(ids.Count);
        var totalSw = Stopwatch.StartNew();
        foreach (var enchantmentId in ids)
        {
            ct.ThrowIfCancellationRequested();
            var row = await RunOneAsync(transport, enchantmentId, ct);
            rows.Add(row);
            onRow?.Invoke(row);
        }
        totalSw.Stop();
        return new SweepReport(
            Kind: "enchantments",
            Rows: rows,
            TotalElapsed: totalSw.Elapsed,
            GameVersion: gameVersion,
            Sampled: sampled,
            UniverseSize: universe.Count);
    }

    private static async System.Threading.Tasks.Task<SweepRow> RunOneAsync(
        ITransport transport, string enchantmentId, System.Threading.CancellationToken outerCt)
    {
        var sw = Stopwatch.StartNew();
        using var cts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(outerCt);
        cts.CancelAfter(PerEnchantmentBudget);
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
                    enchantmentId, SweepOutcome.Crashed, Steps: 0, sw.Elapsed,
                    Detail: $"start_combat returned InProgress=false (enemyCount={combat.EnemyCount})");
            }
            DrainTriggers(await transport.SendAsync<RunStateResult>("run/state"), enchantmentId, firedHooks);

            string targetCard;
            try
            {
                var resp = await transport.EnchantCardAsync(enchantmentId, handIndex: 0, amount: 1);
                targetCard = resp.CardId;
            }
            catch (System.Exception wx) when (SweepInternals.IsWireError(wx))
            {
                sw.Stop();
                var c = SweepInternals.ClassifyWireError("enchantment", enchantmentId, wx);
                return new SweepRow(
                    enchantmentId, c.Outcome, Steps: 0, sw.Elapsed,
                    Detail: $"enchant_card: {c.Detail}");
            }
            DrainTriggers(await transport.SendAsync<RunStateResult>("run/state"), enchantmentId, firedHooks);

            try
            {
                _ = await transport.SendAsync<RunEndTurnResult>("run/end_turn");
                steps++;
                DrainTriggers(await transport.SendAsync<RunStateResult>("run/state"), enchantmentId, firedHooks);
            }
            catch (System.Exception wx) when (SweepInternals.IsWireError(wx))
            {
                if (SweepInternals.IsInternalError(wx))
                {
                    sw.Stop();
                    var c = SweepInternals.ClassifyWireError("enchantment", enchantmentId, wx);
                    return new SweepRow(
                        enchantmentId, c.Outcome, Steps: steps, sw.Elapsed,
                        Detail: $"end_turn: {c.Detail}");
                }
            }

            try
            {
                _ = await transport.KillAllEnemiesAsync();
                DrainTriggers(await transport.SendAsync<RunStateResult>("run/state"), enchantmentId, firedHooks);
            }
            catch (System.Exception wx) when (SweepInternals.IsWireError(wx) && !SweepInternals.IsInternalError(wx)) { }

            sw.Stop();
            var outcome2 = firedHooks.Count > 0 ? SweepOutcome.Triggered : SweepOutcome.Played;
            var detail = $"target={targetCard}"
                + (firedHooks.Count > 0 ? $",hooks: {string.Join(",", firedHooks.OrderBy(h => h, StringComparer.Ordinal))}" : "");
            return new SweepRow(enchantmentId, outcome2, Steps: steps, sw.Elapsed, detail);
        }
        catch (System.OperationCanceledException) when (cts.Token.IsCancellationRequested && !outerCt.IsCancellationRequested)
        {
            sw.Stop();
            return new SweepRow(
                enchantmentId, SweepOutcome.Timeout, Steps: steps, sw.Elapsed,
                Detail: $"per-enchantment budget {PerEnchantmentBudget.TotalSeconds:0}s exceeded");
        }
        catch (System.Exception ex)
        {
            sw.Stop();
            return new SweepRow(
                enchantmentId, SweepOutcome.Crashed, Steps: steps, sw.Elapsed,
                Detail: $"{ex.GetType().Name}: {SweepInternals.Truncate(ex.Message)}");
        }
    }

    private static void DrainTriggers(RunStateResult state, string id, HashSet<string> sink)
    {
        foreach (var ev in state.TriggeredSincePrev)
        {
            if (ev.Kind != TriggerKind.Enchantment) continue;
            if (!string.Equals(ev.Source, id, StringComparison.Ordinal)) continue;
            if (!string.IsNullOrEmpty(ev.Hook)) sink.Add(ev.Hook);
        }
    }
}
