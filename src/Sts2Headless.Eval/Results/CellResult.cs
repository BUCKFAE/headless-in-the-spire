using System.Text.Json.Serialization;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Eval;

// One row in `runs.jsonl`, one cell in the matrix. Append-only — written
// incrementally as cells complete so a partial eval is inspectable
// mid-flight.
//
// NFR-1 (output is the API): adding fields here is fine; removing or
// renaming is a breaking change with the same discipline as the wire
// protocol (AD-5). Downstream tools (plots, leaderboard renderers) read
// this shape.
public sealed record CellResult(
    [property: JsonPropertyName("evalId")]        string                    EvalId,
    [property: JsonPropertyName("agent")]         AgentIdentity             Agent,
    [property: JsonPropertyName("seed")]          ulong                     Seed,
    [property: JsonPropertyName("character")]     Character                 Character,
    [property: JsonPropertyName("ascension")]     int                       Ascension,
    [property: JsonPropertyName("modifiers")]     IReadOnlyList<ModifierId> Modifiers,
    [property: JsonPropertyName("terminus")]      CellTerminus              Terminus,
    [property: JsonPropertyName("floorReached")]  int                       FloorReached,
    [property: JsonPropertyName("finalHp")]       int                       FinalHp,
    [property: JsonPropertyName("maxHp")]         int                       MaxHp,
    [property: JsonPropertyName("gold")]          int                       Gold,
    [property: JsonPropertyName("deckSize")]      int                       DeckSize,
    [property: JsonPropertyName("relicCount")]    int                       RelicCount,
    [property: JsonPropertyName("combatCount")]   int                       CombatCount,
    [property: JsonPropertyName("eliteCount")]    int                       EliteCount,
    [property: JsonPropertyName("bossCount")]     int                       BossCount,
    [property: JsonPropertyName("turnsInCombat")] int                       TurnsInCombat,
    [property: JsonPropertyName("steps")]         int                       Steps,
    [property: JsonPropertyName("wallClockMs")]   long                      WallClockMs,
    [property: JsonPropertyName("replayPath")]    string                    ReplayPath,
    [property: JsonPropertyName("gameVersion")]   string                    GameVersion,
    [property: JsonPropertyName("sts2DllSha256")] string                    Sts2DllSha256,
    [property: JsonPropertyName("scoring")]       ScoringMetrics            Scoring,
    [property: JsonPropertyName("error")]         WireErrorPayload?         Error          = null,
    [property: JsonPropertyName("startedAt")]     string?                   StartedAt      = null,
    [property: JsonPropertyName("completedAt")]   string?                   CompletedAt    = null);

// Per-cell scoring values. Single field today (`Score`, the canonical
// number the scoring function emits) so it round-trips through
// `summary.json`. Add aggregate-shaped fields here only if a scoring
// function legitimately needs them per-row; otherwise compute in
// AgentAggregates.
public sealed record ScoringMetrics(
    [property: JsonPropertyName("score")] double Score);

// Captured when the cell terminated via an error path. Mirrors the
// host wire's `Error` envelope shape so a tool tracing wire-side
// failures sees the same code/message it would see on the host wire.
public sealed record WireErrorPayload(
    [property: JsonPropertyName("code")]    int     Code,
    [property: JsonPropertyName("message")] string  Message,
    [property: JsonPropertyName("stack")]   string? Stack = null);
