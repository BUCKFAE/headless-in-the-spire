using System.Text.Json.Serialization;
using Sts2Headless.Agents.Contracts;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Eval.Protocol;

// Wire DTOs for the `agent/*` dialect (AD-9). The dialect is the mirror
// of the host dialect (AD-2) — same NDJSON envelope, same JSON-RPC
// reserved codes, same EnvelopeIo framing. Three methods total: init,
// decide, teardown. Stateful by design: the agent process is kept alive
// across all agent/decide calls for one cell so planners can cache.

// Sent once per cell, before any agent/decide. Carries the per-cell
// context the agent needs to set up — character, seed, ascension, the
// resolved budgets after per-manifest overrides, and the harness's
// eval-id so the agent can stamp its own logs / caches against the
// same run identity.
public sealed record AgentInitParams(
    [property: JsonPropertyName("gameVersion")]   string                    GameVersion,
    [property: JsonPropertyName("sts2DllSha256")] string                    Sts2DllSha256,
    [property: JsonPropertyName("character")]     Character                 Character,
    [property: JsonPropertyName("seed")]          ulong                     Seed,
    [property: JsonPropertyName("ascension")]     int                       Ascension,
    [property: JsonPropertyName("modifiers")]     IReadOnlyList<ModifierId> Modifiers,
    [property: JsonPropertyName("budgets")]       HarnessBudgets            Budgets,
    [property: JsonPropertyName("evalId")]        string                    EvalId);

// The agent's response to agent/init. Self-reports name + version (the
// harness cross-checks against the manifest), and may attach a free-text
// notes line that lands in the eval log. Mismatched name/version is a
// hard error: the harness fails the cell with HarnessError.
public sealed record AgentInitResult(
    [property: JsonPropertyName("name")]    string  Name,
    [property: JsonPropertyName("version")] string  Version,
    [property: JsonPropertyName("notes")]   string? Notes = null);

// Sent once per host step. Carries the full RunStateResult snapshot the
// host just produced — byte-for-byte the same shape the run/state wire
// method returns. The agent inspects it and returns one AgentAction.
public sealed record AgentDecideParams(
    [property: JsonPropertyName("snapshot")] RunStateResult Snapshot);

// The agent's chosen action plus an optional notes line. Notes are
// captured into a decisions.jsonl sidecar only when
// EvaluationHarnessConfig.CaptureAgentNotes is true (FR-12, deferred to
// v2 — the field exists today so v2 can fill it in without a protocol
// bump).
public sealed record AgentDecideResult(
    [property: JsonPropertyName("action")] AgentAction Action,
    [property: JsonPropertyName("notes")]  string?     Notes = null);

// Final wire call before the agent subprocess is asked to exit. The
// agent flushes caches and replies; the harness then waits a bounded
// time for clean exit before SIGKILLing. A non-Ok teardown is logged
// but doesn't change the cell's terminus — the run is already over.
public sealed record AgentTeardownResult(
    [property: JsonPropertyName("ok")]     bool    Ok,
    [property: JsonPropertyName("reason")] string? Reason = null);
