using System.Text.Json;
using System.Text.Json.Nodes;

namespace Sts2Headless.Protocol;

// Wire-handler adapters shared by the core host (HostMethods) and the
// cheat surface (CheatHostMethods). Sharing this layer is what keeps
// the cheat wire shape from drifting from the core wire shape — both
// routes deserialise typed params and re-serialise typed results
// through EnvelopeIo.JsonOptions.
public static class WireHandlers
{
    // Turns a typed Func<TParams?, TResult> into the JsonNode-shaped
    // delegate the wire dispatch loop expects. Deserialisation tolerates
    // a missing or null params object (TParams? default); the caller
    // decides whether to throw for required fields.
    public static Func<JsonNode?, JsonNode?> Typed<TParams, TResult>(Func<TParams?, TResult> handler)
        => raw =>
        {
            var p = raw is null ? default : raw.Deserialize<TParams>(EnvelopeIo.JsonOptions);
            var r = handler(p);
            return JsonSerializer.SerializeToNode(r, EnvelopeIo.JsonOptions);
        };
}
