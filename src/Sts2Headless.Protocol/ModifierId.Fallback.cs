using System.Text.Json.Serialization;

namespace Sts2Headless.Protocol.Methods;

[OpaqueWireString]
[JsonConverter(typeof(JsonStringEnumConverter<ModifierId>))]
public enum ModifierId
{
    [JsonStringEnumMemberName("UNKNOWN")] Unknown,
}

public static class ModifierIdNames
{
    public static ModifierId FromWire(string wireName) => ModifierId.Unknown;
    public static System.Collections.Generic.IReadOnlyCollection<string> AllWireNames =>
        System.Array.Empty<string>();
}
