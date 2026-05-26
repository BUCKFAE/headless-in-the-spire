using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Eval;

// Universal contract for "anything the harness can spawn as an agent".
//
// Authoring an agent for the eval harness comes down to subclassing
// AgentManifest (for external agents — Python, sibling C# repos, Rust)
// or BundledAgent (for in-repo C#). The class itself is the
// registration: no JSON manifest files, no string-typed factory, no
// reflection on agent constructors. The harness only sees an
// AgentManifest instance; it doesn't care where it came from.
//
// Defaults are tuned so an author who's happy with them writes three
// properties (Name, Version, Command) and stops. The defaults match
// the spec's "Ironclad A0, no modifiers" baseline so a fresh-from-clone
// manifest plugs straight into a smoke eval without configuration.
public abstract class AgentManifest
{
    // ── Required ──────────────────────────────────────────────────────────
    public abstract string Name    { get; }
    public abstract string Version { get; }

    // The argv the harness invokes to spawn the agent subprocess. Index 0
    // is the program; the rest are arguments. The harness sets up the
    // process's stdio for NDJSON exchange and forwards stderr to the eval
    // log. Cwd defaults to the repo root when null.
    public abstract IReadOnlyList<string> Command { get; }

    // ── Optional with defaults ───────────────────────────────────────────
    // Language label — recorded in CellResult.Agent.Language and in
    // config.json so a leaderboard can split csharp vs python vs rust.
    // BundledAgent overrides with "csharp-bundled" (sealed); external
    // manifests pick their own.
    public virtual string?  Language    => null;

    // Process working directory. null ⇒ inherit repo root from the
    // harness. Relative paths are resolved against the repo root.
    public virtual string?  Cwd         => null;

    // Free-text description surfaced in summary.md when present.
    public virtual string?  Description => null;

    // Extra environment variables for the agent subprocess. The harness
    // also sets a small set of intrinsic vars (eval-id, replay path) that
    // override anything declared here.
    public virtual IReadOnlyDictionary<string, string>? Env => null;

    // ── Capabilities ─────────────────────────────────────────────────────
    // Hard-edged: if a manifest's SupportedCharacters omits Silent, the
    // harness skips Silent cells for that agent and logs the skip in
    // summary.md. The agent never sees an unsupported character on the
    // wire — the per-cell pairing is filtered before agent/init.
    public virtual IReadOnlyList<Character>   SupportedCharacters => [Character.Ironclad];
    public virtual IReadOnlyList<int>         SupportedAscensions => [0];
    public virtual IReadOnlyList<ModifierId>? SupportedModifiers  => null;

    // ── Per-agent budget overrides ───────────────────────────────────────
    // Non-null wins over EvaluationHarnessConfig.Budgets (FR-9). Set this
    // on agents whose per-decision budget genuinely differs from the
    // matrix default (e.g. an MCTS planner that wants a longer per-move
    // window without lifting the budget for everyone else).
    public virtual HarnessBudgets? Budgets => null;
}
