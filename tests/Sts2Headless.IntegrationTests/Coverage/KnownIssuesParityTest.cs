using Sts2Headless.MechanicSweep;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests.Coverage;

// Default-running parity guard between SweepKnownIssues (the "we know
// this engine path is broken in headless, classify as KnownUnsafe not
// Crashed" catalog) and the wire-name manifests.
//
// Catalogue rot is the failure mode this catches: someone removes a
// card from the game (or it gets renamed in a GAME_VERSION bump), the
// manifest *Id.g.cs regenerates without it, and SweepKnownIssues now
// references a wire-id that doesn't exist anymore — silently masking
// any future regression in the actual broken path. By failing the
// parity test, we force the cleanup: drop the catalog entry when the
// id leaves the manifest.
//
// Lives next to the other Coverage meta-tests so all
// sweep-registry / known-issues guards run together. No host needed —
// pure static-set comparison.
public class KnownIssuesParityTest
{
    // Each kind in SweepKnownIssues maps to a generated *IdNames class
    // exposing AllWireNames. Listing them explicitly here (rather than
    // reflecting over Sts2Headless.Protocol.Methods) keeps the test
    // self-contained — and any new kind that grows a known-issues row
    // surfaces immediately as a missing-case here.
    public static IEnumerable<object[]> KindUniverses() =>
    [
        ["card",        CardIdNames.AllWireNames.ToHashSet(StringComparer.Ordinal)],
        ["relic",       RelicIdNames.AllWireNames.ToHashSet(StringComparer.Ordinal)],
        ["potion",      PotionIdNames.AllWireNames.ToHashSet(StringComparer.Ordinal)],
        ["event",       EventIdNames.AllWireNames.ToHashSet(StringComparer.Ordinal)],
        ["encounter",   EncounterIdNames.AllWireNames.ToHashSet(StringComparer.Ordinal)],
        ["power",       PowerIdNames.AllWireNames.ToHashSet(StringComparer.Ordinal)],
        ["affliction",  AfflictionIdNames.AllWireNames.ToHashSet(StringComparer.Ordinal)],
        ["enchantment", EnchantmentIdNames.AllWireNames.ToHashSet(StringComparer.Ordinal)],
    ];

    [Fact]
    public void EveryKnownIssue_StillExistsInManifest()
    {
        var universes = KindUniverses()
            .ToDictionary(
                row => (string)row[0],
                row => (HashSet<string>)row[1],
                StringComparer.Ordinal);

        var orphaned = new List<(string Kind, string Id, string Reason)>();
        var unknownKinds = new List<(string Kind, string Id)>();

        foreach (var (kind, id, reason) in SweepKnownIssues.AllEntries())
        {
            if (!universes.TryGetValue(kind, out var universe))
            {
                unknownKinds.Add((kind, id));
                continue;
            }
            if (!universe.Contains(id))
                orphaned.Add((kind, id, reason));
        }

        if (orphaned.Count == 0 && unknownKinds.Count == 0) return;

        var lines = new List<string> { "SweepKnownIssues catalog drifted from the wire manifests." };
        if (orphaned.Count > 0)
        {
            lines.Add("  Orphaned entries (wire-id no longer in the manifest — likely renamed or removed in a GAME_VERSION bump):");
            foreach (var (kind, id, reason) in orphaned.OrderBy(o => o.Kind, StringComparer.Ordinal).ThenBy(o => o.Id, StringComparer.Ordinal))
                lines.Add($"    * [{kind}] {id}  ← {reason}");
            lines.Add("  Action: drop the entry from src/Sts2Headless.MechanicSweep/SweepKnownIssues.cs.");
        }
        if (unknownKinds.Count > 0)
        {
            lines.Add("  Entries with unknown kind (no *IdNames.AllWireNames mapped in KnownIssuesParityTest.KindUniverses):");
            foreach (var (kind, id) in unknownKinds.OrderBy(u => u.Kind, StringComparer.Ordinal).ThenBy(u => u.Id, StringComparer.Ordinal))
                lines.Add($"    * [{kind}] {id}");
            lines.Add("  Action: add the kind's universe to KindUniverses() in this file.");
        }

        Assert.Fail(string.Join('\n', lines));
    }
}
