using Sts2Headless.Protocol.Methods;
using Sts2Headless.Replay;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Smoke test for the host-side wiring of the recording substrate (AD-8).
// Verifies:
//   1. When STS2_REPLAY_OUT is set, the host constructs a recorder on
//      run/new and installs the Harmony hook.
//   2. The next run/new triggers RunManager.CleanUp on the prior run,
//      which fires the BeforeRunManagerCleanUp prefix, which writes the
//      manifest to disk.
//   3. The manifest deserialises with the expected header fields.
//
// Does NOT exercise actual combat recording — that's task #10's
// territory (drives a full combat through to victory and asserts the
// .mcr files materialise). This test isolates the wiring so a host
// regression surfaces here independently of any combat-driving bug.
public class ReplayManifestEmissionTests
{
    [Fact]
    public async Task RunNew_With_Replay_Env_Var_Produces_Manifest_On_Next_RunNew()
    {
        using var tempReplays = new TempReplayRoot();
        await using var host = RecordingHost.Start(tempReplays.Path);

        // First run/new — recorder constructed, hook bound, no manifest yet.
        var first = await host.SendAsync<RunNewResult>("run/new", new RunNewParams(Seed: 42uL));
        Assert.Equal(Character.Ironclad, first.Character);

        // Second run/new — bindings.StartIroncladRun calls
        // RunManager.CleanUp on the in-progress run; our prefix fires;
        // OnRunCleanUp writes manifest.json into the first run's
        // directory.
        var second = await host.SendAsync<RunNewResult>("run/new", new RunNewParams(Seed: 7uL));
        Assert.Equal(Character.Ironclad, second.Character);

        // Find the manifest for the FIRST run (the one CleanUp closed).
        // Layout: <root>/<game-version>/<run-id>/manifest.json. Two
        // run-ids exist (one per run/new), but only the first should
        // have a manifest written (the second is still active).
        var manifestPaths = Directory.GetFiles(tempReplays.Path, ReplayLayout.ManifestFileName, SearchOption.AllDirectories);
        Assert.NotEmpty(manifestPaths);

        var manifest = ReplayManifest.Deserialize(File.ReadAllText(manifestPaths[0]));
        Assert.Equal(ReplayManifest.CurrentVersion, manifest.Version);
        Assert.Equal("v0.103.2", manifest.Header.GameVersion);
        Assert.Equal(1357847701u, manifest.Header.ModelIdHash);
        Assert.Equal(Character.Ironclad, manifest.Header.Character);
        Assert.Equal("42", manifest.Header.Seed);
        Assert.Equal(0, manifest.Header.Ascension);
        Assert.Equal(ReplayHeader.CurrentRunHistorySchemaVersion, manifest.Header.RunHistorySchemaVersion);
        Assert.Equal(ReplayHeader.CurrentProtocolVersion, manifest.Header.ProtocolVersion);
        // Combats array is empty when no combat was driven — Neow lands us in
        // a MapRoom; we never entered a CombatRoom. The empty list is
        // load-bearing: it proves the manifest serialises cleanly with no
        // recorded combats (the typical first-fresh-clone test scenario).
        Assert.Empty(manifest.Combats);

        // The header's sha256 should match GAME_VERSION's pinned value
        // (the recorder reads it from there). Cross-check against the
        // actual file bytes so a stale pin would surface.
        var repoRoot = Runtime.Paths.LocateRepoRoot();
        var actualSha = ReplayHeaderFactory.ComputeSha256(Path.Combine(repoRoot, "vendor", "sts2.dll"));
        Assert.Equal(actualSha, manifest.Header.Sts2DllSha256);
    }

    [Fact]
    public async Task RunNew_Without_Replay_Env_Var_Produces_No_Files()
    {
        // RecordingHost sets STS2_REPLAY_OUT; HostSubprocess (the default
        // fixture) does not. We use HostSubprocess here to lock in the
        // negative invariant: by default, no replay files are written
        // anywhere — recording is strictly opt-in.
        await using var host = new HostSubprocess();
        await host.SendAsync<RunNewResult>("run/new", new RunNewParams(Seed: 42uL));

        // The recorder's default root, if it had been on, would have been
        // vendor/replays/ under the repo root. Assert nothing landed there
        // in the time window of this test.
        var repoRoot = Runtime.Paths.LocateRepoRoot();
        var defaultRoot = Path.Combine(repoRoot, ReplayLayout.DefaultRootRelative);
        if (!Directory.Exists(defaultRoot)) return;
        var freshlyWritten = Directory.GetFiles(defaultRoot, "*", SearchOption.AllDirectories)
            .Where(f => File.GetCreationTimeUtc(f) > DateTime.UtcNow.AddSeconds(-30))
            .ToList();
        Assert.Empty(freshlyWritten);
    }

    private sealed class TempReplayRoot : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sts2-replays-" + Guid.NewGuid().ToString("N"));
        public TempReplayRoot() => Directory.CreateDirectory(Path);
        public void Dispose() { try { Directory.Delete(Path, recursive: true); } catch { /* best-effort */ } }
    }
}
