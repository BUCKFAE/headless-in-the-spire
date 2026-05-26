namespace Sts2Headless.Eval.Protocol;

// Server-defined wire error codes for the `agent/*` dialect.
//
// JSON-RPC 2.0 reserves -32700..-32600 for transport/parsing failures
// and -32000..-32099 for server policy (we use those for the host
// dialect's debug gate, see Sts2Headless.Protocol.WireErrorCode). The
// agent dialect owns the -32200..-32299 range so a stack trace or wire
// log makes clear which side emitted the failure.
//
// Add new codes here rather than scattering integer literals across the
// AgentRunner's dispatch. Clients can rely on the codes being stable;
// message text is allowed to evolve.
public static class AgentErrorCode
{
    // Agent declined to play this cell. Typically a capability mismatch
    // surfaced lazily — the agent received an init for a character or
    // ascension it doesn't support. The harness records the row as
    // terminus = HarnessError (a clean refusal is not a crash).
    public const int AgentDeclinedToInit = -32200;

    // Agent emitted an error from agent/decide that it couldn't handle.
    // Recorded as terminus = AgentCrash with the wire payload captured.
    // Distinct from AgentSnapshotInvalid so cross-version regressions
    // surface separately from agent-side bugs.
    public const int AgentDecisionRefused = -32201;

    // Agent received a snapshot that failed its validation (missing
    // fields after a host-side wire bump, for instance). Recorded as
    // terminus = AgentCrash; the wire payload pinpoints what the agent
    // was missing. Useful signal that the agent and host disagree about
    // the snapshot schema.
    public const int AgentSnapshotInvalid = -32202;
}
