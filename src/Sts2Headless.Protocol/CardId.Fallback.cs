using System.Text.Json.Serialization;
using Sts2Headless.Protocol;

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
//
// MSBuild excludes this file from compilation when CardId.g.cs exists
// (see Sts2Headless.Protocol.csproj's conditional <Compile Remove>),
// so the stub is only ever active in the bootstrap window before the
// generator has produced the real enum.
//
// IMPORTANT: do not add wire-facing CardId values here. The real enum
// is generated; values you'd add by hand would only be visible during
// the bootstrap window and would diverge from the post-generator
// reality.
[OpaqueWireString]
[JsonConverter(typeof(JsonStringEnumConverter<CardId>))]
public enum CardId
{
    [JsonStringEnumMemberName("UNKNOWN")] Unknown,
}

public static class CardIdNames
{
    // Pre-generator stub: every wire id collapses to Unknown until
    // CardId.g.cs replaces this file.
    public static CardId FromWire(string wireName) => CardId.Unknown;

    public static System.Collections.Generic.IReadOnlyCollection<string> AllWireNames =>
        System.Array.Empty<string>();
}
