using Sts2Headless.IntegrationTests.Coverage;
using Xunit;
using Sts2Headless.Runtime.Loading;
using Sts2Headless.Utils;

namespace Sts2Headless.IntegrationTests;

// Locks in the current end-to-end bootstrap state. Mirrors the output of
// `just probe-bootstrap` — if the human-eyeballed probe goes green, the
// matching assertion here keeps it that way under refactors.
//
// Why integration-shaped (loads the real sts2.dll, not mocks): the whole
// point of this test is to catch regressions in vendor resolution, the sync
// context, Harmony patches, GodotStubs surface, and reflection plumbing all
// at once. A unit test that mocks any of those layers would happily pass
// while the real chain is broken.
//
// The expected step list is a SNAPSHOT, not a should-pass set. If a future
// chunk flips a step's Ok value, this test fails — that's the reminder to
// update the snapshot. Same in the other direction: if anything we already
// proved working regresses, the diff shows exactly which step is now wrong.
[Collection(InProcessSts2Collection.Name)]
public class BootstrapSequenceTests
{
    [Fact]
    public void Bootstrap_Walks_To_LivePlayer()
    {
        var repoRoot = Paths.LocateRepoRoot();
        var vendorDir = Path.Combine(repoRoot, "vendor");
        Assert.True(Directory.Exists(vendorDir), $"vendor/ missing at {vendorDir} — run `just setup`.");

        VendorAssemblyResolver.Install(vendorDir);

        var preamble = RuntimeBootstrap.Run(vendorDir);
        Assert.Null(preamble.SetupError);
        Assert.True(preamble.SyncContextInstalled);
        Assert.True(preamble.TestModeEnabled,
            "TestMode.IsOn must be set during the preamble — combat-start branches on it and stays half-initialised otherwise");
        Assert.NotNull(preamble.Sts2);
        Assert.All(preamble.Patches, p =>
            Assert.True(p.Patched, $"patch missing: {p.Target} ({p.Detail})"));

        var steps = BootstrapSequence.Apply(preamble.Sts2!);

        // Snapshot of the known state as of 2026-05-16. Fully green: ModelDb
        // injection runs before InitProgressData so ProgressSaveManager's
        // CHARACTER.* lookups resolve. The bundled hook-patcher step
        // installs Harmony postfixes for relic/card/monster/potion/power
        // hooks; runs after ModelIdSerializationCache (it needs ModelDb
        // populated) and before CreateIroncladSmoke (so the smoke run
        // is instrumented like real runs).
        var expected = new (string Label, bool Ok)[]
        {
            ("TestMode.IsOn = true", true),
            ("PlatformUtil.PrimaryPlatform (warm)", true),
            ("InitProfileId(0)", true),
            ("ModelDb.Inject loop over AbstractModelSubtypes.All", true),
            ("InitProgressData()", true),
            ("InitPrefsDataForTest()", true),
            ("ModelIdSerializationCache.Init()", true),
            ("ModelHookPatcher.Apply (all kinds)", true),
            ("Player.CreateForNewRun<Ironclad>(UnlockState.all, 1uL)", true),
        };

        Assert.Equal(expected.Length, steps.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            var (label, ok) = expected[i];
            var actual = steps[i];
            Assert.Equal(label, actual.Label);
            Assert.True(
                actual.Ok == ok,
                $"step {i} '{label}' expected Ok={ok} but got Ok={actual.Ok} (detail: {actual.Detail ?? "<none>"})");
        }
    }
}
