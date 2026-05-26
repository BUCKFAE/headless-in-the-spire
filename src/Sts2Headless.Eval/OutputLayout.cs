namespace Sts2Headless.Eval;

// Where eval output lands and how the per-eval directory is named.
//
// EvalRoot defaults to `replays/eval-harness/` at the repo root. The
// top-level `replays/` directory is gitignored, and we keep this
// bucket one level down so it never shares a directory with AD-8's
// default `replays/manual/` (the ad-hoc / `record-all` bucket). That
// means an eval-id can't collide with a manual recording and the eval
// tree is safe to `rm -rf` without touching anything else.
//
// EvalIdGenerator defaults to a UTC timestamp ("2026-05-26T19-32-04Z"):
// sortable, collision-free across humans on different machines, no
// dependency on a build system. CI overrides via a delegate that
// stamps in a build number or git SHA.
public sealed record OutputLayout
{
    public string EvalRoot { get; init; } = "replays/eval-harness";

    public Func<DateTimeOffset, string> EvalIdGenerator { get; init; } =
        static now => now.UtcDateTime.ToString("yyyy-MM-ddTHH-mm-ss") + "Z";

    public static OutputLayout Default { get; } = new();
}
