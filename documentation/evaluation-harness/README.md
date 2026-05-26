# Evaluation harness — design-by-example sketches

This folder is a *design-by-example* exercise. It sketches the API
surface of the planned evaluation harness so we can validate that the
call sites *feel* ergonomic before committing to an implementation.

**Nothing in this folder builds.** Every code block is imagined. Real
types from `src/Sts2Headless.Agents/` and `src/Sts2Headless.Protocol/`
(`IAgent`, `HeuristicAgent`, `AgentAction`, `RunStateResult`,
`Character`, …) appear by their actual names. Types proposed for this
feature (`EvaluationHarnessConfig`, `EvaluationHarness`,
`AgentManifest`, `BundledAgent`, `BuiltinAgents`,
`IScoringFunction`, `CellResult`, …) are wishful and will be pinned
down in the implementation ADR (informally "AD-9").

The spec this sketches against is
[../requirements/04-evaluation-harness.md](../requirements/04-evaluation-harness.md).
Read that first if you haven't.

## Reading order

1. **[01-program.md](./01-program.md)** — the main eval program, four
   variants from minimal to maxed-out, plus a custom scoring function
   and the `just` recipes that invoke it. This is the file to
   scrutinise — if the call site doesn't feel good here, something
   upstream is wrong.
2. **[02-agents.md](./02-agents.md)** — what an agent author writes,
   in three flavours: in-repo C#, external-repo C#, and Python.
   Includes the agent-manifest JSON shape and a sketch of the
   `agent/*` wire dialect.
3. **[03-results.md](./03-results.md)** — what the harness emits. The
   per-eval directory tree, sample `summary.md`, `runs.jsonl`,
   `cell.json`, `config.json`. Sibling-shaped to `sweep-*.md` so the
   tooling-shape feels native.

## How to read these sketches

- **Argue with them.** Every API choice is up for grabs. If something
  feels clunky, mark it.
- **Don't extend them speculatively.** Adding "but what if the user
  wants X" makes the sketches less useful, not more — they are
  ergonomics fixtures, not feature lists. Feature growth happens in
  the spec.
- **Cross-check with the spec.** If a sketch implies a behaviour
  [04-evaluation-harness.md](../requirements/04-evaluation-harness.md)
  doesn't promise, that's a divergence worth flagging.
- **Believe the type names.** Real types are real (`IAgent`,
  `AgentAction`, `RunStateResult`, `Character`, `HeuristicAgent`,
  `GreedyAgent`, `IroncladAgent`). Proposed types are clearly new
  (`EvaluationHarnessConfig`, `EvaluationHarness`, `AgentManifest`,
  `BundledAgent`, `BuiltinAgents`, `IScoringFunction`, …) and
  live in a new `src/Sts2Headless.Eval/` project.

## What the harness owes the user (one-screen summary)

- A single call site (`EvaluationHarness.RunAsync(config)`) takes a
  matrix and returns a typed report. No globals, no implicit env vars
  driving behaviour, no required JSON-on-disk to get started.
- Agents in any language plug in through a stdio dialect — the
  harness owns the host *and* the agent subprocess per cell, both
  speak NDJSON.
- Replays land automatically under `replays/eval-harness/<eval-id>/`,
  one AD-8 directory per cell, indexed back to the result row.
- Crashes (agent / engine / host / harness) are first-class outcomes,
  not test failures.
- The leaderboard sort is a plug-in (`IScoringFunction`); the default
  is `lex-sort(win-rate desc, mean-floor desc, mean-wall-clock asc)`.

The rest of these files show what each of those looks like at the
keyboard.
