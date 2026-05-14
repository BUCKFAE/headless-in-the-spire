namespace Sts2Headless.Agents;

// Single-method abstraction over the wire transport. Anything that can issue
// a typed JSON-RPC request to a running headless host implements this — the
// subprocess fixture used by tests, a future in-proc dispatcher, a replay
// player. Decoupling agents from the concrete transport is what lets the
// same GreedyAgent drive a real host today and a replay re-executor tomorrow.
//
// Intentionally minimal. Cancellation, batched requests, and notification
// subscription are deliberately *not* part of the contract yet — they'll be
// added when a concrete consumer needs them, not speculatively.
public interface ITransport
{
    Task<TResult> SendAsync<TResult>(string method, object? @params = null);
}
