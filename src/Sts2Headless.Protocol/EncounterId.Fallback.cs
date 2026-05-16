using System.Text.Json.Serialization;

namespace Sts2Headless.Protocol.Methods;

[OpaqueWireString]
[JsonConverter(typeof(JsonStringEnumConverter<EncounterId>))]
public enum EncounterId
{
    [JsonStringEnumMemberName("UNKNOWN")] Unknown,
}

public static class EncounterIdNames
{
    public static EncounterId FromWire(string wireName) => EncounterId.Unknown;
    public static System.Collections.Generic.IReadOnlyCollection<string> AllWireNames =>
        System.Array.Empty<string>();
}
