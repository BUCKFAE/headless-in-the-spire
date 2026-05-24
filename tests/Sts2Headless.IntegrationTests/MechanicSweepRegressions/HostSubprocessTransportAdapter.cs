using Sts2Headless.Agents.Contracts;

namespace Sts2Headless.IntegrationTests.MechanicSweepRegressions;

// Shared ITransport adapter for the regression-test files in this
// folder. Mirrors the per-file adapter in RestSiteSnapshotTests so the
// CheatClient.* extension methods (SetHpAsync, StartCombatAsync,
// ApplyPowerAsync, …) bind against the same surface the sweeps use.
internal sealed class HostSubprocessTransportAdapter(HostSubprocess host) : ITransport
{
    public Task<TResult> SendAsync<TResult>(string method, object? @params = null) =>
        host.SendAsync<TResult>(method, @params);
}
