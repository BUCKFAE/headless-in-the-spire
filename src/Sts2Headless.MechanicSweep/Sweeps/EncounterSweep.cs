using System.Diagnostics;
using Sts2Headless.Agents.Contracts;
using Sts2Headless.Cheats;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.MechanicSweep.Sweeps;

// Per-EncounterId smoke sweep. For each id in EncounterIdNames.AllWireNames
// (every boss / elite / normal sts2 ships — ~80 entries):
//
//   1. run/new(Ironclad, seed=42)
//   2. debug/set_hp(999, 999)            — survive monster bursts
//   3. debug/replace_deck(STARTER_DECK)  — 4-card Strike/Defend deck so
//                                          turns proceed without empty-
//                                          hand stalls
//   4. debug/start_combat(encounterId)   — bypass map progression;
//                                          BeforeCombatStart fires
//   5. Loop (≤TurnsToDrive):
//        - play first affordable+playable card (target=0)
//        - end_turn — drives the monster turn, exercising every alive
//                     monster's intent path
//   6. debug/kill_all_enemies            — forces the engine into
//                                          AfterCombatVictory / AfterCombatEnd
//
// What this sweep specifically catches that other sweeps don't:
//   * monster constructor / power-application crashes at combat start
//   * monster move bodies that NRE on first execution (the
//     MonsterPatchAudit shape — see HangPatches.Monsters.cs)
//   * the rare "encounter loads but engine pumps an enemy turn into a
//     crash" path
//
// Replaces the historical tests/Sts2Headless.End2EndTests/
// EveryEncounterSmokeTests.cs (deleted alongside the agent-driven
// coverage rewrite). That version used IroncladAgent + Hellraiser+Pommel
// auto-play, which surfaces MCTS-specific NREs (QUEEN_BOSS) that aren't
// engine bugs — this rewrite goes simpler so the signal is just "engine
// fires the encounter's combat start + 2 turns + cleanup without
// throwing." Per-encounter overrides (the old s_expectedDoormakerShape
// pattern) aren't needed because the simple-deck fixture doesn't trip
// HUNGER_POWER / ILLUSION / similar mechanics that broke the old shape.
//
// Triggered axis is left empty for encounters: EncounterModel has zero
// AbstractModel hook overrides in the pinned game version (the
// InstrumentationKindParityTest's hook count for Encounter is 0), and
// monster firings get attributed to the MONSTER id, not the encounter.
// Players who want "which monsters fire hooks" can rely on the future
// MonsterSweep or read the existing relic/monster triggered axes.
public sealed class EncounterSweep
{
    public static readonly System.TimeSpan PerEncounterBudget = System.TimeSpan.FromSeconds(60);

    // Two turns: turn 1 lets the player play a card, turn 2 lets the
    // monsters take their turn (and the engine pumps the enemy-turn
    // path). kill_all_enemies follows so combat-end hooks fire. More
    // turns add wall-clock without finding more bug shapes.
    private const int TurnsToDrive = 2;

    // Same 4-card starter deck the RelicSweep uses. Strike + Defend
    // gives enough action to fire AfterCardPlayed / AfterDamageGiven /
    // AfterBlockGained / AfterEnergySpent on turn 1, plus survive the
    // first round of monster attacks via the Defend block.
    private static readonly (string CardId, int UpgradeLevel)[] FixedDeck =
    [
        ("STRIKE_IRONCLAD", 0),
        ("STRIKE_IRONCLAD", 0),
        ("DEFEND_IRONCLAD", 0),
        ("DEFEND_IRONCLAD", 0),
    ];

