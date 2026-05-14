# headless-in-the-spire-agents

Algorithms and drivers built on top of the
[`headless-in-the-spire`](../headless-in-the-spire/) Python wire client.

This package depends on the wire client via the in-repo uv workspace
(`{ workspace = true }` in `pyproject.toml`). The boundary exists so that
algorithm-side dependencies (numpy, torch, RL libraries, …) don't leak
into the thin client — see AD-5 in
[`documentation/requirements/02-architecture-decisions.md`](../../../documentation/requirements/02-architecture-decisions.md).

## Scope

Where this package owns code:

- Strategy implementations (greedy, minmax, MCTS, …).
- Run drivers that loop over `Client` methods to play full runs.
- Fitness/eval utilities for comparing strategies.
- Replay/trace analysis.

Where it does **not** own code:

- Anything that talks to the wire directly belongs in the
  `headless-in-the-spire` package (`Client`, `Transport`, generated DTOs).
- Anything that touches `sts2.dll` belongs in the C# host.

## Layout

```
src/headless_in_the_spire_agents/   # package source (empty for now)
tests/                              # pytest suite
```

No interface is committed yet: a stable `Agent` shape will fall out of
the second concrete algorithm, not the first. Until then, each algorithm
defines its own entry points and we promote shared abstractions in a
later commit.

## Running tests

From the repo root:

```sh
just test-python   # runs every workspace member's tests
```
