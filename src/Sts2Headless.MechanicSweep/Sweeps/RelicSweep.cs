using System.Diagnostics;
using Sts2Headless.Agents.Contracts;
using Sts2Headless.Cheats;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.MechanicSweep.Sweeps;

// Per-RelicId smoke sweep. For each id in RelicIdNames.AllWireNames:
//
//   1. run/new(Ironclad, seed=42)
//   2. debug/give_relic(R)             — adds to Player.Relics, on-pickup
//                                         hooks fire (AfterRoomEntered,
//                                         AfterCurrentHpChanged, etc.)
//   3. debug/set_hp(999, 999)          — survive enemy retaliation
//   4. debug/replace_deck(FixedDeck)   — 4 starter cards (Strike×2,
//                                         Defend×2) so multi-turn play
//                                         gives broad hook coverage
//   5. debug/start_combat(BENIGN)      — many hooks fire here: Before-
//                                         CombatStart, AfterPlayerTurnStart,
//                                         AfterCardDrawn, ...
//   6. Two turns of "play first playable card, end turn"  — exercises
//                                         AfterCardPlayed, AfterDamage-
//                                         Given, AfterTurnEnd, AfterSide-
//                                         TurnStart, ...
//   7. debug/kill_all_enemies          — AfterDeath, AfterCombatVictory,
//                                         AfterCombatEnd fire
//
// After every action we drain state.TriggeredSincePrev and accumulate
// every entry where Kind=Relic && Source=this-relic's-id. If at least
// one hook fired → Triggered. Otherwise → Played (relic is alive in
// the run, but our fixture didn't trip its specific hook surface — true
// for relics keyed on rare events like gold gain, merchant purchase,
// rest-site heal, etc.).
//
// Tolerated outcomes (informational, not failures):
//   * Played       — relic was given cleanly but didn't fire a hook.
//                    Often a passive relic / wrong-fixture relic.
//   * Triggered    — relic fired ≥1 hook attributed to its id.
//   * Unplayable   — give_relic / play_card / end_turn returned a wire
//                    "no" (relic id rejected, energy insufficient, ...).
//
// Failure outcomes (assertion-grade):
//   * Crashed      — host or runtime threw an unhandled exception.
//   * Timeout      — per-id budget elapsed without resolving.
public sealed class RelicSweep
{
    // Per-id wall-clock cap. A relic fixture does ~8 wire round-trips
    // (run/new + give_relic + set_hp + replace_deck + start_combat + 4
    // turn-internals + kill_all_enemies + ~4 state drains); 30s is
    // generous enough for slow first-call paths.
    public static readonly System.TimeSpan PerRelicBudget = System.TimeSpan.FromSeconds(30);

    // SLIMES_NORMAL: same minimal Act-1 fixture CardSweep uses. Two
    // slimes, no SUMMON / EXHAUST / DOOM mechanics, low damage.
    public const string BenignEncounter = "SLIMES_NORMAL";

    // Four-card starter-pattern deck. Multi-card so we can play several
    // cards per turn (energy=3, cost=1 each → up to 3 plays). Strike +
    // Defend together trip AfterCardPlayed, AfterDamageGiven (Strike
    // hits slime), AfterBlockGained (Defend), AfterEnergySpent — most
    // common relic hook listeners. Two of each so the next-turn draw
    // (after discard) is still meaningful.
    private static readonly (string CardId, int UpgradeLevel)[] FixedDeck =
    [
        ("STRIKE_IRONCLAD", 0),
        ("STRIKE_IRONCLAD", 0),
        ("DEFEND_IRONCLAD", 0),
        ("DEFEND_IRONCLAD", 0),
    ];

    // Two play-then-end-turn iterations. More turns ≠ better hook
    // coverage (each hook either fires per turn or never), and adding
    // turns just spends wall-clock.
    private const int TurnsToDrive = 2;

    public async System.Threading.Tasks.Task<SweepReport> RunAsync(
        ITransport transport,
        System.Collections.Generic.IReadOnlyList<string>? sampleIds = null,
        string gameVersion = "unknown",
        System.Action<SweepRow>? onRow = null,
        System.Threading.CancellationToken ct = default)
    {
        var universe = SweepInternals.FilterReachable(RelicIdNames.AllWireNames);
        var ids = sampleIds is { Count: > 0 } ? sampleIds : universe;
        var sampled = sampleIds is { Count: > 0 };

        var rows = new System.Collections.Generic.List<SweepRow>(ids.Count);
        var totalSw = Stopwatch.StartNew();
        foreach (var relicId in ids)
        {
            ct.ThrowIfCancellationRequested();
            var row = await RunOneAsync(transport, relicId, ct);
            rows.Add(row);
            onRow?.Invoke(row);
        }
        totalSw.Stop();
        return new SweepReport(
            Kind: "relics",
            Rows: rows,
            TotalElapsed: totalSw.Elapsed,
            GameVersion: gameVersion,
            Sampled: sampled,
            UniverseSize: universe.Count);
    }

