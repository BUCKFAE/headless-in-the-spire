using Sts2Headless.Eval.Scoring;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Eval;

// Single source of truth for what an eval *is*. The harness has no
// implicit globals and no hard-coded knobs — everything tunable goes
// through this record. A canonical run that publishes its config
// alongside the results is bit-identical (modulo wall-clock noise) to
// anyone else's run of the same config against the same `GAME_VERSION`
// pin. That is the definition of a reproducible result.
//
// Matrix axes (`Agents`, `Seeds`, `Characters`, `Ascensions`,
// `Modifiers`) form the cartesian product the harness expands into
// per-cell work. Defaults collapse the matrix to the minimum a caller
// wants to think about — `Agents = [Greedy], Seeds = SeedBanks.Smoke`
// is a 5-cell Ironclad-A0-no-modifiers matrix.
public sealed record EvaluationHarnessConfig
{
    // ── Required matrix axes ─────────────────────────────────────────────
    public required IReadOnlyList<AgentManifest> Agents { get; init; }
    public required SeedBank                     Seeds  { get; init; }

    // ── Defaulted matrix axes ────────────────────────────────────────────
    public IReadOnlyList<Character>  Characters { get; init; } = [Character.Ironclad];
    public IReadOnlyList<int>        Ascensions { get; init; } = [0];
    public IReadOnlyList<ModifierId> Modifiers  { get; init; } = [];

    // ── Per-cell budgets ─────────────────────────────────────────────────
    // Per-manifest Budgets win when non-null (FR-9).
    public HarnessBudgets Budgets { get; init; } = HarnessBudgets.Default;

    // ── Parallelism ──────────────────────────────────────────────────────
    // null ⇒ auto: min(matrixSize, max(1, ⌊Environment.ProcessorCount / 2⌋)).
    // Each cell holds two processes (host + agent) and the host alone is
    // hundreds of MB resident, so the default cap is conservative.
    public int? Workers { get; init; } = null;

    // ── Pluggable scoring ────────────────────────────────────────────────
    public IScoringFunction Scoring { get; init; } = ScoringFunctions.Default;

    // ── Output ───────────────────────────────────────────────────────────
    public OutputLayout Output { get; init; } = OutputLayout.Default;

    // ── Toggles (FR-11, FR-12 — deferred to v2) ──────────────────────────
    // Field exists today so v2 can fill it in without a protocol bump.
    public bool EnableDeterminismCanary { get; init; } = false;
    public bool CaptureAgentNotes       { get; init; } = false;

    // ── Diagnostic affordances ──────────────────────────────────────────
    // When true, the harness forwards host stderr + agent stderr to the
    // eval-log under <eval-id>/logs/. Off by default to keep eval roots
    // tidy; turn it on while debugging a regression.
    public bool TeeProcessStderr { get; init; } = false;

    // Override the host-DLL path; useful in tests that run before
    // `dotnet build` has produced Sts2Headless.dll alongside the eval
    // binary. null ⇒ resolve from AppContext.BaseDirectory.
    public string? HostDllPath { get; init; } = null;
}
