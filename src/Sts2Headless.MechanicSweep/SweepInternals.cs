namespace Sts2Headless.MechanicSweep;

// Shared classification + formatting helpers used by every per-kind
// sweep (CardSweep, RelicSweep, …). Lives here instead of inside each
// sweep class so the "what counts as Crashed vs Unplayable" rule has
// a single source of truth — drift across sweeps would make their
// outcomes incomparable.
internal static class SweepInternals
{
    // HostSubprocess.SendAsync throws XunitException on wire-error
    // envelopes (the host returned a structured error, e.g.
    // "code=-32602 message=..."). We classify those as wire-level
    // outcomes (Unplayable / Crashed disambiguated by IsInternalError).
    // Other exception types — TaskCanceledException, IOException, raw
    // engine NREs that escape the host wrapper — surface as Crashed
    // through the outer try/catch.
    public static bool IsWireError(System.Exception ex) =>
        ex.GetType().Name.Equals("XunitException", System.StringComparison.Ordinal)
        || ex.Message.Contains("code=", System.StringComparison.Ordinal);

    // Within wire errors, distinguish "the engine deliberately refused
    // this action" (insufficient energy, wrong target type, X-cost with
    // no resource, the card's own CanPlay validator returning false —
    // all clean refusals) from "the engine wrapped an internal
    // exception in an error envelope" (the host's catch-all for
    // unhandled engine exceptions, surfaced via JSON-RPC -32603 with a
    // Missing*Exception / NullReferenceException / etc. in the
    // message). The first is honest "this mechanic isn't reachable in
    // this fixture"; the second is the mechanic itself being broken —
    // which is what the sweep exists to surface as Crashed.
    public static bool IsInternalError(System.Exception ex)
    {
        var msg = ex.Message;
        // -32603 is JSON-RPC's "internal error" generic bucket. The
        // host wraps engine exceptions into this code, but ALSO emits
        // it for some clean refusals (notably curses/statuses
        // returning false from CanPlay). Carve out the known
        // clean-refusal sub-cases so they're not flagged as crashes.
        if (msg.Contains("CanPlay returned false", System.StringComparison.Ordinal))
            return false;

        return msg.Contains("MissingMethodException", System.StringComparison.Ordinal)
            || msg.Contains("MissingFieldException", System.StringComparison.Ordinal)
            || msg.Contains("NullReferenceException", System.StringComparison.Ordinal)
            || msg.Contains("ArgumentOutOfRangeException", System.StringComparison.Ordinal)
            || msg.Contains("ArgumentNullException", System.StringComparison.Ordinal)
            || msg.Contains("TargetInvocationException", System.StringComparison.Ordinal)
            || msg.Contains("IndexOutOfRangeException", System.StringComparison.Ordinal)
            || msg.Contains("StackOverflowException", System.StringComparison.Ordinal)
            // Generic "internal error:" prefix from any other engine
            // throw the host doesn't specifically recognize — covers
            // future exception types we haven't listed by name.
            || (msg.Contains("internal error:", System.StringComparison.Ordinal)
                && !msg.Contains("CanPlay returned false", System.StringComparison.Ordinal));
    }

    public static string Truncate(string s) =>
        s.Length > 240 ? string.Concat(s.AsSpan(0, 240), "...") : s;

    // SCREAMING_SNAKE_CASE → PascalCase. The wire form is
    // SCREAMING_SNAKE; the C# enum form is PascalCase via
    // ToPascalCase in GenerateContentIdsCommand. Reimplemented here
    // (the generator's copy is private) so the sweeps stay
    // dependency-free.
    public static string ToPascalCase(string snake)
    {
        var sb = new System.Text.StringBuilder(snake.Length);
        var atWordStart = true;
        foreach (var ch in snake)
        {
            if (ch == '_') { atWordStart = true; continue; }
            sb.Append(atWordStart ? char.ToUpperInvariant(ch) : char.ToLowerInvariant(ch));
            atWordStart = false;
        }
        return sb.ToString();
    }
}
