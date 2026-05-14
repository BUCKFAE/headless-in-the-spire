using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Wire-level basics: ping round-trip and unknown-method handling. These
// exercise the envelope plumbing — no sts2 state, no run lifecycle — but
// still need a live subprocess because the StdioHost loop is the thing
// under test. Neither test mutates session state, so they share a single
// HostSubprocess via IClassFixture.
public class HostBasicTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;

    public HostBasicTests(HostSubprocess host) => _host = host;

    [Fact]
    public async Task Ping_RoundTrips_WithGameVersionDetails()
    {
        var ping = await _host.SendAsync<HostPingResult>("host/ping");

        Assert.True(ping.Ok);
        Assert.NotNull(ping.GameSha256);
    }

    [Fact]
    public async Task UnknownMethod_ReturnsMethodNotFoundError()
    {
        var error = await _host.ExpectErrorAsync("does/not/exist");

        Assert.Equal(-32601, error.Code);
        Assert.Contains("does/not/exist", error.Message);
    }
}
