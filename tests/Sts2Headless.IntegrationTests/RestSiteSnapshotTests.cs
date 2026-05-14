using Sts2Headless.Agents;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Coverage for the rest-site slice of the snapshot wire surface. The host
// surfaces AvailableRestSiteOptions on every snapshot-bearing result, gated
// to RestSiteRoom (analogous to AvailableEventOptions ↔ EventRoom).
//
// Setup uses the GreedyAgent (from Sts2Headless.Agents) to walk the run
// forward until standing on a rest site. The agent is the canonical greedy
// driver under AD-6; reusing it here avoids duplicating its map/combat/
// rewards logic in a parallel "drive forward" helper that would silently
// drift from the e2e suite.
//
// Discipline:
//   1. We don't pin specific OptionIds — sts2 can rename or reshuffle the
//      rest-site option set without us breaking. The test asserts shape
//      (non-empty, every option has an id and an IsEnabled flag).
//   2. The HEAL exit test confirms the room-transition contract the agent
//      depends on. If sts2 changes HEAL to keep us in the rest room, the
//      test message names that the contract changed.
public class RestSiteSnapshotTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;

    public RestSiteSnapshotTests(HostSubprocess host) => _host = host;

    // Drive the greedy agent until it hits a rest site and surfaces
    // non-empty options. The agent stops *before* selecting, so the
    // returned snapshot is the rest-room-on-entry state we want to assert
    // against.
    private async Task<RunStateResult> WalkToRestSiteEntry()
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

        var transport = new HostSubprocessAgentTransport(_host);
        var agent = new GreedyAgent();
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));

        return await agent.DriveUntilAsync(
            transport,
            stopWhen: s => s.CurrentRoomType == RoomType.RestSiteRoom
                            && s.AvailableRestSiteOptions.Count > 0,
            ct: cts.Token);
    }

    [Fact]
    public async Task WalkToRestSite_SurfacesEnabledOptions_WithStableShape()
    {
        var state = await WalkToRestSiteEntry();

        Assert.Equal(RoomType.RestSiteRoom, state.CurrentRoomType);
        Assert.NotEmpty(state.AvailableRestSiteOptions);
        Assert.All(state.AvailableRestSiteOptions, o =>
            Assert.False(string.IsNullOrEmpty(o.OptionId),
                $"every rest-site option must carry a non-empty OptionId; saw index={o.Index} optionId=\"{o.OptionId}\""));
        Assert.True(
            state.AvailableRestSiteOptions.Any(o => o.IsEnabled),
            $"reached RestSiteRoom but no options are enabled. Options seen: " +
            $"[{string.Join(", ", state.AvailableRestSiteOptions.Select(o => $"{o.OptionId}({(o.IsEnabled ? "on" : "off")})"))}]. " +
            "Either the seed lands on a degenerate rest site or IsEnabled is wired wrong.");
    }

    [Fact]
    public async Task SelectHeal_ExitsRestSite_ToMapRoom()
    {
        var entry = await WalkToRestSiteEntry();

        // Find HEAL by substring so a rename (e.g. "HEAL_FULL") still hits.
        var heal = entry.AvailableRestSiteOptions.FirstOrDefault(o =>
            o.IsEnabled
            && o.OptionId.Contains("HEAL", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(heal);

        var hpBefore = entry.Hp;
        var afterPick = await _host.SendAsync<RunSelectRestSiteOptionResult>(
            "run/select_rest_site_option",
            new RunSelectRestSiteOptionParams(OptionIndex: heal!.Index));

        // HEAL exits to MapRoom (engine auto-advance). Either the response
        // itself reports MapRoom, or a follow-up run/state does — accept both
        // because the engine's exact transition timing isn't a wire contract
        // worth pinning here.
        var finalRoom = afterPick.CurrentRoomType == RoomType.MapRoom
            ? afterPick.CurrentRoomType
            : (await _host.SendAsync<RunStateResult>("run/state")).CurrentRoomType;
        Assert.Equal(RoomType.MapRoom, finalRoom);

        // Soft sanity: HP didn't drop (HEAL might no-op at full HP).
        var post = await _host.SendAsync<RunStateResult>("run/state");
        Assert.True(post.Hp >= hpBefore,
            $"HP dropped from {hpBefore} to {post.Hp} after HEAL; HEAL must not be lossy.");
    }
}

// Adapter so the agent can drive the integration-test HostSubprocess.
// Mirrors End2EndTests/HostSubprocessTransport; kept local because each
// project owns its own xUnit test infrastructure.
internal sealed class HostSubprocessAgentTransport(HostSubprocess host) : ITransport
{
    public Task<TResult> SendAsync<TResult>(string method, object? @params = null) =>
        host.SendAsync<TResult>(method, @params);
}