    // Two ways to drive the sweep:
    //
    //   * RunAsync(transport, ...)           — single shared transport for
    //                                          every encounter. Fast but
    //                                          relies on the host to fully
    //                                          recover between iterations.
    //                                          Some boss encounters
    //                                          (QUEEN_BOSS-shape multi-
    //                                          phase mechanics) leave the
    //                                          engine in a state that
    //                                          breaks the NEXT iteration's
    //                                          run/new with an NRE, then
    //                                          every later encounter
    //                                          cascades. Not safe for
    //                                          this kind.
    //
    //   * RunAsync(transportFactory, ...)    — caller provides a factory
    //                                          that returns (ITransport,
    //                                          IAsyncDisposable) per
    //                                          encounter. The sweep
    //                                          disposes between
    //                                          iterations, fully
    //                                          isolating each one.
    //                                          Slower (~1.5s host boot
    //                                          per encounter) but each
    //                                          row's outcome is
    //                                          attributable to ITS
    //                                          encounter, not the
    //                                          previous one's bleed.
    //
    // The encounter sweep test uses the factory form for accurate per-
    // encounter signal; the other sweeps (Card/Relic/Potion/Event) reuse
    // a single transport because their fixtures don't reach the host-
    // breaking depths.
    public async System.Threading.Tasks.Task<SweepReport> RunAsync(
        ITransport transport,
        System.Collections.Generic.IReadOnlyList<string>? sampleIds = null,
        string gameVersion = "unknown",
        System.Action<SweepRow>? onRow = null,
        System.Threading.CancellationToken ct = default)
    {
        var ids = ResolveIds(sampleIds, out var sampled, out var universeSize);
        var rows = new System.Collections.Generic.List<SweepRow>(ids.Count);
        var totalSw = Stopwatch.StartNew();
        foreach (var encounterId in ids)
        {
            ct.ThrowIfCancellationRequested();
            var row = await RunSingleAsync(transport, encounterId, ct);
            rows.Add(row);
            onRow?.Invoke(row);
        }
        totalSw.Stop();
        return new SweepReport(
            Kind: "encounters",
            Rows: rows,
            TotalElapsed: totalSw.Elapsed,
            GameVersion: gameVersion,
            Sampled: sampled,
            UniverseSize: universeSize);
    }

    // Per-encounter isolated mode. `transportFactory` is called once per
    // id; the returned IAsyncDisposable is disposed before the next
    // iteration. Use this when the per-encounter fixture might leave the
    // host in a broken state (boss encounters with multi-phase mechanics,
    // etc.).
    public async System.Threading.Tasks.Task<SweepReport> RunAsync(
        System.Func<System.Threading.Tasks.Task<(ITransport Transport, System.IAsyncDisposable Lifetime)>> transportFactory,
        System.Collections.Generic.IReadOnlyList<string>? sampleIds = null,
        string gameVersion = "unknown",
        System.Action<SweepRow>? onRow = null,
        System.Threading.CancellationToken ct = default)
    {
        var ids = ResolveIds(sampleIds, out var sampled, out var universeSize);
        var rows = new System.Collections.Generic.List<SweepRow>(ids.Count);
        var totalSw = Stopwatch.StartNew();
        foreach (var encounterId in ids)
        {
            ct.ThrowIfCancellationRequested();
            SweepRow row;
            var (transport, lifetime) = await transportFactory();
            try
            {
                row = await RunSingleAsync(transport, encounterId, ct);
            }
            finally
            {
                await lifetime.DisposeAsync();
            }
            rows.Add(row);
            onRow?.Invoke(row);
        }
        totalSw.Stop();
        return new SweepReport(
            Kind: "encounters",
            Rows: rows,
            TotalElapsed: totalSw.Elapsed,
            GameVersion: gameVersion,
            Sampled: sampled,
            UniverseSize: universeSize);
    }

    private static System.Collections.Generic.IReadOnlyList<string> ResolveIds(
        System.Collections.Generic.IReadOnlyList<string>? sampleIds,
        out bool sampled,
        out int universeSize)
    {
        var universe = SweepInternals.FilterReachable(EncounterIdNames.AllWireNames);
        universeSize = universe.Count;
        sampled = sampleIds is { Count: > 0 };
        return sampled ? sampleIds! : universe;
    }

