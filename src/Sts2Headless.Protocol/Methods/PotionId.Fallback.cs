using System.Text.Json.Serialization;

namespace Sts2Headless.Protocol.Methods;

[OpaqueWireString]
[JsonConverter(typeof(JsonStringEnumConverter<PotionId>))]
public enum PotionId
{
    [JsonStringEnumMemberName("UNKNOWN")] Unknown,
}

public static class PotionIdNames
{
    public static PotionId FromWire(string wireName) => PotionId.Unknown;
    public static System.Collections.Generic.IReadOnlyCollection<string> AllWireNames =>
        System.Array.Empty<string>();
}
