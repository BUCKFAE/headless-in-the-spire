# headless-in-the-spire-leaderboard

Downstream consumer of the C# evaluation harness.

The C# `Sts2Headless.Eval` orchestrator (under `src/Sts2Headless.Eval/`)
runs the matrix and emits `summary.json` + `runs.jsonl` per eval. This
package reads those files and renders a static leaderboard. By design
it authors *nothing* — every fact in the leaderboard is sourced from the
C# emitter (AD-6, NFR-1).

## Usage

```bash
uv run sts2-leaderboard <eval-directory>
```

For example:

```bash
uv run sts2-leaderboard replays/eval-harness/2026-05-26T19-32-04Z/
```

Prints the summary in the same shape the C#-emitted `summary.md` does;
the goal of the parity is to allow a CI gate that compares the two
renderings line-for-line if needed.
