using System.Text.Json.Serialization;

namespace Sts2Headless.Eval;

// A named, versioned, gameVersion-pinned list of seeds. Authored on disk
// as JSON under `documentation/eval/seeds/<name>.json`; loaded into
// memory via `SeedBanks.Smoke` / `Reference` / `Deep` (committed banks)
// or `SeedBanks.FromFile` (ad-hoc).
//
// Semantics (FR-4):
//   * Seeds may be APPENDED but never removed or reordered. A result
//     from yesterday remains comparable to a result from today on the
//     same bank. Material content change ⇒ new bank, new name.
//   * Bank Version is a string so semver-style "1.1" works on the
//     append path.
//   * GameVersion ties the bank to the pin it was generated against;
//     running against a different pin is allowed but `runs.jsonl`
//     records the *eval's* gameVersion. Cross-version aggregation
//     refuses to mix.
public sealed record SeedBank(
    [property: JsonPropertyName("name")]             string  Name,
    [property: JsonPropertyName("version")]          string  Version,
    [property: JsonPropertyName("createdAt")]        string? CreatedAt,
    [property: JsonPropertyName("gameVersion")]      string? GameVersion,
    [property: JsonPropertyName("generationMethod")] string? GenerationMethod,
    [property: JsonPropertyName("seeds")]            IReadOnlyList<ulong> Seeds);
