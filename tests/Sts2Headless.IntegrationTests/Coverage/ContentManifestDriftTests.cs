using Sts2Headless.Commands;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests.Coverage;

// Drift guard for the generated *Id.g.cs manifests in
// src/Sts2Headless.Protocol/.
//
// Each test (a) walks ModelDb fresh via GenerateContentIdsCommand's
// reusable enumeration logic, (b) compares the resulting set to the on-
// disk manifest's {Kind}IdNames.AllWireNames collection. A mismatch means
// either:
//
//   * sts2.dll has been bumped (vendor/) but `just generate-content-ids`
//     wasn't re-run, so the committed enum is stale and downstream code
//     compiled against a phantom set of ids.
//   * The generator's enumeration strategy for that kind has drifted (a
//     new ModelDb.AllX shape, a renamed namespace, etc.) and the test
//     needs an update to match.
//
// Either way the resolution is a deterministic one-command fix:
//   just generate-content-ids
// followed by a re-run.
//
// We never assert specific counts or specific ids — those are facts about
// the proprietary game content (same rationale as CardMechanicsCoverage-
// Tests' deliberate omission of count assertions). The test compares two
// sets sourced from the same proprietary DLL and fails on inequality only.
[Collection(InProcessSts2Collection.Name)]
public class ContentManifestDriftTests : IClassFixture<ContentManifestFixture>
{
    private readonly ContentManifestFixture _f;
    public ContentManifestDriftTests(ContentManifestFixture f) { _f = f; }

    [Fact] public void CardManifest_IsInSyncWithModelDb()         => AssertInSync("Card",        CardIdNames.AllWireNames);
    [Fact] public void RelicManifest_IsInSyncWithModelDb()        => AssertInSync("Relic",       RelicIdNames.AllWireNames);
    [Fact] public void PotionManifest_IsInSyncWithModelDb()       => AssertInSync("Potion",      PotionIdNames.AllWireNames);
    [Fact] public void MonsterManifest_IsInSyncWithModelDb()      => AssertInSync("Monster",     MonsterIdNames.AllWireNames);
    [Fact] public void EncounterManifest_IsInSyncWithModelDb()    => AssertInSync("Encounter",   EncounterIdNames.AllWireNames);
    [Fact] public void EventManifest_IsInSyncWithModelDb()        => AssertInSync("Event",       EventIdNames.AllWireNames);
    [Fact] public void PowerManifest_IsInSyncWithModelDb()        => AssertInSync("Power",       PowerIdNames.AllWireNames);
    [Fact] public void AfflictionManifest_IsInSyncWithModelDb()   => AssertInSync("Affliction",  AfflictionIdNames.AllWireNames);
    [Fact] public void ModifierManifest_IsInSyncWithModelDb()     => AssertInSync("Modifier",    ModifierIdNames.AllWireNames);
    [Fact] public void EnchantmentManifest_IsInSyncWithModelDb()  => AssertInSync("Enchantment", EnchantmentIdNames.AllWireNames);
    [Fact] public void OrbManifest_IsInSyncWithModelDb()          => AssertInSync("Orb",         OrbIdNames.AllWireNames);

    // CardOriginPool maps cardId → pool category (Ironclad / Regent /
    // Curse / …). Drift here means the generated map is stale relative
    // to ModelDb.AllCardPools — e.g. a new Regent card landed but
    // CardOriginPool.g.cs still claims Unknown. Same one-command fix as
    // the *Id manifests: `just generate-content-ids`.
    [Fact]
    public void CardOriginPoolManifest_IsInSyncWithModelDb()
    {
        var fresh = GenerateContentIdsCommand.EnumerateCardPoolMembership(_f.ModelDbType);
        var diff = new System.Collections.Generic.List<string>();
        foreach (var (cardId, expected) in fresh)
        {
            var actual = CardOriginPools.OfCard(cardId).ToString();
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
                diff.Add($"{cardId}: disk={actual} fresh={expected}");
        }
        if (diff.Count > 0)
            Assert.Fail($"CardOriginPool drift — re-run `just generate-content-ids`. "
                + $"First {Math.Min(8, diff.Count)} of {diff.Count}: [{string.Join("; ", diff.Take(8))}].");
    }

    private void AssertInSync(string kind, System.Collections.Generic.IReadOnlyCollection<string> onDisk)
    {
        var spec = GenerateContentIdsCommand.Kinds.SingleOrDefault(k => k.Kind == kind)
            ?? throw new InvalidOperationException($"no KindSpec for '{kind}' — generator is out of sync with tests");
        var fresh = GenerateContentIdsCommand.EnumerateIds(spec, _f.ModelDbType, _f.ContentById);

        var diskSet = new SortedSet<string>(onDisk, StringComparer.Ordinal);
        var freshSet = new SortedSet<string>(fresh, StringComparer.Ordinal);

        var inFreshOnly = freshSet.Except(diskSet, StringComparer.Ordinal).ToList();
        var inDiskOnly = diskSet.Except(freshSet, StringComparer.Ordinal).ToList();

        if (inFreshOnly.Count == 0 && inDiskOnly.Count == 0) return;

        var msg = $"{kind}Id manifest drift — re-run `just generate-content-ids`.";
        if (inFreshOnly.Count > 0) msg += $" Missing from disk: [{string.Join(", ", inFreshOnly.Take(8))}{(inFreshOnly.Count > 8 ? $" + {inFreshOnly.Count - 8} more" : "")}].";
        if (inDiskOnly.Count > 0)  msg += $" Stale on disk: [{string.Join(", ", inDiskOnly.Take(8))}{(inDiskOnly.Count > 8 ? $" + {inDiskOnly.Count - 8} more" : "")}].";
        Assert.Fail(msg);
    }
}