    private static async System.Threading.Tasks.Task<SweepRow> RunOneAsync(
        ITransport transport, string relicId, System.Threading.CancellationToken outerCt)
    {
        var sw = Stopwatch.StartNew();
        using var cts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(outerCt);
        cts.CancelAfter(PerRelicBudget);
        var ct = cts.Token;

        // Hooks this relic fired during the fixture, aggregated across
        // every state drain. We only care about the SET — a relic
        // firing AfterCardPlayed 3 times still counts as "fired
        // AfterCardPlayed" — so HashSet<string>.
        var firedHooks = new HashSet<string>(StringComparer.Ordinal);
        var steps = 0;

        try
        {
            // 1. Fresh run.
            await transport.SendAsync<RunNewResult>(
                "run/new",
                new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

            // 2. Grant the relic. Drains-and-classifies because some
            // relics have on-pickup CanPlay-shaped refusals (already
            // owned, character-locked, ...) we want to classify as
            // Unplayable; engine NREs from on-pickup hooks land as
            // Crashed.
            try
            {
                await transport.GiveRelicAsync(relicId);
            }
            catch (System.Exception wx) when (SweepInternals.IsWireError(wx))
            {
                sw.Stop();
                var c = SweepInternals.ClassifyWireError("relic", relicId, wx);
                return new SweepRow(
                    relicId, c.Outcome, Steps: 0, sw.Elapsed,
                    Detail: $"give_relic: {c.Detail}");
            }
            DrainTriggers(await transport.SendAsync<RunStateResult>("run/state"), relicId, firedHooks);

            // 3-4. Set up deck + HP, force benign combat.
            await transport.SetHpAsync(999, 999);
            await transport.ReplaceDeckAsync(FixedDeck);
            DrainTriggers(await transport.SendAsync<RunStateResult>("run/state"), relicId, firedHooks);

            // 5. Force combat start.
            var combat = await transport.StartCombatAsync(BenignEncounter);
            if (!combat.InProgress)
            {
                sw.Stop();
                return new SweepRow(
                    relicId, SweepOutcome.Crashed, Steps: steps, sw.Elapsed,
                    Detail: $"start_combat returned InProgress=false (enemyCount={combat.EnemyCount})");
            }
            DrainTriggers(await transport.SendAsync<RunStateResult>("run/state"), relicId, firedHooks);

            // 6. Two turns of play. Per turn: play every card we can
            // afford, then end_turn. Drains between every action so the
            // accumulated hook set is complete.
            for (var turn = 0; turn < TurnsToDrive; turn++)
            {
                ct.ThrowIfCancellationRequested();

                // Inner play loop — bounded so a weird "card was added
                // mid-play" loop can't run away.
                for (var play = 0; play < 6; play++)
                {
                    var state = await transport.SendAsync<RunStateResult>("run/state");
                    DrainTriggers(state, relicId, firedHooks);
                    if (state.CombatState is not { IsInProgress: true, IsPlayPhase: true } cs) break;

                    var idx = FindFirstAffordablePlayable(cs);
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
                            var c = SweepInternals.ClassifyWireError("relic", relicId, wx);
                            return new SweepRow(
                                relicId, c.Outcome, Steps: steps, sw.Elapsed,
                                Detail: $"play_card: {c.Detail}");
                        }
                        break; // benign refusal — bail this turn
                    }
                }

                // End the player's turn (and the engine pumps the
                // monster turn + next-player-turn-start).
                var preEnd = await transport.SendAsync<RunStateResult>("run/state");
                DrainTriggers(preEnd, relicId, firedHooks);
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
                        var c = SweepInternals.ClassifyWireError("relic", relicId, wx);
                        return new SweepRow(
                            relicId, c.Outcome, Steps: steps, sw.Elapsed,
                            Detail: $"end_turn: {c.Detail}");
                    }
                    break;
                }
            }

            // 7. Force combat end so AfterCombatVictory / AfterCombatEnd
            // fire even if the player didn't actually kill the slimes.
            // Wrapped so a kill_all_enemies wire error doesn't drown
            // the meaningful c.Outcome; if the engine itself crashes here,
            // the outer catch records it.
            try
            {
                _ = await transport.KillAllEnemiesAsync();
                DrainTriggers(await transport.SendAsync<RunStateResult>("run/state"), relicId, firedHooks);
            }
            catch (System.Exception wx) when (SweepInternals.IsWireError(wx) && !SweepInternals.IsInternalError(wx))
            {
                // Benign wire error from kill_all_enemies (combat
                // already ended naturally, no enemies to kill) —
                // swallow and accept the c.Outcome we have.
            }

            sw.Stop();
            var outcome2 = firedHooks.Count > 0 ? SweepOutcome.Triggered : SweepOutcome.Played;
            var detail = firedHooks.Count > 0
                ? $"hooks: {string.Join(",", firedHooks.OrderBy(h => h, StringComparer.Ordinal))}"
                : null;
            return new SweepRow(relicId, outcome2, Steps: steps, sw.Elapsed, detail);
        }
        catch (System.OperationCanceledException) when (cts.Token.IsCancellationRequested && !outerCt.IsCancellationRequested)
        {
            sw.Stop();
            return new SweepRow(
                relicId, SweepOutcome.Timeout, Steps: steps, sw.Elapsed,
                Detail: $"per-relic budget {PerRelicBudget.TotalSeconds:0}s exceeded");
        }
        catch (System.Exception ex)
        {
            sw.Stop();
            return new SweepRow(
                relicId, SweepOutcome.Crashed, Steps: steps, sw.Elapsed,
                Detail: $"{ex.GetType().Name}: {SweepInternals.Truncate(ex.Message)}");
        }
    }

    // First card in hand whose cost fits current energy AND whose
    // engine-side CanPlay validator returns true. Skipping CanPlay=false
    // cards avoids tripping the host's -32603 "CanPlay returned false"
    // path (which we'd classify as Unplayable but it just wastes time).
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

    private static void DrainTriggers(
        RunStateResult state,
        string relicId,
        HashSet<string> sink)
    {
        foreach (var ev in state.TriggeredSincePrev)
        {
            if (ev.Kind != TriggerKind.Relic) continue;
            if (!string.Equals(ev.Source, relicId, StringComparison.Ordinal)) continue;
            if (!string.IsNullOrEmpty(ev.Hook)) sink.Add(ev.Hook);
        }
    }
}
