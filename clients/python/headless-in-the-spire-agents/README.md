# headless-in-the-spire-agents

Python-side **user tools** for driving runs through the
[`headless-in-the-spire`](../headless-in-the-spire/) wire client.

Behavioral source-of-truth lives in C# — canonical agents, drivers,
scenarios, and regression tests are authored in `src/Sts2Headless.Agents/`
and the C# test trees per
[AD-6](../../../documentation/requirements/02-architecture-decisions.md).
This package does **not** author canonical agents and is **not** part of
the regression net.

What it *does* offer:

- A small action algebra + run loop so engineers can prototype against
  the wire from Python without rebuilding the dispatch boilerplate.
- A reference `GreedyAgent` illustrating the shape — useful for
  exercising the Python client end-to-end, not for "this is how greedy
  is supposed to play" (that question is answered by the C# reference).
- Parity-test scaffolding (future) — given a recorded scenario, assert
  the Python client reproduces the C#-canonical outcome.

This package depends on the wire client via the in-repo uv workspace
(`{ workspace = true }` in `pyproject.toml`). The boundary exists so
algorithm-side dependencies (numpy, torch, RL libraries, …) don't leak
into the thin client.

## Where things live

| Concern | Home |
| --- | --- |
| Canonical agents (greedy, MCTS, replay re-executor) | `src/Sts2Headless.Agents/` (C#) |
| Scenarios / regression tests | C# unit / integration / end-to-end suites |
| Python wire client | `clients/python/headless-in-the-spire/` |
| Python user-side run loop + reference agent | this package |
| Python parity tests against C# canon | `tests/` here (parity only — never behavioral) |

## Layout

```
src/headless_in_the_spire_agents/
  actions.py             # Action algebra (PlayCard, EndTurn, …)
  state.py               # GameSnapshot Protocol + Phase detection
  agent.py               # Agent Protocol + HeuristicAgent convenience base
  driver.py              # play_run loop + apply_action dispatch
  agents/greedy.py       # Reference GreedyAgent (illustrative, not canonical)
  examples/greedy_run.py # Runnable: drive the host with GreedyAgent + log
tests/                    # unit tests of the Python layer (no live host)
```

## Running tests

From the repo root:

```sh
just validation::test-python   # runs every workspace member's tests
```

## Running the example

```sh
uv run python -m headless_in_the_spire_agents.examples.greedy_run \
    --character ironclad --seed 42 --max-steps 500
```

Spawns the C# host, starts a new run, and prints one line per step:

```
step=0000  floor=0  hp=80  phase=map      room=MapRoom      -> select_map_node(col=3, row=0)
step=0001  floor=1  hp=80  phase=combat   room=CombatRoom   -> play_card(card=0, target=0)
...
--- end: terminated_by=game_over  steps=137  hp=0  floor=11  room=CombatRoom  game_over=True
```
