using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Runtime.Bindings;

// Treasure-room operations. TreasureRoom.DoNormalRewards → relic-pick →
// CompleteWithNoRelics → DoExtraRewardsIfNeeded → EnterRoom(MapRoom).
// Soft-bound to the same `_treasure*` fields declared in Sts2Bindings.cs.
public sealed partial class Sts2Bindings
{
    // The TreasureRoom instance we have already driven DoNormalRewards on.
    // The synchronizer's CurrentRelics doesn't distinguish "not yet
    // populated" from "populated but empty" (empty chests are a legitimate
    // hypothetical with SilverCrucible-style modifiers), so we track
    // population per-room ourselves. ReferenceEquals against the live
    // CurrentRoom — a new treasure room is a fresh instance, so the cache
    // self-invalidates on room change.
    private object? _populatedTreasureRoom;

    // Read the chest's offering, populating it on first call. Returns an
    // empty list when the player isn't in a treasure room, or when the
    // chest legitimately has no offering. Wire-shaped: each entry is
    // suitable for direct inclusion in a RunSnapshot.
    //
    // Why populate from the snapshot path: in vanilla flow the offering
    // only exists after DoNormalRewards runs, and DoNormalRewards lives
    // inside the engine's grant chain. To preview a chest without
    // committing to a pick, we drive DoNormalRewards eagerly here. The
    // synchronizer's TCS-based picking flow is left untouched — the actual
    // grant (or skip) happens in LeaveTreasureRoom, which closes the
    // session via CompleteWithNoRelics regardless of which path the
    // caller chooses.
    public IReadOnlyList<TreasureRelic> GetTreasureOffering(RunHandle handle)
    {
        if (_treasureRoomType is null
            || _treasureRoomDoNormalRewards is null
            || _runManagerTreasureRoomRelicSync is null
            || _treasureSyncCurrentRelics is null)
        {
            return Array.Empty<TreasureRelic>();
        }

        var room = _runStateCurrentRoom.GetValue(handle.RunState);
        if (room is null || !_treasureRoomType.IsInstanceOfType(room))
        {
            // Reset the cache so a future treasure room re-populates.
            _populatedTreasureRoom = null;
            return Array.Empty<TreasureRelic>();
        }

        if (!ReferenceEquals(_populatedTreasureRoom, room))
        {
            // Drain any prior synchronizer session before populating fresh —
            // mirrors sts2-cli's BUG-013 fix in LeaveTreasureRoom.
            DrainActionExecutor(handle);
            _syncCtx?.Pump();

            try
            {
                var doNormalResult = _treasureRoomDoNormalRewards.Invoke(room, null);
                if (doNormalResult is Task t) t.GetAwaiter().GetResult();
                _syncCtx?.Pump();
                DrainActionExecutor(handle);
            }
            catch
            {
                // Soft-fail: leave the cache miss so the next snapshot retries.
                // The caller sees an empty offering and can still call
                // run/leave_treasure_room (which has its own grant path).
                return Array.Empty<TreasureRelic>();
            }

            _populatedTreasureRoom = room;
        }

        var sync = _runManagerTreasureRoomRelicSync.GetValue(handle.RunManager);
        if (sync is null) return Array.Empty<TreasureRelic>();

        var result = new List<TreasureRelic>();
        if (_treasureSyncCurrentRelics.GetValue(sync) is System.Collections.IEnumerable cr)
        {
            foreach (var r in cr)
            {
                if (r is null) continue;
                var id = _relicModelId is not null ? ReadEntryId(_relicModelId, r) : null;
                if (id is not null) result.Add(new TreasureRelic(id));
            }
        }
        return result;
    }

