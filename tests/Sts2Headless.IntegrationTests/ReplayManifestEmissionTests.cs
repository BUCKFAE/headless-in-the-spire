using Sts2Headless.Protocol.Methods;
using Sts2Headless.Replay;
using Sts2Headless.TestSupport;
using Sts2Headless.Utils;
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
        using var tempReplays = new TempDir("sts2-replays");
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
        var repoRoot = Paths.LocateRepoRoot();
        var actualSha = FileHash.Sha256(Paths.Sts2DllPath(Paths.VendorDir(repoRoot)));
        Assert.Equal(actualSha, manifest.Header.Sts2DllSha256);
    }

    [Fact]
    public async Task HostSubprocess_With_OptOut_Produces_No_Files()
    {
        // Recording is on-by-default at the host level (lands in
        // vendor/replays/). The HostSubprocess test fixture sets
        // STS2_REPLAY_OUT=off so generic integration tests don't
        // pollute the repo. This test locks in that opt-out: when the
        // sentinel is set, no replay files are written anywhere.
        //
        // Detection: snapshot-diff. A wall-clock window
        // (File.GetCreationTimeUtc > now-30s) was tried first but proved
        // brittle — local dev activity (probe-combat-stall, ad-hoc
        // record-* recipes) seeds vendor/replays/ with recent files, and
        // the assertion then fired on files this test never touched. The
        // diff makes the invariant precise: only paths created BETWEEN
        // the pre-host snapshot and the post-host snapshot count.
        var repoRoot = Paths.LocateRepoRoot();
        var defaultRoot = Path.Combine(repoRoot, ReplayLayout.DefaultRootRelative);
        var before = SnapshotReplayRoot(defaultRoot);

        await using var host = new HostSubprocess();
        await host.SendAsync<RunNewResult>("run/new", new RunNewParams(Seed: 42uL));
        await host.DisposeAsync();

        var after = SnapshotReplayRoot(defaultRoot);
        var added = after.Except(before).ToList();
        Assert.Empty(added);
    }

    private static HashSet<string> SnapshotReplayRoot(string root) =>
        Directory.Exists(root)
            ? new HashSet<string>(Directory.GetFiles(root, "*", SearchOption.AllDirectories), StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
}
