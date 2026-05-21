using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Runtime;

// Event-room operations. Wire flow:
//   * snapshot surfaces AvailableEventOptions (via ReadAvailableEventOptions);
//   * caller picks an index via SelectEventOption → EventOption.Chosen();
//   * if the engine resolves the event without flipping CurrentRoom, the
//     auto-advance fallback (AutoAdvanceFinishedEvent) forces EnterRoom(MapRoom);
//   * ProceedEvent is the manual escape hatch when CurrentRoom stays EventRoom
//     with IsFinished=true and no auto-advance fired.
//
// All members are backed by the `_event*` / `_runManager*` fields declared
// in Sts2Bindings.cs.
public sealed partial class Sts2Bindings
{
    // Walk RunManager.EventSynchronizer → GetLocalEvent → CurrentOptions and
    // shape each option into the wire record. Mirrors sts2-cli's
    // EventChoiceState reduced to the data fields we surface — the loc-lookup
    // and dynamic-vars layers stay client-side, keeping the host loc-free.
    //
    // Returns [] when:
    // - EventSynchronizer is null (e.g. the engine cleared it between rooms);
    // - GetLocalEvent returns null (no event currently live);
    // - IsFinished is true (the event has resolved but the room hasn't flipped yet);
    // - CurrentOptions is null/empty.
    // Each "[]" case means a caller-visible event isn't actually pickable;
    // the room transition is the engine's responsibility to drive.
    private IReadOnlyList<EventOption> ReadAvailableEventOptions(object runManager)
    {
        var sync = _runManagerEventSynchronizer.GetValue(runManager);
        if (sync is null) return Array.Empty<EventOption>();

        var localEvent = _eventSyncGetLocalEvent.Invoke(sync, null);
        if (localEvent is null) return Array.Empty<EventOption>();

        if ((bool)_eventIsFinished.GetValue(localEvent)!) return Array.Empty<EventOption>();

        if (_eventCurrentOptions.GetValue(localEvent) is not System.Collections.IList options
            || options.Count == 0)
        {
            return Array.Empty<EventOption>();
        }

        var result = new List<EventOption>(options.Count);
        for (var i = 0; i < options.Count; i++)
        {
            var opt = options[i];
            if (opt is null) continue;
            var textKey = _eventOptionTextKey?.GetValue(opt) as string;
            var isLocked = (bool)_eventOptionIsLocked.GetValue(opt)!;
            result.Add(new EventOption(i, textKey, isLocked));
        }
        return result;
    }

    // True when the engine considers the current event resolved but the
    // room hasn't transitioned back to MapRoom yet. The wire surface
    // signals this state by reporting CurrentRoomType=EventRoom with an
    // empty AvailableEventOptions list; callers can confirm via this
    // helper (used by ProceedEvent's caller-guard).
    private bool IsLocalEventFinished(object runManager)
    {
        var sync = _runManagerEventSynchronizer.GetValue(runManager);
        if (sync is null) return false;
        var localEvent = _eventSyncGetLocalEvent.Invoke(sync, null);
        if (localEvent is null) return false;
        return (bool)_eventIsFinished.GetValue(localEvent)!;
    }

