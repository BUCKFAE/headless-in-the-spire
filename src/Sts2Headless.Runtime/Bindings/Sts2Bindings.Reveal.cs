using System.Collections;
using System.Reflection;

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
        if (act is null)
        {
            return new ActScheduleSnapshot(
                ActIndex: -1,
                BossId: null, SecondBossId: null, AncientId: null,
                NormalEncounterIds: Array.Empty<string>(),
                EliteEncounterIds: Array.Empty<string>(),
                EventIds: Array.Empty<string>(),
                NormalEncountersVisited: 0,
                EliteEncountersVisited: 0,
                EventsVisited: 0);
        }

        // Resolve RoomSet lazily — sts2 stores it as private field
        // `_rooms` on ActModel. Probe both backing-field naming
        // conventions just in case the engine renames it.
        var actType = act.GetType();
        var roomsField = actType.GetField("_rooms", BindingFlags.NonPublic | BindingFlags.Instance)
                       ?? actType.GetField("rooms", BindingFlags.NonPublic | BindingFlags.Instance);
        var roomsProp = actType.GetProperty("Rooms", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var rooms = roomsField?.GetValue(act) ?? roomsProp?.GetValue(act);

        var actIndex = ReadActIndex(act);
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

        return new ActScheduleSnapshot(
            ActIndex: actIndex,
            BossId: ReadRoomSetId(rooms, "BossId"),
            SecondBossId: ReadRoomSetId(rooms, "SecondBossId"),
            AncientId: ReadRoomSetId(rooms, "AncientId"),
            NormalEncounterIds: ReadIdEnumerable(rooms, "NormalEncounterIds"),
            EliteEncounterIds: ReadIdEnumerable(rooms, "EliteEncounterIds"),
            EventIds: ReadIdEnumerable(rooms, "EventIds"),
            NormalEncountersVisited: ReadIntProperty(rooms, "NormalEncountersVisited") ?? 0,
            EliteEncountersVisited: ReadIntProperty(rooms, "EliteEncountersVisited") ?? 0,
            EventsVisited: ReadIntProperty(rooms, "EventsVisited") ?? 0);
    }

    private static int ReadActIndex(object act)
    {
        var t = act.GetType();
        var prop = t.GetProperty("ActIndex", BindingFlags.Public | BindingFlags.Instance)
                   ?? t.GetProperty("Index", BindingFlags.Public | BindingFlags.Instance);
        if (prop?.GetValue(act) is int n) return n;
        // Fallback: derive from class name (Act1Model → 0, Act2Model → 1)
        var name = t.Name;
        if (name.StartsWith("Act", StringComparison.Ordinal) && name.Length > 3
            && int.TryParse(name.AsSpan(3, 1), out var parsed))
        {
            return parsed - 1;
        }
        return -1;
    }

    // ModelId-shaped property (or string?) → wire entry string.
    private string? ReadRoomSetId(object rooms, string propName)
    {
        var t = rooms.GetType();
        var prop = t.GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var field = t.GetField(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var v = prop?.GetValue(rooms) ?? field?.GetValue(rooms);
        if (v is null) return null;
        if (v is string s) return s;
        // ModelId struct → read Entry.
        var entryProp = v.GetType().GetProperty("Entry", BindingFlags.Public | BindingFlags.Instance);
        return entryProp?.GetValue(v)?.ToString() ?? v.ToString();
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
}
