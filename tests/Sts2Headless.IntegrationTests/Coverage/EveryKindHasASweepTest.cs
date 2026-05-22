using Sts2Headless.Commands;
using Sts2Headless.MechanicSweep;
using Xunit;

namespace Sts2Headless.IntegrationTests.Coverage;

// Default-running parity guard between GenerateContentIdsCommand.Kinds
// and SweepRegistry's two lists (ImplementedSweeps + PlannedSweeps).
// Every manifest kind MUST be tracked — either a sweep exists today
// (Implemented) or a sweep is pending with a one-line reason (Planned).
//
// What this catches:
//
//   * NEW MANIFEST KIND — sts2.dll grew a new top-level namespace,
//     NewContentKindTests fires first, the contributor extends
//     GenerateContentIdsCommand.Kinds. THIS test fires next: the new
//     kind isn't in SweepRegistry, so a deliberate "we'll plan a sweep
//     for this" entry is required before the change lands.
//
//   * REMOVED IMPLEMENTED SWEEP — `CardSweep` is deleted/renamed →
//     the typeof() reference in ImplementedSweeps stops compiling →
//     the contributor either restores the sweep or moves Card back
//     to Planned (with a reason).
//
//   * STALE PLANNED — a kind appears in PlannedSweeps but isn't in
//     GenerateContentIdsCommand.Kinds anymore (manifest deletion).
//     Drop the planned entry.
//
//   * IMPLEMENTED AND PLANNED — same kind in both lists. Pick one.
//
// No host subprocess needed — pure static-list comparison. Lives next
// to the other Coverage meta-tests so all kind-registry guards run
// together.
public class EveryKindHasASweepTest
{
    [Fact]
    public void EveryGeneratedKind_IsImplementedOrPlanned()
    {
        var manifestKinds = GenerateContentIdsCommand.Kinds
            .Select(k => k.Kind)
            .ToHashSet(System.StringComparer.Ordinal);
        var implementedKinds = SweepRegistry.ImplementedSweeps
            .Select(s => s.Kind)
            .ToHashSet(System.StringComparer.Ordinal);
        var plannedKinds = SweepRegistry.PlannedSweeps
            .Select(s => s.Kind)
            .ToHashSet(System.StringComparer.Ordinal);
        var trackedKinds = new HashSet<string>(implementedKinds, System.StringComparer.Ordinal);
        foreach (var k in plannedKinds) trackedKinds.Add(k);

        var untracked = manifestKinds
            .Except(trackedKinds, System.StringComparer.Ordinal)
            .OrderBy(s => s, System.StringComparer.Ordinal)
            .ToList();
        var orphaned = trackedKinds
            .Except(manifestKinds, System.StringComparer.Ordinal)
            .OrderBy(s => s, System.StringComparer.Ordinal)
            .ToList();
        var duplicated = implementedKinds
            .Intersect(plannedKinds, System.StringComparer.Ordinal)
            .OrderBy(s => s, System.StringComparer.Ordinal)
            .ToList();

        if (untracked.Count == 0 && orphaned.Count == 0 && duplicated.Count == 0) return;

        var lines = new List<string> { "Sweep parity drifted from GenerateContentIdsCommand.Kinds." };
        if (untracked.Count > 0)
            lines.Add(
                $"  Untracked kinds (need an entry in SweepRegistry.ImplementedSweeps or .PlannedSweeps): "
                + $"[{string.Join(", ", untracked)}]. "
                + "Edit src/Sts2Headless.MechanicSweep/SweepRegistry.cs.");
        if (orphaned.Count > 0)
            lines.Add(
                $"  Orphaned in SweepRegistry (no matching kind in manifest): [{string.Join(", ", orphaned)}]. "
                + "Drop the entry, or add a KindSpec to GenerateContentIdsCommand.Kinds.");
        if (duplicated.Count > 0)
            lines.Add(
                $"  Listed in both Implemented and Planned: [{string.Join(", ", duplicated)}]. Pick one.");

        Assert.Fail(string.Join('\n', lines));
    }
}