    // Finished-event auto-advance. When an event resolves (sts2 sets
    // IsFinished=true on the local event), the engine sometimes does
    // NOT flip CurrentRoom back to MapRoom on its own — observed on
    // seed 42 Act 3 floor 10 (FakeMerchant). The wire surface signals
    // this state by reporting CurrentRoomType=EventRoom with an empty
    // AvailableEventOptions list; this method drives the transition
    // explicitly, mirroring sts2-cli's `Leave` pattern at
    // RunSimulator.cs:1626-1646:
    //   1. RunManager.ProceedFromTerminalRewardsScreen() — graceful
    //      exit through the engine's natural reward-screen handler.
    //   2. If still in EventRoom afterward, force EnterRoom(MapRoom) —
    //      the engine occasionally leaves the room half-transitioned
    //      and the explicit EnterRoom snaps it back to the map.
    //
    // Caller guard: rejects unless CurrentRoom is EventRoom AND the
    // local event reports IsFinished=true. Anything else is a caller
    // mistake — events with active options should be advanced via
    // run/select_event_option, not this method.
    public void ProceedEvent(RunHandle handle)
    {
        var room = _runStateCurrentRoom.GetValue(handle.RunState)
            ?? throw new InvalidOperationException("RunState.CurrentRoom was null");
        var roomTypeName = room.GetType().Name;
        var roomType = Enum.TryParse<RoomType>(roomTypeName, ignoreCase: false, out var parsedRoom)
            ? parsedRoom
            : RoomType.Unknown;
        if (roomType != RoomType.EventRoom)
        {
            throw new InvalidOperationException(
                $"run/proceed_event called but current room is {roomTypeName}, not EventRoom. " +
                $"Only legal when an event has finished and the room hasn't auto-transitioned.");
        }
        if (!IsLocalEventFinished(handle.RunManager))
        {
            throw new InvalidOperationException(
                "run/proceed_event called but the local event is not finished. " +
                "Advance the event via run/select_event_option first.");
        }

        if (_runManagerProceedFromTerminalRewards is not null)
        {
            try
            {
                var proceed = _runManagerProceedFromTerminalRewards.Invoke(handle.RunManager, null);
                if (proceed is Task pt) pt.GetAwaiter().GetResult();
                _syncCtx?.Pump();
            }
            catch { /* sts2-cli also swallows; the EnterRoom fallback below covers it */ }
        }

        // Re-read CurrentRoom — if Proceed succeeded the room has flipped
        // to MapRoom and we're done. Otherwise force the transition.
        var roomAfter = _runStateCurrentRoom.GetValue(handle.RunState);
        var stillEvent = roomAfter is not null
            && Enum.TryParse<RoomType>(roomAfter.GetType().Name, ignoreCase: false, out var parsedAfter)
            && parsedAfter == RoomType.EventRoom;
        if (stillEvent && _runManagerEnterRoom is not null && _mapRoomType is not null)
        {
            try
            {
                var mapRoom = Activator.CreateInstance(_mapRoomType)
                    ?? throw new InvalidOperationException($"{_mapRoomType.FullName} default ctor returned null");
                var enter = _runManagerEnterRoom.Invoke(handle.RunManager, new[] { mapRoom });
                if (enter is Task et) et.GetAwaiter().GetResult();
                _syncCtx?.Pump();
            }
            catch { /* caller sees CurrentRoomType=EventRoom on the next snapshot and can decide */ }
        }
        DrainActionExecutor(handle);
    }

    // Fire EventOption.Chosen() for the option at `optionIndex` on the
    // currently-live event. No-op if no event is live or the index is out of
    // range — the caller will see the unchanged AvailableEventOptions in the
    // next snapshot and can decide how to recover. Mirrors sts2-cli's
    // DoChooseOption(EventRoom) path, minus the card-reward / pending-bundles
    // detection (those gates don't yet exist on our wire).
    public void SelectEventOption(RunHandle handle, int optionIndex)
    {
        var sync = _runManagerEventSynchronizer.GetValue(handle.RunManager)
            ?? throw new InvalidOperationException("RunManager.EventSynchronizer was null");
        var localEvent = _eventSyncGetLocalEvent.Invoke(sync, null)
            ?? throw new InvalidOperationException("EventSynchronizer.GetLocalEvent returned null");
        if ((bool)_eventIsFinished.GetValue(localEvent)!)
        {
            throw new InvalidOperationException("event is already finished");
        }
        if (_eventCurrentOptions.GetValue(localEvent) is not System.Collections.IList options)
        {
            throw new InvalidOperationException("event has no CurrentOptions");
        }
        if (optionIndex < 0 || optionIndex >= options.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(optionIndex),
                $"optionIndex {optionIndex} out of range; event has {options.Count} options");
        }

