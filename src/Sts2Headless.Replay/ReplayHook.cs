using System.Reflection;
using HarmonyLib;

namespace Sts2Headless.Replay;

// Static install-once Harmony hooks for the recording lifecycle. Three
// patches:
//
//   1. Prefix on `CombatReplayWriter.WriteReplay(string, bool)` —
//      fires at the end of every combat. The engine's
//      `CombatManager.EndCombatInternal` calls
//      `RunManager.WriteReplay(stopRecording: true)` which routes
//      through `CombatReplayWriter.WriteReplay`. At that exact
//      moment `_replay` is fully populated with the combat's events
//      and checksums; immediately after the original method runs it
//      calls `StopRecording()` which sets `_replay = null`. Prefix
//      placement captures the data before the engine drops it. The
//      original write itself goes through `Godot.FileAccess` (no-op
//      stubbed in our headless context), so we are not duplicating
//      a working write — we are providing the only write that
//      reaches disk.
//
//   2. Prefix on `RunManager.CleanUp(bool)` — run-end. Flushes any
//      tail-end combat that wasn't followed by another write, then
//      closes out the per-run manifest. PREFIX not postfix: CleanUp
//      disposes the CombatReplayWriter at line 1203 of its body, so
//      a postfix would observe a disposed writer.
//
//   3. Prefix on `RunHistorySaveManager.SaveHistory(RunHistory)` —
//      `.run` JSON emission. The engine builds a `RunHistory` model
//      object in `RunHistoryUtilities.CreateRunHistoryEntry` (called
//      from `RunManager.OnEnded`) and hands it to
//      `RunHistorySaveManager.SaveHistory` for serialisation +
//      write-to-disk. The original write goes through `Godot.FileAccess`
//      and our stubs no-op it, so the engine's `.run` never lands.
//      The hook re-runs the same serialisation (via
//      `JsonSerializationUtility.ToJson<RunHistory>(history)` — the
//      game's own serializer with its own source-generated type-info)
//      and writes the bytes into our `<RunDirectory>/run.json` via
//      System.IO. AD-8 byte-fidelity: the .run we emit IS the
//      retail-game shape, because the game's serializer produces the
//      bytes; we just intercept where they land.
//
// All patches are thin shims forwarding to a singleton
// `ReplayRecorder` registered via `Bind`/`Unbind`. The recorder owns
// the per-run state; the hook owns nothing but the singleton slot and
// the Harmony instance.
//
// Threading: the engine drives every event on our
// InlineSynchronizationContext, so hooks fire on the same logical
// thread that produced the events. No locking needed inside the
// recorder.
public static class ReplayHook
{
    private const string HarmonyId = "headless-in-the-spire.replay";
    private static Harmony? _harmony;
    private static ReplayRecorder? _current;

