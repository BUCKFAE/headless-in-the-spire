using Sts2Headless.IntegrationTests.Coverage;
using Xunit;
using Sts2Headless.Runtime.Loading;
using Sts2Headless.Utils;

namespace Sts2Headless.IntegrationTests;

// Failure-mode regression: a Harmony patch target was nominally declared
// in `HangPatches.Apply` but silently skipped at runtime, so the gameplay
// bug it was supposed to suppress only surfaced under a specific encounter
// as an opaque "combat exceeded N rounds" hang.
//
// Doormaker was the original surfacing case. `Doormaker.SwapPhasePower<T>`
// is open-generic; the patch loop's `IsGenericMethodDefinition` guard
// quietly emitted `(skipped: open-generic, not Harmony-patchable)` into
// the PatchOutcome.Detail string and continued. `Patched: true` stayed
// true (other methods on the same monster patched fine), so the existing
// BootstrapSequenceTests guard couldn't see the skip — it only checks
// the boolean. The boss never advanced its power rotation, and the
// failure mode hid for months as a "Timeout" sweep entry.
//
// Contract:
//   * Any "(skipped:" annotation inside any PatchOutcome.Detail fails
//     this test, UNLESS the label is on `AcknowledgedSkips` with a
//     written justification.
//   * `AcknowledgedSkips` should be empty in steady state. Adding an
//     entry is a deliberate human acknowledgement: "yes, we know this
//     can't be patched in the current shape, and here's what we'd need
//     to change to remove it."
//   * Removing a skip-emitting code path AND removing the corresponding
//     allow-list entry must happen together. An orphaned allow-list
//     entry whose pattern no longer matches also fails — so we notice
//     when an underlying issue resolves and the acknowledgement can go.
[Collection(InProcessSts2Collection.Name)]
public class HangPatchesNoSilentSkipTests
{
    // label-prefix → why we accept that this surface still skips at
    // least one method. Empty today: the closed-generic auto-patch in
    // PatchMonsterMethods handles the only case (Doormaker.SwapPhasePower)
    // that previously emitted a skip. Add a row here ONLY when a new
    // patch surface genuinely can't be reached and a follow-up issue
    // exists for it.
    private static readonly Dictionary<string, string> AcknowledgedSkips = new(StringComparer.Ordinal);

    [Fact]
    public void NoPatchTarget_SilentlySkipped()
    {
        var repoRoot = Paths.LocateRepoRoot();
        var vendorDir = Path.Combine(repoRoot, "vendor");
        Assert.True(Directory.Exists(vendorDir), $"vendor/ missing at {vendorDir} — run `just setup`.");

        VendorAssemblyResolver.Install(vendorDir);
        var preamble = RuntimeBootstrap.Run(vendorDir);
        Assert.Null(preamble.SetupError);
        Assert.NotNull(preamble.Sts2);

        // Pair each outcome with whether its Detail contains a skip
        // marker. The marker format is anchored on "(skipped:" — every
        // skip path in HangPatches.Monsters.cs / .Powers.cs uses this
        // exact prefix so the assertion has a single string to anchor on.
        var skippedOutcomes = preamble.Patches
            .Where(p => p.Detail is not null && p.Detail.Contains("(skipped:", StringComparison.Ordinal))
            .ToList();

        // Unacknowledged skips — the failure mode this test exists for.
        var unacknowledged = skippedOutcomes
            .Where(p => !AcknowledgedSkips.ContainsKey(p.Target))
            .Select(p => $"  - {p.Target}\n      detail: {p.Detail}")
            .ToList();

        // Stale acknowledgements — patch surfaces we previously accepted
        // as un-patchable but which no longer emit a skip. Fail so the
        // allow-list and the patch code stay in sync.
        var stale = AcknowledgedSkips.Keys
            .Where(label => !skippedOutcomes.Any(p => p.Target == label))
            .Select(label => $"  - {label} (reason: {AcknowledgedSkips[label]})")
            .ToList();

        var failures = new List<string>();
        if (unacknowledged.Count > 0)
            failures.Add(
                $"{unacknowledged.Count} silent-skip(s) in HangPatches.Apply:\n" +
                string.Join("\n", unacknowledged) + "\n" +
                "Either fix the patch (e.g. PatchMonsterMethods auto-patches closed " +
                "generic instantiations now) or add an entry to AcknowledgedSkips with " +
                "a written justification.");
        if (stale.Count > 0)
            failures.Add(
                $"{stale.Count} stale AcknowledgedSkips entry/entries (no longer skipping):\n" +
                string.Join("\n", stale) + "\n" +
                "The underlying patch issue resolved. Remove these entries.");

        Assert.True(failures.Count == 0, string.Join("\n\n", failures));
    }
}
