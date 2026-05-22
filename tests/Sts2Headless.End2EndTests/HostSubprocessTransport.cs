using Sts2Headless.Agents.Contracts;
using Sts2Headless.IntegrationTests;

namespace Sts2Headless.End2EndTests;

// Adapts the existing HostSubprocess fixture (shared from IntegrationTests
// via source-link) to the Agents project's ITransport interface. Kept as a
// thin adapter rather than modifying HostSubprocess so IntegrationTests
// stays independent of the Agents project.
public sealed class HostSubprocessTransport(HostSubprocess host) : ITransport
{
    public Task<TResult> SendAsync<TResult>(string method, object? @params = null) =>
        host.SendAsync<TResult>(method, @params);
}
