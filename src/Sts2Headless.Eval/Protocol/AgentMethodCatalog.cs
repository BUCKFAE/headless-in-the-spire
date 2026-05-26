using Sts2Headless.Protocol;

namespace Sts2Headless.Eval.Protocol;

// Single source of truth for the `agent/*` dialect (the mirror of
// MethodCatalog.Core for the host dialect). Both the AgentRunner exe
// (which serves the dialect for in-repo C# agents) and the harness's
// AgentTransport (which speaks the client side) read from this list
// so a method added in isolation surfaces as a parity failure rather
// than as a silent drift.
//
// The Sts2Headless.SchemaExport project picks this up alongside
// MethodCatalog.Core to emit protocol/agent-openrpc.json, the sibling
// schema artefact external (Python / Rust / sibling C#) clients
// generate typed bindings from.
public static class AgentMethodCatalog
{
    public static IReadOnlyList<MethodEntry> All { get; } = new MethodEntry[]
    {
        new("agent/init",
            ParamsType: typeof(AgentInitParams),
            ResultType: typeof(AgentInitResult),
            Summary: "Initialise the agent for one cell. Sent exactly once, before any agent/decide. Carries character, seed, ascension, modifiers, resolved budgets, and the harness eval-id. The agent self-reports its name + version; mismatched identity fails the cell with HarnessError."),

        new("agent/decide",
            ParamsType: typeof(AgentDecideParams),
            ResultType: typeof(AgentDecideResult),
            Summary: "Decide one action against the current snapshot. Sent once per host step. The snapshot is the full RunStateResult byte-for-byte. The agent replies with one AgentAction (closed union — PlayCard, EndTurn, …, StopRun); per-decision wall-clock budget applies."),

        new("agent/teardown",
            ParamsType: null,
            ResultType: typeof(AgentTeardownResult),
            Summary: "Drain caches, flush logs, exit cleanly. Final wire call before SIGTERM. A non-Ok teardown is logged but does not change the cell's terminus — the run is already over."),
    };
}
