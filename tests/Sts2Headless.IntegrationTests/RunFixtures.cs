using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.IntegrationTests;

// Shared run-start helpers. Every STS2 run begins at the Neow blessing
// EventRoom (no opt-out — that's how the game works), so tests that
// just want a fresh run "at the map" need a Neow-dismissal step. This
// helper encapsulates the pattern; mirrors HeuristicAgent's default
// DecideEvent strategy (pick the last unlocked option — sts2 events
// conventionally place the "Leave / Decline" choice last).
//
// The host has graceful recovery for Neow picks whose card-selection
// side effects it can't yet service (Sts2Bindings.Events.AutoAdvance-
// FinishedEvent force-transitions to MapRoom). So even seeds whose
// last Neow option triggers card-selection still land at MapRoom —
// just without the Neow relic on those seeds. Tests that depend on a
// specific Neow relic should drive `run/select_event_option` directly.
internal static class RunFixtures
{
    // Start a fresh run and dismiss Neow. Returns the snapshot after
    // dismissal — `CurrentRoomType` is normally MapRoom (floor 1), but
    // callers should treat it as "wherever the engine landed after the
    // Neow choice resolved" rather than asserting hard.
    public static async Task<RunStateResult> StartFreshRunAtMap(
        HostSubprocess host,
        Character? character = null,
        ulong? seed = null,
        int? ascension = null,
        IReadOnlyList<ModifierId>? modifiers = null)
    {
        await host.SendAsync<RunNewResult>("run/new", new RunNewParams(
            Character: character,
            Seed: seed,
            Ascension: ascension,
            Modifiers: modifiers));
        return await DismissNeow(host);
    }

    // RecordingHost overload — same shape, different host type. Recording
    // tests need this so the .mcr + run.json artefacts cover the Neow
    // pick the same way a manual run would.
    public static async Task<RunStateResult> StartFreshRunAtMap(
        RecordingHost host,
        Character? character = null,
        ulong? seed = null,
        int? ascension = null,
        IReadOnlyList<ModifierId>? modifiers = null)
    {
        await host.SendAsync<RunNewResult>("run/new", new RunNewParams(
            Character: character,
            Seed: seed,
            Ascension: ascension,
            Modifiers: modifiers));
        return await DismissNeow(host);
    }

    // Advance past the Neow blessing pick. No-op if the host already
    // landed past the event (e.g. graceful recovery already transitioned
    // to MapRoom). After the pick the room may still be EventRoom with
    // `IsFinished=true` and no event options — in that shape we issue
    // `run/proceed_event` (mirrors HeuristicAgent.DecideEventFinished).
    public static Task<RunStateResult> DismissNeow(HostSubprocess host) =>
        DismissNeowCore(
            stateAsync: () => host.SendAsync<RunStateResult>("run/state"),
            pickAsync: i => host.SendAsync<RunSelectEventOptionResult>(
                "run/select_event_option", new RunSelectEventOptionParams(OptionIndex: i)),
            proceedAsync: () => host.SendAsync<RunProceedEventResult>("run/proceed_event"));

    // RecordingHost overload — see HostSubprocess overload doc.
    public static Task<RunStateResult> DismissNeow(RecordingHost host) =>
        DismissNeowCore(
            stateAsync: () => host.SendAsync<RunStateResult>("run/state"),
            pickAsync: i => host.SendAsync<RunSelectEventOptionResult>(
                "run/select_event_option", new RunSelectEventOptionParams(OptionIndex: i)),
            proceedAsync: () => host.SendAsync<RunProceedEventResult>("run/proceed_event"));

    private static async Task<RunStateResult> DismissNeowCore(
        Func<Task<RunStateResult>> stateAsync,
        Func<int, Task<RunSelectEventOptionResult>> pickAsync,
        Func<Task<RunProceedEventResult>> proceedAsync)
    {
        var state = await stateAsync();
        if (state.CurrentRoomType != RoomType.EventRoom) return state;

        if (state.AvailableEventOptions.Count > 0)
        {
            EventOption? pick = null;
            for (var i = state.AvailableEventOptions.Count - 1; i >= 0; i--)
            {
                if (!state.AvailableEventOptions[i].IsLocked)
                {
                    pick = state.AvailableEventOptions[i];
                    break;
                }
            }
            pick ??= state.AvailableEventOptions[^1];
            await pickAsync(pick.Index);
            state = await stateAsync();
        }

        // After the option resolves the engine usually auto-advances to
        // MapRoom; some option text paths leave us in a finished EventRoom
        // until proceed_event drives RunManager.ProceedFromTerminalRewardsScreen.
        if (state.CurrentRoomType == RoomType.EventRoom
            && state.AvailableEventOptions.Count == 0)
        {
            await proceedAsync();
            state = await stateAsync();
        }

        return state;
    }
}
