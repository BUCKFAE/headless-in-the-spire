using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Agents.Contracts;

// The single method the agent driver speaks: snapshot → action.
// Stateless from the protocol's perspective; carry state on `this` if
// needed. Mirrors the Python `Agent` Protocol.
//
// What an agent does NOT do:
//   * Touch ITransport — the driver dispatches.
//   * Loop — the driver loops.
//   * Detect terminal / stall — the driver handles both.
//   * Catch exceptions from the wire — the driver surfaces them.
//
// What an agent DOES do:
//   * Inspect the snapshot.
//   * Return one AgentAction (or StopRun if it wants to bail).
//
// HeuristicAgent is the convenience base that splits Decide() into
// per-phase hooks with sensible defaults; most rule-based agents
// should inherit from it rather than implement IAgent directly.
public interface IAgent
{
    AgentAction Decide(RunStateResult state);
}

public sealed class NoLegalActionException : InvalidOperationException
{
    public RunStateResult State { get; }

    public NoLegalActionException(string message, RunStateResult state)
        : base(message)
    {
        State = state;
    }
}
