using Sts2Headless.Content;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Coverage for the per-act content/* methods. These resolve their act via
// the live run (so run/new is a prerequisite), but the data they emit
// describes the *pool* the engine drafts from — content, not roll state —
// so we only need the run to exist; we don't need to traverse the map.
public class ContentActTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;
    public ContentActTests(HostSubprocess host) => _host = host;

    private async Task EnsureRun()
    {
        await RunFixtures.StartFreshRunAtMap(_host, character: Character.Ironclad, seed: 42uL);
    }

    [Fact]
    public async Task EncounterRules_ReportsCanonicalRules()
    {
        // Static rules — same answer regardless of run state. Pin the
        // engine's current truths (weakEncountersFirst=true,
        // eliteRollCount=15, noAdjacentSharedTags=true) so a future
        // engine change here surfaces loudly.
        var result = await _host.SendAsync<ContentEncounterRulesResult>(
            "content/encounter_rules");
        Assert.True(result.Ok);
        Assert.True(result.WeakEncountersFirst);
        Assert.Equal(15, result.EliteRollCount);
        Assert.True(result.NoAdjacentSharedTags);
        Assert.False(string.IsNullOrWhiteSpace(result.Notes));
    }

    [Fact]
    public async Task UnknownNodeOdds_AfterRunNew_ReturnsNonEmpty()
    {
        await EnsureRun();
        var result = await _host.SendAsync<ContentUnknownNodeOddsResult>(
            "content/unknown_node_odds", new ContentUnknownNodeOddsParams());
        Assert.True(result.Ok);
        // The fallback table reports 5 rows (CombatRoom, EventRoom,
        // TreasureRoom, MerchantRoom, RestSiteRoom); a live read may
        // surface more or fewer. Pin >= 3 so the test doesn't bind to
        // an exact count.
        Assert.True(result.BaseOdds.Count >= 3,
            $"expected at least 3 odds rows, got {result.BaseOdds.Count}");
        Assert.All(result.BaseOdds, row => Assert.True(row.Weight > 0));
    }

    // The next three tests are skipped pending a fix to ContentReader.FindAct.
    // Per-act tests source the current act index from
    // RunState.CurrentActIndex (the bound `_runStateCurrentActIndex`) —
    // the ActModel itself doesn't carry an Index property at the
    // current pin. Always run from a fresh seed-42 Ironclad run so
    // actIndex=0 is meaningful.

    [Fact]
    public async Task DescribeAct_Act1_ReturnsBossPool()
    {
        await EnsureRun();
        var result = await _host.SendAsync<ContentDescribeActResult>(
            "content/describe_act", new ContentDescribeActParams(ActIndex: 0));
        Assert.True(result.Ok);
        Assert.Equal(0, result.ActIndex);
        Assert.NotEmpty(result.BossPool);
    }

    [Fact]
    public async Task ListEventsForAct_Act1_ReturnsNonEmpty()
    {
        await EnsureRun();
        var result = await _host.SendAsync<ContentListEventsForActResult>(
            "content/list_events_for_act",
            new ContentListEventsForActParams(ActIndex: 0));
        Assert.True(result.Ok);
        Assert.Equal(0, result.ActIndex);
        Assert.NotEmpty(result.Events);
        Assert.All(result.Events, e => Assert.False(string.IsNullOrWhiteSpace(e.Id)));
    }

    [Fact]
    public async Task ListEncountersForAct_Act1_EliteTier_FiltersToElite()
    {
        await EnsureRun();
        var result = await _host.SendAsync<ContentListEncountersForActResult>(
            "content/list_encounters_for_act",
            new ContentListEncountersForActParams(ActIndex: 0, Tier: EncounterTier.Elite));
        Assert.True(result.Ok);
        Assert.NotEmpty(result.Encounters);
        Assert.All(result.Encounters, e => Assert.Equal(EncounterTier.Elite, e.Tier));
    }
}
