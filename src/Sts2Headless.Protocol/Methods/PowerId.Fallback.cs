using System.Text.Json.Serialization;

namespace Sts2Headless.Protocol.Methods;

[OpaqueWireString]
[JsonConverter(typeof(JsonStringEnumConverter<PowerId>))]
public enum PowerId
{
    [JsonStringEnumMemberName("UNKNOWN")] Unknown,
}

public static class PowerIdNames
{
    public static PowerId FromWire(string wireName) => PowerId.Unknown;
    public static System.Collections.Generic.IReadOnlyCollection<string> AllWireNames =>
        System.Array.Empty<string>();
}
