using System.Diagnostics;
using Sts2Headless.Agents.Contracts;
using Sts2Headless.Cheats;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.MechanicSweep.Sweeps;

// Per-CardId smoke sweep. For each id in CardIdNames.AllWireNames:
//
//   1. run/new(Ironclad, seed=42)                — fresh state
//   2. debug/set_hp(999, 999)                    — survive incidental damage
//   3. debug/replace_deck([(card, 0)])           — single-card deck so it
//                                                  lands in the opening hand
//   4. debug/start_combat("SLIMES_NORMAL")       — benign Act-1 fight
//   5. Pump up to MaxTurnsToFindCard turns       — find the card in hand;
//                                                  play it at target=0 if found
//   6. Classify outcome                          — Played / Unreachable /
//                                                  Unplayable / Crashed / Timeout
//
// Tolerated outcomes (informational, not failures):
//   * Unreachable — single-card deck shouldn't fail this often, but cards
//     forced into Innate / draw-pile-only states might. Surfacing the
//     count tells us where to extend the fixture.
//   * Unplayable — wire said no (insufficient energy for 3-cost cards on
//     turn 1, wrong target type, etc.). Engine handled the request and
//     replied with a structured error; no crash.
//
// Failure outcomes (assertion-grade):
//   * Crashed — the host or runtime threw an unhandled exception.
//   * Timeout — the per-id budget elapsed without resolving.
//
// Design notes:
//   * Target=0 for every card. Cards with TargetType.None / AllEnemies /
//     Self ignore the target; cards with TargetType.AnyEnemy hit the
//     first slime. No per-card target customization in this sweep —
//     that's a richer-fixture concern, not "does it crash" concern.
//   * CardSelectIndices=null lets the host's ICardSelector pick the first
//     valid card per prompt (Headbutt → first discard pile card,
//     Armaments → first hand card, …). Deterministic enough for smoke.
//   * The sweep reuses ONE host subprocess across all ids — run/new
//     resets between cards so state doesn't leak.
public sealed class CardSweep
{
    // Per-id wall-clock cap. A healthy card resolves in well under a second
    // through this fixture; 20s gives plenty of headroom for one-off slow
    // paths (e.g. cards that trigger many AfterCardPlayed listeners on the
    // first SLIMES_NORMAL turn).
    public static readonly System.TimeSpan PerCardBudget = System.TimeSpan.FromSeconds(20);

    // Cards with Innate or "draw later" mechanics may not appear in the
    // opening hand of a single-card deck. Loop a couple of end-turns so
    // the second/third draw can surface them. More than 3 turns is
    // probably a different fixture's problem.
    public const int MaxTurnsToFindCard = 3;

    // SLIMES_NORMAL is the canonical "smallest threat in the game" — two
    // slimes, low damage, no SUMMON / EXHAUST / DOOM mechanics that would
    // mess with the card under test. Same encounter the negative-control
    // baseline uses elsewhere.
    public const string BenignEncounter = "SLIMES_NORMAL";

    public async System.Threading.Tasks.Task<SweepReport> RunAsync(
        ITransport transport,
        System.Collections.Generic.IReadOnlyList<string>? sampleIds = null,
        string gameVersion = "unknown",
        System.Action<SweepRow>? onRow = null,
        System.Threading.CancellationToken ct = default)
    {
        var universe = CardIdNames.AllWireNames
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();
        var ids = sampleIds is { Count: > 0 } ? sampleIds : universe;
        var sampled = sampleIds is { Count: > 0 };

        var rows = new System.Collections.Generic.List<SweepRow>(ids.Count);
        var totalSw = Stopwatch.StartNew();
        foreach (var cardId in ids)
        {
            ct.ThrowIfCancellationRequested();
            var row = await RunOneAsync(transport, cardId, ct);
            rows.Add(row);
            onRow?.Invoke(row);
        }
        totalSw.Stop();
        return new SweepReport(
            Kind: "cards",
            Rows: rows,
            TotalElapsed: totalSw.Elapsed,
            GameVersion: gameVersion,
            Sampled: sampled,
            UniverseSize: universe.Count);
    }

