using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Wire-level basics: ping round-trip and unknown-method handling. These
// exercise the envelope plumbing — no sts2 state, no run lifecycle — but
// still need a live subprocess because the StdioHost loop is the thing
// under test.
public class HostBasicTests
{
    [Fact]
    public async Task Ping_RoundTrips_WithGameVersionDetails()
    {
        await using var host = new HostSubprocess();

        var ping = await host.SendAsync<HostPingResult>("host/ping");

        Assert.True(ping.Ok);
        Assert.NotNull(ping.GameSha256);
    }

    [Fact]
    public async Task UnknownMethod_ReturnsMethodNotFoundError()
    {
        await using var host = new HostSubprocess();

        var error = await host.ExpectErrorAsync("does/not/exist");

        Assert.Equal(-32601, error.Code);
        Assert.Contains("does/not/exist", error.Message);
    }
}
