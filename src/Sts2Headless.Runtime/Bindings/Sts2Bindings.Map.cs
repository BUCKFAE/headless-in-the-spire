using System.Reflection;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Runtime.Bindings;

// Map traversal + act-transition operations. EnterMapCoord drives the wire
// run/select_map_node call; EnterNextAct is the boss → next-act bridge that
// the engine doesn't fire on its own once boss rewards are drained.
// ReadAvailableMapNodes is the snapshot helper consumed by Sts2Bindings.Read.cs.
// All backed by the `_map*` / `_runState*` fields declared in Sts2Bindings.cs.
public sealed partial class Sts2Bindings
{
    // Boss → next act transition. After an act boss is defeated and its
    // rewards drained, the engine leaves the player in a stale MapRoom
    // whose CurrentMapCoord no longer points at a real node — the snapshot
    // surfaces an empty AvailableMapNodes. EnterNextAct bumps
    // RunState.CurrentActIndex, regenerates the next act's map, and
    // re-enters at the start node. Sts2-cli mirrors this at
    // RunSimulator.cs:2221 — only safe to call once the wire reports
    // CurrentRoomType == BossRoom with combat ended and the boss reward
    // chain consumed.
    //
    // Caller guard: we require the engine-reported `CurrentRoom` to be a
    // CombatRoom whose current map point is a Boss point (i.e. what the
    // snapshot surfaces as BossRoom). Calling from anywhere else throws.
    public void EnterNextAct(RunHandle handle)
    {
        var room = _runStateCurrentRoom.GetValue(handle.RunState)
            ?? throw new InvalidOperationException("RunState.CurrentRoom was null");
        var roomTypeName = room.GetType().Name;
        var roomType = Enum.TryParse<RoomType>(roomTypeName, ignoreCase: false, out var parsedRoom)
            ? parsedRoom
            : RoomType.Unknown;

        // Two legal entry conditions, both meaning "post-boss, pre-next-act":
        //   1. CurrentRoom is the boss CombatRoom and combat has ended —
        //      sts2-cli's pattern (RunSimulator.cs:1655). Rare for us in
        //      practice because our reward-drain flow tends to advance
        //      past the room before the caller invokes enter_next_act.
        //   2. CurrentRoom is MapRoom but AvailableMapNodes is empty —
        //      the engine has flipped the room post-rewards but the
        //      current map coord no longer points at a real node, so
        //      forward map traversal is blocked. This is the state
        //      DiagnoseAct2WalkTests observed on seed 42 floor 17.
        // Anything else (mid-combat, mid-event, non-boss map node with
        // valid children) is a caller mistake — reject so a stray call
        // never silently corrupts run state by skipping an act.
        var onBossPoint = roomType == RoomType.CombatRoom && IsCurrentMapPointBoss(handle.RunState);
        var stuckPostBossMap = roomType == RoomType.MapRoom
            && ReadAvailableMapNodes(handle.RunState).Count == 0;
        if (!onBossPoint && !stuckPostBossMap)
        {
            throw new InvalidOperationException(
                $"run/enter_next_act called but current room is {roomTypeName} with " +
                $"{ReadAvailableMapNodes(handle.RunState).Count} available map nodes. " +
                $"Only legal after defeating an act boss and draining rewards.");
        }

        _syncCtx?.Pump();
        DrainActionExecutor(handle);

        var result = _runManagerEnterNextAct.Invoke(handle.RunManager, null);
        if (result is Task task) task.GetAwaiter().GetResult();
        _syncCtx?.Pump();
        DrainActionExecutor(handle);
    }

    // Read the current map point's PointType, or null if we're not standing
    // on a map point (pre-EnterAct, between rooms, stale coord). Used by
    // BuildSnapshot to flip CombatRoom → BossRoom for the act-boss combat,
    // which the engine itself reports as a plain CombatRoom.
    private string? ReadCurrentMapPointTypeName(object runState)
    {
        var map = _runStateMap.GetValue(runState);
        if (map is null) return null;
        var currentCoord = _runStateCurrentMapCoord.GetValue(runState);
        if (currentCoord is null) return null;
        var currentPoint = _mapGetPoint.Invoke(map, new[] { currentCoord });
        if (currentPoint is null) return null;
        return _mapPointPointType.GetValue(currentPoint)?.ToString();
    }

    private bool IsCurrentMapPointBoss(object runState)
        => string.Equals(ReadCurrentMapPointTypeName(runState), "Boss", StringComparison.Ordinal);

