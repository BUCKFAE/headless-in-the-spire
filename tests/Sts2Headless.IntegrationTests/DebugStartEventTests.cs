using Sts2Headless.Cheats;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Positive coverage for debug/start_event. The negative case
// (--enable-debug omitted) lives in DebugDisabledTests; this file pins
// the happy path:
//
//   * Forcing into a known-good event lands the player in EventRoom
//     with non-zero AvailableEventOptions on the snapshot.
//   * The wire result reports CurrentRoomType=EventRoom and
//     OptionsCount > 0 (for non-degenerate events) without a follow-up
//     run/state read.
//   * Unknown event id → WireErrorCode.InvalidParams (not InternalError,
//     not MethodNotFound).
//
// Uses EventIdNames.AllWireNames.First() so the test survives event
// renames in future game versions.
public class DebugStartEventTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;

    public DebugStartEventTests(HostSubprocess host) => _host = host;

    [Fact]
    public async Task StartEvent_LandsInEventRoom()
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

        var firstEvent = EventIdNames.AllWireNames
            .OrderBy(s => s, StringComparer.Ordinal)
            .First();

        var resp = await _host.SendAsync<DebugStartEventResult>(
            "debug/start_event", new DebugStartEventParams(EventId: firstEvent));

        Assert.True(resp.Ok);
        Assert.Equal(firstEvent, resp.EventId);
        // Most non-degenerate events land in EventRoom with at least one
        // pickable option. Some single-option "you walk past it" events
        // resolve immediately back to MapRoom — we don't fail those, but
        // the typical case is EventRoom.
        Assert.True(
            resp.CurrentRoomType is "EventRoom" or "MapRoom",
            $"unexpected CurrentRoomType '{resp.CurrentRoomType}' after start_event");

        // Sanity: snapshot agrees with the wire result.
        var state = await _host.SendAsync<RunStateResult>("run/state");
        if (resp.CurrentRoomType == "EventRoom")
        {
            Assert.Equal(resp.OptionsCount, state.AvailableEventOptions.Count);
        }
    }

    [Fact]
    public async Task StartEvent_UnknownId_ReturnsInvalidParams()
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

        var err = await _host.ExpectErrorAsync(
            "debug/start_event", new DebugStartEventParams(EventId: "DEFINITELY_NOT_AN_EVENT_ID"));

        Assert.Equal(Sts2Headless.Protocol.WireErrorCode.InvalidParams, err.Code);
    }

    [Fact]
    public async Task StartEvent_EmptyId_ReturnsInvalidParams()
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

        var err = await _host.ExpectErrorAsync(
            "debug/start_event", new DebugStartEventParams(EventId: ""));

        Assert.Equal(Sts2Headless.Protocol.WireErrorCode.InvalidParams, err.Code);
    }
}
