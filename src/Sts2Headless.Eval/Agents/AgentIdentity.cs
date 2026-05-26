using System.Text.Json.Serialization;

namespace Sts2Headless.Eval;

// Stable identity tuple recorded in every CellResult and the
// EvaluationSummary ranking. Name + Version are author-declared on the
// manifest; Language is set by the manifest base class ("csharp-bundled"
// for BundledAgent, free-form on AgentManifest); ManifestType is the
// .NET FQN of the manifest class, captured for traceability so a
// `config.json` round-trip can re-instantiate the exact same wrapper.
public sealed record AgentIdentity(
    [property: JsonPropertyName("name")]         string  Name,
    [property: JsonPropertyName("version")]      string  Version,
    [property: JsonPropertyName("language")]     string? Language,
    [property: JsonPropertyName("manifestType")] string  ManifestType);
