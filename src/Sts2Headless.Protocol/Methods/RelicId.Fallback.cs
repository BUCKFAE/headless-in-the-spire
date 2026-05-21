using System.Text.Json.Serialization;

namespace Sts2Headless.Protocol.Methods;

[OpaqueWireString]
[JsonConverter(typeof(JsonStringEnumConverter<RelicId>))]
public enum RelicId
{
    [JsonStringEnumMemberName("UNKNOWN")] Unknown,
}

public static class RelicIdNames
{
    public static RelicId FromWire(string wireName) => RelicId.Unknown;
    public static System.Collections.Generic.IReadOnlyCollection<string> AllWireNames =>
        System.Array.Empty<string>();
}
