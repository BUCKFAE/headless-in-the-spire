using Sts2Headless.Commands;
using Sts2Headless.Runtime.Hooks;
using Xunit;

namespace Sts2Headless.IntegrationTests.Coverage;

// Default-running parity guard between the two "what kinds of content
// exist" lists in the codebase:
//
//   * GenerateContentIdsCommand.Kinds — the manifest universe: every
//     KindSpec ends up emitting a *Id.g.cs enum and feeding the
//     CardSweep / RelicSweep / etc. matrix.
//   * HookPatchKinds.All — the runtime hook-instrumentation registry:
//     every entry installs Harmony postfixes that record TriggerEvents
//     to TriggerLog for that kind.
//
// These MUST stay in lockstep — if a kind is enumerated but not
// instrumented, the wire's TriggeredSincePrev is silently incomplete
// for that kind, and the existing coverage axis "did this <kind> fire
// in any run?" lies by omission.
//
// Drift in either direction is a real signal:
//
//   * Missing from HookPatchKinds — sts2.dll just grew a new top-level
//     namespace (caught first by NewContentKindTests), the generator
//     was extended, but the runtime patcher wasn't. Adds two lines:
//     one in HookPatchKinds.cs's All list, one in TriggerKind in
//     Methods.cs (then `just regen`).
//   * Extra in HookPatchKinds — a hook-patch entry references a kind
//     no one enumerates ids for. Either drop the entry or extend the
//     generator's Kinds list with a matching KindSpec.
//
// No host subprocess needed — this is a pure static-list parity check,
// runs in milliseconds, lives in the default `just test-integration`
// run.
public class InstrumentationKindParityTest
{
    [Fact]
    public void EveryGeneratedKind_IsInstrumentedByHookPatcher()
    {
        var manifestKinds = GenerateContentIdsCommand.Kinds
            .Select(k => k.Kind)
            .ToHashSet(System.StringComparer.Ordinal);
        var instrumentedKinds = HookPatchKinds.All
            .Select(k => k.Kind.ToString())
            .ToHashSet(System.StringComparer.Ordinal);

        var missing = manifestKinds
            .Except(instrumentedKinds, System.StringComparer.Ordinal)
            .OrderBy(s => s, System.StringComparer.Ordinal)
            .ToList();
        var extra = instrumentedKinds
            .Except(manifestKinds, System.StringComparer.Ordinal)
            .OrderBy(s => s, System.StringComparer.Ordinal)
            .ToList();

        if (missing.Count == 0 && extra.Count == 0) return;

        var lines = new List<string>
        {
            "Hook-patch parity drifted from GenerateContentIdsCommand.Kinds.",
        };
        if (missing.Count > 0)
            lines.Add(
                $"  Missing from HookPatchKinds.All: [{string.Join(", ", missing)}]. "
                + "Append an entry to src/Sts2Headless.Runtime/Hooks/HookPatchKinds.cs "
                + "AND a matching value to TriggerKind in "
                + "src/Sts2Headless.Protocol/Methods/Methods.cs (run `just regen` after).");
        if (extra.Count > 0)
            lines.Add(
                $"  Extra in HookPatchKinds.All: [{string.Join(", ", extra)}]. "
                + "Either drop the entry, or add a matching KindSpec to "
                + "GenerateContentIdsCommand.Kinds.");

        Assert.Fail(string.Join('\n', lines));
    }
}
