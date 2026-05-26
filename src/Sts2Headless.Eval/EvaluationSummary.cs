using System.Text.Json.Serialization;
using Sts2Headless.Eval.Scoring;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Eval;

// Per-eval rollup. The downstream contract (NFR-1) is the JSON shape of
// `summary.json` (this record) + `runs.jsonl` (`CellResult` lines).
public sealed record EvaluationSummary(
    [property: JsonPropertyName("evalId")]        string                    EvalId,
    [property: JsonPropertyName("gameVersion")]   string                    GameVersion,
    [property: JsonPropertyName("sts2DllSha256")] string                    Sts2DllSha256,
    [property: JsonPropertyName("seedBank")]      SeedBankReference         SeedBank,
    [property: JsonPropertyName("characters")]    IReadOnlyList<Character>  Characters,
    [property: JsonPropertyName("ascensions")]    IReadOnlyList<int>        Ascensions,
    [property: JsonPropertyName("modifiers")]     IReadOnlyList<ModifierId> Modifiers,
    [property: JsonPropertyName("scoring")]       ScoringFunctionReference  Scoring,
    [property: JsonPropertyName("elapsedMs")]     long                      ElapsedMs,
    [property: JsonPropertyName("cellCount")]     int                       CellCount,
    [property: JsonPropertyName("workers")]       int                       Workers,
    [property: JsonPropertyName("ranking")]       IReadOnlyList<AgentRanking> Ranking,
    [property: JsonPropertyName("notableCells")]  IReadOnlyList<NotableCell>  NotableCells);

// Identity-shaped reference to the seed bank used. Just enough to find
// the bank on disk without re-serialising every seed into summary.json.
public sealed record SeedBankReference(
    [property: JsonPropertyName("name")]    string Name,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("count")]   int    Count);

// Identity-shaped reference to the scoring function. Recorded so a
// downstream consumer can recognise that two leaderboards using
// different functions over the same data are not directly comparable.
public sealed record ScoringFunctionReference(
    [property: JsonPropertyName("name")]    string Name,
    [property: JsonPropertyName("version")] string Version);

// One row in `summary.json`'s `notableCells` list. Auto-populated with
// every cell whose terminus is in the crash family — those are the
// cells a maintainer wants to triage first.
public sealed record NotableCell(
    [property: JsonPropertyName("agent")]      string       Agent,
    [property: JsonPropertyName("seed")]       ulong        Seed,
    [property: JsonPropertyName("terminus")]   CellTerminus Terminus,
    [property: JsonPropertyName("floor")]      int          Floor,
    [property: JsonPropertyName("replayPath")] string       ReplayPath,
    [property: JsonPropertyName("error")]      WireErrorPayload? Error = null);
