using Sts2Headless.Agents.Driving;

namespace Sts2Headless.Eval;

// Per-cell budgets. Three layers:
//
//   * PerDecision — soft, applied to each agent/decide round-trip.
//     Exceeded ⇒ terminus = Timeout (NOT AgentCrash).
//   * PerCell     — hard, applied to total wall-clock per cell.
//     Exceeded ⇒ terminus = Timeout. Harness SIGTERMs the agent (and
//     host), waits a bounded time, SIGKILLs if still alive.
//   * MaxSteps    — integer cap on driver steps. Exceeded ⇒ terminus =
//     MaxSteps. Mirrors `AgentDriver.DefaultMaxSteps` so the driver
//     and the harness trip the limit simultaneously.
//
// AD-9 defaults: PerDecision = 30s, PerCell = 10min, MaxSteps = 4000.
// These are calibrated against ParallelHostThroughputBenchmark and
// BeatGameOnSeed42Tests; tune at the call site via `with` or init
// syntax, not by editing the defaults here.
public sealed record HarnessBudgets
{
    public TimeSpan PerDecision { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan PerCell     { get; init; } = TimeSpan.FromMinutes(10);
    public int      MaxSteps    { get; init; } = AgentDriver.DefaultMaxSteps;

    public static HarnessBudgets Default { get; } = new();
}