    // Enumerate next-move candidates by mirroring sts2-cli's MapSelectState:
    // - currentCoord null → starting position; offer the start node plus its
    //   children (sts2 lets you pick any of the starting-row entries).
    // - currentCoord set + GetPoint returns a MapPoint → its Children.
    // - currentCoord set + GetPoint returns null → stale coord after a forced
    //   transition; fall back to StartingMapPoint.Children so the wire stays
    //   honest rather than silently empty.
    // Returns [] if the Map isn't available yet (pre-EnterAct, mid-event).
    private IReadOnlyList<MapNode> ReadAvailableMapNodes(object runState)
    {
        var map = _runStateMap.GetValue(runState);
        if (map is null) return Array.Empty<MapNode>();

        var currentCoord = _runStateCurrentMapCoord.GetValue(runState);

        if (currentCoord is null)
        {
            var startingPoint = _mapStartingMapPoint.GetValue(map);
            if (startingPoint is null) return Array.Empty<MapNode>();

            var result = new List<MapNode> { ToMapNode(startingPoint) };
            AppendChildren(startingPoint, result);
            return result;
        }

        var currentPoint = _mapGetPoint.Invoke(map, new[] { currentCoord });
        if (currentPoint is null)
        {
            var startingPoint = _mapStartingMapPoint.GetValue(map);
            if (startingPoint is null) return Array.Empty<MapNode>();
            var fallback = new List<MapNode>();
            AppendChildren(startingPoint, fallback);
            return fallback;
        }

        var children = new List<MapNode>();
        AppendChildren(currentPoint, children);
        return children;
    }

    private void AppendChildren(object mapPoint, List<MapNode> sink)
    {
        if (_mapPointChildren.GetValue(mapPoint) is not System.Collections.IEnumerable children) return;
        foreach (var child in children)
        {
            if (child is null) continue;
            sink.Add(ToMapNode(child));
        }
    }

    private MapNode ToMapNode(object mapPoint)
    {
        var coord = _mapPointCoord.GetValue(mapPoint)
            ?? throw new InvalidOperationException("MapPoint.coord was null");
        var col = Convert.ToInt32(_mapCoordColField.GetValue(coord));
        var row = Convert.ToInt32(_mapCoordRowField.GetValue(coord));

        var pointTypeValue = _mapPointPointType.GetValue(mapPoint);
        var pointTypeName = pointTypeValue?.ToString();
        // Same Unknown-fallback discipline as RoomType. If the test harness
        // reports an Unknown that isn't actually unknown, the fix is to add
        // the value to MapNodeType — not to widen the parse.
        var type = pointTypeName is not null
                   && Enum.TryParse<MapNodeType>(pointTypeName, ignoreCase: false, out var parsed)
            ? parsed
            : MapNodeType.Unknown;

        return new MapNode(col, row, type);
    }

    // Build a MapCoord from {col, row} and pump RunManager.EnterMapCoord.
    // MapCoord is a struct with public Col/Row fields (probed at Bind time);
    // construct via Activator.CreateInstance + field set rather than chasing
    // ctor overloads, since structs in sts2 favour init-set fields.
    public void EnterMapCoord(RunHandle handle, int col, int row)
    {
        var coord = Activator.CreateInstance(_mapCoordType)
            ?? throw new InvalidOperationException($"{_mapCoordType.FullName} default ctor returned null");
        SetCoordComponent(coord, "col", col);
        SetCoordComponent(coord, "row", row);

        var result = _runManagerEnterMapCoord.Invoke(handle.RunManager, new Dictionary<string, object?>
        {
            ["coord"] = coord,
        });
        if (result is Task t) t.GetAwaiter().GetResult();

        // EnterMapCoord returns once the room transition starts; combat-setup
        // actions (CombatRoom.EnterInternal → CombatManager.SetUpCombat →
        // StartCombatInternal) are queued on the ActionExecutor and need
        // draining before the snapshot will see IsInProgress=true.
        DrainActionExecutor(handle);
    }

    // MapCoord's fields are lowercase in sts2 (`col`, `row`); fall back to
    // PascalCase if a future version renames them. FieldInfo.SetValue on a
    // boxed value-type writes directly into the box, which is what we want
    // since we pass `coord` through to EnterMapCoord unmodified.
    private static void SetCoordComponent(object coord, string lowerName, int value)
    {
        var t = coord.GetType();
        var upperName = char.ToUpperInvariant(lowerName[0]) + lowerName[1..];
        var field = t.GetField(lowerName, BindingFlags.Public | BindingFlags.Instance)
                    ?? t.GetField(upperName, BindingFlags.Public | BindingFlags.Instance);
        if (field is not null) { field.SetValue(coord, value); return; }
        var prop = t.GetProperty(lowerName, BindingFlags.Public | BindingFlags.Instance)
                   ?? t.GetProperty(upperName, BindingFlags.Public | BindingFlags.Instance)
                   ?? throw new InvalidOperationException($"MapCoord has no '{lowerName}' field or property");
        prop.SetValue(coord, value);
    }
}
