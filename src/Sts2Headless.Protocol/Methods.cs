using System.Text.Json.Serialization;

namespace Sts2Headless.Protocol.Methods;

// Per-method request/response DTOs. The Envelope layer (Request/Response)
// carries these as JsonNode payloads; both the host's method handlers and
// the integration tests (de)serialise through these records so a renamed
// field is a compile error on both sides, not a silently-passing test.
//
// Naming: <Method>Params for inputs (omit if no params), <Method>Result for
// outputs. Property names map to JSON via [JsonPropertyName] — explicit on
// every field so the wire shape is grep-able from this file alone.
//
// All response shapes carry `ok` as a redundant boolean — clients should
// branch on the envelope's `error` field, but `ok: true` is a useful sanity
// hint in logs and matches the pattern sts2-cli established.

// ── Enums ────────────────────────────────────────────────────────────────

// Playable characters. Wire shape is lowercase ("ironclad", "silent", ...)
// to match sts2-cli convention; on the .NET side the standard PascalCase
// enum names are used. Future-binding values (Silent, Defect, …) are listed
// even though the host only accepts Ironclad — they form the wire schema
// callers can target, and the host's "not yet supported" error message
// reports the requested value.
[JsonConverter(typeof(JsonStringEnumConverter<Character>))]
public enum Character
{
    [JsonStringEnumMemberName("ironclad")] Ironclad,
    [JsonStringEnumMemberName("silent")] Silent,
    [JsonStringEnumMemberName("defect")] Defect,
    [JsonStringEnumMemberName("watcher")] Watcher,
    [JsonStringEnumMemberName("regent")] Regent,
    [JsonStringEnumMemberName("necrobinder")] Necrobinder,
}

// Room types we've seen the engine land at via the StartRun chain. Wire
// shape matches the sts2 type name exactly (PascalCase like "MapRoom"),
// which is what `room.GetType().Name` returns server-side. Enum values
// missing from this list arrive as `Unknown` so the wire never carries
// a free-form string for a type we haven't catalogued — that would be
// the spelling-mistake trap this enum exists to prevent.
[JsonConverter(typeof(JsonStringEnumConverter<RoomType>))]
public enum RoomType
{
    Unknown,
    MapRoom,
    EventRoom,
    CombatRoom,
    RestSiteRoom,
    MerchantRoom,
    TreasureRoom,
    BossRoom,
}

// Map-node types we've seen sts2's MapPoint.PointType report. Distinct from
// RoomType: this is the *kind* of node painted on the act map (Monster,
// Elite, …), whereas RoomType is the runtime room you land in after picking
// the node (Elite resolves to CombatRoom, etc.). Same Unknown-fallback
// discipline as RoomType — grow the enum as integration tests surface new
// names rather than widening the parser.
//
// Observed so far: Monster (act 0 starting nodes). Elite/Event/RestSite/
// Treasure/Merchant/Boss are speculative — left in to document the schema
// callers can expect, validated when the corresponding nodes actually appear.
[JsonConverter(typeof(JsonStringEnumConverter<MapNodeType>))]
public enum MapNodeType
{
    Unknown,
    Monster,
    Elite,
    Event,
    RestSite,
    Treasure,
    Merchant,
    Boss,
}

// One legal next move from the current map position. Col/row feed straight
// back into run/select_map_node; Type lets clients distinguish "definitely
// combat" from "definitely shop" without booting the room first.
public sealed record MapNode(
    [property: JsonPropertyName("col")] int Col,
    [property: JsonPropertyName("row")] int Row,
    [property: JsonPropertyName("type")] MapNodeType Type);

// One option offered by an Event the player is currently standing on.
//
// Index is the position in the current page's option list — pass it back via
// run/select_event_option. We don't surface a stable id because sts2 itself
// doesn't expose one beyond the loc text-key, and the index alone is what
// EventOption.Chosen() dispatches on.
//
// TextKey is the loc string the game uses to look up the option's title
// (e.g. "NEOW.pages.INITIAL.options.STONE_HUMIDIFIER"). It's the closest
// thing to a stable identifier and lets callers branch on "the relic option"
// without booking knowledge of the index ordering. Null when the option has
// no loc binding (a few procedurally-generated options).
//
// IsLocked mirrors the in-game "you can't pick this yet" flag — surfaced so
// callers can grey-out rather than guess. The host does not block locked
// picks server-side; if a caller still selects one, sts2's Chosen() will
// no-op and the room will stay put.
public sealed record EventOption(
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("textKey")] string? TextKey,
    [property: JsonPropertyName("isLocked")] bool IsLocked);

// ── host/ping ────────────────────────────────────────────────────────────

