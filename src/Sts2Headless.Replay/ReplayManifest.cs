using System.Text.Json;
using System.Text.Json.Serialization;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Replay;

// The one file in a replay directory we author. Everything else
// (combats/*.mcr, run.json) is bytes the game writes — we adopt those
// verbatim per AD-8. The manifest's job is to pin the run to a game
// version, record the engine identity (sts2.dll SHA-256, modelIdHash,
// gitCommit, schema_version) so a consumer can decide whether re-execution
// is safe, and index the per-combat .mcr files so a tool doesn't have to
// scan the directory.
//
// Serialised as JSON with PropertyNamingPolicy.SnakeCaseLower to match the
// snake_case shape the game itself uses in run.json. This is purely
// cosmetic — the manifest is ours, not the game's — but consistency makes
// the directory eyeball-scannable.

// Top-level manifest document. version is the manifest's own schema (not
// the game's); bump on shape changes so old recordings stay readable.
//
// DisplayName / Outcome / EndedAtUnix are populated at finalize time and
// exist so a UI (or the runs-index aggregator) can show a human-readable
// label without re-parsing each combat. They derive from Header + Combats
// + (when present) run.json, so they are redundant — but recomputing them
// on every viewer load would force the viewer to know our derivation
// rules, which we don't want.
public sealed record ReplayManifest(
    int Version,
    ReplayHeader Header,
    IReadOnlyList<ReplayCombatEntry> Combats,
    string? DisplayName = null,
    ReplayCombatOutcome Outcome = ReplayCombatOutcome.Unknown,
    long? EndedAtUnix = null)
{
    public const int CurrentVersion = 2;

    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    public string Serialize() => JsonSerializer.Serialize(this, JsonOptions);

    public static ReplayManifest Deserialize(string json)
        => JsonSerializer.Deserialize<ReplayManifest>(json, JsonOptions)
           ?? throw new InvalidDataException("manifest deserialised to null");
}

// Header fields are the version-pin equivalents from AD-3 (game_version /
// sts2_dll_sha256) plus the engine-identity values the game itself stamps
// into a .mcr (model_id_hash / git_commit / runhistory_schema_version) plus
// the run-identity (seed / character / ascension / modifiers / start_time)
// the player chose. protocol_version pins our wire protocol so a
// run-recorded-by-an-older-host stays interpretable when openrpc.json
// evolves.
public sealed record ReplayHeader(
    string GameVersion,
    string Sts2DllSha256,
    uint ModelIdHash,
    string GitCommit,
    int RunHistorySchemaVersion,
    int ProtocolVersion,
    string Seed,
    Character Character,
    int Ascension,
    IReadOnlyList<string> Modifiers,
    long StartTimeUnix,
    string Agent = ReplayHeader.UnknownAgent)
{
    public const int CurrentProtocolVersion = 1;

    // Sentinel used when no STS2_REPLAY_AGENT was supplied. Distinct
    // from an empty string so the viewer can show "unknown" rather than
    // collapsing the field altogether.
    public const string UnknownAgent = "unknown";

    // Schema version of `RunHistory` JSON we mirror. The pinned game
    // (v0.103.2) writes schema_version=9; bump alongside any AD-3
    // game-version bump that changes RunHistory shape.
    public const int CurrentRunHistorySchemaVersion = 9;
}

// One entry per recorded combat, in chronological order. McrFile is
// path-relative to the run directory (always under combats/). The
// coordinate fields identify where in the run the combat happened so a
// tool can splice .mcr playback into a RunHistory walk (the stretch-goal
// full-run replayer described in AD-8).
public sealed record ReplayCombatEntry(
    string McrFile,
    int ActIndex,
    int Floor,
    RoomType RoomType,
    EncounterId? Encounter,
    ReplayCombatOutcome Outcome,
    int ActionCount,
    int ChecksumCount);

public enum ReplayCombatOutcome
{
    Unknown,
    Victory,
    Defeat,
    Abandoned,
}
