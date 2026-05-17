using System.Globalization;
using System.Reflection;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Replay;

// Orchestrates the per-run recording lifecycle. Owned by the host's
// Session (one recorder per active run). Subscribes to the static
// ReplayHook for combat-end / run-end notifications; writes per-combat
// `.mcr` files via CombatReplayBytes; accumulates manifest entries.
//
// Lifecycle:
//   1. Host's run/new constructs a recorder, calls ReplayHook.Bind(this).
//   2. Engine enters first combat room → RecordInitialState fires →
//      hook calls OnAboutToOverwriteReplay → first call no-ops (no
//      prior replay to flush) but captures the engine's current
//      _replay-creation as "start of combat N".
//   3. Engine exits combat room, enters next room → RecordInitialState
//      fires again → hook calls OnAboutToOverwriteReplay → the prior
//      _replay (still in memory at this point) is flushed to disk and
//      appended to the manifest accumulator.
//   4. Run ends → RunManager.CleanUp postfix → hook calls OnRunCleanUp
//      → tail-end flush + manifest emission to disk + ReplayHook.Unbind.
public sealed class ReplayRecorder
{
    private readonly Assembly _sts2;
    private readonly CombatReplayBytes _bytes;
    private readonly CombatTimelineEmitter _timeline;
    private readonly FieldInfo _replayField;
    private readonly List<ReplayCombatEntry> _combats = new();


    // Sequential per-run counter — survives non-combat rooms (which also
    // trigger RecordInitialState but produce empty replays we skip).
    // Exposed for tests so they can assert the recorder saw the expected
    // number of combats without having to scan the directory.
    public int FlushedCombatCount { get; private set; }

    // RunHistory → JSON via the game's own source-generated serializer.
    // Bound at recorder construction so the reflection happens once per
    // run; the closed-generic MethodInfo is reusable for every
    // SaveHistory call. AD-8: the bytes our `run.json` carries are the
    // game's own serialiser output — we just intercept where they land.
    private readonly MethodInfo _runHistoryToJson;

    // RunHistoryUtilities.CreateRunHistoryEntry + RunManager.ToSave
    // are how we synthesise run.json for runs that the engine itself
    // never wrote one for. The engine's `OnEnded(isVictory: false)`
    // call site is wrapped in `if (TestMode.IsOff)` (CreatureCmd.Kill)
    // and we set TestMode.IsOn during bootstrap, so death-path
    // SaveRunHistory never fires. Even if it did, the second gate is
    // `if (ShouldSave)` inside OnEnded — `RunManager.SetUpTest` sets
    // ShouldSave=false by default. Two gates, both wired off in our
    // headless config.
    //
    // We bypass both by calling CreateRunHistoryEntry ourselves at
    // run-cleanup time. The method's body is pure: build a RunHistory
    // from the SerializableRun, then call
    // SaveManager.SaveRunHistory(history) which routes through
    // RunHistorySaveManager.SaveHistory — exactly the method our
    // Harmony prefix already hooks. No engine patching needed.
    private readonly MethodInfo _runHistoryCreateEntry;
    private readonly MethodInfo _runManagerToSave;
    private readonly PropertyInfo _runManagerIsAbandoned;
    private readonly PropertyInfo _runManagerIsGameOver;
    private readonly PropertyInfo _runManagerHistory;
    private readonly PropertyInfo _runManagerNetService;
    private readonly PropertyInfo _runManagerIsInProgress;
    private readonly PropertyInfo _netServicePlatform;

    // CombatReplayWriter.IsEnabled property setter — the engine itself
    // defaults this to `!TestMode.IsOn`, and our bootstrap turns
    // TestMode on so the engine's RunHistory/SaveRun side-effects don't
    // fire on every test. That same toggle disables the
    // CombatReplayWriter, gating RecordInitialState. We flip it back to
    // true once the recorder is bound — see EnableEngineRecording().
    private readonly PropertyInfo _writerIsEnabled;

