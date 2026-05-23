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

        // Force the instrumentation step into its WITH-patches branch for this
        // assertion: the un-instrumented branch is exercised by the sibling
        // test below. Either branch must keep the step-count snapshot stable;
        // only the Detail of ApplyHookPatches changes between them.
        var prior = Environment.GetEnvironmentVariable(BootstrapSequence.InstrumentHooksEnvVar);
        Environment.SetEnvironmentVariable(BootstrapSequence.InstrumentHooksEnvVar, "1");
        IReadOnlyList<BootstrapSequence.StepOutcome> steps;
        try
        {
            steps = BootstrapSequence.Apply(preamble.Sts2!);
        }
        finally
        {
            Environment.SetEnvironmentVariable(BootstrapSequence.InstrumentHooksEnvVar, prior);
        }

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

        // Locks down the WITH-instrumentation Detail format so a regression
        // that silently skips the patcher (and breaks MechanicSweep's
        // TriggeredSincePrev classification) surfaces here. The detail
        // begins with the first kind's report — Affliction (alphabetical
        // in HookPatchKinds.All).
        var hookStep = steps[7];
        Assert.False(
            hookStep.Detail?.StartsWith("skipped", StringComparison.Ordinal) ?? true,
            $"ModelHookPatcher should have run with the env var set, but Detail=\"{hookStep.Detail}\"");
    }

    [Fact]
    public void Bootstrap_SkipsHookPatcher_WhenEnvVarUnset()
    {
        var repoRoot = Paths.LocateRepoRoot();
        var vendorDir = Path.Combine(repoRoot, "vendor");
        Assert.True(Directory.Exists(vendorDir), $"vendor/ missing at {vendorDir} — run `just setup`.");

        VendorAssemblyResolver.Install(vendorDir);
        var preamble = RuntimeBootstrap.Run(vendorDir);
        Assert.NotNull(preamble.Sts2);

        // Hook instrumentation is opt-in since 2026-05 — walking every
        // concrete AbstractModel subtype to install Harmony postfixes costs
        // ~440ms per cold start. Most tests don't read TriggeredSincePrev,
        // so they should skip the work. Pin that with the env var
        // explicitly cleared (the MechanicSweepTests assembly initializer
        // sets it; this test would otherwise inherit "1" when both
        // assemblies share a runner process).
        var prior = Environment.GetEnvironmentVariable(BootstrapSequence.InstrumentHooksEnvVar);
        Environment.SetEnvironmentVariable(BootstrapSequence.InstrumentHooksEnvVar, null);
        IReadOnlyList<BootstrapSequence.StepOutcome> steps;
        try
        {
            steps = BootstrapSequence.Apply(preamble.Sts2!);
        }
        finally
        {
            Environment.SetEnvironmentVariable(BootstrapSequence.InstrumentHooksEnvVar, prior);
        }

        var hookStep = steps.First(s => s.Label == "ModelHookPatcher.Apply (all kinds)");
        Assert.True(hookStep.Ok, "skipping the patcher is a healthy outcome, not a failure");
        Assert.NotNull(hookStep.Detail);
        Assert.StartsWith("skipped", hookStep.Detail);
        Assert.Contains(BootstrapSequence.InstrumentHooksEnvVar, hookStep.Detail);
    }
}
