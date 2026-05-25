using System.Text.Json.Serialization;

namespace Sts2Headless.Protocol.Methods;

[OpaqueWireString]
[JsonConverter(typeof(JsonStringEnumConverter<PotionId>))]
public enum PotionId
{
    [JsonStringEnumMemberName("UNKNOWN")] Unknown,

    // Potions referenced by Sts2Headless.Agents
    // (CheatingHellRaisingSeed42Agent.Examples). Listed here so the
    // bootstrap build resolves these symbols before PotionId.g.cs
    // exists; superseded by the generated file on dev machines.
    AttackPotion,
    BlockPotion,
    BloodPotion,
    DexterityPotion,
    EnergyPotion,
    FirePotion,
    FlexPotion,
    FocusPotion,
    PoisonPotion,
    RegenPotion,
    StrengthPotion,
    VulnerablePotion,
    WeakPotion,
}

public static class PotionIdNames
{
    public static PotionId FromWire(string wireName) => PotionId.Unknown;
    public static System.Collections.Generic.IReadOnlyCollection<string> AllWireNames =>
        System.Array.Empty<string>();
}
