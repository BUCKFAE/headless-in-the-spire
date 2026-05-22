using Sts2Headless.Agents.Contracts;
using Sts2Headless.IntegrationTests;

namespace Sts2Headless.MechanicSweepTests;

// Adapts the HostSubprocess fixture (source-linked from IntegrationTests)
// to the Agents project's ITransport interface. Mirrors the same adapter
// End2EndTests uses (HostSubprocessTransport) so MechanicSweep code can
// run against either project's transport without caring which test
// project owns the host process.
internal sealed class TransportAdapter(HostSubprocess host) : ITransport
{
    public Task<TResult> SendAsync<TResult>(string method, object? @params = null) =>
        host.SendAsync<TResult>(method, @params);
}
