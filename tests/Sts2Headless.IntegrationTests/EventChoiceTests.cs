using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// run/select_event_option — Neow blessing plus in-run `?`-rooms (sts2's
// PointType.Unknown nodes resolve into an EventRoom on entry, surfacing
// SunkenStatue / Wellspring / etc.). File is named after the wire method
// rather than the event, since both routes share the same select-option
// surface.
//
// In-run events require LocPatches' EventOption.AddLocVars / ToString
// suppressions; without those the ctor NREs on character-asset loc vars
// (Texture2D / StringName fields that ResourceLoader can't populate in
// headless mode). Neow sidesteps that path because its option text-keys
// don't reference the null vars.
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

    [Fact]
    public async Task QuestionMarkRoom_LandsInEventRoom_AndOptionPickAdvances()
    {
        // Seed 1's row-2 layout after the first combat exposes a `?`-node
        // (MapNodeType.Unknown / sts2 PointType.Unknown) at (col 2, row 2).
        // Stepping on it makes sts2 roll the destination → SunkenStatue
        // EventRoom for this seed (other seeds may roll the same Unknown
        // into a CombatRoom instead — `?` is intentionally non-deterministic
        // across seeds, deterministic within one). The patches in LocPatches
        // let the EventOption ctor run without NRE-ing on character-texture
        // loc vars; without them _currentOptions stays empty and this test
        // would surface no options. Picking option 0 (GrabSword) auto-
        // advances back to MapRoom — same wire contract as the Neow
        // PHIAL_HOLSTER path.
        await _host.SendAsync<RunNewResult>("run/new", new RunNewParams(Seed: 1uL));
        var afterCombat = await MapHelpers.WalkPastFirstCombat(_host);
        var mystery = afterCombat.AvailableMapNodes.First(n => n.Type == MapNodeType.Unknown);

        var afterPick = await _host.SendAsync<RunSelectMapNodeResult>(
            "run/select_map_node", new RunSelectMapNodeParams(Col: mystery.Col, Row: mystery.Row));

        Assert.Equal(RoomType.EventRoom, afterPick.CurrentRoomType);
        Assert.Equal(2, afterPick.AvailableEventOptions.Count);
        // Both options should carry a SunkenStatue text-key; the engine
        // generates them via SunkenStatue.GenerateInitialOptions() →
        // EventOption ctors with text-keys like
        // SUNKEN_STATUE.pages.INITIAL.options.GRAB_SWORD/DIVE_INTO_WATER.
        Assert.All(afterPick.AvailableEventOptions, o =>
        {
            Assert.False(string.IsNullOrEmpty(o.TextKey));
            Assert.Contains("SUNKEN_STATUE", o.TextKey!);
        });

        var afterChoose = await _host.SendAsync<RunSelectEventOptionResult>(
            "run/select_event_option", new RunSelectEventOptionParams(OptionIndex: 0));

        Assert.True(afterChoose.Ok);
        Assert.Equal(RoomType.MapRoom, afterChoose.CurrentRoomType);
        Assert.Empty(afterChoose.AvailableEventOptions);
        Assert.NotEmpty(afterChoose.AvailableMapNodes);
    }
}
