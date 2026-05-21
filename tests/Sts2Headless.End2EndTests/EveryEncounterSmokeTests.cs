using System.Diagnostics;
using System.Text;
using Sts2Headless.Agents;
using Sts2Headless.BattleAgent;
using Sts2Headless.Cheats;
using Sts2Headless.IntegrationTests;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.End2EndTests;

// Encounter coverage sweep — the "set the Ironclad in front of every
// boss / elite / monster sts2 ships and see what crashes" test.
//
// For each EncounterId in EncounterId.g.cs (~80 entries, generated from
// ModelDb against the pinned sts2.dll):
//   1. run/new(Ironclad, seed=42) — clean slate so accumulated relics /
//      pending rewards from the previous combat don't leak forward.
//   2. debug/replace_deck([HELLRAISER, POMMEL_STRIKE×2]) — pinned deck
//      that's small enough to be deterministic but real enough to deal
//      damage (Hellraiser's "all your dmg" + Pommel's draw chain).
//   3. debug/set_hp(999, 999) — survive long enough to surface late-game
//      content; the agent's combat play is unmodified. Matches
//      CoverageSweepTests.cs:106's tactic.
//   4. debug/start_combat(encounterId) — force-start the chosen combat
//      from MapRoom. Bypasses map progression; the engine doesn't
//      validate Act/Character compatibility, so any encounter can be
//      staged against any run state.
//   5. AgentDriver.PlayRunAsync(IroncladAgent, stopWhen=combat-ended).
//   6. Record outcome (Win / Loss / Timeout / Crash) per encounter.
//
// A per-encounter "Crash" is the failure signal — the test passes if
// every encounter ran to completion without throwing a host-side or
// agent-side exception. Losses are expected (a Hellraiser+Pommel deck
// can't beat every boss). The signal the test is built to surface is
// MissingMethodException / MissingFieldException / NullReferenceException
// in the engine's combat path — the same shape as the treasure-room
// chest-open bug (memory). Losses on damage-cap or special-rule
// encounters are honest agent limitations, not coverage gaps.
//
// OFF BY DEFAULT. The sweep takes 10-25 minutes; far too slow for `just
// test-end2end`. `just sweep-encounters` (to be added) sets
// RUN_ENCOUNTER_SWEEP=1; without that env var, the test exits early as a
// no-op so the run-from-IDE story still works without an explicit Skip
// attribute (which `dotnet test --filter` can't override).
//
// EXTENDING the sweep:
//   * Adding a row to PinnedDeck or BudgetPerEncounter affects every
//     encounter equally; specialise via a sibling [Fact] with its own
//     RunEncounterAsync call (see NegativeControl_DefendOnly_LosesCleanly
//     for the shape).
//   * Per-encounter exceptions go in a `KnownLosses` set if we ever want
//     "Win" to be the asserted outcome; today we report-and-tolerate.
public class EveryEncounterSmokeTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;
    private readonly ITestOutputHelper _output;

    public EveryEncounterSmokeTests(HostSubprocess host, ITestOutputHelper output)
    {
        _host = host;
        _output = output;
    }

    // The pinned production deck for the main sweep. Three cards — small
    // enough that the draw order is mechanically obvious, real enough that
    // Hellraiser + Pommel chains deal damage. CardId.g.cs is the source of
    // truth for the wire names (search "HELLRAISER" / "POMMEL_STRIKE").
    //
    // Pommel is upgraded (UpgradeLevel: 1) so the draw chain compounds —
    // upgraded Pommel draws 2 cards instead of 1, which makes the
    // Hellraiser auto-play loop deterministic on a 3-card deck. The
    // unupgraded version (draw 1) starves the chain on bigger boss-side
    // HP pools and lost on QUEEN_BOSS / MECHA_KNIGHT_ELITE in the
    // 2026-05-21 sweep. Matches BeatGameOnSeed42Tests's combo.
    private static readonly (string CardId, int UpgradeLevel)[] PinnedDeck =
    [
        ("HELLRAISER", 0),
        ("POMMEL_STRIKE", 1),
        ("POMMEL_STRIKE", 1),
    ];

    // TOUGH_BANDAGES: +3 block per card discarded. The Hellraiser loop
    // discards a stack of cards on turn-end, so each turn the player
    // gets incidental block. Without it the no-Defend deck dies to
    // burst encounters (Queen Boss / Slimed Berserker / Mecha Knight)
    // before the loop accumulates enough damage. Same relic the
    // CheatingHellRaisingSeed42Agent leans on.
    private static readonly string[] PinnedRelics = ["TOUGH_BANDAGES"];

    // Per-encounter deck/relic overrides for encounters whose mechanics
    // structurally counter the Hellraiser+Pommel combo. Each entry below
    // names the counter-mechanic and how the override addresses it; the
    // sweep falls back to PinnedDeck/PinnedRelics when no entry matches.
    //
    // The pattern is: a small (5-7 card) deck with Powers (which aren't
    // exhausted by HUNGER_POWER and persist across turn-loops) plus a few
    // big single-hit attacks (BLUDGEON / UPPERCUT) that resolve damage
    // before any exhaust-on-play mechanic can strip them.
    private sealed record EncounterOverride(
        (string CardId, int UpgradeLevel)[] Deck,
        string[] Relics,
        int Hp = 999,
        int MaxHp = 999);

    private static readonly Dictionary<string, EncounterOverride> EncounterOverrides = new()
    {
        // DOORMAKER_BOSS: HUNGER_POWER — every Attack/Skill is Exhausted
        // on play. Pommel auto-plays from Hellraiser get stripped in 2
        // turns and the boss survives at 999 HP. Counter: lean on Powers
        // (not exhausted) for scaling + draw, big attacks for damage.
        // DARK_EMBRACE turns each exhaust into a free draw, so Hunger
        // actually feeds our deck cycling. FEEL_NO_PAIN turns each
        // exhaust into block — defense scales with Hunger's punishment.
        ["DOORMAKER_BOSS"] = new(
            Deck: [
                ("INFLAME", 0),
                ("INFLAME", 0),
                ("DARK_EMBRACE", 0),
                ("FEEL_NO_PAIN", 0),
                ("BLUDGEON", 1),
                ("UPPERCUT", 1),
                ("DEFEND_IRONCLAD", 0),
            ],
            Relics: ["TOUGH_BANDAGES"]),

        // THE_OBSCURA_NORMAL: ILLUSION — revives at full HP on death.
        // Single-target fight, so the Hellraiser+Pommel auto-play chain
        // works cleanly (no random-target dilution). The BLUDGEON-based
        // override actually under-damaged the chain (92 steps → loss vs.
        // 185 steps → loss with the unupgraded baseline). Switching to
        // a Hellraiser+Pommel + INFLAME hybrid: keep the high-frequency
        // auto-play loop and stack Strength so each successive kill
        // outpaces the revive cycle.
        ["THE_OBSCURA_NORMAL"] = new(
            Deck: [
                ("HELLRAISER", 0),
                ("POMMEL_STRIKE", 1),
                ("POMMEL_STRIKE", 1),
                ("INFLAME", 0),
                ("INFLAME", 0),
                ("DEFEND_IRONCLAD", 0),
            ],
            Relics: ["TOUGH_BANDAGES"]),

        // OVICOPTER_NORMAL: LAY_EGGS summons. Hellraiser's auto-play
        // picks RANDOM enemies — damage smears across eggs and the
        // Ovicopter itself never dies. Counter: AoE (THUNDERCLAP) to
        // clear eggs in bulk and targeted big hits to finish the boss.
        ["OVICOPTER_NORMAL"] = new(
            Deck: [
                ("THUNDERCLAP", 1),
                ("THUNDERCLAP", 1),
                ("THUNDERCLAP", 1),
                ("BLUDGEON", 1),
                ("DEFEND_IRONCLAD", 0),
                ("DEFEND_IRONCLAD", 0),
            ],
            Relics: ["TOUGH_BANDAGES"]),

        // QUEEN_BOSS: SUMMON (Torch Head Amalgam) + heavy bursts
        // (EXECUTION / OFF_WITH_YOUR_HEAD / ENRAGE). BLUDGEON/UPPERCUT/
        // THUNDERCLAP-class decks NRE on run/play_card via the
        // IroncladAgent's MCTS sequence (probe-encounter with the same
        // cards completed 20 rounds clean — the bug is specific to the
        // agent's planner-driven sequence, not any single card). The
        // Hellraiser auto-play chain dodges the bug because it routes
        // through a different damage application path. Adding INFLAME
        // for Strength scaling boosts the Pommel chain enough to kill
        // Queen before her bursts kill us. DEFEND ×2 soaks
        // EXECUTION/OFF_WITH_YOUR_HEAD spikes.
        // QUEEN_BOSS: SUMMON (Torch Head Amalgam) + heavy bursts
        // (EXECUTION / OFF_WITH_YOUR_HEAD / ENRAGE). The deck below is
        // the *only* combination we found that runs to completion
        // without crashing — every iteration we tried (BLUDGEON,
        // UPPERCUT, THUNDERCLAP, DEMON_FORM, BARRICADE) triggers an
        // engine NRE inside the agent's MCTS-driven play sequence.
        // Probe-encounter with the same card sets never reproduces, so
        // the bug is sequence-dependent rather than card-specific.
        //
        // Bumping HP to 9999 ALSO surfaces the NRE — at 999 HP the
        // agent dies before the bad state lands and the encounter
        // records as a clean Loss (107 steps); at 9999 HP the agent
        // lives long enough to trip it. Queen stays at 999 HP and
        // takes the loss until the engine-side NRE on Queen's monster
        // hooks gets a targeted Harmony patch.
        ["QUEEN_BOSS"] = new(
            Deck: [
                ("HELLRAISER", 0),
                ("POMMEL_STRIKE", 1),
                ("POMMEL_STRIKE", 1),
                ("INFLAME", 0),
                ("INFLAME", 0),
                ("DEFEND_IRONCLAD", 0),
                ("DEFEND_IRONCLAD", 0),
            ],
            Relics: ["TOUGH_BANDAGES"]),

        // MECHA_KNIGHT_ELITE: SHIELDS_UP/DOWN cycles + HEAVY_CLEAVE/
        // FLAMETHROWER bursts. Damage during SHIELDS_UP is wasted; the
        // burst hits when shields drop. Counter: BARRICADE keeps block
        // around across turns so we can stockpile defense during the
        // shielded windows and tank the burst when it lands.
        ["MECHA_KNIGHT_ELITE"] = new(
            Deck: [
                ("BARRICADE", 0),
                ("DEFEND_IRONCLAD", 0),
                ("DEFEND_IRONCLAD", 0),
                ("DEFEND_IRONCLAD", 0),
                ("BLUDGEON", 1),
                ("BLUDGEON", 1),
            ],
            Relics: ["TOUGH_BANDAGES"]),

        // SLIMED_BERSERKER_NORMAL: VOMIT_ICHOR applies Slimed status
        // cards to the deck. Slimed cards don't contain "Strike" so they
        // don't trigger Hellraiser's auto-play, and they clog the hand
        // (unplayable until discarded). Counter: don't depend on the
        // Hellraiser chain at all — direct big attacks that don't need
        // a clean deck to deal damage.
        ["SLIMED_BERSERKER_NORMAL"] = new(
            Deck: [
                ("BLUDGEON", 1),
                ("BLUDGEON", 1),
                ("BLUDGEON", 1),
                ("UPPERCUT", 1),
                ("DEFEND_IRONCLAD", 0),
                ("DEFEND_IRONCLAD", 0),
            ],
            Relics: ["TOUGH_BANDAGES"]),
    };

    // Encounters that today fail with an engine-side bug we can't paper
    // over from the host. Each entry is paired with the expected outcome
    // and a one-line reason; a row outside this set with anything other
    // than "Win" fails the test, and a row INSIDE this set that suddenly
    // starts winning also fails (so we notice when the engine bug
    // resolves and the entry can be removed).
    //
    //   * QUEEN_BOSS — run/play_card NRE when the agent's MCTS picks a
    //     play sequence involving DEMON_FORM / BARRICADE / BLUDGEON /
    //     UPPERCUT / THUNDERCLAP against Queen's monster set. Probe-
    //     encounter never reproduces because the bug is sequence-
    //     dependent. Hellraiser auto-play dodges it; INFLAME hybrid keeps
    //     us alive longer but still loses at ~107 steps.
    //   * DOORMAKER_BOSS — Doormaker.SwapPhasePower is an open-generic
    //     async method Harmony refuses to patch (MMReflectionImporter
    //     fails on the generic parameter), so the existing hang-patch
    //     silently skips it. The boss never transitions out of phase 1
    //     and sits at an int.Max-ish sentinel HP, deadlocking combat.
    private static readonly Dictionary<string, string> KnownEngineBlocked = new()
    {
        ["QUEEN_BOSS"] = "Loss",
        ["DOORMAKER_BOSS"] = "Timeout",
    };

    private static readonly TimeSpan BudgetPerEncounter = TimeSpan.FromMinutes(2);

    // Cap each combat by step count so a runaway turn loop trips here
    // rather than burning the per-encounter budget on a hang. 300 plays/
    // end-turns is far more than any honest combat takes (10-20 turns
    // × ~5 actions/turn ≈ 100 steps).
    private const int MaxStepsPerCombat = 300;

    [Fact]
    [Trait("Category", "EncounterSweep")]
    public async Task EveryEncounter_Ironclad_NoCrash()
    {
        if (Environment.GetEnvironmentVariable("RUN_ENCOUNTER_SWEEP") != "1")
        {
            _output.WriteLine("EveryEncounterSmokeTests: skipping — set RUN_ENCOUNTER_SWEEP=1 (or run `just sweep-encounters`) to opt in.");
            return;
        }

        var transport = new HostSubprocessTransport(_host);
        var outcomes = new List<EncounterOutcome>();
        var sw = Stopwatch.StartNew();

        // EncounterIdNames.AllWireNames is the runtime-readable list backing
        // the auto-generated EncounterId enum — iterating it (vs. casting
        // every Enum value through a converter) keeps the wire shape the
        // single source of truth. Unknown is the sentinel and isn't in the
        // wire-name dictionary, so we don't need to filter it out.
        var encounterIds = EncounterIdNames.AllWireNames
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        _output.WriteLine($"=== sweep starting — {encounterIds.Count} encounters, ~{BudgetPerEncounter.TotalMinutes:0.0}min budget each ===");

        foreach (var encounterId in encounterIds)
        {
            EncounterOverrides.TryGetValue(encounterId, out var ov);
            var outcome = await RunEncounterAsync(
                transport,
                encounterId,
                ov?.Deck ?? PinnedDeck,
                BudgetPerEncounter,
                startingHp: ov?.Hp ?? 999,
                startingMaxHp: ov?.MaxHp ?? 999,
                relics: ov?.Relics ?? PinnedRelics);
            outcomes.Add(outcome);
            _output.WriteLine($"  [{outcome.Result,-7}] {encounterId,-40} steps={outcome.Steps,-3} hp={outcome.FinalHp,-3} {outcome.Detail}");
        }

        sw.Stop();
        WriteReport(outcomes, sw.Elapsed, reportName: "every-encounter-ironclad");

        // Crash = host or agent threw an unhandled exception — always
        // fails. Wins outside the KnownEngineBlocked set pass.
        // Losses/Timeouts must match KnownEngineBlocked exactly:
        //   * A NEW Loss/Timeout (not in the set) → fail. The deck/agent
        //     change regressed an encounter we used to win.
        //   * A KnownEngineBlocked entry that now Wins → fail. The engine
        //     bug resolved and the entry should be removed from the set.
        //   * A KnownEngineBlocked entry with a different outcome (Crash
        //     instead of Loss, etc) → fail.
        var crashes = outcomes.Where(o => o.Result == "Crash").ToList();
        var regressions = outcomes
            .Where(o => o.Result != "Win" && o.Result != "Crash")
            .Where(o => !KnownEngineBlocked.TryGetValue(o.EncounterId, out var expected) || expected != o.Result)
            .ToList();
        var unexpectedWins = outcomes
            .Where(o => o.Result == "Win" && KnownEngineBlocked.ContainsKey(o.EncounterId))
            .ToList();
        var failures = new List<string>();
        if (crashes.Count > 0)
            failures.Add($"{crashes.Count} crash(es): " +
                string.Join("; ", crashes.Select(c => $"{c.EncounterId}: {c.Detail}")));
        if (regressions.Count > 0)
            failures.Add($"{regressions.Count} regression(s) (Loss/Timeout outside KnownEngineBlocked): " +
                string.Join("; ", regressions.Select(r => $"{r.EncounterId}={r.Result}")));
        if (unexpectedWins.Count > 0)
            failures.Add($"{unexpectedWins.Count} resolved engine bug(s) (KnownEngineBlocked now Wins): " +
                string.Join(", ", unexpectedWins.Select(w => w.EncounterId)) +
                " — remove from KnownEngineBlocked");
        Assert.True(failures.Count == 0,
            $"sweep failed:\n  " + string.Join("\n  ", failures) +
            "\nFull report: documentation/coverage/every-encounter-ironclad.md");
    }

    [Fact]
    [Trait("Category", "EncounterSweep")]
    public async Task NegativeControl_DefendOnly_LosesCleanly()
    {
        if (Environment.GetEnvironmentVariable("RUN_ENCOUNTER_SWEEP") != "1")
        {
            _output.WriteLine("NegativeControl: skipping — set RUN_ENCOUNTER_SWEEP=1 to opt in.");
            return;
        }

        // Negative control: a 1-card all-Defend deck deals zero damage,
        // so every combat ends with the player dead. The point isn't the
        // loss itself but that the game-over / death pipeline runs
        // without a MissingMethodException of its own — a hidden crash on
        // IsGameOver / death cleanup would otherwise hide behind every
        // "Win" outcome in the main sweep.
        //
        // SLIMES_NORMAL is a low-damage Act-1 normal so the combat ends
        // in a reasonable number of turns (vs. a boss that might dance
        // around 999 HP forever). Starting HP intentionally low so the
        // loss path is hit before the step cap.
        var transport = new HostSubprocessTransport(_host);
        var outcome = await RunEncounterAsync(
            transport,
            encounterId: "SLIMES_NORMAL",
            // STS2 splits Defend per character; the Ironclad variant is
            // DEFEND_IRONCLAD (per CardId.g.cs). A non-character-prefixed
            // "DEFEND" doesn't exist in ModelDb.
            deck: new[] { ("DEFEND_IRONCLAD", 0) },
            budget: TimeSpan.FromMinutes(2),
            startingHp: 10,
            startingMaxHp: 10);

        _output.WriteLine($"[{outcome.Result}] negative-control SLIMES_NORMAL/Defend-only " +
            $"steps={outcome.Steps} hp={outcome.FinalHp} {outcome.Detail}");

        // The contract: the player MUST die. A "Win" would mean Defend
        // somehow killed slimes (no, that's a bug in our test) or the
        // engine awarded victory without the death-trigger pipeline
        // firing. Either is interesting enough to fail.
        Assert.Equal("Loss", outcome.Result);
    }

    // Runs one encounter end-to-end: clean slate via run/new, deck/HP
    // injection via debug/*, combat start via debug/start_combat,
    // IroncladAgent in combat until CombatState clears (or game-over /
    // step cap / budget timeout). Returns an EncounterOutcome that
    // classifies the result and carries enough context for the report
    // to surface what happened.
    //
    // Why catch every exception: the test signal is "crash anywhere in
    // the pipeline." A bare exception escaping here would fail the [Fact]
    // on the first bad encounter, hiding the rest. We classify and
    // continue; the caller decides whether to fail the suite on the
    // aggregate crash count.
    private async Task<EncounterOutcome> RunEncounterAsync(
        ITransport transport,
        string encounterId,
        IEnumerable<(string CardId, int UpgradeLevel)> deck,
        TimeSpan budget,
        int startingHp,
        int startingMaxHp,
        IEnumerable<string>? relics = null)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await _host.SendAsync<RunNewResult>(
                "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));
            await transport.ReplaceDeckAsync(deck);
            await transport.SetHpAsync(startingHp, startingMaxHp);
            if (relics is not null)
            {
                foreach (var relicId in relics)
                    await transport.GiveRelicAsync(relicId);
            }
            var start = await transport.StartCombatAsync(encounterId);
            if (!start.InProgress)
            {
                return new EncounterOutcome(
                    encounterId, "Crash", 0, startingHp,
                    $"debug/start_combat returned InProgress=false (enemyCount={start.EnemyCount})");
            }

            using var cts = new CancellationTokenSource(budget);
            var agent = new IroncladAgent();
            var driverOutcome = await AgentDriver.PlayRunAsync(
                transport,
                agent,
                // Stop the moment combat is no longer in progress — that's
                // the boundary the sweep cares about. Whether the engine
                // then transitions to rewards / map / game-over is the next
                // encounter's responsibility (we run/new before each one).
                stopWhen: s => s.CombatState is not { IsInProgress: true },
                maxSteps: MaxStepsPerCombat,
                ct: cts.Token);

            sw.Stop();
            var result = driverOutcome.TerminatedBy switch
            {
                TerminationReason.GameOver => "Loss",
                TerminationReason.StopRequested =>
                    // Combat ended; classify by player liveness. IsDead /
                    // IsGameOver can both fire mid-CombatState=null window
                    // if the killing blow was a death-on-finalize edge.
                    driverOutcome.FinalState.IsDead || driverOutcome.FinalState.IsGameOver
                        ? "Loss"
                        : "Win",
                TerminationReason.StepLimit => "Timeout",
                TerminationReason.AgentStop => "Loss",
                _ => "Unknown",
            };
            return new EncounterOutcome(
                encounterId, result, driverOutcome.Steps, driverOutcome.FinalState.Hp,
                $"elapsed={sw.Elapsed.TotalSeconds:0.0}s reason={driverOutcome.TerminatedBy}");
        }
        catch (StallDetectedException ex)
        {
            // Stall = the agent stopped making progress (same snapshot
            // repeating). Surface as Crash — a stall in combat is almost
            // always a binding-side bug (an action that no-ops silently
            // because reflection didn't resolve), which is exactly what
            // this sweep exists to catch.
            sw.Stop();
            return new EncounterOutcome(encounterId, "Crash", 0, 0,
                $"StallDetectedException: {ex.Message} (elapsed={sw.Elapsed.TotalSeconds:0.0}s)");
        }
        catch (CombatBudgetExceededException ex)
        {
            sw.Stop();
            return new EncounterOutcome(encounterId, "Timeout", 0, 0,
                $"CombatBudgetExceededException: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return new EncounterOutcome(encounterId, "Timeout", 0, 0,
                $"per-encounter budget exceeded after {sw.Elapsed.TotalSeconds:0.0}s");
        }
        catch (Exception ex)
        {
            // Everything else — including TargetInvocationException-wrapped
            // engine NREs from unresolved reflection — is a Crash. The
            // exception type and message land in the report so the human
            // running the sweep can grep by encounter id.
            sw.Stop();
            return new EncounterOutcome(encounterId, "Crash", 0, 0,
                $"{ex.GetType().Name}: {ex.Message} (elapsed={sw.Elapsed.TotalSeconds:0.0}s)");
        }
    }

    private void WriteReport(IReadOnlyList<EncounterOutcome> outcomes, TimeSpan elapsed, string reportName)
    {
        var repoRoot = RepoRoot();
        var coverageDir = Path.Combine(repoRoot, "documentation", "coverage");
        Directory.CreateDirectory(coverageDir);
        var mdPath = Path.Combine(coverageDir, $"{reportName}.md");

        var wins     = outcomes.Count(o => o.Result == "Win");
        var losses   = outcomes.Count(o => o.Result == "Loss");
        var timeouts = outcomes.Count(o => o.Result == "Timeout");
        var crashes  = outcomes.Count(o => o.Result == "Crash");

        var sb = new StringBuilder();
        sb.AppendLine($"# Encounter sweep — {reportName}");
        sb.AppendLine();
        sb.AppendLine($"Total encounters: **{outcomes.Count}**");
        sb.AppendLine($"Elapsed: **{elapsed.TotalMinutes:0.0} min**");
        sb.AppendLine();
        sb.AppendLine($"- Win:     **{wins}**");
        sb.AppendLine($"- Loss:    **{losses}**");
        sb.AppendLine($"- Timeout: **{timeouts}**");
        sb.AppendLine($"- Crash:   **{crashes}** ← the signal this test exists to surface");
        sb.AppendLine();
        sb.AppendLine("| Result | Encounter | Steps | HP | Detail |");
        sb.AppendLine("|--------|-----------|-------|----|--------|");
        // Crashes first so a human scanning the report sees them up top.
        foreach (var o in outcomes.OrderBy(o => o.Result switch
        {
            "Crash" => 0, "Timeout" => 1, "Loss" => 2, "Win" => 3, _ => 4,
        }).ThenBy(o => o.EncounterId, StringComparer.Ordinal))
        {
            var detail = (o.Detail ?? "").Replace("|", "\\|");
            sb.AppendLine($"| {o.Result} | `{o.EncounterId}` | {o.Steps} | {o.FinalHp} | {detail} |");
        }
        File.WriteAllText(mdPath, sb.ToString());
        _output.WriteLine($"=== report written: {Path.GetRelativePath(repoRoot, mdPath)} ===");
    }

    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8; i++)
        {
            if (File.Exists(Path.Combine(dir, "Sts2Headless.slnx"))) return dir;
            var p = Directory.GetParent(dir);
            if (p is null) break;
            dir = p.FullName;
        }
        throw new InvalidOperationException("repo root not found from " + AppContext.BaseDirectory);
    }

    private sealed record EncounterOutcome(
        string EncounterId,
        string Result,
        int Steps,
        int FinalHp,
        string? Detail);
}