    // Open the chest in the current treasure room and exit to MapRoom.
    // The engine flow we drive (discovered by reflection probe):
    //   1. TreasureRoom.DoNormalRewards() (Task<int>) populates
    //      RunManager.TreasureRoomRelicSynchronizer.CurrentRelics with the
    //      chest's offering — typically one chest-tier relic. Skipped if
    //      GetTreasureOffering has already populated for this room.
    //   2. We can't drive the synchronizer's TCS-based picking flow in
    //      headless — the engine awaits an animation-completion signal
    //      raised by the UI screen normally. Instead, when `skip` is
    //      false (the default), we read the first relic from CurrentRelics
    //      and grant it via RelicCmd.Obtain (the same engine path
    //      RelicReward.OnSelectWrapper uses, so Player.Relics + on-obtain
    //      listener hooks stay aligned). When `skip` is true, we leave
    //      the offering ungranted. Either way we call
    //      SyncCompleteWithNoRelics to release the synchronizer session.
    //      Skipping the release would trip the next treasure room's
    //      "session already occurring" guard (sts2-cli's BUG-013).
    //   3. TreasureRoom.DoExtraRewardsIfNeeded() covers act-3 / ascension
    //      extras (typically a no-op for Act 1).
    //   4. EnterRoom(MapRoom) flips the room — the engine does not flip
    //      it on its own after the chest is empty.
    //
    // Empty chests (CurrentRelics.Count == 0) close out via
    // CompleteWithNoRelics so the synchronizer state doesn't linger and
    // a future treasure room can BeginRelicPicking cleanly.
    public void LeaveTreasureRoom(RunHandle handle, bool skip = false)
    {
        if (_treasureRoomType is null)
        {
            throw new InvalidOperationException(
                "treasure-room binding not resolved at boot — TreasureRoom type missing. " +
                "Either the engine renamed it or the bootstrap walk did not surface it.");
        }
        var room = _runStateCurrentRoom.GetValue(handle.RunState)
            ?? throw new InvalidOperationException("RunState.CurrentRoom was null");
        if (!_treasureRoomType.IsInstanceOfType(room))
        {
            throw new InvalidOperationException(
                $"run/leave_treasure_room called but current room is {room.GetType().Name}, not TreasureRoom");
        }

        if (_treasureRoomDoNormalRewards is null
            || _treasureRoomDoExtraRewards is null
            || _runManagerTreasureRoomRelicSync is null
            || _treasureSyncCompleteWithNoRelics is null)
        {
            throw new InvalidOperationException(
                "treasure-room binding not fully resolved at boot — one of " +
                "DoNormalRewards / DoExtraRewardsIfNeeded / TreasureRoomRelicSynchronizer / " +
                "CompleteWithNoRelics is missing. The engine likely renamed a member.");
        }

        // BUG-013 (sts2-cli): drain any pending relic-picking session from a
        // prior room before invoking the chest reward chain.
        DrainActionExecutor(handle);
        _syncCtx?.Pump();

        // 1. Set up the chest offering, unless GetTreasureOffering has
        //    already populated it for this room (which it does as part of
        //    the snapshot preview path). DoNormalRewards is not idempotent
        //    — re-invoking it on an already-populated synchronizer trips
        //    the engine's "session already occurring" guard.
        if (!ReferenceEquals(_populatedTreasureRoom, room))
        {
            var doNormalResult = _treasureRoomDoNormalRewards.Invoke(room, null);
            if (doNormalResult is Task t1) t1.GetAwaiter().GetResult();
            _syncCtx?.Pump();
            DrainActionExecutor(handle);
            _populatedTreasureRoom = room;
        }

        // 2. Grant the offered relic unless the caller is skipping. When
        //    skip=true we leave the offering ungranted but still close the
        //    synchronizer session via CompleteWithNoRelics so the next
        //    treasure room boots cleanly. When skip=false we read the
        //    first relic from CurrentRelics and grant it via RelicCmd.Obtain
        //    (the same engine path RelicReward.OnSelectWrapper uses, so
        //    Player.Relics + listener hooks stay aligned).
        var sync = _runManagerTreasureRoomRelicSync.GetValue(handle.RunManager)
            ?? throw new InvalidOperationException(
                "RunManager.TreasureRoomRelicSynchronizer was null after DoNormalRewards");

        var hasRelics = false;
        if (_treasureSyncCurrentRelics?.GetValue(sync) is System.Collections.IEnumerable currentRelics)
        {
            foreach (var _ in currentRelics) { hasRelics = true; break; }
        }

        if (hasRelics && !skip)
        {
            // Pick the first relic from the offering. We can't reliably
            // drive the synchronizer's TCS-based picking flow in headless
            // (the engine awaits an animation-completion signal normally
            // raised by the UI screen), so we:
            //   1. Pull the first RelicModel from CurrentRelics
            //   2. Grant it via RelicCmd.Obtain — the same engine path that
            //      RelicReward.OnSelectWrapper uses, so Player.Relics + the
            //      on-obtain listener pipeline stay aligned
            //   3. Call SyncCompleteWithNoRelics to release the synchronizer
            //      session — without this, the next treasure room trips
            //      "session already occurring" (BUG-013).
            //
            // If anything in this chain fails, fall through to
            // CompleteWithNoRelics so the run isn't stuck on a dangling
            // synchronizer state — the caller will see 0 relics granted and
            // the next snapshot reports MapRoom either way.
            if (_relicCmdObtain is not null && _treasureSyncCurrentRelics is not null)
            {
                object? pickedRelic = null;
                if (_treasureSyncCurrentRelics.GetValue(sync) is System.Collections.IEnumerable cr)
                {
                    foreach (var r in cr) { pickedRelic = r; break; }
                }
                if (pickedRelic is not null)
                {
                    // The model from CurrentRelics is already a mutable
                    // per-run instance (probe showed IsMutable=False on the
                    // canonical RELIC.GORGET, but the synchronizer cache
                    // gives us the runtime-instance variant; ToMutable
                    // produces a fresh mutable if it isn't already).
                    var modelToGrant = _relicModelToMutable is not null
                        ? (_relicModelToMutable.Invoke(pickedRelic, null) ?? pickedRelic)
                        : pickedRelic;
                    var paramCount = _relicCmdObtain.GetParameters().Length;
                    var args = new object?[paramCount];
                    args[0] = modelToGrant;
                    args[1] = handle.Player;
                    for (var i = 2; i < paramCount; i++) args[i] = Type.Missing;
                    try
                    {
                        var obtainResult = _relicCmdObtain.Invoke(null, args);
                        if (obtainResult is Task ot) ot.GetAwaiter().GetResult();
                        _syncCtx?.Pump();
                        DrainActionExecutor(handle);
                    }
                    catch { /* fall through; CompleteWithNoRelics below at least closes the session */ }
                }
            }
        }
        // Always close out the synchronizer — either because the chest was
        // empty, or because we already granted the relic directly above and
        // the session would otherwise linger.
        _treasureSyncCompleteWithNoRelics.Invoke(sync, null);
        _syncCtx?.Pump();
        DrainActionExecutor(handle);

        // 3. Run any extra-reward path (act-3 / ascension chests). Typically
        //    a no-op for Act 1.
        var doExtraResult = _treasureRoomDoExtraRewards.Invoke(room, null);
        if (doExtraResult is Task t2) t2.GetAwaiter().GetResult();
        _syncCtx?.Pump();
        DrainActionExecutor(handle);

        // 4. ForceToMap. Without this, CurrentRoom stays TreasureRoom even
        //    though every reward has been granted.
        if (_runManagerProceedFromTerminalRewards is not null)
        {
            try
            {
                var proceed = _runManagerProceedFromTerminalRewards.Invoke(handle.RunManager, null);
                if (proceed is Task pt) pt.GetAwaiter().GetResult();
                _syncCtx?.Pump();
            }
            catch { /* sts2-cli also swallows */ }
        }
        if (_runManagerEnterRoom is not null && _mapRoomType is not null)
        {
            try
            {
                var mapRoom = Activator.CreateInstance(_mapRoomType)
                    ?? throw new InvalidOperationException($"{_mapRoomType.FullName} default ctor returned null");
                var enter = _runManagerEnterRoom.Invoke(handle.RunManager, new[] { mapRoom });
                if (enter is Task et) et.GetAwaiter().GetResult();
                _syncCtx?.Pump();
            }
            catch { /* caller will see TreasureRoom remains and can decide */ }
        }
        DrainActionExecutor(handle);

        // Clear the populated-cache after the room exits — the next
        // treasure room will be a fresh instance, so ReferenceEquals will
        // miss naturally, but resetting here also covers the case where
        // a future caller re-enters a treasure (e.g. via debug warp).
        _populatedTreasureRoom = null;
    }
}
