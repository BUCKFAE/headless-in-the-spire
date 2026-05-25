using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Agents.Driving;

// Tripwire for infinite-but-progressing combats — the case StallDetector
// misses on purpose.
//
// StallDetector fires when K snapshots have an IDENTICAL fingerprint
// (round, energy, hp, enemy hp+powers, …). That catches engine hangs
// where the wire keeps returning the same state — typical of an async
// monster move that NRE'd internally.
//
// CombatBudgetGuard catches a different shape: the combat IS advancing
// — round counter goes up, sometimes powers tick — but no one is winning
// or losing. Two canonical examples:
//
//   * Hellraiser + 2× Pommel Strike, no strength source → infinite
//     loop deals zero damage, both sides survive indefinitely; the
//     round counter advances on every snapshot so the fingerprint
//     keeps changing.
//   * Reactive enemies (Time Eater clones, Spore Cloud) that the agent
//     can't crack — the combat sustains but nothing gives.
//
// Two complementary budgets:
//
//   1. MaxCombatRounds  — hard ceiling on a single combat. Bosses
//                          average <30 rounds; pathological greedy-agent
//                          fights might hit ~50. Default 80 catches
//                          true loops without false-firing on hard
//                          legitimate fights.
//
//   2. MaxNoProgressRounds — round-over-round HP-and-powers change
//                            tracker. If K consecutive rounds register
//                            no HP delta on either side AND no power
//                            stack changes, treat as deadlock.
//                            Default 20 — more aggressive than
//                            MaxCombatRounds and the more interesting
//                            signal for "this combat will never end."
//
// Both throw CombatBudgetExceededException with a fingerprint pointing
// at the offending combat (room, floor, enemy ids, round) so the
// operator can reproduce and write a targeted InfiniteLoopGuardTests
// fixture for it.
//
// Wire-in is identical to StallDetector: AgentDriver.PlayRunAsync owns
// an instance and calls Observe(state) after each ApplyAsync. No
// per-agent change required.
public sealed class CombatBudgetGuard
{
    public const int DefaultMaxCombatRounds = 80;
    public const int DefaultMaxNoProgressRounds = 20;

    private readonly int _maxCombatRounds;
    private readonly int _maxNoProgressRounds;

    // Tracked across snapshots within a single combat. Reset when combat
    // ends (CombatState becomes null) or when the encounter changes
    // (different enemy ids — e.g. one fight ended and another started in
    // the same round, which the wire surfaces as a non-null CombatState
    // both times).
    private string? _currentEncounterKey;
    private int _lastRound;
    private string? _lastVitalsFingerprint;
    private int _noProgressRounds;

    public CombatBudgetGuard(
        int maxCombatRounds = DefaultMaxCombatRounds,
        int maxNoProgressRounds = DefaultMaxNoProgressRounds)
    {
        if (maxCombatRounds < 2)
            throw new ArgumentOutOfRangeException(nameof(maxCombatRounds), "maxCombatRounds must be >= 2 (at least one round + the boundary).");
        if (maxNoProgressRounds < 2)
            throw new ArgumentOutOfRangeException(nameof(maxNoProgressRounds), "maxNoProgressRounds must be >= 2.");
        _maxCombatRounds = maxCombatRounds;
        _maxNoProgressRounds = maxNoProgressRounds;
    }

    public void Observe(RunStateResult state)
    {
        var combat = state.CombatState;
        if (combat is null || !combat.IsInProgress)
        {
            // Out of combat — reset all per-combat tracking. The next
            // CombatState we see opens a fresh budget.
            _currentEncounterKey = null;
            _lastRound = 0;
            _lastVitalsFingerprint = null;
            _noProgressRounds = 0;
            return;
        }

        var encounterKey = EncounterKey(state, combat);
        if (encounterKey != _currentEncounterKey)
        {
            // Different combat — reset. The previous fight may have
            // ended and a new one started without us seeing a null
            // CombatState in between (back-to-back encounters or a
            // chained spawn).
            _currentEncounterKey = encounterKey;
            _lastRound = combat.Round;
            _lastVitalsFingerprint = VitalsFingerprint(state, combat);
            _noProgressRounds = 0;
            return;
        }

        // Same combat as previous observation.

        // (1) Hard round-count ceiling. We compare against Round, not
        // a step counter — short-circuits long fights without false-
        // firing on chatty per-round snapshots.
        if (combat.Round > _maxCombatRounds)
        {
            throw new CombatBudgetExceededException(
                BudgetKind.MaxRounds,
                budget: _maxCombatRounds,
                observed: combat.Round,
                encounter: encounterKey,
                fingerprint: VitalsFingerprint(state, combat),
                advisory: BuildAdvisory(combat));
        }

        // (2) Round-over-round no-progress detection. We compare
        // vitals (player + enemy HP, enemy powers) on each NEW round.
        // Within a single round we accept many snapshots (the agent
        // takes multiple actions per turn); only the cross-round delta
        // counts.
        if (combat.Round != _lastRound)
        {
            var currentVitals = VitalsFingerprint(state, combat);
            if (currentVitals == _lastVitalsFingerprint)
            {
                _noProgressRounds++;
                if (_noProgressRounds >= _maxNoProgressRounds)
                {
                    throw new CombatBudgetExceededException(
                        BudgetKind.MaxNoProgressRounds,
                        budget: _maxNoProgressRounds,
                        observed: _noProgressRounds,
                        encounter: encounterKey,
                        fingerprint: currentVitals,
                        advisory: BuildAdvisory(combat));
                }
            }
            else
            {
                _noProgressRounds = 0;
                _lastVitalsFingerprint = currentVitals;
            }
            _lastRound = combat.Round;
        }
    }

