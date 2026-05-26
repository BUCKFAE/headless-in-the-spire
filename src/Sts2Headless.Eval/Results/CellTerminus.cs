using System.Text.Json.Serialization;

namespace Sts2Headless.Eval;

// Closed set of outcomes for one matrix cell. The first six are
// *results* — the cell ran cleanly, the agent (or game state) determined
// the outcome. The three *Crash variants are *attribution* — somebody
// crashed; the row records who. HarnessError is the only one that flips
// the harness's exit code (NFR-4): a CI gate that fires on agent
// crashes would be hostile to development on the harness itself.
[JsonConverter(typeof(JsonStringEnumConverter<CellTerminus>))]
public enum CellTerminus
{
    // The agent beat the Act 3 boss (per sts2-game-facts.md). The
    // canonical "win" signal.
    Victory,

    // Player HP hit zero in combat. Most common non-win outcome.
    Death,

    // The agent emitted `StopRun` mid-run. Recorded so the leaderboard
    // can tell "agent gave up" apart from "agent died".
    Abandoned,

    // `StallDetector` tripped (~8 identical snapshots in a row). The
    // engine is stuck and not advancing; the agent kept making wire
    // calls that did not move state. The offending fingerprint lands in
    // the row's error payload.
    Stalled,

    // Hit `Budgets.MaxSteps` without termination. The driver step cap
    // is the floor before the StallDetector — most stalls trip first.
    MaxSteps,

    // Wall-clock budget (`Budgets.PerCell`, or a per-decision timeout)
    // expired. The harness SIGTERMs the agent + host, waits, then
    // SIGKILLs.
    Timeout,

    // The host returned a wire error (engine NRE wrapped as
    // -32603 InternalError, validation failures, etc.). The wire
    // payload is captured in the row.
    EngineCrash,

    // The host process died — stdout EOF before a response. Could be a
    // segfault inside sts2.dll, an unhandled exception in the host
    // dispatch loop, an OOM kill. The host's stderr (if any) is
    // captured in the row.
    HostCrash,

    // The agent process died — stdout EOF before a response. Same
    // attribution shape as HostCrash, on the agent side.
    AgentCrash,

    // The harness itself failed to set up, drive, or finalise the cell
    // — e.g. failure to write `runs.jsonl`, a manifest reflection
    // error, an unexpected OS exception. The only terminus that flips
    // the eval exit code; everything else is an expected result and
    // exits zero.
    HarnessError,
}
