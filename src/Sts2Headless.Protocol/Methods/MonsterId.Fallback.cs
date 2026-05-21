using System.Text.Json.Serialization;

namespace Sts2Headless.Protocol.Methods;

[OpaqueWireString]
[JsonConverter(typeof(JsonStringEnumConverter<MonsterId>))]
public enum MonsterId
{
    [JsonStringEnumMemberName("UNKNOWN")] Unknown,
}

public static class MonsterIdNames
{
    public static MonsterId FromWire(string wireName) => MonsterId.Unknown;
    public static System.Collections.Generic.IReadOnlyCollection<string> AllWireNames =>
        System.Array.Empty<string>();
}
