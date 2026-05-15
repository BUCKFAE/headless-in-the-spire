namespace Sts2Headless.Protocol;

// Marks an enum whose values are proprietary content that must not appear
// in committed wire artefacts (openrpc.json, generated client DTOs). The
// schema emitter (Sts2Headless.SchemaExport) renders these as opaque
// `{"type": "string"}` instead of expanding the enum membership list,
// and CollectHoistSet skips them entirely.
//
// In-process C# code keeps the strong typing: deserialisation reads the
// wire string and maps it to the enum value via JsonStringEnumConverter;
// agent code compares against `CardId.X` directly. The wire round-trips
// strings; only the in-memory representation is the enum.
//
// Today only CardId carries this attribute (the 577-name card list is
// derived from vendor/sts2.dll and treated as proprietary). Future enums
// in the same shape — Relic IDs, Potion IDs, Monster IDs — would get the
// same marker when added.
[AttributeUsage(AttributeTargets.Enum)]
public sealed class OpaqueWireStringAttribute : Attribute { }
