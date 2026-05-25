using System.Text.Json.Serialization;

namespace Sts2Headless.Protocol.Methods;

[OpaqueWireString]
[JsonConverter(typeof(JsonStringEnumConverter<PowerId>))]
public enum PowerId
{
    [JsonStringEnumMemberName("UNKNOWN")] Unknown,

    // Powers referenced by Sts2Headless.Agents (CardMechanics +
    // CheatingHellRaisingSeed42Agent). Listed here so the bootstrap build
    // — `dotnet build src/Sts2Headless/Sts2Headless.csproj` before the
    // generator has produced PowerId.g.cs — resolves these symbols. The
    // generated PowerId.g.cs supersedes the entire file on dev machines.
    StrengthPower,
    WeakPower,
    VulnerablePower,
    SlipperyPower,
}

public static class PowerIdNames
{
    public static PowerId FromWire(string wireName) => PowerId.Unknown;
    public static System.Collections.Generic.IReadOnlyCollection<string> AllWireNames =>
        System.Array.Empty<string>();
}
