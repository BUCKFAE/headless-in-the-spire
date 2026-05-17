using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Wire-level smoke for the `run/history` method (AD-8 / task #12).
// Two paths:
//   1. Without STS2_REPLAY_OUT — the default host has no recorder, so
//      run/history must throw a typed wire error (not crash, not
//      silently return null).
//   2. With STS2_REPLAY_OUT but no run completed — the recorder exists
//      but run.json hasn't landed yet, so run/history must throw with
//      the "run hasn't ended" message that points the caller at the
//      right precondition.
//
// The positive path (recorder + run.json present + parses) is covered
// by RunJsonEmissionTests + ReplayQueryTests at the unit level and
// will be the main concern of #10's end-to-end orchestration once a
// run-completion drive is wired.
public class RunHistoryMethodTests
{
    [Fact]
    public async Task RunHistory_Without_Recording_Returns_NoActiveRun_Error()
    {
        await using var host = new HostSubprocess();
        await host.SendAsync<RunNewResult>("run/new", new RunNewParams(Seed: 42uL));

        var err = await host.ExpectErrorAsync("run/history");
        Assert.NotNull(err);
        Assert.Contains("STS2_REPLAY_OUT", err.Message);
    }

    [Fact]
    public async Task RunHistory_With_Recording_But_No_Completed_Run_Returns_NotYet_Error()
    {
        using var tempReplays = new TempReplayRoot();
        await using var host = RecordingHost.Start(tempReplays.Path);
        await host.SendAsync<RunNewResult>("run/new", new RunNewParams(Seed: 42uL));

        var err = await host.ExpectRawErrorAsync("run/history");
        Assert.NotNull(err);
        Assert.Contains("hasn't ended", err.Message);
    }

    private sealed class TempReplayRoot : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sts2-replays-" + Guid.NewGuid().ToString("N"));
        public TempReplayRoot() => Directory.CreateDirectory(Path);
        public void Dispose() { try { Directory.Delete(Path, recursive: true); } catch { /* best-effort */ } }
    }
}
