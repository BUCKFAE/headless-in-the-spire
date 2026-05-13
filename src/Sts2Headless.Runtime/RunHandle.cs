using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Runtime;

// Wire-facing types that the binding layer produces and the host layer
// consumes. Kept separate from Sts2Bindings so callers can import just the
// shape without pulling in the reflection internals.

// A "live run" is a triple: the Player aggregate, the RunState owned by the
// game, and the RunManager singleton instance that mutates them. Wire code
// treats it opaquely; the binding layer is the only thing that destructures.
public sealed record RunHandle(object Player, object RunState, object RunManager);

// Snapshot of the run for read-only wire surfacing. ExpandableRecord pattern:
// add fields as we bind more reads, never break existing JSON shape.
// CurrentRoomType is the Protocol enum, mapped from sts2's `room.GetType().Name`
// at the binding layer — unknown sts2 rooms come back as RoomType.Unknown.
// AvailableMapNodes is the list of legal next moves from the current map
// position; empty when the player isn't standing on the map.
// AvailableEventOptions are the current-page picks for an active Event;
// empty unless CurrentRoomType == EventRoom.
public sealed record RunSnapshot(
    int CurrentHp,
    int MaxHp,
    int Gold,
    int DeckSize,
    RoomType CurrentRoomType,
    int ActFloor,
    bool IsGameOver,
    IReadOnlyList<MapNode> AvailableMapNodes,
    IReadOnlyList<EventOption> AvailableEventOptions);
