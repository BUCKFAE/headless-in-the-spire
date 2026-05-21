using System.Text.Json.Serialization;

namespace Sts2Headless.Protocol.Methods;

[OpaqueWireString]
[JsonConverter(typeof(JsonStringEnumConverter<OrbId>))]
public enum OrbId
{
    [JsonStringEnumMemberName("UNKNOWN")] Unknown,
}

public static class OrbIdNames
{
    public static OrbId FromWire(string wireName) => OrbId.Unknown;
    public static System.Collections.Generic.IReadOnlyCollection<string> AllWireNames =>
        System.Array.Empty<string>();
}