    // Identifies the combat encounter for change detection. Includes the
    // room coordinate so a second combat in the same room (e.g. an event
    // that spawns a fight) doesn't accidentally inherit the previous
    // fight's no-progress counter.
    private static string EncounterKey(RunStateResult state, CombatState combat) =>
        $"act={state.CurrentActIndex}.{state.ActFloor}|room={state.CurrentRoomType}|" +
        string.Join(",", combat.Enemies.Select(e => e.MonsterId.ToString()).OrderBy(s => s, StringComparer.Ordinal));

    // What "made progress this round" means: player HP, all enemy HP +
    // block + powers. Ignores energy / hand contents — those reset every
    // round even in a stalemate.
    private static string VitalsFingerprint(RunStateResult state, CombatState combat) =>
        $"hp={state.Hp}/{state.MaxHp}|" +
        string.Join(",", combat.Enemies.Select(e =>
            $"{e.MonsterId}:{e.Hp}+{e.Block}|" +
            string.Join("/", e.Powers.Select(p => $"{p.Id}:{p.Amount}"))));

    // Sentinel-HP advisory. Real STS2 bosses have HP in the hundreds;
    // anything ≥ 100k is either an engine-design placeholder (Doormaker
    // ships MaxHp=999999999), an "uninitialized phase" marker, or a
    // headless bug where a phase-transition Task never ran. When the
    // budget guard trips, surfacing the suspicious enemies in the
    // exception message turns "combat exceeded N rounds" from a riddle
    // into a pointer: "this fight never had a real win condition, look
    // at why HP is X." Doormaker was the surfacing case — a fingerprint
    // line of `DOORMAKER:999996514+0` should never need a forensic
    // investigation to understand.
    private const int SentinelHpThreshold = 100_000;

    internal static string? BuildAdvisory(CombatState combat)
    {
        var suspicious = combat.Enemies
            .Where(e => e.Hp >= SentinelHpThreshold || e.MaxHp >= SentinelHpThreshold)
            .ToList();
        if (suspicious.Count == 0) return null;
        var rows = suspicious.Select(e =>
            $"    {e.MonsterId}: hp={e.Hp:N0}/{e.MaxHp:N0}{(e.Powers.Count > 0 ? " powers=" + string.Join(",", e.Powers.Select(p => $"{p.Id}:{p.Amount}")) : "")}");
        return
            $"  ⚠ sentinel-HP enemy(s) detected (HP ≥ {SentinelHpThreshold:N0}):\n" +
            string.Join("\n", rows) + "\n" +
            $"    This is almost never a real fight — it's an engine-design placeholder " +
            $"(Doormaker ships MaxHp≈10⁹) or a phase-transition Task that NRE'd in " +
            $"headless. Check HangPatches for the monster's move set; if a Task method " +
            $"was silently skipped, NoSilentSkipTests will surface the cause.";
    }
}

public enum BudgetKind
{
    // Combat exceeded MaxCombatRounds.
    MaxRounds,
    // Combat exceeded MaxNoProgressRounds (K rounds without a vitals delta).
    MaxNoProgressRounds,
}

public sealed class CombatBudgetExceededException : InvalidOperationException
{
    public BudgetKind Kind { get; }
    public int Budget { get; }
    public int Observed { get; }
    public string Encounter { get; }
    public string Fingerprint { get; }
    public string? Advisory { get; }

    public CombatBudgetExceededException(BudgetKind kind, int budget, int observed, string encounter, string fingerprint, string? advisory = null)
        : base(BuildMessage(kind, budget, observed, encounter, fingerprint, advisory))
    {
        Kind = kind;
        Budget = budget;
        Observed = observed;
        Encounter = encounter;
        Fingerprint = fingerprint;
        Advisory = advisory;
    }

    private static string BuildMessage(BudgetKind kind, int budget, int observed, string encounter, string fingerprint, string? advisory)
    {
        var head = kind switch
        {
            BudgetKind.MaxRounds => $"CombatBudgetGuard: combat exceeded {budget} rounds (observed round={observed}). " +
                $"Likely an infinite-but-progressing loop (Hellraiser + Pommel Strike-class, or a reactive enemy the agent can't crack).\n" +
                $"  encounter: {encounter}\n" +
                $"  fingerprint: {fingerprint}",
            BudgetKind.MaxNoProgressRounds => $"CombatBudgetGuard: {observed} consecutive rounds without HP/block/power change. " +
                $"Deadlock detected — neither side is doing anything that matters. Cap was {budget} rounds.\n" +
                $"  encounter: {encounter}\n" +
                $"  fingerprint: {fingerprint}",
            _ => $"CombatBudgetGuard: unknown budget kind {kind}",
        };
        return advisory is null ? head : head + "\n" + advisory;
    }
}
