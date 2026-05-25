using System.Text.Json.Serialization;

namespace Sts2Headless.Protocol.Methods;

// Compile-only stub. The real enum is emitted by `just build::generate-content-ids`
// into the gitignored AfflictionId.g.cs. See CardId.Fallback.cs for the
// full rationale — same pattern, one stub per kind.
[OpaqueWireString]
[JsonConverter(typeof(JsonStringEnumConverter<AfflictionId>))]
public enum AfflictionId
{
    [JsonStringEnumMemberName("UNKNOWN")] Unknown,
}

public static class AfflictionIdNames
{
    public static AfflictionId FromWire(string wireName) => AfflictionId.Unknown;
    public static System.Collections.Generic.IReadOnlyCollection<string> AllWireNames =>
        System.Array.Empty<string>();
}
