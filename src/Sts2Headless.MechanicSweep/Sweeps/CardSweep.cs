using System.Diagnostics;
using Sts2Headless.Agents.Contracts;
using Sts2Headless.Cheats;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.MechanicSweep.Sweeps;

// Per-CardId smoke sweep. For each id in CardIdNames.AllWireNames:
//
//   1. run/new(<owning character>, seed=42)      — fresh state. Character
//                                                  picked via CardOriginPools
//                                                  .OwningCharacter so that
//                                                  Regent / Defect / etc.
//                                                  cards exercise with their
//                                                  native resource system
//                                                  (Stars vs Energy etc.).
//                                                  Falls back to Ironclad
//                                                  for shared / colorless /
//                                                  curse / status cards.
//   2. debug/set_hp(999, 999)                    — survive incidental damage
//   3. debug/replace_deck(test + 4 starter)      — single deck containing the
//                                                  test card plus Strike×2,
//                                                  Defend×2 filler so the
//                                                  opening hand has neighbors
//                                                  AND post-play piles are
//                                                  non-empty for "Draw 1 /
//                                                  Look at top X / random
//                                                  from your deck" cards
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
//     replied with a structured error; no crash. Detail prefix carries
//     the card's pool category so a reader can tell at-a-glance whether
//     this is an expected refusal (curse / status — always Unplayable by
//     design) or a fixture-staging gap to investigate.
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
    // opening hand of a single-card deck. Loop a few end-turns so the
    // second/third draw can surface them AND give one or two retries
    // on cards whose CanPlay flips true once a passive condition holds
    // (resource accumulation, stance acquisition, etc.). 5 turns is
    // the saturation point in practice — bumping higher (tested 8) did
    // not unblock any additional cards; the remaining Unplayables have
    // hard conditional CanPlay that the smoke fixture doesn't stage
    // (cataloged in SweepKnownIssues.CardExpectedRefusals).
    public const int MaxTurnsToFindCard = 5;

    // SLIMES_NORMAL is the canonical "smallest threat in the game" — two
    // slimes, low damage, no SUMMON / EXHAUST / DOOM mechanics that would
    // mess with the card under test. Same encounter the negative-control
    // baseline uses elsewhere.
    public const string BenignEncounter = "SLIMES_NORMAL";

    // Per-character filler. The engine's replace_deck rejects non-native
    // starter cards (an Ironclad Strike won't drop into a Regent deck —
    // the Regent player can't legally have Ironclad Strikes), so each
    // character gets its own Strike/Defend pair. The four-card filler
    // gives the engine something to pull from for "Draw 1" / "Look at
    // top X" / "Pick from discard" cards; without filler, a single-card
    // deck empties as soon as the test card lands in hand and any
    // subsequent draw faces empty piles. Same total deck size (5 = 4
    // filler + 1 test) for every character so the engine's 5-card
    // opening-hand draw lands the test card on turn 1.
    private static readonly (string CardId, int UpgradeLevel)[] IroncladFiller =
    [
        ("STRIKE_IRONCLAD", 0), ("STRIKE_IRONCLAD", 0),
        ("DEFEND_IRONCLAD", 0), ("DEFEND_IRONCLAD", 0),
    ];
    private static readonly (string CardId, int UpgradeLevel)[] SilentFiller =
    [
        ("STRIKE_SILENT", 0), ("STRIKE_SILENT", 0),
        ("DEFEND_SILENT", 0), ("DEFEND_SILENT", 0),
    ];
    private static readonly (string CardId, int UpgradeLevel)[] DefectFiller =
    [
        ("STRIKE_DEFECT", 0), ("STRIKE_DEFECT", 0),
        ("DEFEND_DEFECT", 0), ("DEFEND_DEFECT", 0),
    ];
    private static readonly (string CardId, int UpgradeLevel)[] RegentFiller =
    [
        ("STRIKE_REGENT", 0), ("STRIKE_REGENT", 0),
        ("DEFEND_REGENT", 0), ("DEFEND_REGENT", 0),
    ];
    private static readonly (string CardId, int UpgradeLevel)[] NecrobinderFiller =
    [
        ("STRIKE_NECROBINDER", 0), ("STRIKE_NECROBINDER", 0),
        ("DEFEND_NECROBINDER", 0), ("DEFEND_NECROBINDER", 0),
    ];

    private static (string CardId, int UpgradeLevel)[] FillerDeckFor(Character c) => c switch
    {
        Character.Ironclad    => IroncladFiller,
        Character.Silent      => SilentFiller,
        Character.Defect      => DefectFiller,
        Character.Regent      => RegentFiller,
        Character.Necrobinder => NecrobinderFiller,
        _ => IroncladFiller,
    };

    // Annotate a Detail with the card's pool category so readers can
    // distinguish a curse / status being Unplayable (expected by engine
    // design — these are never playable in any combat) from a fixture-
    // staging gap (a real card that needs richer setup to exercise).
    // Empty for character-class cards: the pool prefix would just clutter
    // the row when there's no design-time refusal to flag.
    private static string AnnotatePool(CardOriginPool pool, string detail) => pool switch
    {
        CardOriginPool.Curse  => $"pool=curse (always Unplayable by design); {detail}",
        CardOriginPool.Status => $"pool=status (always Unplayable by design); {detail}",
        CardOriginPool.Quest  => $"pool=quest (event-spawned, not a regular play target); {detail}",
        CardOriginPool.Token  => $"pool=token (engine-spawned mid-combat, not deck-playable); {detail}",
        CardOriginPool.Event  => $"pool=event (event-tied; CanPlay typically requires the event context); {detail}",
        _ => detail,
    };

    public async System.Threading.Tasks.Task<SweepReport> RunAsync(
        ITransport transport,
        System.Collections.Generic.IReadOnlyList<string>? sampleIds = null,
        string gameVersion = "unknown",
        System.Action<SweepRow>? onRow = null,
        System.Threading.CancellationToken ct = default)
    {
        var universe = SweepInternals.FilterReachable(CardIdNames.AllWireNames);
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

        // Pool category drives both the starting Character (so Regent
        // cards run with Regent's Stars resource, Defect cards with their
        // own, etc.) AND the Detail prefix on Unplayable outcomes so
        // expected-by-design refusals (curses, statuses) read clearly.
        var pool = CardOriginPools.OfCard(cardId);
        var character = CardOriginPools.OwningCharacter(cardId) ?? Character.Ironclad;

        try
        {
            // 1. Fresh run — cleans accumulated relics, pending rewards,
            // half-transitioned combat from the previous card.
            await transport.SendAsync<RunNewResult>(
                "run/new",
                new RunNewParams(Character: character, Seed: 42uL));

            // 2-4. Setup: HP cheat, test card + starter filler, force
            // benign combat. The test card goes first in the deck so the
            // engine's opening-hand draw (top 5) includes it. Filler
            // is character-appropriate (FillerDeckFor): non-Ironclad
            // characters use their own STRIKE/DEFEND variants because
            // the Ironclad starters refuse to enter another character's
            // deck under replace_deck.
            await transport.SetHpAsync(999, 999);
            var filler = FillerDeckFor(character);
            var deck = new System.Collections.Generic.List<(string, int)>(filler.Length + 1) { (cardId, 0) };
            deck.AddRange(filler);
            await transport.ReplaceDeckAsync(deck);
            var combat = await transport.StartCombatAsync(BenignEncounter);
            if (!combat.InProgress)
            {
                sw.Stop();
                return new SweepRow(
                    cardId, SweepOutcome.Crashed, Steps: 0, sw.Elapsed,
                    Detail: $"debug/start_combat returned InProgress=false (enemyCount={combat.EnemyCount})");
            }

            // 5. Find the card in hand and try to play it. Loop a few
            // end-turns so (a) Innate / draw-pile-only cards still
            // surface AND (b) resource-cost cards (Regent's Stars build
            // over turns) accumulate the cost before we conclude they're
            // unplayable. On a Played outcome we return immediately; on
            // an Unplayable we retry on the next turn (resource may have
            // accrued); the LAST attempt's classification is what
            // surfaces in the row.
            string? lastUnplayableDetail = null;
            int lastUnplayableTurn = 0;
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
                    catch (System.Exception wx) when (SweepInternals.IsWireError(wx))
                    {
                        var c = SweepInternals.ClassifyWireError("card", cardId, wx);
                        // Crashed / KnownUnsafe → no point waiting for
                        // resources; the engine is broken on this id.
                        // Unplayable → maybe a resource-cost issue that
                        // will resolve on a later turn (Regent Stars,
                        // ICONIC orange-accumulator cards, etc.). Record
                        // the detail and fall through to end_turn so the
                        // next iteration re-checks.
                        if (c.Outcome != SweepOutcome.Unplayable)
                        {
                            sw.Stop();
                            return new SweepRow(cardId, c.Outcome, Steps: turn, sw.Elapsed,
                                Detail: AnnotatePool(pool, c.Detail));
                        }
                        lastUnplayableDetail = c.Detail;
                        lastUnplayableTurn = turn;
                    }
                }

                // Not in hand — end turn to draw the next set. Wrapped
                // because the post-turn-end engine pump can itself crash.
                try
                {
                    _ = await transport.SendAsync<RunEndTurnResult>("run/end_turn");
                }
                catch (System.Exception wx) when (SweepInternals.IsWireError(wx))
                {
                    sw.Stop();
                    var c = SweepInternals.ClassifyWireError("card", cardId, wx);
                    return new SweepRow(
                        cardId, c.Outcome, Steps: turn, sw.Elapsed,
                        Detail: AnnotatePool(pool, $"end_turn failed: {c.Detail}"));
                }
            }

            sw.Stop();
            // If we saw the card in hand but Unplayable on every turn we
            // tried, surface that — it's a more useful row than
            // Unreachable. Otherwise the card never drew (uncommon with
            // a 5-card deck and 5-turn budget; usually means the card
            // was self-Exhausted or transformed during another effect).
            if (lastUnplayableDetail is not null)
            {
                return new SweepRow(
                    cardId, SweepOutcome.Unplayable,
                    Steps: lastUnplayableTurn, sw.Elapsed,
                    Detail: AnnotatePool(pool, lastUnplayableDetail));
            }
            return new SweepRow(
                cardId, SweepOutcome.Unreachable, Steps: MaxTurnsToFindCard, sw.Elapsed,
                Detail: AnnotatePool(pool, $"card never drawn in {MaxTurnsToFindCard} turns"));
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
                Detail: $"{ex.GetType().Name}: {SweepInternals.Truncate(ex.Message)}");
        }
    }

    // CardId enum values surface as PascalCase via .ToString(); the wire
    // form is SCREAMING_SNAKE_CASE. SweepInternals.ToPascalCase handles
    // the conversion so the sweep's hand-walk compares against the right
    // form.
    private static int FindCardInHand(CombatState cs, string wireCardId)
    {
        var pascal = SweepInternals.ToPascalCase(wireCardId);
        for (int i = 0; i < cs.Hand.Count; i++)
        {
            if (string.Equals(cs.Hand[i].Id.ToString(), pascal, StringComparison.Ordinal))
                return i;
        }
        return -1;
    }
}
