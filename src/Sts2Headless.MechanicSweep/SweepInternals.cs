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
        // it for clean game-rule refusals (curses/statuses returning
        // false from CanPlay, enchantments / afflictions that the
        // engine rejects on a given card target). Carve out these
        // known clean-refusal shapes so the sweep classifies them as
        // Unplayable, not Crashed.
        if (msg.Contains("CanPlay returned false", System.StringComparison.Ordinal))
            return false;
        // CardCmd.Enchant / CardCmd.Afflict surface a clean "Cannot
        // enchant CARD.X with ENCHANTMENT.Y." / "Cannot afflict
        // CARD.X with AFFLICTION.Y." InvalidOperationException from the
        // engine when the card type isn't a valid target for the
        // modifier. Same shape as CanPlay-false: the engine deliberately
        // said no.
        if (msg.Contains("Cannot enchant ", System.StringComparison.Ordinal)) return false;
        if (msg.Contains("Cannot afflict ", System.StringComparison.Ordinal)) return false;

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

    // 480 chars is comfortably enough for the JSON-RPC error prefix +
    // unwrapped exception name + message + 2-4 game-side stack frames
    // surfaced by Diagnostics.DescribeWithStack. The host's internal-
    // error format pipes frames after the exception header, so the
    // deepest (closest-to-throw) frames land in the first ~200 chars
    // and survive even tight truncation; this gives us room for a
    // second-frame call site too.
    // Test-fixture and scripted-kill ids that exist in the generated
    // manifests but aren't reachable through normal play — same filter
    // posture as the old CoverageAggregator.IsEngineExcluded. Carried
    // forward into the new sweep matrix so per-id sweeps don't surface
    // these as crashes (they're engine-internal probes, not playable
    // content, so any failure on them is a different kind of signal).
    // Widen as more unreachable-by-design ids surface.
    public static bool IsEngineExcluded(string id)
    {
        if (id.StartsWith("DEPRECATED_", System.StringComparison.Ordinal)) return true;
        if (id.StartsWith("FAKE_", System.StringComparison.Ordinal)) return true;
        if (id.StartsWith("MOCK_", System.StringComparison.Ordinal)) return true;
        if (id.EndsWith("_DUMMY", System.StringComparison.Ordinal)) return true;
        if (id.EndsWith("_ATTACK_MOVE_MONSTER", System.StringComparison.Ordinal)) return true;
        return id is "ONE_HP_MONSTER" or "TEN_HP_MONSTER" or "TEST_SUBJECT" or "ARCHITECT";
    }

    // Standard universe → reachable-set transformation used by every
    // per-id sweep. Sorted by id, engine-excluded ids dropped.
    public static System.Collections.Generic.List<string> FilterReachable(
        System.Collections.Generic.IReadOnlyCollection<string> universe) =>
        universe
            .Where(id => !IsEngineExcluded(id))
            .OrderBy(s => s, System.StringComparer.Ordinal)
            .ToList();

    public static string Truncate(string s) =>
        s.Length > 480 ? string.Concat(s.AsSpan(0, 480), "...") : s;

    // Single classification gate for "what outcome should this wire
    // exception become" — used by every sweep so the
    // Crashed/Unplayable/KnownUnsafe split stays consistent. The kind
    // string ("card", "relic", …) routes the known-unsafe lookup; pass
    // the sweep's own kind even if its known-issues list is empty (the
    // dictionary returns false uniformly).
    //
    // Returned detail folds in the reason when KnownUnsafe so the row
    // is self-explanatory ("known-unsafe: <reason> | <wire-msg>") and
    // a reader doesn't need to cross-reference SweepKnownIssues.
    public static (SweepOutcome Outcome, string Detail) ClassifyWireError(
        string kind, string id, System.Exception wx)
    {
        var wireMsg = $"{wx.GetType().Name}: {Truncate(wx.Message)}";
        if (!IsInternalError(wx))
        {
            // Clean refusal — annotate with the per-id expected-refusal
            // reason when we have one. The Unplayable outcome stays
            // (non-failure), the Detail just gains a one-line
            // breadcrumb so a reader can tell this is a known
            // fixture-staging gap rather than an un-investigated row.
            if (SweepKnownIssues.TryGetExpectedRefusal(kind, id, out var refReason))
                return (SweepOutcome.Unplayable, $"expected-refusal: {refReason} | {wireMsg}");
            return (SweepOutcome.Unplayable, wireMsg);
        }
        if (SweepKnownIssues.TryGetReason(kind, id, out var reason))
            return (SweepOutcome.KnownUnsafe, $"known-unsafe: {reason} | {wireMsg}");
        return (SweepOutcome.Crashed, wireMsg);
    }

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
