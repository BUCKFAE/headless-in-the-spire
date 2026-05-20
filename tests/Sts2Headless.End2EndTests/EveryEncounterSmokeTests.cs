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
    private static readonly (string CardId, int UpgradeLevel)[] PinnedDeck =
    [
        ("HELLRAISER", 0),
        ("POMMEL_STRIKE", 0),
        ("POMMEL_STRIKE", 0),
    ];

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
            var outcome = await RunEncounterAsync(
                transport,
                encounterId,
                PinnedDeck,
                BudgetPerEncounter,
                startingHp: 999,
                startingMaxHp: 999);
            outcomes.Add(outcome);
            _output.WriteLine($"  [{outcome.Result,-7}] {encounterId,-40} steps={outcome.Steps,-3} hp={outcome.FinalHp,-3} {outcome.Detail}");
        }

        sw.Stop();
        WriteReport(outcomes, sw.Elapsed, reportName: "every-encounter-ironclad");

        // Crash = host or agent threw an unhandled exception. That's the
        // signal this test exists to surface; everything else (loss to a
        // burst-damage boss, agent runs out of cards, hits step cap) is
        // an honest agent/deck limitation and reported but not failed.
        var crashes = outcomes.Where(o => o.Result == "Crash").ToList();
        Assert.True(crashes.Count == 0,
            $"{crashes.Count} encounter(s) crashed — see report. Crashes: " +
            string.Join("; ", crashes.Select(c => $"{c.EncounterId}: {c.Detail}")));
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
        int startingMaxHp)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await _host.SendAsync<RunNewResult>(
                "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));
            await transport.ReplaceDeckAsync(deck);
            await transport.SetHpAsync(startingHp, startingMaxHp);
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