public sealed record HostPingResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("gameVersion")] string? GameVersion,
    [property: JsonPropertyName("gameSha256")] string? GameSha256);

// ── run/new ──────────────────────────────────────────────────────────────

// Fields optional on the wire — character defaults to Ironclad, seed to 1,
// withNeow to false. Defaults are applied in the handler, not the record,
// so the JSON schema matches "field absent" cleanly and the deserialiser
// doesn't need to know.
//
// withNeow=true opts into the Neow blessing event (lands CurrentRoom on the
// Neow EventRoom). No wire method yet exists to *dismiss* the event, so
// clients that opt in are accepting a room they can't currently leave;
// useful for state-shape tests, not for end-to-end runs.
public sealed record RunNewParams(
    [property: JsonPropertyName("character")] Character? Character = null,
    [property: JsonPropertyName("seed")] ulong? Seed = null,
    [property: JsonPropertyName("withNeow")] bool? WithNeow = null);

public sealed record RunNewResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("character")] Character Character,
    [property: JsonPropertyName("seed")] ulong Seed,
    [property: JsonPropertyName("playerType")] string PlayerType,
    [property: JsonPropertyName("currentRoomType")] RoomType CurrentRoomType,
    // Legal next moves from the current map position. Empty when the player
    // isn't standing on the map (e.g. mid-combat, on Neow's event); callers
    // should re-issue run/state once the current room resolves.
    [property: JsonPropertyName("availableMapNodes")] IReadOnlyList<MapNode> AvailableMapNodes,
    // Legal event-option picks when the current room is an Event. Empty
    // unless currentRoomType == EventRoom; mirrors the "current page" of the
    // active Event, so callers should re-read after run/select_event_option
    // (multi-page events refresh this list each turn).
    [property: JsonPropertyName("availableEventOptions")] IReadOnlyList<EventOption> AvailableEventOptions);

// ── run/state ────────────────────────────────────────────────────────────

// No params record — run/state reads from session state.
public sealed record RunStateResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("character")] Character? Character,
    [property: JsonPropertyName("seed")] ulong Seed,
    [property: JsonPropertyName("hp")] int Hp,
    [property: JsonPropertyName("maxHp")] int MaxHp,
    [property: JsonPropertyName("gold")] int Gold,
    [property: JsonPropertyName("deckSize")] int DeckSize,
    [property: JsonPropertyName("currentRoomType")] RoomType CurrentRoomType,
    [property: JsonPropertyName("actFloor")] int ActFloor,
    [property: JsonPropertyName("isGameOver")] bool IsGameOver,
    [property: JsonPropertyName("availableMapNodes")] IReadOnlyList<MapNode> AvailableMapNodes,
    [property: JsonPropertyName("availableEventOptions")] IReadOnlyList<EventOption> AvailableEventOptions);

// ── run/select_map_node ──────────────────────────────────────────────────

public sealed record RunSelectMapNodeParams(
    [property: JsonPropertyName("col")] int Col,
    [property: JsonPropertyName("row")] int Row);

public sealed record RunSelectMapNodeResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("col")] int Col,
    [property: JsonPropertyName("row")] int Row,
    [property: JsonPropertyName("currentRoomType")] RoomType CurrentRoomType,
    [property: JsonPropertyName("actFloor")] int ActFloor,
    [property: JsonPropertyName("isGameOver")] bool IsGameOver,
    [property: JsonPropertyName("hp")] int Hp,
    [property: JsonPropertyName("availableMapNodes")] IReadOnlyList<MapNode> AvailableMapNodes,
    [property: JsonPropertyName("availableEventOptions")] IReadOnlyList<EventOption> AvailableEventOptions);

// ── run/select_event_option ──────────────────────────────────────────────

// optionIndex matches the EventOption.Index returned by the most recent
// run/state (or run/new) when the current room is an Event. Picking a
// locked option is permitted by the wire layer — sts2 simply ignores it —
// and the resulting AvailableEventOptions tells the caller whether the
// page advanced.
public sealed record RunSelectEventOptionParams(
    [property: JsonPropertyName("optionIndex")] int OptionIndex);

public sealed record RunSelectEventOptionResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("optionIndex")] int OptionIndex,
    [property: JsonPropertyName("currentRoomType")] RoomType CurrentRoomType,
    [property: JsonPropertyName("actFloor")] int ActFloor,
    [property: JsonPropertyName("isGameOver")] bool IsGameOver,
    [property: JsonPropertyName("hp")] int Hp,
    [property: JsonPropertyName("availableMapNodes")] IReadOnlyList<MapNode> AvailableMapNodes,
    [property: JsonPropertyName("availableEventOptions")] IReadOnlyList<EventOption> AvailableEventOptions);