    public static void Install(Assembly sts2)
    {
        if (_harmony is not null) return;

        // Resolve reflection for the per-floor stats stamper before
        // patching; the prefix shim depends on a populated cache.
        PlayerStatsStamper.Init(sts2);

        var writerType = sts2.GetType("MegaCrit.Sts2.Core.Multiplayer.Replay.CombatReplayWriter")
            ?? throw new InvalidOperationException("CombatReplayWriter not found in sts2 — refusing to install replay hooks");
        var writeReplay = writerType.GetMethod("WriteReplay", BindingFlags.Public | BindingFlags.Instance, [typeof(string), typeof(bool)])
            ?? throw new InvalidOperationException("CombatReplayWriter.WriteReplay(string, bool) not found");

        var runManagerType = sts2.GetType("MegaCrit.Sts2.Core.Runs.RunManager")
            ?? throw new InvalidOperationException("RunManager not found in sts2 — refusing to install replay hooks");
        var cleanUp = runManagerType.GetMethod("CleanUp", BindingFlags.Public | BindingFlags.Instance, [typeof(bool)])
            ?? throw new InvalidOperationException("RunManager.CleanUp(bool) not found");

        var historyManagerType = sts2.GetType("MegaCrit.Sts2.Core.Saves.Managers.RunHistorySaveManager")
            ?? throw new InvalidOperationException("RunHistorySaveManager not found in sts2 — refusing to install replay hooks");
        var runHistoryType = sts2.GetType("MegaCrit.Sts2.Core.Runs.RunHistory")
            ?? throw new InvalidOperationException("RunHistory not found in sts2 — refusing to install replay hooks");
        var saveHistory = historyManagerType.GetMethod("SaveHistory", BindingFlags.Public | BindingFlags.Instance, [runHistoryType])
            ?? throw new InvalidOperationException("RunHistorySaveManager.SaveHistory(RunHistory) not found");

        // RunManager.UpdatePlayerStatsInMapPointHistory is the engine's
        // own per-floor HP / gold / max-HP stamper. It's called from
        // RunManager.EnterMapPointInternal (every room transition) and
        // from OnEnded (which we bypass) — but the very first line of
        // its body is `if (TestMode.IsOn || State == null) return;`.
        // We set TestMode.IsOn during bootstrap, so the stamper is a
        // no-op the entire run and player_stats in map_point_history
        // ends up with current_hp / max_hp / current_gold all 0. The
        // viewer then renders every floor as "0/0 — died". Prefix-patch
        // it to run the body without the TestMode guard. Returning
        // false short-circuits the original, which is fine: the
        // original IS the same body we're running, just gated.
        var updateStats = runManagerType.GetMethod("UpdatePlayerStatsInMapPointHistory", BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)
            ?? throw new InvalidOperationException("RunManager.UpdatePlayerStatsInMapPointHistory not found");

        _harmony = new Harmony(HarmonyId);
        _harmony.Patch(writeReplay, prefix: new HarmonyMethod(typeof(ReplayHook).GetMethod(nameof(BeforeWriteReplay), BindingFlags.NonPublic | BindingFlags.Static)));
        _harmony.Patch(cleanUp, prefix: new HarmonyMethod(typeof(ReplayHook).GetMethod(nameof(BeforeRunManagerCleanUp), BindingFlags.NonPublic | BindingFlags.Static)));
        _harmony.Patch(updateStats, prefix: new HarmonyMethod(typeof(ReplayHook).GetMethod(nameof(BeforeUpdatePlayerStats), BindingFlags.NonPublic | BindingFlags.Static)));
        // Postfix (not prefix) because RunHistorySaveManager.SaveHistory
        // stamps `history.SchemaVersion = migrationManager.GetLatestVersion<RunHistory>()`
        // as its very first statement. A prefix would observe the
        // unstamped object (SchemaVersion = 0) and serialise it without
        // a version number, which then breaks every downstream parser
        // that uses schema_version to gate compatibility. Postfix runs
        // after that stamp; the original method's `_saveStore.WriteFile`
        // is a Godot.FileAccess no-op in our stubs so there's no real
        // disk write to "race".
        _harmony.Patch(saveHistory, postfix: new HarmonyMethod(typeof(ReplayHook).GetMethod(nameof(AfterSaveHistory), BindingFlags.NonPublic | BindingFlags.Static)));
    }

    public static void Uninstall()
    {
        _harmony?.UnpatchAll(HarmonyId);
        _harmony = null;
        _current = null;
    }

    // Bind the active recorder. Per-run: host calls Bind on run/new and
    // Unbind on run cleanup. Only one recorder is active at a time —
    // single-session host model (Session.cs) makes this safe.
    public static void Bind(ReplayRecorder recorder) => _current = recorder;
    public static void Unbind(ReplayRecorder recorder)
    {
        if (ReferenceEquals(_current, recorder)) _current = null;
    }

    // Test affordance: the test suite needs to confirm the recorder is
    // wired to the hook without exposing the singleton slot directly.
    public static bool HasActiveRecorder => _current is not null;

    private static void BeforeWriteReplay(object __instance, string filePath, bool stopRecording)
    {
        // __instance is the CombatReplayWriter. _replay is populated;
        // we read it before the original runs (which will null it via
        // StopRecording). Exceptions swallowed — a broken recorder
        // must never crash the engine's combat-end flow.
        try
        {
            _current?.OnCombatWriteReplay(__instance);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ReplayHook.BeforeWriteReplay: {ex}");
        }
        _ = (filePath, stopRecording);  // engine's intended path is its own no-op concern
    }

    private static void BeforeRunManagerCleanUp(object __instance, bool graceful)
    {
        try
        {
            _current?.OnRunCleanUp(__instance, graceful);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ReplayHook.BeforeRunManagerCleanUp: {ex}");
        }
    }

    // Prefix on RunManager.UpdatePlayerStatsInMapPointHistory. Runs
    // the same body the engine would have run, minus the TestMode
    // gate, then returns false so the original (gated, no-op in our
    // config) method doesn't fire. Catches every per-room-transition
    // stamp the engine calls — see PlayerStatsStamper for the why.
    private static bool BeforeUpdatePlayerStats(object __instance)
    {
        try
        {
            PlayerStatsStamper.Stamp(__instance);
        }
        catch (Exception ex)
        {
            // A broken stamper must not crash combat — log and
            // continue. The TestMode-gated original would have been
            // a no-op anyway, so worst case we land where we were.
            Console.Error.WriteLine($"ReplayHook.BeforeUpdatePlayerStats: {ex}");
        }
        return false;
    }

    // First positional argument of `SaveHistory(RunHistory history)`.
    // Harmony binds it by name when the signature matches. Postfix
    // placement intentional — see the Patch call site for why.
    private static void AfterSaveHistory(object history)
    {
        try
        {
            _current?.OnSaveHistory(history);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ReplayHook.AfterSaveHistory: {ex}");
        }
    }
}