    private static async System.Threading.Tasks.Task<SweepRow> RunOneAsync(
        ITransport transport, string cardId, System.Threading.CancellationToken outerCt)
    {
        var sw = Stopwatch.StartNew();
        using var cts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(outerCt);
        cts.CancelAfter(PerCardBudget);
        var ct = cts.Token;
        try
        {
            // 1. Fresh run — cleans accumulated relics, pending rewards,
            // half-transitioned combat from the previous card.
            await transport.SendAsync<RunNewResult>(
                "run/new",
                new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

            // 2-4. Setup: HP cheat, single-card deck, force benign combat.
            await transport.SetHpAsync(999, 999);
            await transport.ReplaceDeckAsync(new[] { (cardId, 0) });
            var combat = await transport.StartCombatAsync(BenignEncounter);
            if (!combat.InProgress)
            {
                sw.Stop();
                return new SweepRow(
                    cardId, SweepOutcome.Crashed, Steps: 0, sw.Elapsed,
                    Detail: $"debug/start_combat returned InProgress=false (enemyCount={combat.EnemyCount})");
            }

            // 5. Find the card in hand. Loop a couple of end-turns so
            // Innate / draw-pile-only cards still surface.
            for (int turn = 0; turn < MaxTurnsToFindCard; turn++)
            {
                ct.ThrowIfCancellationRequested();
                var state = await transport.SendAsync<RunStateResult>("run/state");
                if (state.CombatState is not { IsInProgress: true } cs)
                {
                    sw.Stop();
                    return new SweepRow(
                        cardId, SweepOutcome.Unreachable, Steps: turn, sw.Elapsed,
                        Detail: "combat ended before card appeared in hand");
                }

                var handIdx = FindCardInHand(cs, cardId);
                if (handIdx >= 0)
                {
                    // Found it. Play it at target=0 — the wire ignores
                    // target for TargetType.None / AllEnemies / Self, so
                    // the value is only used by AnyEnemy cards (slimes
                    // are at indices 0/1, so 0 is always safe).
                    try
                    {
                        _ = await transport.SendAsync<RunPlayCardResult>(
                            "run/play_card",
                            new RunPlayCardParams(CardIndex: handIdx, TargetIndex: 0));
                        sw.Stop();
                        return new SweepRow(cardId, SweepOutcome.Played, Steps: turn, sw.Elapsed);
                    }
                    catch (System.Exception wx) when (IsWireError(wx))
                    {
                        sw.Stop();
                        var outcome = IsInternalError(wx) ? SweepOutcome.Crashed : SweepOutcome.Unplayable;
                        return new SweepRow(
                            cardId, outcome, Steps: turn, sw.Elapsed,
                            Detail: $"{wx.GetType().Name}: {Truncate(wx.Message)}");
                    }
                }

                // Not in hand — end turn to draw the next set. Wrapped
                // because the post-turn-end engine pump can itself crash.
                try
                {
                    _ = await transport.SendAsync<RunEndTurnResult>("run/end_turn");
                }
                catch (System.Exception wx) when (IsWireError(wx))
                {
                    sw.Stop();
                    var outcome = IsInternalError(wx) ? SweepOutcome.Crashed : SweepOutcome.Unplayable;
                    return new SweepRow(
                        cardId, outcome, Steps: turn, sw.Elapsed,
                        Detail: $"end_turn failed: {wx.GetType().Name}: {Truncate(wx.Message)}");
                }
            }

            sw.Stop();
            return new SweepRow(
                cardId, SweepOutcome.Unreachable, Steps: MaxTurnsToFindCard, sw.Elapsed,
                Detail: $"card never drawn in {MaxTurnsToFindCard} turns");
        }
        catch (System.OperationCanceledException) when (cts.Token.IsCancellationRequested && !outerCt.IsCancellationRequested)
        {
            sw.Stop();
            return new SweepRow(
                cardId, SweepOutcome.Timeout, Steps: 0, sw.Elapsed,
                Detail: $"per-card budget {PerCardBudget.TotalSeconds:0}s exceeded");
        }
        catch (System.Exception ex)
        {
            sw.Stop();
            return new SweepRow(
                cardId, SweepOutcome.Crashed, Steps: 0, sw.Elapsed,
                Detail: $"{ex.GetType().Name}: {Truncate(ex.Message)}");
        }
    }

    // CardId enum values surface as PascalCase via .ToString(); the wire
    // form is SCREAMING_SNAKE_CASE. Compare via the wire→pascal conversion
    // — the inverse lives in CoverageRecorder elsewhere in the codebase
    // but is private; reimplemented here to keep MechanicSweep dependency-
    // free.
    private static int FindCardInHand(CombatState cs, string wireCardId)
    {
        var pascal = ToPascalCase(wireCardId);
        for (int i = 0; i < cs.Hand.Count; i++)
        {
            if (string.Equals(cs.Hand[i].Id.ToString(), pascal, StringComparison.Ordinal))
                return i;
        }
        return -1;
    }

    // HostSubprocess.SendAsync throws XunitException on wire-error envelopes
    // (the host returned a structured error, e.g. "code=-32602 message=..").
    // We treat that as a wire-level outcome (Unplayable OR Crashed,
    // disambiguated by IsInternalError) rather than a host-process crash.
    // Other exception types — TaskCanceledException, IOException, raw
    // engine NREs that escape the host wrapper — surface as Crashed
    // through the outer try/catch.
    private static bool IsWireError(System.Exception ex) =>
        ex.GetType().Name.Equals("XunitException", System.StringComparison.Ordinal)
        || ex.Message.Contains("code=", System.StringComparison.Ordinal);

    // Within wire errors, distinguish "the engine deliberately refused
    // this play" (insufficient energy, wrong target type, X-cost with no
    // resource, the card's own CanPlay validator returned false — all
    // clean refusals) from "the engine wrapped an internal exception in
    // an error envelope" (the host's catch-all for unhandled engine
    // exceptions, surfaced via JSON-RPC -32603 with a Missing*Exception /
    // NullReferenceException / etc. in the message). The first is
    // honest "this mechanic isn't reachable in this fixture"; the
    // second is the mechanic itself being broken — which is exactly
    // what the sweep exists to surface as Crashed.
    private static bool IsInternalError(System.Exception ex)
    {
        var msg = ex.Message;
        // -32603 is JSON-RPC's "internal error" generic bucket. The host
        // wraps engine exceptions into this code, but ALSO emits it for
        // some clean refusals (notably curses/statuses returning false
        // from CanPlay). Carve out the known clean-refusal sub-cases so
        // they're not flagged as crashes.
        if (msg.Contains("CanPlay returned false", System.StringComparison.Ordinal))
            return false;

        return msg.Contains("MissingMethodException", System.StringComparison.Ordinal)
            || msg.Contains("MissingFieldException", System.StringComparison.Ordinal)
            || msg.Contains("NullReferenceException", System.StringComparison.Ordinal)
            || msg.Contains("ArgumentOutOfRangeException", System.StringComparison.Ordinal)
            || msg.Contains("ArgumentNullException", System.StringComparison.Ordinal)
            || msg.Contains("TargetInvocationException", System.StringComparison.Ordinal)
            || msg.Contains("IndexOutOfRangeException", System.StringComparison.Ordinal)
            || msg.Contains("StackOverflowException", System.StringComparison.Ordinal)
            // Generic "internal error:" prefix from any other engine
            // throw the host doesn't specifically recognize — covers
            // future exception types we haven't listed by name.
            || (msg.Contains("internal error:", System.StringComparison.Ordinal)
                && !msg.Contains("CanPlay returned false", System.StringComparison.Ordinal));
    }

    private static string Truncate(string s) =>
        s.Length > 240 ? string.Concat(s.AsSpan(0, 240), "...") : s;

    private static string ToPascalCase(string snake)
    {
        var sb = new System.Text.StringBuilder(snake.Length);
        var atWordStart = true;
        foreach (var ch in snake)
        {
            if (ch == '_') { atWordStart = true; continue; }
            sb.Append(atWordStart ? char.ToUpperInvariant(ch) : char.ToLowerInvariant(ch));
            atWordStart = false;
        }
        return sb.ToString();
    }
}
