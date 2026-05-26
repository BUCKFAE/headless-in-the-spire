using System.Text.Json;
using System.Text.Json.Serialization;
using Sts2Headless.Protocol;

namespace Sts2Headless.Eval.Json;

// Single source of truth for JSON serialisation across the eval harness.
//
// We share `EnvelopeIo.JsonOptions`'s null-handling so a CellResult / DTO
// serialises with the same null-omitted shape every wire surface in the
// repo uses, then add:
//
//   * PropertyNamingPolicy = CamelCase — the eval's own DTOs (CellResult,
//     EvaluationSummary, AgentAggregates) live in plain C# records with
//     PascalCase property names. We want camelCase on the wire and in
//     summary.json / runs.jsonl, matching the existing Methods.cs shape.
//     Methods.cs DTOs that travel through these options (e.g.
//     RunStateResult inside an agent/decide snapshot) carry explicit
//     [JsonPropertyName] attributes so the policy is overridden where it
//     matters and irrelevant where it doesn't.
//
//   * TimeSpan converter — System.Text.Json's default TimeSpan format is
//     awkward to read. We round-trip with the standard "c" format
//     ("00:00:30", "00:10:00") because it's both human-legible and
//     ISO-8601-adjacent.
//
//   * WriteIndented — applied to disk-bound payloads (config.json,
//     summary.json, cell.json) via a separate `Pretty` instance. The
//     wire-bound stream stays compact.
public static class EvalJson
{
    public static JsonSerializerOptions Wire { get; } = Build(indented: false);
    public static JsonSerializerOptions Pretty { get; } = Build(indented: true);

    private static JsonSerializerOptions Build(bool indented)
    {
        var opts = new JsonSerializerOptions(EnvelopeIo.JsonOptions)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = indented,
        };
        opts.Converters.Add(new HumanReadableTimeSpanConverter());
        return opts;
    }
}

internal sealed class HumanReadableTimeSpanConverter : JsonConverter<TimeSpan>
{
    public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        TimeSpan.Parse(reader.GetString() ?? throw new JsonException("TimeSpan: expected string"));

    public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString("c"));
}
