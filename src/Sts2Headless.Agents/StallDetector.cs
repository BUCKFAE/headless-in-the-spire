using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Agents;

// Watchdog for agent drive loops: when K consecutive snapshots have an
// identical fingerprint, the engine is stuck in the same phase and the
// agent is making wire calls without advancing state. Throws with the
// offending fingerprint so the operator gets a pointer to the exact
// combat / enemy / power, instead of waiting out a long cancellation
// budget.
//
// Why this exists: hangs in headless live inside async monster move
// methods or power hooks (the body NREs internally, the exception is
// swallowed by TaskHelper.LogTaskExceptions, CombatManager is left
// half-transitioned — see HangPatches.cs for the canonical examples).
// From the wire's perspective the symptom is identical: every
// run/end_turn comes back with `IsPlayPhase=false`, the agent keeps
// pumping end_turn, and the only visible signal is "nothing changes."
// Bare timeouts on the test see minutes of empty round-trips before
// they fire. The stall detector turns that into seconds.
//
// USAGE — automatic via AgentDriver:
//
// Agents implement IAgent.Decide(state) → AgentAction. AgentDriver
// constructs a StallDetector internally and calls Observe() after
// every dispatched action. The detector cannot be forgotten or
// bypassed by an agent implementation.
//
// To customise the threshold or share an instance across runs, pass
// one explicitly:
//
//     var stall = new StallDetector(maxIdentical: 16);
//     var outcome = await AgentDriver.PlayRunAsync(host, agent,
//         stallDetector: stall, ...);
//
// FINGERPRINT — these fields change on every legitimate progression
// step, so an identical fingerprint across K calls is a stall:
//   * Room type + act/floor
//   * Player HP / gold / deck size
//   * Combat round / phase / energy / block / hand size
//   * Per-enemy: monster id, hp, block, active powers
//
// THRESHOLD TUNING — default 8 identical steps. Real combats advance
// their round counter every Step (each Step does one play_card or
// end_turn and reads back); 8 leaves slack for benign repeated reads
// (e.g. draining a small reward menu) without false-firing. Bump via
// the ctor if a particular agent has a multi-call sequence that
// legitimately reads the same snapshot more than 8 times — but
// preferably refactor the agent's sequence instead, since a single
// "decision = one wire call = one snapshot" cadence is the spirit of
// the IAgent contract.
//
// FALSE-FIRE CASES — places to be careful:
//   * Reward menus: the host might surface the same RewardsState
//     across multiple reads if the agent's pick/skip logic loops
//     re-reading without acting. The fingerprint includes deck/gold,
//     so a successful pick changes it; only a no-op loop trips.
//   * Boss rooms with the same enemy across rounds where the player
//     has no playable cards and just ends turns: round counter still
//     advances, so the fingerprint differs.
//   * Map rooms after enter_next_act: the wire briefly reports an
//     empty MapRoom with no nodes before the next-act regeneration
//     completes. Threshold 8 should ride over this — the next state
//     read brings the new nodes.
//
// EXTENDING TO OTHER AGENTS — nothing to do. Every IAgent run through
// AgentDriver.PlayRunAsync gets a StallDetector wired automatically.
// New agents inherit the watchdog from the framework; they cannot
// opt out short of bypassing the driver.
public sealed class StallDetector
{
    public const int DefaultMaxIdentical = 8;

    private readonly int _maxIdentical;
    private string _lastFingerprint = "";
    private int _identicalCount;

    public StallDetector(int maxIdentical = DefaultMaxIdentical)
    {
        if (maxIdentical < 2)
            throw new ArgumentOutOfRangeException(nameof(maxIdentical), "maxIdentical must be >= 2 (one transition + one repeat).");
        _maxIdentical = maxIdentical;
    }

    public void Observe(RunStateResult state)
    {
        var fp = Fingerprint(state);
        if (fp == _lastFingerprint)
        {
            _identicalCount++;
            if (_identicalCount >= _maxIdentical)
            {
                throw new StallDetectedException(_identicalCount, fp);
            }
        }
        else
        {
            _lastFingerprint = fp;
            _identicalCount = 0;
        }
    }

    private static string Fingerprint(RunStateResult s)
    {
        var combat = s.CombatState is null
            ? "no-combat"
            : $"round={s.CombatState.Round}|playPhase={s.CombatState.IsPlayPhase}|" +
              $"inProgress={s.CombatState.IsInProgress}|" +
              $"energy={s.CombatState.Energy}/{s.CombatState.MaxEnergy}|" +
              $"block={s.CombatState.PlayerBlock}|" +
              $"hand={s.CombatState.Hand.Count}|" +
              $"enemies=[{string.Join(",", s.CombatState.Enemies.Select(e => $"{e.MonsterId}:{e.Hp}/{e.MaxHp}+{e.Block}|" +
                  string.Join("/", e.Powers.Select(p => $"{p.Id}:{p.Amount}"))))}]";
        return $"room={s.CurrentRoomType}|act={s.CurrentActIndex}.{s.ActFloor}|" +
               $"hp={s.Hp}/{s.MaxHp}|gold={s.Gold}|deck={s.DeckSize}|{combat}";
    }
}

public sealed class StallDetectedException : InvalidOperationException
{
    public string Fingerprint { get; }
    public int IdenticalSnapshots { get; }

    public StallDetectedException(int identicalSnapshots, string fingerprint)
        : base($"StallDetector: {identicalSnapshots} consecutive snapshots had identical fingerprint. " +
               $"The engine is stuck in the same phase — likely a hang inside a monster move method " +
               $"or a power hook that NRE's internally and is swallowed by TaskHelper.LogTaskExceptions. " +
               $"See HangPatches.cs for the patch shape (Harmony prefix → Task.CompletedTask).\n" +
               $"  fingerprint: {fingerprint}")
    {
        Fingerprint = fingerprint;
        IdenticalSnapshots = identicalSnapshots;
    }
}
