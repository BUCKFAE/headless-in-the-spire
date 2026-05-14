using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// run/select_event_option — the Neow blessing is currently the only event we
// can reliably reach in tests (withNeow=true at run/new). When we wire more
// rooms that branch into events (?-rooms during a run), additional scenarios
// belong here; the file is named after the wire method, not the event.
//
// Shares one HostSubprocess across the class via IClassFixture: every test
// starts with run/new, which resets the prior RunManager via Sts2Bindings.
public class EventChoiceTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;

    public EventChoiceTests(HostSubprocess host) => _host = host;

    [Fact]
    public async Task RunNew_WithNeow_Surfaces_EventOptions()
    {
        var result = await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Seed: 1uL, WithNeow: true));

        Assert.Equal(RoomType.EventRoom, result.CurrentRoomType);
        Assert.NotEmpty(result.AvailableEventOptions);
        // Each option should have stable indices [0..N-1] and a non-null text
        // key (Neow's options are all loc-bound). Locked options are
        // permitted but the Neow first-page picks all start unlocked.
        for (var i = 0; i < result.AvailableEventOptions.Count; i++)
        {
            var opt = result.AvailableEventOptions[i];
            Assert.Equal(i, opt.Index);
            Assert.False(string.IsNullOrEmpty(opt.TextKey),
                $"option {i} has empty TextKey — Neow options should all be loc-bound");
        }
        // No map choices while at an event room — the snapshot gates these
        // on currentRoomType so they shouldn't leak through.
        Assert.Empty(result.AvailableMapNodes);
    }

    [Fact]
    public async Task SelectEventOption_AdvancesPastNeow_ToMapRoom()
    {
        // Pick the option whose text-key contains PHIAL_HOLSTER. Other Neow
        // options (LEAD_PAPERWEIGHT, PRECARIOUS_SHEARS) trigger card-selection
        // side effects the host can't yet service — those paths are listed
        // in CLAUDE.md as deferred work. PHIAL_HOLSTER finishes cleanly,
        // which is enough to verify the full pick → auto-advance flow.
        var start = await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Seed: 1uL, WithNeow: true));
        var picks = start.AvailableEventOptions
            .Where(o => o.TextKey is not null && o.TextKey.Contains("PHIAL_HOLSTER"))
            .ToList();
        Assert.NotEmpty(picks);

        var after = await _host.SendAsync<RunSelectEventOptionResult>(
            "run/select_event_option",
            new RunSelectEventOptionParams(OptionIndex: picks[0].Index));

        Assert.True(after.Ok);
        // After choosing, the host auto-advances out of the EventRoom via
        // ProceedFromTerminalRewardsScreen → EnterRoom(MapRoom). The caller
        // should land on the map with the floor-1 next moves available.
        Assert.Equal(RoomType.MapRoom, after.CurrentRoomType);
        Assert.False(after.IsGameOver);
        Assert.NotEmpty(after.AvailableMapNodes);
        Assert.Empty(after.AvailableEventOptions);
        Assert.Equal(1, after.ActFloor);

        // Session reflects the transition.
        var state = await _host.SendAsync<RunStateResult>("run/state");
        Assert.Equal(RoomType.MapRoom, state.CurrentRoomType);
        Assert.Empty(state.AvailableEventOptions);
    }

    [Fact]
    public async Task SelectEventOption_OutOfRange_ReturnsInternalError()
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Seed: 1uL, WithNeow: true));

        var error = await _host.ExpectErrorAsync(
            "run/select_event_option",
            new RunSelectEventOptionParams(OptionIndex: 99));

        Assert.Equal(-32603, error.Code);
        Assert.Contains("out of range", error.Message);
    }

    [Fact]
    public async Task SelectEventOption_NotInEventRoom_ReturnsInternalError()
    {
        // From a MapRoom (no Neow), picking an event option is meaningless.
        // The bindings raise InvalidOperationException on the null Event;
        // surface that as an internal error so callers can't drift state.
        await _host.SendAsync<RunNewResult>("run/new", new RunNewParams(Seed: 1uL));

        var error = await _host.ExpectErrorAsync(
            "run/select_event_option",
            new RunSelectEventOptionParams(OptionIndex: 0));

        Assert.Equal(-32603, error.Code);
    }
}
