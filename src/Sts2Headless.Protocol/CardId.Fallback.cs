using System.Text.Json.Serialization;

namespace Sts2Headless.Protocol.Methods;

// Compile-only stub for the CardId enum + CardIdNames lookup. The real
// 577-value enum is emitted by `just generate-card-ids` into the
// gitignored CardId.g.cs (sourced from the proprietary vendor/sts2.dll
// — never committed to the repo). This file exists so:
//
//   1. The Protocol project compiles on a fresh clone *before* the
//      generator has run (e.g. so the generator itself, which lives
//      under Sts2Headless and depends on this project, can be built).
//   2. `just setup` can chain: validate sts2 install → pull DLLs →
//      build the solution (compiling with this stub) → run the
//      generator → final build picks up CardId.g.cs.
//   3. CI (which never has vendor/sts2.dll) can compile downstream
//      projects — notably Sts2Headless.Agents — that statically
//      reference specific named card members.
//
// MSBuild excludes this file from compilation when CardId.g.cs exists
// (see Sts2Headless.Protocol.csproj's conditional <Compile Remove>),
// so the stub is only ever active when no generated enum is present.
//
// IMPORTANT: names-only here. Do NOT attach explicit numeric backings
// or [JsonStringEnumMemberName(...)] attributes to entries below — the
// stub never round-trips wire payloads (nothing executes the host on
// the no-vendor build path), and any hand-authored value/attribute
// would risk diverging from the generated enum. Card names themselves
// are public game knowledge, not proprietary bytes from sts2.dll, so
// listing the names downstream code references is safe.
[OpaqueWireString]
[JsonConverter(typeof(JsonStringEnumConverter<CardId>))]
public enum CardId
{
    [JsonStringEnumMemberName("UNKNOWN")] Unknown,

    // Ironclad starter.
    StrikeIronclad,
    DefendIronclad,
    Bash,

    // Cards referenced by Sts2Headless.Agents (CardMechanics / Seed42Agent).
    // Listed here so the no-vendor (CI) build path resolves these symbols;
    // the generated CardId.g.cs supersedes the entire file on dev machines.
    BodySlam,
    Tremble,
    SwordBoomerang,
    Headbutt,
    ExpectAFight,
    BurningPact,
    Bully,
    Thunderclap,
    Bludgeon,
    Dismantle,
    Cascade,
    Uppercut,
    Armaments,
    StoneArmor,
    TrueGrit,
    SecondWind,
    Taunt,
    BloodWall,
    Infection,
}

public static class CardIdNames
{
    // Pre-generator stub: every wire id collapses to Unknown until
    // CardId.g.cs replaces this file.
    public static CardId FromWire(string wireName) => CardId.Unknown;

    public static System.Collections.Generic.IReadOnlyCollection<string> AllWireNames =>
        System.Array.Empty<string>();
}
