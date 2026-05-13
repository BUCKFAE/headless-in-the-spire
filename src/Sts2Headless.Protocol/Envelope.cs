using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Sts2Headless.Protocol;

// AD-2: NDJSON over stdio, JSON-RPC-style envelope. Three shapes:
//   request:      { "id": N, "method": "...", "params": { ... } }
//   response:     { "id": N, "result": { ... } }  | { "id": N, "error": { ... } }
//   notification: { "method": "...", "params": { ... } }
//
// We keep `params` / `result` as JsonNode at this layer so the envelope is
// agnostic to specific method payloads — those live alongside the methods
// they belong to. Method dispatch upstack deserialises into concrete records.

public sealed record Request(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("method")] string Method,
    [property: JsonPropertyName("params")] JsonNode? Params
);

public sealed record Response(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("result")] JsonNode? Result,
    [property: JsonPropertyName("error")] Error? Error
);

public sealed record Notification(
    [property: JsonPropertyName("method")] string Method,
    [property: JsonPropertyName("params")] JsonNode? Params
);

public sealed record Error(
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("data")] JsonNode? Data
);
