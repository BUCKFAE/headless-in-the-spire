using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Agents;

// Drives a live run on a host until a caller-supplied stop condition fires.
// Stateless across calls: every input the agent acts on lives on the
// snapshots it gets back from the transport, so two concurrent runs can
// share one agent instance without coordination.
//
// The stop-condition lambda keeps the agent useful for several jobs without
// embedding policy: "reach the boss room", "finish Act 1", "play until HP
// drops below 20", "stop when the next reward is offered". The agent should
// re-evaluate the stop condition after every snapshot it sees.
//
// Returning the snapshot that matched the stop condition lets callers
// inspect it without round-tripping run/state again.
public interface IAgent
{
    Task<RunStateResult> DriveUntilAsync(
        ITransport host,
        Func<RunStateResult, bool> stopWhen,
        CancellationToken ct = default);
}
