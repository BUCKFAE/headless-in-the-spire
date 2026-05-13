using System.Text.Json.Nodes;
using Sts2Headless.Protocol;
using Sts2Headless.Runtime;

namespace Sts2Headless;

// Stdio NDJSON dispatch loop. One line in, one line out, until stdin closes.
// No shared per-request state yet — the game bootstrap and its live objects
// arrive in the next pass.
//
// Dispatch is a flat dictionary. Anything more elaborate (attributes,
// source generators) is premature with a handful of methods.
public static class StdioHost
{
    public delegate JsonNode? Handler(JsonNode? @params);

    public static int Run(
        TextReader stdin,
        TextWriter stdout,
        IReadOnlyDictionary<string, Handler> methods)
    {
        while (true)
        {
            Request? request;
            try
            {
                request = EnvelopeIo.ReadRequest(stdin);
            }
            catch (Exception ex)
            {
                // We can't recover the request id from a malformed line, so
                // respond with id=-1 and JSON-RPC's parse-error code.
                EnvelopeIo.WriteResponse(stdout, new Response(
                    -1, null, new Error(-32700, "parse error: " + ex.Message, null)));
                continue;
            }
            if (request is null) return 0;

            Response response;
            if (!methods.TryGetValue(request.Method, out var handler))
            {
                response = new Response(request.Id, null,
                    new Error(-32601, $"method not found: {request.Method}", null));
            }
            else
            {
                try
                {
                    var result = handler(request.Params);
                    response = new Response(request.Id, result, null);
                }
                catch (Exception ex)
                {
                    var unwrapped = Diagnostics.Unwrap(ex);
                    response = new Response(request.Id, null,
                        new Error(-32603, "internal error: " + Diagnostics.Describe(unwrapped), null));
                }
            }
            EnvelopeIo.WriteResponse(stdout, response);
        }
    }
}
