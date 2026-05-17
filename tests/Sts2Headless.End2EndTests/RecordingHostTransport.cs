using Sts2Headless.Agents;
using Sts2Headless.IntegrationTests;

namespace Sts2Headless.End2EndTests;

// Adapter from the IntegrationTests RecordingHost (which sets
// STS2_REPLAY_OUT and so produces .mcr / timeline.json / manifest.json
// artifacts) to the Agents project's ITransport interface. Parallel to
// HostSubprocessTransport — the choice between the two is whether a
// given End2End test needs the replay-recorder side-effects.
internal sealed class RecordingHostTransport(RecordingHost host) : ITransport
{
    public Task<TResult> SendAsync<TResult>(string method, object? @params = null) =>
        host.SendAsync<TResult>(method, @params);
}
