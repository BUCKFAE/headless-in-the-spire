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

    // Three ways to drive the sweep:
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
    //                                          this kind on its own.
    //
    //   * RunAsync(transportFactory, ...,    — shared-with-recovery mode.
    //       freshHostPerId: false)             Reuses one host across
    //                                          iterations and only
    //                                          recreates when an iteration
    //                                          Crashes/Times-out (or when
    //                                          a Crashed iteration's retry
    //                                          on a fresh host turns
    //                                          Played, proving the prior
    //                                          host was the source).
    //                                          ~3-4× faster than the
    //                                          fresh-per-id mode while
    //                                          preserving per-row
    //                                          attribution: any boss that
    //                                          bleeds state into the next
    //                                          iteration triggers a single
    //                                          recreate, and the iteration
    //                                          that would have erroneously
    //                                          crashed is retried on the
    //                                          fresh host.
    //
    //   * RunAsync(transportFactory, ...,    — per-encounter isolated mode.
    //       freshHostPerId: true)              The factory is called once
    //                                          per id; the lifetime is
    //                                          disposed before the next
    //                                          iteration. Use this when
    //                                          debugging a specific
    //                                          encounter where you want
    //                                          zero ambiguity about
    //                                          state-bleed (set
    //                                          MECHANIC_SWEEP_FRESH_HOST_PER_ID=1
    //                                          on the test).
    //
    // The encounter sweep test uses the shared-with-recovery form by
    // default for ~3-4× speedup; the other sweeps (Card/Relic/Potion/Event)
    // reuse a single transport directly because their fixtures don't reach
    // the host-breaking depths.
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

    // Per-encounter factory-driven mode. `transportFactory` is called
    // lazily: once at startup, again only when the previous iteration's
    // host needs to be replaced (state-bleed signal: Crashed/Timeout, or
    // `freshHostPerId: true` which forces a recreate after every id).
    //
    // Recovery is two-stage:
    //   1. If an iteration returns Crashed AND it was running on a host
    //      reused from an earlier iteration, dispose+recreate and retry
    //      THIS encounter on the fresh host. A Played retry confirms the
    //      prior boss bled state; the original Crashed row is discarded
    //      in favor of the retry's outcome.
    //   2. After every iteration that ended in Crashed/Timeout, dispose
    //      the host so the next iteration starts fresh. This bounds the
    //      blast radius of any state-bleed we couldn't recover from.
    public async System.Threading.Tasks.Task<SweepReport> RunAsync(
        System.Func<System.Threading.Tasks.Task<(ITransport Transport, System.IAsyncDisposable Lifetime)>> transportFactory,
        System.Collections.Generic.IReadOnlyList<string>? sampleIds = null,
        string gameVersion = "unknown",
        System.Action<SweepRow>? onRow = null,
        bool freshHostPerId = false,
        System.Threading.CancellationToken ct = default)
    {
        var ids = ResolveIds(sampleIds, out var sampled, out var universeSize);
        var rows = new System.Collections.Generic.List<SweepRow>(ids.Count);
        var totalSw = Stopwatch.StartNew();

        // current is the shared host being reused across iterations, or
        // null if the previous iteration tore it down (forcing a fresh
        // boot on the next iteration). isFreshHost tracks whether the
        // host has executed an encounter yet — used to decide whether
        // a Crashed outcome is worth retrying on a recreated host.
        (ITransport Transport, System.IAsyncDisposable Lifetime)? current = null;
        var isFreshHost = false;

        try
        {
            foreach (var encounterId in ids)
            {
                ct.ThrowIfCancellationRequested();

                if (current is null)
                {
                    current = await transportFactory();
                    isFreshHost = true;
                }

                SweepRow row;
                try
                {
                    row = await RunSingleAsync(current.Value.Transport, encounterId, ct);
                }
                catch (System.Exception ex)
                {
                    // RunSingleAsync has its own broad catch and shouldn't
                    // throw, but if the transport itself died we end up
                    // here. Treat as Crashed-with-broken-host: dispose and
                    // (below) retry on a fresh host if applicable.
                    row = new SweepRow(
                        encounterId, SweepOutcome.Crashed, Steps: 0, Elapsed: System.TimeSpan.Zero,
                        Detail: $"transport: {ex.GetType().Name}: {SweepInternals.Truncate(ex.Message)}");
                }

                // Recovery stage 1: if this iteration crashed and the
                // host was carried over from a prior iteration, the crash
                // may be state-bleed from the previous encounter rather
                // than this one's fault. Recreate + retry once.
                if (row.Outcome == SweepOutcome.Crashed && !isFreshHost && !freshHostPerId)
                {
                    await current.Value.Lifetime.DisposeAsync();
                    current = await transportFactory();
                    isFreshHost = true;
                    SweepRow retry;
                    try
                    {
                        retry = await RunSingleAsync(current.Value.Transport, encounterId, ct);
                    }
                    catch (System.Exception ex)
                    {
                        retry = new SweepRow(
                            encounterId, SweepOutcome.Crashed, Steps: 0, Elapsed: System.TimeSpan.Zero,
                            Detail: $"transport (retry): {ex.GetType().Name}: {SweepInternals.Truncate(ex.Message)}");
                    }
                    // Use the retry outcome regardless: a Played retry
                    // proves the original crash was state-bleed and the
                    // encounter itself is fine; a Crashed retry confirms
                    // the encounter genuinely broke and the row stays red.
                    row = retry;
                }

                rows.Add(row);
                onRow?.Invoke(row);

                isFreshHost = false;

                // Recovery stage 2: any Crashed/Timeout — or any iteration
                // when freshHostPerId is on — invalidates the shared host
                // for safety; the next iteration boots a fresh one.
                var shouldRecreate = freshHostPerId
                    || row.Outcome is SweepOutcome.Crashed or SweepOutcome.Timeout;
                if (shouldRecreate)
                {
                    await current.Value.Lifetime.DisposeAsync();
                    current = null;
                }
            }
        }
        finally
        {
            if (current is not null) await current.Value.Lifetime.DisposeAsync();
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
                var c = SweepInternals.ClassifyWireError("encounter", encounterId, wx);
                return new SweepRow(
                    encounterId, c.Outcome, Steps: 0, sw.Elapsed,
                    Detail: $"start_combat: {c.Detail}");
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
                            var c = SweepInternals.ClassifyWireError("encounter", encounterId, wx);
                            return new SweepRow(
                                encounterId, c.Outcome, Steps: steps, sw.Elapsed,
                                Detail: $"play_card: {c.Detail}");
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
                        var c = SweepInternals.ClassifyWireError("encounter", encounterId, wx);
                        return new SweepRow(
                            encounterId, c.Outcome, Steps: steps, sw.Elapsed,
                            Detail: $"end_turn: {c.Detail}");
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
