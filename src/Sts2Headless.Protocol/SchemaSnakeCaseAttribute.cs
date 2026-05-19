namespace Sts2Headless.Protocol;

// Marks a record whose wire payload uses snake_case property names instead
// of the default PascalCase that `EnvelopeIo.JsonOptions` produces. The
// schema emitter (`Sts2Headless.SchemaExport`) renders the property names
// in snake_case for these types, and the nullable-property reconciliation
// looks them up under the same casing — without this, nullable fields get
// silently held in the schema's `required` list and the generated Python
// DTOs end up with required fields that the on-wire payload omits.
//
// Today only `RunHistoryDocument` and its nested records carry this marker
// (AD-8: replay artefacts adopt the game's own snake_case shape verbatim).
// The runtime side already serialises through
// `RunHistoryDocument.JsonOptions` (`PropertyNamingPolicy = SnakeCaseLower`)
// at the host's `run/history` wire boundary; this attribute is the
// schema-export-side mirror so the published artefact matches the wire.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class SchemaSnakeCaseAttribute : Attribute { }
