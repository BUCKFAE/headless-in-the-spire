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
    [property: JsonPropertyName("currentRoomType")] RoomType CurrentRoomType);

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
    [property: JsonPropertyName("isGameOver")] bool IsGameOver);

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
    [property: JsonPropertyName("hp")] int Hp);
