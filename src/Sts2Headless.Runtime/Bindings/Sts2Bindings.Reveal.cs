using System.Collections;
using System.Reflection;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Runtime.Bindings;

// Seed-deterministic reveal surface (the AD-7 debug counterpart to
// content/*). These methods leak info that *is* knowable from the run's
// seed — pre-rolled encounter sequences, the chosen ancient (Neow), the
// map layout's resolved point types — but is normally hidden from the
// player. Gated by --enable-debug at the wire layer (CheatHostMethods).
//
// Source of truth (from documentation/research/...):
//   - ActModel._rooms : RoomSet, populated by ActModel.GenerateRooms.
//   - RoomSet exposes NormalEncounterIds, EliteEncounterIds, EventIds,
//     BossId, SecondBossId, AncientId, plus per-pool consumed counters.
//
// All reflection here is best-effort: a missing field returns an empty
// list. A live host without an active run returns null for the schedule
// (callers must run/new first — same posture as every other debug method).
public sealed partial class Sts2Bindings
{
    // Wire-shape projection of the act's pre-rolled schedule. All ids are
    // engine-stable wire strings; null when the schedule isn't available.
    public sealed record ActScheduleSnapshot(
        int ActIndex,
        string? BossId,
        string? SecondBossId,
        string? AncientId,
        IReadOnlyList<string> NormalEncounterIds,
        IReadOnlyList<string> EliteEncounterIds,
        IReadOnlyList<string> EventIds,
        int NormalEncountersVisited,
        int EliteEncountersVisited,
        int EventsVisited);

