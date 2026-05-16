using System.Text.Json.Serialization;

namespace Sts2Headless.Protocol.Methods;

[OpaqueWireString]
[JsonConverter(typeof(JsonStringEnumConverter<EventId>))]
public enum EventId
{
    [JsonStringEnumMemberName("UNKNOWN")] Unknown,
}

public static class EventIdNames
{
    public static EventId FromWire(string wireName) => EventId.Unknown;
    public static System.Collections.Generic.IReadOnlyCollection<string> AllWireNames =>
        System.Array.Empty<string>();
}
