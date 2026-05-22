using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Runtime.Bindings;

// Rest-site operations. Wire flow: snapshot surfaces AvailableRestSiteOptions
// (read via ReadAvailableRestSiteOptions); caller picks an index via
// SelectRestSiteOption, which fires RestSiteSynchronizer.ChooseLocalOption
// and auto-advances to MapRoom when the engine clears Options. Backed by
// the `_restSite*` fields declared in Sts2Bindings.cs.
public sealed partial class Sts2Bindings
{
    // Walk RunState.CurrentRoom (cast to RestSiteRoom) → Options and shape
    // each option into the wire record. Mirrors sts2-cli's RestSiteState
    // reduced to the data fields we surface.
    //
    // Returns [] when the binding never resolved, the current room isn't a
    // RestSiteRoom (defence in depth — callers gate by RoomType already), or
    // the engine cleared Options after a pick. An empty list while standing
    // on a RestSiteRoom is the engine's "decision already made, advance to
    // map" signal — handled by sts2-cli via ForceToMap; we leave the wire
    // honest about the state and let the next caller drive the transition.
    private IReadOnlyList<RestSiteOption> ReadAvailableRestSiteOptions(object runState)
    {
        if (_restSiteRoomOptions is null) return Array.Empty<RestSiteOption>();
        var room = _runStateCurrentRoom.GetValue(runState);
        if (room is null) return Array.Empty<RestSiteOption>();
        if (_restSiteRoomOptions.DeclaringType is null
            || !_restSiteRoomOptions.DeclaringType.IsInstanceOfType(room))
        {
            return Array.Empty<RestSiteOption>();
        }
        if (_restSiteRoomOptions.GetValue(room) is not System.Collections.IList options
            || options.Count == 0)
        {
            return Array.Empty<RestSiteOption>();
        }
        var result = new List<RestSiteOption>(options.Count);
        for (var i = 0; i < options.Count; i++)
        {
            var opt = options[i];
            if (opt is null) continue;
            var optionId = _restSiteOptionOptionId?.GetValue(opt) as string ?? opt.GetType().Name;
            var isEnabled = _restSiteOptionIsEnabled is not null
                && (bool)(_restSiteOptionIsEnabled.GetValue(opt) ?? false);
            result.Add(new RestSiteOption(i, optionId, isEnabled));
        }
        return result;
    }

    // Fire RunManager.RestSiteSynchronizer.ChooseLocalOption(optionIndex)
    // and pump the sync context so synchronous follow-ups complete inside
    // the call. After single-pick options sts2 clears the room's Options
    // list but doesn't auto-transition to MapRoom — sts2-cli's ForceToMap
    // pattern covers this. Mirror it: if Options is empty after the
    // synchronous call, EnterRoom(MapRoom) so the next wire snapshot
    // reports MapRoom, the post-rest contract callers rely on.
    //
    // SMITH's CardSelectCmd.FromDeckForUpgrade prompt is intercepted by
    // the installed HeadlessCardSelector — callers stage card indices via
    // RunSelectRestSiteOptionParams.CardSelectIndices, which HostMethods
    // queues into the selector before invoking us. The selector returns
    // those cards synchronously inside the await, so the upgrade has
    // already happened by the time ChooseLocalOption's task completes.
    //
    // ShouldDisableRemainingRestSiteOptions hooks (e.g. a multi-pick
    // relic) can leave Options non-empty after a pick — the auto-advance
    // guard below skips MapRoom in that case and the next snapshot
    // surfaces the remaining options for another pick.
    public void SelectRestSiteOption(RunHandle handle, int optionIndex)
    {
        if (_runManagerRestSiteSynchronizer is null || _restSiteSyncChooseLocalOption is null)
        {
            throw new InvalidOperationException(
                "rest-site binding not resolved at boot — RestSiteSynchronizer.ChooseLocalOption is missing. " +
                "Either the engine moved the type or the bootstrap walk did not surface it.");
        }
        var sync = _runManagerRestSiteSynchronizer.GetValue(handle.RunManager)
            ?? throw new InvalidOperationException("RunManager.RestSiteSynchronizer was null");
        var result = _restSiteSyncChooseLocalOption.Invoke(sync, new object?[] { optionIndex });
        if (result is Task t) t.GetAwaiter().GetResult();
        _syncCtx?.Pump();

        // Auto-advance after any pick — HEAL / SMITH / DIG / ... all
        // leave Options empty once accepted (single-pick default), and
        // the engine doesn't auto-flip CurrentRoom. We force it. If a
        // ShouldDisableRemainingRestSiteOptions hook keeps options
        // enabled (multi-pick relic), the guard below skips MapRoom and
        // the agent picks again on the next snapshot.
        if (_restSiteRoomOptions is not null && _runManagerEnterRoom is not null && _mapRoomType is not null)
        {
            var room = _runStateCurrentRoom.GetValue(handle.RunState);
            if (room is not null
                && _restSiteRoomOptions.DeclaringType is not null
                && _restSiteRoomOptions.DeclaringType.IsInstanceOfType(room))
            {
                var options = _restSiteRoomOptions.GetValue(room) as System.Collections.IList;
                if (options is null || options.Count == 0)
                {
                    try
                    {
                        var mapRoom = Activator.CreateInstance(_mapRoomType)
                            ?? throw new InvalidOperationException($"{_mapRoomType.FullName} default ctor returned null");
                        var enter = _runManagerEnterRoom.Invoke(handle.RunManager, new[] { mapRoom });
                        if (enter is Task et) et.GetAwaiter().GetResult();
                        _syncCtx?.Pump();
                    }
                    catch { /* sts2-cli also swallows — caller will see RestSiteRoom remains and can recover */ }
                }
            }
        }
    }
}