    public ActScheduleSnapshot ReadActSchedule(RunHandle handle)
    {
        var act = _runStateAct?.GetValue(handle.RunState);
        // ActIndex lives on RunState (CurrentActIndex), not on ActModel —
        // ActModel itself is a single concrete class at the current pin
        // and doesn't carry an Index property. Read it through the
        // already-bound _runStateCurrentActIndex regardless of whether
        // the act resolves.
        var actIndex = ReadCurrentActIndex(handle.RunState);

        if (act is null)
        {
            return new ActScheduleSnapshot(
                ActIndex: actIndex,
                BossId: null, SecondBossId: null, AncientId: null,
                NormalEncounterIds: Array.Empty<string>(),
                EliteEncounterIds: Array.Empty<string>(),
                EventIds: Array.Empty<string>(),
                NormalEncountersVisited: 0,
                EliteEncountersVisited: 0,
                EventsVisited: 0);
        }

        // Resolve RoomSet — sts2 stores it as private field `_rooms` on
        // ActModel (confirmed via IL probe of ActModel.PullNextEncounter
        // at the current pin).
        var actType = act.GetType();
        var roomsField = actType.GetField("_rooms", BindingFlags.NonPublic | BindingFlags.Instance)
                       ?? actType.GetField("rooms", BindingFlags.NonPublic | BindingFlags.Instance);
        var roomsProp = actType.GetProperty("Rooms", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var rooms = roomsField?.GetValue(act) ?? roomsProp?.GetValue(act);
        if (rooms is null)
        {
            return new ActScheduleSnapshot(
                ActIndex: actIndex,
                BossId: null, SecondBossId: null, AncientId: null,
                NormalEncounterIds: Array.Empty<string>(),
                EliteEncounterIds: Array.Empty<string>(),
                EventIds: Array.Empty<string>(),
                NormalEncountersVisited: 0,
                EliteEncountersVisited: 0,
                EventsVisited: 0);
        }

        // Boss + SecondBoss live on ActModel directly (not on RoomSet);
        // Ancient is RoomSet.Ancient (a model). Schedule lists are
        // private camelCase fields on RoomSet — names confirmed via IL
        // probe of RoomSet.ToSave / MarkVisited at the current pin.
        return new ActScheduleSnapshot(
            ActIndex: actIndex,
            BossId: ReadModelEntryId(act, "BossEncounter"),
            SecondBossId: ReadModelEntryId(act, "SecondBossEncounter"),
            AncientId: ReadModelEntryId(rooms, "Ancient"),
            NormalEncounterIds: ReadIdEnumerable(rooms, "normalEncounters"),
            EliteEncounterIds: ReadIdEnumerable(rooms, "eliteEncounters"),
            EventIds: ReadIdEnumerable(rooms, "events"),
            NormalEncountersVisited: ReadIntProperty(rooms, "normalEncountersVisited") ?? 0,
            EliteEncountersVisited: ReadIntProperty(rooms, "eliteEncountersVisited") ?? 0,
            EventsVisited: ReadIntProperty(rooms, "eventsVisited") ?? 0);
    }

    // Walk obj.<propName>.Id.Entry for a model-shaped property (or
    // field) — wraps the common chain into one call. Returns null when
    // any link is missing.
    private static string? ReadModelEntryId(object obj, string propName)
    {
        var t = obj.GetType();
        var prop = t.GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var field = t.GetField(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var model = prop?.GetValue(obj) ?? field?.GetValue(obj);
        if (model is null) return null;
        var idProp = model.GetType().GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
        var id = idProp?.GetValue(model);
        if (id is null) return null;
        var entryProp = id.GetType().GetProperty("Entry", BindingFlags.Public | BindingFlags.Instance);
        return entryProp?.GetValue(id)?.ToString();
    }

    // RunState.CurrentActIndex — bound on Sts2Bindings.cs as a public
    // PropertyInfo (`_runStateCurrentActIndex`). The act-level Index is
    // not exposed on the ActModel directly at the current pin (there's
    // only one concrete ActModel type; per-act variation lives on the
    // instance's data fields, not its CLR type).
    private int ReadCurrentActIndex(object runState)
    {
        if (_runStateCurrentActIndex is null) return -1;
        return _runStateCurrentActIndex.GetValue(runState) is int n ? n : -1;
    }

    private IReadOnlyList<string> ReadIdEnumerable(object rooms, string propName)
    {
        var t = rooms.GetType();
        var prop = t.GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var field = t.GetField(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var v = prop?.GetValue(rooms) ?? field?.GetValue(rooms);
        if (v is not IEnumerable enumerable) return Array.Empty<string>();
        var result = new List<string>();
        foreach (var entry in enumerable)
        {
            if (entry is null) continue;
            if (entry is string s) { result.Add(s); continue; }
            var entryProp = entry.GetType().GetProperty("Entry", BindingFlags.Public | BindingFlags.Instance);
            var wireId = entryProp?.GetValue(entry)?.ToString() ?? entry.ToString();
            if (!string.IsNullOrEmpty(wireId)) result.Add(wireId!);
        }
        return result;
    }

    private static int? ReadIntProperty(object obj, string propName)
    {
        var prop = obj.GetType().GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (prop?.GetValue(obj) is int n) return n;
        var field = obj.GetType().GetField(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (field?.GetValue(obj) is int m) return m;
        return null;
    }

    // ── debug/reveal_map_layout ──────────────────────────────────────────────

    // One node in the pre-rolled map layout, plus the (col,row) edges to
    // points in the next row that are reachable from it. PointType is the
    // engine's resolved value at generation time — including for what the
    // player would see as `?` (Unknown). Unknown points whose runtime room
    // is *still* rolled lazily on entry (UnknownMapPointOdds.Roll) stay
    // Unknown here; the wire is honest about that prior nature.
    public sealed record MapLayoutPointSnapshot(
        int Col,
        int Row,
        MapNodeType Type,
        IReadOnlyList<(int Col, int Row)> Children);

    // Full pre-rolled layout for the current act. ActIndex is -1 when no
    // map is bound (pre-EnterAct / between acts); Points is empty in that
    // case so callers always get a wire-stable shape.
    public sealed record MapLayoutSnapshot(
        int ActIndex,
        IReadOnlyList<MapLayoutPointSnapshot> Points);

    public MapLayoutSnapshot ReadMapLayout(RunHandle handle)
    {
        var runState = handle.RunState;
        var actIndex = ReadCurrentActIndex(runState);

        var map = _runStateMap.GetValue(runState);
        if (map is null)
        {
            return new MapLayoutSnapshot(
                ActIndex: actIndex,
                Points: Array.Empty<MapLayoutPointSnapshot>());
        }

        // Enumerate every point on the map. Engine API is
        // ActMap.GetAllMapPoints() : IEnumerable<MapPoint>. Probe via
        // reflection so AD-4 stays intact (no compile-time sts2 ref).
        var mapType = map.GetType();
        var getAllPoints = mapType.GetMethod(
            "GetAllMapPoints",
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null);
        if (getAllPoints is null)
        {
            return new MapLayoutSnapshot(
                ActIndex: actIndex,
                Points: Array.Empty<MapLayoutPointSnapshot>());
        }

        IEnumerable? allPoints;
        try
        {
            allPoints = getAllPoints.Invoke(map, null) as IEnumerable;
        }
        catch
        {
            // Soft-fail: a broken walk shouldn't poison the wire surface.
            allPoints = null;
        }
        if (allPoints is null)
        {
            return new MapLayoutSnapshot(
                ActIndex: actIndex,
                Points: Array.Empty<MapLayoutPointSnapshot>());
        }

        var result = new List<MapLayoutPointSnapshot>();
        foreach (var point in allPoints)
        {
            if (point is null) continue;
            try
            {
                var coord = _mapPointCoord.GetValue(point);
                if (coord is null) continue;
                var col = Convert.ToInt32(_mapCoordColField.GetValue(coord));
                var row = Convert.ToInt32(_mapCoordRowField.GetValue(coord));

                var pointTypeValue = _mapPointPointType.GetValue(point);
                var pointTypeName = pointTypeValue?.ToString();
                var type = pointTypeName is not null
                           && Enum.TryParse<MapNodeType>(pointTypeName, ignoreCase: false, out var parsed)
                    ? parsed
                    : MapNodeType.Unknown;

                var children = new List<(int Col, int Row)>();
                if (_mapPointChildren.GetValue(point) is IEnumerable childEnumerable)
                {
                    foreach (var child in childEnumerable)
                    {
                        if (child is null) continue;
                        var childCoord = _mapPointCoord.GetValue(child);
                        if (childCoord is null) continue;
                        var childCol = Convert.ToInt32(_mapCoordColField.GetValue(childCoord));
                        var childRow = Convert.ToInt32(_mapCoordRowField.GetValue(childCoord));
                        children.Add((childCol, childRow));
                    }
                }

                result.Add(new MapLayoutPointSnapshot(col, row, type, children));
            }
            catch
            {
                // Per-point soft-fail — skip and keep walking.
                continue;
            }
        }

        return new MapLayoutSnapshot(
            ActIndex: actIndex,
            Points: result);
    }
}
