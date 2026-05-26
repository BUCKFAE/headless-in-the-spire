using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Eval.Execution;

// One unit of work the harness schedules: agent + seed + character +
// ascension + modifiers. The cartesian product of the matrix axes
// produces the cell list before any process starts. Skipped cells (agent
// doesn't support character / ascension / modifier) never reach the
// executor — they're filtered during expansion.
//
// `RelativeReplayDir` is the directory under the eval root where this
// cell's `cell.json` + AD-8 recorder bytes land. We bake the layout in
// during expansion so the executor doesn't have to reconstruct it.
public sealed record Cell(
    AgentManifest             Manifest,
    ulong                     Seed,
    Character                 Character,
    int                       Ascension,
    IReadOnlyList<ModifierId> Modifiers,
    string                    RelativeReplayDir,
    HarnessBudgets            Budgets);
