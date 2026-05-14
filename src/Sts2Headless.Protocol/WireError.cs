namespace Sts2Headless.Protocol;

// Wire-level error code conventions. JSON-RPC 2.0 reserves -32700..-32600 for
// transport/parsing failures (parse error, invalid request, method not found,
// invalid params, internal error). The -32099..-32000 range is left to the
// server to define — this is where we put our policy errors.
//
// Add new codes here rather than scattering integer literals across the
// dispatch. Clients can rely on the codes being stable; the message text is
// allowed to evolve.
public static class WireErrorCode
{
    // JSON-RPC reserved (mirrored here so handlers can throw with the right
    // code rather than rediscovering them).
    public const int ParseError = -32700;
    public const int InvalidRequest = -32600;
    public const int MethodNotFound = -32601;
    public const int InvalidParams = -32602;
    public const int InternalError = -32603;

    // Server-defined range. Specific codes are documented next to their
    // throw sites; they exist as named constants so generated clients can
    // depend on them.

    // Method exists in the catalogue but is gated off in this host process
    // (currently only debug/* methods, gated by the host's --enable-debug
    // CLI flag). Distinct from MethodNotFound so an accidental debug call
    // surfaces a typed signal rather than a generic 404.
    public const int DebugMethodDisabled = -32001;
}

// Throw from a method handler when the failure shape is a known wire-level
// error (debug gate, validation, etc.) rather than an internal exception.
// StdioHost catches WireException specifically and translates it to an
// Error envelope with the embedded code, sidestepping the generic
// InternalError wrap for unexpected exceptions.
public sealed class WireException(int code, string message) : Exception(message)
{
    public int Code { get; } = code;
}