    public ReplayRecorder(Assembly sts2, string root, ReplayHeader header)
    {
        _sts2 = sts2;
        Header = header;
        _bytes = new CombatReplayBytes(sts2);
        _timeline = new CombatTimelineEmitter(sts2);

        var writerType = sts2.GetType("MegaCrit.Sts2.Core.Multiplayer.Replay.CombatReplayWriter")
            ?? throw new InvalidOperationException("CombatReplayWriter not found in sts2");
        _replayField = writerType.GetField("_replay", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("CombatReplayWriter._replay backing field not found");
        _writerIsEnabled = writerType.GetProperty("IsEnabled", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("CombatReplayWriter.IsEnabled not found");

        var jsonUtilType = sts2.GetType("MegaCrit.Sts2.Core.Saves.JsonSerializationUtility")
            ?? throw new InvalidOperationException("JsonSerializationUtility not found in sts2");
        var runHistoryType = sts2.GetType("MegaCrit.Sts2.Core.Runs.RunHistory")
            ?? throw new InvalidOperationException("RunHistory not found in sts2");
        var toJsonGeneric = jsonUtilType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m is { Name: "ToJson", IsGenericMethodDefinition: true } && m.GetParameters().Length == 1)
            ?? throw new InvalidOperationException("JsonSerializationUtility.ToJson<T>(T) not found");
        _runHistoryToJson = toJsonGeneric.MakeGenericMethod(runHistoryType);

        var runManagerType = sts2.GetType("MegaCrit.Sts2.Core.Runs.RunManager")
            ?? throw new InvalidOperationException("RunManager not found in sts2");
        _runManagerToSave = runManagerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == "ToSave" && m.GetParameters().Length == 1)
            ?? throw new InvalidOperationException("RunManager.ToSave(AbstractRoom?) not found");
        _runManagerIsAbandoned = runManagerType.GetProperty("IsAbandoned", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("RunManager.IsAbandoned not found");
        _runManagerIsGameOver = runManagerType.GetProperty("IsGameOver", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("RunManager.IsGameOver not found");
        _runManagerHistory = runManagerType.GetProperty("History", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("RunManager.History not found");
        _runManagerNetService = runManagerType.GetProperty("NetService", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("RunManager.NetService not found");
        _runManagerIsInProgress = runManagerType.GetProperty("IsInProgress", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("RunManager.IsInProgress not found");

        var utilitiesType = sts2.GetType("MegaCrit.Sts2.Core.Runs.RunHistoryUtilities")
            ?? throw new InvalidOperationException("RunHistoryUtilities not found in sts2");
        _runHistoryCreateEntry = utilitiesType.GetMethod("CreateRunHistoryEntry", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("RunHistoryUtilities.CreateRunHistoryEntry not found");

        var netGameServiceType = sts2.GetType("MegaCrit.Sts2.Core.Multiplayer.Game.INetGameService")
            ?? throw new InvalidOperationException("INetGameService not found in sts2");
        _netServicePlatform = netGameServiceType.GetProperty("Platform", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("INetGameService.Platform not found");

        var runId = ReplayLayout.NewRunId(DateTimeOffset.FromUnixTimeSeconds(header.StartTimeUnix), header.Seed);
        RunDirectory = ReplayLayout.RunDirectory(root, header.GameVersion, runId);
        Directory.CreateDirectory(ReplayLayout.CombatsDirectory(RunDirectory));
    }

    public ReplayHeader Header { get; }
    public string RunDirectory { get; }
    public IReadOnlyList<ReplayCombatEntry> Combats => _combats;

    // Flips two engine toggles that bootstrap-time TestMode forces off:
    //
    //  1. `CombatReplayWriter.IsEnabled` — the engine defaults this to
    //     `!TestMode.IsOn` in RunManager.InitializeShared. We set
    //     TestMode.IsOn during bootstrap (so SaveRun's RunHistory-upload
    //     side-effects don't fire on every test, see BootstrapSequence),
    //     which leaves recording off. With the recorder bound, recording
    //     is exactly the thing we want.
    //
    //  2. `ChecksumTracker.IsEnabled` — the engine defaults this to
    //     `!TestMode.IsOn && NetService.Type.IsMultiplayer()`. In our
    //     singleplayer + TestMode-on configuration both gates fail and
    //     the tracker boots disabled, so its eight `GenerateChecksum`
    //     call sites (turn boundaries, action completion, room exits)
    //     short-circuit and `CombatReplayWriter.RecordChecksum` never
    //     fires. The .mcr ends up with zero entries in `checksumData`,
    //     which strips us of the determinism canary the recorded
    //     `NetFullCombatState` checksums were meant to enable. Flipping
    //     this back is safe in singleplayer: `_netService.Type ==
    //     Singleplayer`, so the `if (Type == Client)` send-message branch
    //     in GenerateChecksum is dead, and `CheckAgainstReplayChecksum`
    //     no-ops with `_replayChecksums == null`.
    //
    // Called by the host after Bind. Idempotent.
    public void EnableEngineRecording()
    {
        var runManagerType = _sts2.GetType("MegaCrit.Sts2.Core.Runs.RunManager")
            ?? throw new InvalidOperationException("RunManager not found");
        var instance = runManagerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
            ?? throw new InvalidOperationException("RunManager.Instance is null — bootstrap missing?");

        var writer = runManagerType.GetProperty("CombatReplayWriter", BindingFlags.Public | BindingFlags.Instance)?.GetValue(instance)
            ?? throw new InvalidOperationException("RunManager.CombatReplayWriter is null — run not started?");
        _writerIsEnabled.SetValue(writer, true);

        var tracker = runManagerType.GetProperty("ChecksumTracker", BindingFlags.Public | BindingFlags.Instance)?.GetValue(instance)
            ?? throw new InvalidOperationException("RunManager.ChecksumTracker is null — run not started?");
        var trackerIsEnabled = tracker.GetType().GetProperty("IsEnabled", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("ChecksumTracker.IsEnabled not found");
        trackerIsEnabled.SetValue(tracker, true);
    }

    // Fired by the Harmony prefix on CombatReplayWriter.WriteReplay.
    // The engine calls this at combat-end (CombatManager.EndCombatInternal
    // → RunManager.WriteReplay(true) → CombatReplayWriter.WriteReplay)
    // and immediately afterward nulls `_replay` via StopRecording. Prefix
    // placement captures the full event/checksum buffer before that drop.
    // The original write itself goes through `Godot.FileAccess` (no-op
    // in our headless context), so we are not duplicating an
    // already-working write — we are providing the only one that reaches
    // disk.
    public void OnCombatWriteReplay(object combatReplayWriter)
    {
        var replay = _replayField.GetValue(combatReplayWriter);
        if (replay is null) return;
        var events = _bytes.EventCount(replay);
        var checksums = _bytes.ChecksumCount(replay);
        if (events == 0 && checksums == 0) return;

        var context = ReadCurrentCombatContext()
            ?? new PendingCombatContext(ActIndex: 0, Floor: 0, RoomType: RoomType.Unknown, Encounter: null, RoomSlug: "unknown");
        FlushPendingCombat(replay, context, events, checksums);
    }

    // Fired by the Harmony prefix on RunManager.CleanUp(bool). Engine
    // is about to tear down — flush any in-memory replay and emit the
    // manifest. Idempotent: the host may call us twice (e.g. via the
    // hook AND via an explicit FinalizeAndUnbind from Session.Clear),
    // and the second call no-ops.
    public void OnRunCleanUp(object runManager, bool graceful)
    {
        if (IsFinalized) return;
        FinalFlush(runManager);
        WriteRunHistoryIfMissing(runManager);
        WriteManifest();
        IsFinalized = true;
        _ = graceful;
    }

    // Fired by the Harmony prefix on RunHistorySaveManager.SaveHistory.
    // The engine's `RunHistory` model is fully populated by this point;
    // we re-run its serialisation through the game's
    // JsonSerializationUtility (the same call site the original
    // SaveHistory uses) and write the bytes into our run directory.
    // The original write proceeds — its `_saveStore.WriteFile` goes
    // through `Godot.FileAccess` and our stubs no-op it, so the only
    // copy of the .run JSON on disk is ours.
    //
    // Called at most once per run (engine guards with
    // `_runHistoryWasUploaded`). Idempotent at our layer too: if
    // run.json already exists in the directory we overwrite, since the
    // engine's RunHistory may have been mutated between calls.
    public void OnSaveHistory(object runHistory)
    {
        if (runHistory is null) return;
        var json = (string)(_runHistoryToJson.Invoke(null, [runHistory])
            ?? throw new InvalidOperationException("JsonSerializationUtility.ToJson returned null for RunHistory"));
        File.WriteAllText(ReplayLayout.RunHistoryPath(RunDirectory), json);
    }

    // Explicit finaliser for the host path that wants to flush + write
    // manifest outside the Harmony hook (e.g. on graceful shutdown
    // without the engine's CleanUp running). Mirrors OnRunCleanUp but
    // doesn't take a runManager argument — pulls Instance reflectively.
    // Renamed away from `Finalize` to avoid colliding with
    // System.Object.Finalize (the C# destructor name).
    public void FinalizeRun()
    {
        if (IsFinalized) return;
        var runManagerType = _sts2.GetType("MegaCrit.Sts2.Core.Runs.RunManager");
        var instance = runManagerType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        if (instance is not null)
        {
            FinalFlush(instance);
            WriteRunHistoryIfMissing(instance);
        }
        WriteManifest();
        IsFinalized = true;
    }

    public bool IsFinalized { get; private set; }

    // Backstop for runs whose final combat ended without triggering
    // CombatReplayWriter.WriteReplay (e.g. abandoned mid-combat). Mostly
    // a no-op in practice — the engine flushes per-combat at
    // CombatManager.EndCombatInternal — but kept so a future engine
    // path that drops the natural flush still leaves something on disk.
    private void FinalFlush(object runManager)
    {
        var runManagerType = runManager.GetType();
        var writerProp = runManagerType.GetProperty("CombatReplayWriter", BindingFlags.Public | BindingFlags.Instance);
        var writer = writerProp?.GetValue(runManager);
        if (writer is null) return;
        var replay = _replayField.GetValue(writer);
        if (replay is null) return;
        var events = _bytes.EventCount(replay);
        var checksums = _bytes.ChecksumCount(replay);
        if (events == 0 && checksums == 0) return;
        var context = ReadCurrentCombatContext()
            ?? new PendingCombatContext(0, 0, RoomType.Unknown, null, "unknown");
        FlushPendingCombat(replay, context, events, checksums);
    }

    // Synthesises run.json for runs that the engine itself never
    // emitted one for. Called from OnRunCleanUp / FinalizeRun before
    // the manifest is written. See the field-doc on
    // _runHistoryCreateEntry for the why; this method is the how.
    //
    // The engine's `CreateRunHistoryEntry` builds a RunHistory from
    // the SerializableRun and calls
    // `SaveManager.SaveRunHistory(history)`, which routes to
    // `RunHistorySaveManager.SaveHistory` — exactly the method our
    // existing Harmony prefix already hooks. So the actual disk
    // write reuses our existing OnSaveHistory path; this method
    // just produces the bytes the engine would have produced.
    //
    // Skipped (no-op) when:
    //   * RunManager.History is already populated — engine's own
    //     OnEnded fired (heart-victory path) and ran the same code
    //     path. We must not double-emit.
    //   * RunManager.IsInProgress is false — the engine torn down
    //     before this point. Calling ToSave on a null State would
    //     crash; the manifest still lands.
    //
    // Failures are swallowed (logged to stderr) so an unexpected
    // shape from a future game version doesn't lose the .mcr files
    // we already wrote.
    private void WriteRunHistoryIfMissing(object runManager)
    {
        try
        {
            // The engine sets History to non-null inside
            // CreateRunHistoryEntry. If it's already set we've either
            // (a) been called twice, or (b) the engine fired OnEnded
            // itself on a heart-victory. Either way, the run.json we
            // care about is already in flight via the engine's path.
            if (_runManagerHistory.GetValue(runManager) is not null) return;

            // Without an active state there's nothing to serialise.
            // This happens if CleanUp runs after the engine already
            // tore down — rare but observed in shutdown races.
            if (_runManagerIsInProgress.GetValue(runManager) is not true) return;

            // Stamp the CURRENT map-point's player stats before
            // serialising. Mirrors OnEnded's call to
            // UpdatePlayerStatsInMapPointHistory — the engine itself
            // stamps prior entries on each EnterMapPointInternal, but
            // the LAST entry (the room the run ends in) is stamped
            // only by OnEnded, which we bypass. Calling Stamp() here
            // makes the final floor's HP / gold / max-HP appear in
            // run.json instead of staying at zero.
            PlayerStatsStamper.Stamp(runManager);

            // SerializableRun = ToSave(null). The `null` is the
            // pre-finished room — `OnEnded` passes null too. The
            // result is the same SerializableRun the engine would
            // ship to its run-history pipeline.
            var serializableRun = _runManagerToSave.Invoke(runManager, [null])
                ?? throw new InvalidOperationException("RunManager.ToSave returned null");

            // Classify the run-end:
            //   * If the engine flagged IsGameOver, the player died
            //     in the current room → victory=false, isAbandoned
            //     stays as the engine reports it.
            //   * Otherwise the run is in-progress (test stopped or
            //     host shut down with the agent alive) — mark it
            //     abandoned, so the viewer doesn't claim "killed by
            //     X" when the player's HP is still positive.
            // CreateRunHistoryEntry's body uses both flags to fill
            // killed_by_encounter / killed_by_event; passing isAbandoned
            // suppresses that branch.
            var engineIsGameOver = (bool)(_runManagerIsGameOver.GetValue(runManager) ?? false);
            var engineIsAbandoned = (bool)(_runManagerIsAbandoned.GetValue(runManager) ?? false);
            var victory = false;
            var isAbandoned = engineIsGameOver ? engineIsAbandoned : true;

            var netService = _runManagerNetService.GetValue(runManager)
                ?? throw new InvalidOperationException("RunManager.NetService is null");
            var platform = _netServicePlatform.GetValue(netService)
                ?? throw new InvalidOperationException("NetService.Platform is null");

            _runHistoryCreateEntry.Invoke(null, [serializableRun, victory, isAbandoned, platform]);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ReplayRecorder.WriteRunHistoryIfMissing: {ex}");
        }
    }

    private void WriteManifest()
    {
        var manifest = new ReplayManifest(
            Version: ReplayManifest.CurrentVersion,
            Header: Header,
            Combats: _combats.ToArray());
        var json = manifest.Serialize();
        File.WriteAllText(ReplayLayout.ManifestPath(RunDirectory), json);
    }

    private void FlushPendingCombat(object replay, PendingCombatContext pending, int events, int checksums)
    {
        FlushedCombatCount++;
        var fileName = ReplayLayout.CombatFileName(pending.ActIndex, pending.Floor, pending.RoomSlug);
        var path = Path.Combine(ReplayLayout.CombatsDirectory(RunDirectory), fileName);
        var result = _bytes.Write(replay, path);
        // Emit timeline.json alongside the .mcr. The replay we just
        // serialised to disk is the same in-memory instance we hand to
        // the emitter — no re-read needed. Timeline emission is a soft
        // dependency on the .mcr: if the engine ever stops populating
        // the CombatReplay fields we read (events / checksumData /
        // serializableRun), the next pin bump will surface here loudly,
        // but the .mcr itself still lands on disk.
        _timeline.EmitNextTo(replay, path);
        _combats.Add(new ReplayCombatEntry(
            McrFile: Path.GetRelativePath(RunDirectory, path).Replace(Path.DirectorySeparatorChar, '/'),
            ActIndex: pending.ActIndex,
            Floor: pending.Floor,
            RoomType: pending.RoomType,
            Encounter: pending.Encounter,
            Outcome: ReplayCombatOutcome.Unknown,  // TODO(#7): derive from RunState.IsGameOver / IsVictory at next-room time
            ActionCount: result.Events,
            ChecksumCount: result.Checksums));
        _ = events;     // kept on the signature for symmetry with the result
        _ = checksums;
    }

    // Reads RunManager.Instance.State to extract the act/floor + room
    // context for the combat we are about to flush. Called by both
    // OnCombatWriteReplay and FinalFlush. `State` is a PRIVATE property
    // on RunManager (decompile line 163) — must use NonPublic | Instance,
    // otherwise the lookup returns null and the recorder silently degrades
    // to "unknown" context for every combat.
    private PendingCombatContext? ReadCurrentCombatContext()
    {
        // CombatReplayWriter doesn't expose a direct path to RunState;
        // we walk via RunManager.Instance singleton. `State` is a PRIVATE
        // property on RunManager (line 163 of the decompile) — must use
        // BindingFlags.NonPublic | Instance to find it, otherwise the
        // lookup returns null and we'd silently skip every flush.
        var runManagerType = _sts2.GetType("MegaCrit.Sts2.Core.Runs.RunManager");
        var instanceProp = runManagerType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        var runManager = instanceProp?.GetValue(null);
        var stateProp = runManagerType?.GetProperty("State", BindingFlags.NonPublic | BindingFlags.Instance);
        var state = stateProp?.GetValue(runManager);
        if (state is null) return null;

        var stateType = state.GetType();
        var actIndex = (int)(stateType.GetProperty("CurrentActIndex")?.GetValue(state) ?? 0);
        var floor = (int)(stateType.GetProperty("ActFloor")?.GetValue(state) ?? 0);

        // RoomType comes from RunState.CurrentRoom?.GetType().Name —
        // mirror the convention Sts2Bindings uses. By the time the
        // engine calls WriteReplay (combat-end), CurrentRoom is the
        // combat room that's just finished, so the type read is
        // accurate for the flushed combat.
        var currentRoomObj = stateType.GetProperty("CurrentRoom")?.GetValue(state);
        var roomName = currentRoomObj?.GetType().Name ?? string.Empty;
        var roomType = MapRoomType(roomName);

        return new PendingCombatContext(actIndex, floor, roomType, Encounter: null, RoomSlug: RoomSlug(roomName));
    }

    private static RoomType MapRoomType(string roomClassName) => roomClassName switch
    {
        "CombatRoom" => RoomType.CombatRoom,
        "MonsterRoom" => RoomType.CombatRoom,
        "EliteRoom" => RoomType.CombatRoom,
        "BossRoom" => RoomType.BossRoom,
        "TreasureRoom" => RoomType.TreasureRoom,
        "MerchantRoom" => RoomType.MerchantRoom,
        "RestSiteRoom" => RoomType.RestSiteRoom,
        "EventRoom" => RoomType.EventRoom,
        "MapRoom" => RoomType.MapRoom,
        _ => RoomType.Unknown,
    };

    private static string RoomSlug(string roomClassName)
    {
        var stripped = roomClassName.EndsWith("Room", StringComparison.Ordinal)
            ? roomClassName[..^4]
            : roomClassName;
        return string.IsNullOrEmpty(stripped)
            ? "unknown"
            : stripped.ToLower(CultureInfo.InvariantCulture);
    }

    private sealed record PendingCombatContext(int ActIndex, int Floor, RoomType RoomType, EncounterId? Encounter, string RoomSlug);
}
