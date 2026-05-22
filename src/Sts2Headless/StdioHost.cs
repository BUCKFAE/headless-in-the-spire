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
                    -1, null, new Error(WireErrorCode.ParseError, "parse error: " + ex.Message, null)));
                continue;
            }
            if (request is null) return 0;

            Response response;
            if (!methods.TryGetValue(request.Method, out var handler))
            {
                response = new Response(request.Id, null,
                    new Error(WireErrorCode.MethodNotFound, $"method not found: {request.Method}", null));
            }
            else
            {
                try
                {
                    var result = handler(request.Params);
                    response = new Response(request.Id, result, null);
                }
                catch (WireException wex)
                {
                    // Typed wire-level errors (debug gate, future validation
                    // codes) carry their own code; surface it verbatim rather
                    // than wrapping in the generic InternalError.
                    response = new Response(request.Id, null,
                        new Error(wex.Code, wex.Message, null));
                }
                catch (Exception ex)
                {
                    var unwrapped = Diagnostics.Unwrap(ex);
                    // DescribeWithStack appends pipe-separated game-side
                    // (MegaCrit.*) frames so the throw site is visible in
                    // the wire error. Was Describe — name-only — until
                    // the MechanicSweep crash reports had no actionable
                    // info for NRE / OOR / ArgNull crashes. The extra
                    // frames are filtered to game-side only, so the
                    // payload stays bounded (typically 100–400 bytes).
                    response = new Response(request.Id, null,
                        new Error(WireErrorCode.InternalError, "internal error: " + Diagnostics.DescribeWithStack(unwrapped), null));
                }
            }
            EnvelopeIo.WriteResponse(stdout, response);
        }
    }
}
