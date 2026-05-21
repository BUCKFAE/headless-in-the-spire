using System.Text.Json.Serialization;

namespace Sts2Headless.Protocol.Methods;

[OpaqueWireString]
[JsonConverter(typeof(JsonStringEnumConverter<EnchantmentId>))]
public enum EnchantmentId
{
    [JsonStringEnumMemberName("UNKNOWN")] Unknown,
}

public static class EnchantmentIdNames
{
    public static EnchantmentId FromWire(string wireName) => EnchantmentId.Unknown;
    public static System.Collections.Generic.IReadOnlyCollection<string> AllWireNames =>
        System.Array.Empty<string>();
}
