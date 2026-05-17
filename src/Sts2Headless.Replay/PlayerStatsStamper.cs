using System.Reflection;

namespace Sts2Headless.Replay;

// Mirrors `RunManager.UpdatePlayerStatsInMapPointHistory()` without
// the `if (TestMode.IsOn || State == null) return;` guard. The engine
// uses that method as its per-floor HP / gold / max-HP stamper —
// `EnterMapPointInternal` calls it on every room transition and
// `OnEnded` calls it at run-end. Both call sites are no-ops in our
// headless config because we set TestMode.IsOn during bootstrap, so
// the engine never populates `current_hp` / `max_hp` / `current_gold`
// in `MapPointHistoryEntry.PlayerStats` and the viewer ends up
// rendering "0/0 — died" on every floor.
//
// We resolve the path
//
//   RunManager.State (private) →
//     State.Players (IReadOnlyList<Player>)
//     State.CurrentMapPointHistoryEntry.GetEntry(player.NetId) →
//       entry.CurrentGold = player.Gold
//       entry.CurrentHp   = player.Creature.CurrentHp
//       entry.MaxHp       = player.Creature.MaxHp
//
// at Init() time so per-room-transition cost is just GetValue /
// SetValue calls. The engine-side method is patched by ReplayHook to
// call Stamp() and return false (skip the original gated body).
public static class PlayerStatsStamper
{
    private static PropertyInfo? _runManagerState;
    private static PropertyInfo? _statePlayers;
    private static PropertyInfo? _stateCurrentMapPointHistoryEntry;
    private static MethodInfo? _mapPointEntryGetEntry;
    private static PropertyInfo? _playerNetId;
    private static PropertyInfo? _playerGold;
    private static PropertyInfo? _playerCreature;
    private static PropertyInfo? _creatureCurrentHp;
    private static PropertyInfo? _creatureMaxHp;
    private static PropertyInfo? _playerEntryCurrentGold;
    private static PropertyInfo? _playerEntryCurrentHp;
    private static PropertyInfo? _playerEntryMaxHp;
    private static bool _initialised;

    // Resolves all reflection handles. Idempotent. Throws if any
    // required member is missing (game-version drift) so the bootstrap
    // surfaces it loudly instead of the recorder silently degrading
    // back to 0/0 HP.
    public static void Init(Assembly sts2)
    {
        if (_initialised) return;

        var runManagerType = sts2.GetType("MegaCrit.Sts2.Core.Runs.RunManager")
            ?? throw new InvalidOperationException("RunManager not found in sts2");
        // RunManager.State is declared as PRIVATE — mirror what
        // ReplayRecorder.ReadCurrentCombatContext does, the only
        // access path the engine itself uses.
        _runManagerState = runManagerType.GetProperty("State", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("RunManager.State (private) not found");

        // RunState is an internal-but-public class; resolve via the
        // State property's declared type rather than naming it.
        var runStateType = _runManagerState.PropertyType;
        _statePlayers = runStateType.GetProperty("Players", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("RunState.Players not found");
        _stateCurrentMapPointHistoryEntry = runStateType.GetProperty("CurrentMapPointHistoryEntry", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("RunState.CurrentMapPointHistoryEntry not found");

        var mapPointEntryType = _stateCurrentMapPointHistoryEntry.PropertyType;
        _mapPointEntryGetEntry = mapPointEntryType.GetMethod("GetEntry", BindingFlags.Public | BindingFlags.Instance, [typeof(ulong)])
            ?? throw new InvalidOperationException("MapPointHistoryEntry.GetEntry(ulong) not found");

        var playerType = sts2.GetType("MegaCrit.Sts2.Core.Entities.Players.Player")
            ?? throw new InvalidOperationException("Player not found in sts2");
        _playerNetId = playerType.GetProperty("NetId", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Player.NetId not found");
        _playerGold = playerType.GetProperty("Gold", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Player.Gold not found");
        _playerCreature = playerType.GetProperty("Creature", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Player.Creature not found");

        var creatureType = sts2.GetType("MegaCrit.Sts2.Core.Entities.Creatures.Creature")
            ?? throw new InvalidOperationException("Creature not found in sts2");
        _creatureCurrentHp = creatureType.GetProperty("CurrentHp", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Creature.CurrentHp not found");
        _creatureMaxHp = creatureType.GetProperty("MaxHp", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Creature.MaxHp not found");

        var entryType = sts2.GetType("MegaCrit.Sts2.Core.Runs.PlayerMapPointHistoryEntry")
            ?? throw new InvalidOperationException("PlayerMapPointHistoryEntry not found in sts2");
        _playerEntryCurrentGold = entryType.GetProperty("CurrentGold", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("PlayerMapPointHistoryEntry.CurrentGold not found");
        _playerEntryCurrentHp = entryType.GetProperty("CurrentHp", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("PlayerMapPointHistoryEntry.CurrentHp not found");
        _playerEntryMaxHp = entryType.GetProperty("MaxHp", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("PlayerMapPointHistoryEntry.MaxHp not found");

        _initialised = true;
    }

    // The body of the engine's UpdatePlayerStatsInMapPointHistory
    // (line 35993 of the v0.103.2 decompile), minus the TestMode /
    // State-null guard. Called from ReplayHook.BeforeUpdatePlayerStats
    // as a Harmony prefix; the prefix returns false so the original
    // (gated, no-op in our config) method doesn't run.
    public static void Stamp(object runManager)
    {
        if (!_initialised) return;
        var state = _runManagerState!.GetValue(runManager);
        if (state is null) return;

        var currentEntry = _stateCurrentMapPointHistoryEntry!.GetValue(state);
        if (currentEntry is null) return;

        if (_statePlayers!.GetValue(state) is not System.Collections.IEnumerable players) return;

        foreach (var player in players)
        {
            if (player is null) continue;
            var netId = (ulong)_playerNetId!.GetValue(player)!;
            // MapPointHistoryEntry.GetEntry throws if PlayerStats
            // lacks the netId — should never happen because the
            // constructor pre-populates one entry per player, but
            // we swallow defensively rather than crash the engine.
            object? entry;
            try { entry = _mapPointEntryGetEntry!.Invoke(currentEntry, [netId]); }
            catch { continue; }
            if (entry is null) continue;

            _playerEntryCurrentGold!.SetValue(entry, (int)_playerGold!.GetValue(player)!);

            var creature = _playerCreature!.GetValue(player);
            if (creature is null) continue;
            _playerEntryCurrentHp!.SetValue(entry, (int)_creatureCurrentHp!.GetValue(creature)!);
            _playerEntryMaxHp!.SetValue(entry, (int)_creatureMaxHp!.GetValue(creature)!);
        }
    }
}