    // Exposed for callers that want full per-id control (e.g. the
    // EncounterSweepTests fresh-subprocess loop).
    public static async System.Threading.Tasks.Task<SweepRow> RunSingleAsync(
        ITransport transport, string encounterId, System.Threading.CancellationToken outerCt)
    {
        var sw = Stopwatch.StartNew();
        using var cts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(outerCt);
        cts.CancelAfter(PerEncounterBudget);
        var ct = cts.Token;

        var steps = 0;
        var monsterIdsSeen = new HashSet<string>(StringComparer.Ordinal);

        try
        {
            // 1. Fresh run.
            await transport.SendAsync<RunNewResult>(
                "run/new",
                new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

            // 2-3. Setup.
            await transport.SetHpAsync(999, 999);
            await transport.ReplaceDeckAsync(FixedDeck);

            // 4. Force the encounter.
            DebugStartCombatResult combat;
            try
            {
                combat = await transport.StartCombatAsync(encounterId);
            }
            catch (System.Exception wx) when (SweepInternals.IsWireError(wx))
            {
                sw.Stop();
                var outcome = SweepInternals.IsInternalError(wx) ? SweepOutcome.Crashed : SweepOutcome.Unplayable;
                return new SweepRow(
                    encounterId, outcome, Steps: 0, sw.Elapsed,
                    Detail: $"start_combat: {wx.GetType().Name}: {SweepInternals.Truncate(wx.Message)}");
            }
            if (!combat.InProgress)
            {
                sw.Stop();
                return new SweepRow(
                    encounterId, SweepOutcome.Unplayable, Steps: 0, sw.Elapsed,
                    Detail: $"start_combat returned InProgress=false (enemyCount={combat.EnemyCount})");
            }

            // Capture the monster set once so the Detail line tells the
            // reader which enemies this encounter spawned without
            // needing to cross-reference the catalog.
            var initialState = await transport.SendAsync<RunStateResult>("run/state");
            if (initialState.CombatState is CombatState cs)
            {
                foreach (var enemy in cs.Enemies)
                {
                    if (!string.IsNullOrEmpty(enemy.MonsterId))
                        monsterIdsSeen.Add(enemy.MonsterId);
                }
            }

            // 5. Two turns. Each iteration: play every affordable card,
            // then end_turn (monster-turn engine pump runs here).
            for (var turn = 0; turn < TurnsToDrive; turn++)
            {
                ct.ThrowIfCancellationRequested();

                for (var play = 0; play < 6; play++)
                {
                    var state = await transport.SendAsync<RunStateResult>("run/state");
                    if (state.CombatState is not { IsInProgress: true, IsPlayPhase: true } combatState) break;

                    var idx = FindFirstAffordablePlayable(combatState);
                    if (idx < 0) break;

                    try
                    {
                        _ = await transport.SendAsync<RunPlayCardResult>(
                            "run/play_card",
                            new RunPlayCardParams(CardIndex: idx, TargetIndex: 0));
                        steps++;
                    }
                    catch (System.Exception wx) when (SweepInternals.IsWireError(wx))
                    {
                        if (SweepInternals.IsInternalError(wx))
                        {
                            sw.Stop();
                            return new SweepRow(
                                encounterId, SweepOutcome.Crashed, Steps: steps, sw.Elapsed,
                                Detail: $"play_card: {wx.GetType().Name}: {SweepInternals.Truncate(wx.Message)}");
                        }
                        break;
                    }
                }

                var preEnd = await transport.SendAsync<RunStateResult>("run/state");
                if (preEnd.CombatState is not { IsInProgress: true }) break;

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
                            encounterId, SweepOutcome.Crashed, Steps: steps, sw.Elapsed,
                            Detail: $"end_turn: {wx.GetType().Name}: {SweepInternals.Truncate(wx.Message)}");
                    }
                    break;
                }
            }

            // 6. Forced kill so AfterCombatVictory / AfterCombatEnd fire
            // even if we didn't actually clear the encounter.
            try
            {
                _ = await transport.KillAllEnemiesAsync();
            }
            catch (System.Exception wx) when (SweepInternals.IsWireError(wx) && !SweepInternals.IsInternalError(wx))
            {
                // Benign — combat may have ended naturally.
            }

            sw.Stop();
            var detail = monsterIdsSeen.Count > 0
                ? $"monsters: {string.Join(",", monsterIdsSeen.OrderBy(s => s, StringComparer.Ordinal))}"
                : null;
            return new SweepRow(encounterId, SweepOutcome.Played, Steps: steps, sw.Elapsed, detail);
        }
        catch (System.OperationCanceledException) when (cts.Token.IsCancellationRequested && !outerCt.IsCancellationRequested)
        {
            sw.Stop();
            return new SweepRow(
                encounterId, SweepOutcome.Timeout, Steps: steps, sw.Elapsed,
                Detail: $"per-encounter budget {PerEncounterBudget.TotalSeconds:0}s exceeded");
        }
        catch (System.Exception ex)
        {
            sw.Stop();
            return new SweepRow(
                encounterId, SweepOutcome.Crashed, Steps: steps, sw.Elapsed,
                Detail: $"{ex.GetType().Name}: {SweepInternals.Truncate(ex.Message)}");
        }
    }

    private static int FindFirstAffordablePlayable(CombatState cs)
    {
        for (int i = 0; i < cs.Hand.Count; i++)
        {
            var card = cs.Hand[i];
            if (card.Cost > cs.Energy) continue;
            if (!card.CanPlay) continue;
            return i;
        }
        return -1;
    }
}
