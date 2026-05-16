using Sts2Headless.Cheats;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Coverage for the relics slice of the snapshot wire surface. Relics live on
// every snapshot-bearing result (run/new, run/state, run/select_*, run/end_turn,
// run/play_card, run/select_reward, run/skip_reward) because they're run-
// scoped state, unlike combatState / rewardsState which are room-scoped.
//
// "Won't break in the future" discipline:
//   1. We don't assert exact relic ids — the starter relic and any rebalance
//      pick is matched by substring ("BURNING_BLOOD") so a rename surfaces a
//      legible diagnostic instead of a silent miss.
//   2. We don't pin the relic *count* on a fresh run — sts2 may add a second
//      starter pin (anvil/wristband/etc.) in a patch; we assert "starter is
//      present" not "the bag has exactly one element".
//   3. The failure path emits the full id list so a future engineer reading a
//      red CI log knows which ids did surface.
//
// Shares one HostSubprocess via IClassFixture; every test starts with run/new.
public class RelicsSnapshotTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;

    public RelicsSnapshotTests(HostSubprocess host) => _host = host;

    [Fact]
    public async Task RunNew_Ironclad_SurfacesStarterRelic_OnFirstSnapshot()
    {
        // Ironclad's starter relic is Burning Blood — granted during
        // RunManager.FinalizeStartingRelics, which runs as part of
        // StartIroncladRun before run/new returns. The first snapshot must
        // already carry it; otherwise a client driving the wire would never
        // see relics granted via this path.
        var start = await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

        Assert.NotEmpty(start.Relics);
        Assert.True(
            start.Relics.Any(r => r.Id.Contains("BURNING_BLOOD", StringComparison.OrdinalIgnoreCase)),
            $"Ironclad starter relic (BURNING_BLOOD) not found in run/new snapshot. " +
            $"Relics seen: [{string.Join(", ", start.Relics.Select(r => r.Id))}]. " +
            $"If the starter relic was renamed or replaced, update the substring " +
            $"match in this test to target the replacement.");
        Assert.All(start.Relics, r =>
            Assert.False(string.IsNullOrEmpty(r.Id),
                "every surfaced relic should carry a non-empty id"));
    }

    [Fact]
    public async Task DebugGiveRelic_AppendsToRelicsList_OnNextSnapshot()
    {
        // debug/give_relic uses RelicCmd.Obtain — same engine path the
        // RelicReward picker takes — so a granted relic must show up in
        // Player.Relics by the time the next snapshot is read. This pins
        // the wire contract: clients can drive give_relic and then trust
        // run/state to reflect the new bag.
        var start = await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));
        var idsBefore = start.Relics.Select(r => r.Id).ToHashSet(StringComparer.Ordinal);

        var afterInject = await _host.SendAsync<DebugGiveRelicResult>(
            "debug/give_relic", new DebugGiveRelicParams(RelicId: "LUCKY_FYSH"));
        Assert.True(afterInject.Ok);

        var afterState = await _host.SendAsync<RunStateResult>("run/state");
        Assert.NotEmpty(afterState.Relics);

        // Set-diff rather than count-pin: a future change that grants a
        // bonus relic alongside the explicit one (or normalises ids) still
        // passes as long as the requested id is in the bag.
        var newIds = afterState.Relics
            .Where(r => !idsBefore.Contains(r.Id))
            .Select(r => r.Id)
            .ToList();
        Assert.True(
            newIds.Any(id => id.Contains("LUCKY_FYSH", StringComparison.OrdinalIgnoreCase)),
            $"debug/give_relic LUCKY_FYSH should surface a new entry on the next snapshot; " +
            $"before=[{string.Join(",", idsBefore)}], " +
            $"after=[{string.Join(",", afterState.Relics.Select(r => r.Id))}].");
    }

    [Fact]
    public async Task Relics_PersistAcrossSnapshots_BetweenRunNewAndRunState()
    {
        // The wire contract: snapshot-bearing results all carry relics, and
        // back-to-back snapshots (run/new → run/state, no mutations between)
        // should agree on the bag. A drift here would signal the snapshot is
        // re-reading sts2 in a way that loses state (cache miss, owner re-
        // assignment, etc.).
        var start = await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));
        var state = await _host.SendAsync<RunStateResult>("run/state");

        var startIds = start.Relics.Select(r => r.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();
        var stateIds = state.Relics.Select(r => r.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();
        Assert.Equal(startIds, stateIds);
    }
}