        var option = options[optionIndex]
            ?? throw new InvalidOperationException($"event option at index {optionIndex} was null");

        // Chosen() is synchronous in the no-card-reward case (Neow's GAIN
        // OBOL etc.). Options that branch into card selection
        // (CardSelectCmd.From*, NSimpleCardSelectScreen.Create) load .tscn
        // assets that aren't present in headless — Create returns null,
        // the event-model body NREs (or throws ArgumentNullException via
        // LINQ on a null collection downstream). HangPatches turns the
        // factory NRE into `await null`, but the model-layer dereference
        // still throws. Rather than let one bad option kill the host,
        // catch the exception and force-advance the room: AutoAdvance's
        // EnterRoom(MapRoom) fallback flips the room type even when
        // IsFinished is still false. The player keeps their pre-event HP/
        // gold (the event's gameplay effect is partially or wholly
        // skipped), the agent continues. This is identical-shape recovery
        // to what AutoAdvance already does post-successful-pick; the
        // try-catch just extends it to "engine threw mid-Chosen."
        Exception? chosenError = null;
        try
        {
            var result = _eventOptionChosen.Invoke(option, null);
            if (result is Task t) t.GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            chosenError = ex.InnerException ?? ex;
        }

        // sts2-cli pattern: after the event finishes, the game still leaves
        // CurrentRoom as the EventRoom until something nudges the next
        // transition. Without this, the wire reports CurrentRoomType=EventRoom
        // forever and the caller has no way to leave. Mirror sts2-cli's force-
        // transition (ProceedFromTerminalRewardsScreen → EnterRoom(MapRoom))
        // so a successful pick — or a recovered failure — lands the player
        // back on the map.
        var nowFinished = (bool)_eventIsFinished.GetValue(localEvent)!;
        if (nowFinished || chosenError is not null)
        {
            AutoAdvanceFinishedEvent(handle.RunManager, handle.RunState);
        }

        if (chosenError is not null)
        {
            // Verify the recovery actually moved us off EventRoom; if not
            // the engine is in a state we can't reason about and surfacing
            // the original exception is the honest answer.
            var currentName = _runStateCurrentRoom.GetValue(handle.RunState)?.GetType().Name;
            if (currentName == "EventRoom")
            {
                throw new InvalidOperationException(
                    $"EventOption.Chosen() threw and AutoAdvance failed to leave EventRoom. " +
                    $"Original error: {chosenError.GetType().Name}: {chosenError.Message}",
                    chosenError);
            }
        }
    }

    private void AutoAdvanceFinishedEvent(object runManager, object runState)
    {
        if (_runManagerProceedFromTerminalRewards is not null)
        {
            try
            {
                var proceed = _runManagerProceedFromTerminalRewards.Invoke(runManager, null);
                if (proceed is Task pt) pt.GetAwaiter().GetResult();
            }
            catch { /* sts2-cli also swallows — terminal-rewards may not apply */ }
        }

        // Only force-enter the map if we're still in a room that won't
        // resolve on its own: a finished Event whose room didn't flip, or a
        // CombatRoom whose combat ended but didn't auto-transition. For
        // rooms that resolve naturally (e.g. an event that leaves the
        // player at a treasure room) we don't override the engine.
        var currentName = _runStateCurrentRoom.GetValue(runState)?.GetType().Name;
        var stillStuck = currentName is "EventRoom" or "CombatRoom";
        if (stillStuck && _runManagerEnterRoom is not null && _mapRoomType is not null)
        {
            try
            {
                var mapRoom = Activator.CreateInstance(_mapRoomType)
                    ?? throw new InvalidOperationException($"{_mapRoomType.FullName} default ctor returned null");
                var enter = _runManagerEnterRoom.Invoke(runManager, new[] { mapRoom });
                if (enter is Task et) et.GetAwaiter().GetResult();
            }
            catch { /* sts2-cli also swallows — caller will see EventRoom remains and can decide */ }
        }
    }
}
