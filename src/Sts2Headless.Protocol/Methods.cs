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

// ── host/ping ────────────────────────────────────────────────────────────

public sealed record HostPingResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("gameVersion")] string? GameVersion,
    [property: JsonPropertyName("gameSha256")] string? GameSha256);

// ── run/new ──────────────────────────────────────────────────────────────

// Both fields optional on the wire — character defaults to "ironclad", seed
// to 1. Defaults are applied in the handler, not the record, so the JSON
// schema matches "field absent" cleanly and the deserialiser doesn't need
// to know.
public sealed record RunNewParams(
    [property: JsonPropertyName("character")] string? Character = null,
    [property: JsonPropertyName("seed")] ulong? Seed = null);

public sealed record RunNewResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("character")] string Character,
    [property: JsonPropertyName("seed")] ulong Seed,
    [property: JsonPropertyName("playerType")] string PlayerType,
    [property: JsonPropertyName("currentRoomType")] string CurrentRoomType);

// ── run/state ────────────────────────────────────────────────────────────

// No params record — run/state reads from session state.
public sealed record RunStateResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("character")] string? Character,
    [property: JsonPropertyName("seed")] ulong Seed,
    [property: JsonPropertyName("hp")] int Hp,
    [property: JsonPropertyName("maxHp")] int MaxHp,
    [property: JsonPropertyName("gold")] int Gold,
    [property: JsonPropertyName("deckSize")] int DeckSize,
    [property: JsonPropertyName("currentRoomType")] string CurrentRoomType,
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
    [property: JsonPropertyName("currentRoomType")] string CurrentRoomType,
    [property: JsonPropertyName("actFloor")] int ActFloor,
    [property: JsonPropertyName("isGameOver")] bool IsGameOver,
    [property: JsonPropertyName("hp")] int Hp);
