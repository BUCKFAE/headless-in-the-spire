"""Run every Python agent against the host and record their replays.

For each (agent, seed) pair the script:
1. Spawns its own host subprocess with `STS2_REPLAY_OUT=<out>/<agent.name>`,
   so finished runs land in a per-agent folder.
2. Starts a fresh run with the requested character + seed.
3. Drives the agent to completion (or hits `--max-steps`).
4. Prints one prefixed status line on entry and one on exit.

Parallelism is opt-in via `--workers`. Each worker spawns its own host
subprocess, which loads a fresh copy of sts2.dll (hundreds of MB
resident) — so the default cap is intentionally conservative. Crank it
up if you have the RAM.

Per AD-6 this is a Python user tool. The canonical "play many agents,
diff their behaviour" surface should eventually live in C# under
`src/Sts2Headless.Agents/`; until then this script is the most direct
way to fill a `replays/` library from Python.
"""

import argparse
import os
import sys
import time
import traceback
from collections.abc import Callable, Sequence
from concurrent.futures import ThreadPoolExecutor, as_completed
from dataclasses import dataclass
from pathlib import Path

from headless_in_the_spire import Client
from headless_in_the_spire._models import Character, RunNewParams
from headless_in_the_spire_agents import (
    Agent,
    AttackAgent,
    BlockAgent,
    GreedyAgent,
    RandomAgent,
    RunOutcome,
    play_run,
)

# Registry of every agent this script can drive. The factory takes the
# run seed so a stochastic agent (RandomAgent) gets a deterministic
# stream; deterministic agents ignore it. Keep this dict the single
# source of truth — `--agents` filters by these keys.
#
# The pyright suppression on the lambdas is unavoidable: each concrete
# agent class declares `name` via HeuristicAgent's ClassVar contract, but
# the Agent protocol declares it as a plain `str`. The asymmetry is
# benign at runtime (both shapes resolve to a class-level string) and
# tightening the protocol to ClassVar cascades into errors on every
# concrete subclass that uses plain `name = "..."` syntax.
AGENT_FACTORIES: dict[str, Callable[[int], Agent]] = {
    GreedyAgent.name: lambda _seed: GreedyAgent(),  # pyright: ignore[reportAssignmentType]
    RandomAgent.name: lambda seed: RandomAgent(seed=seed),  # pyright: ignore[reportAssignmentType]
    BlockAgent.name: lambda _seed: BlockAgent(),  # pyright: ignore[reportAssignmentType]
    AttackAgent.name: lambda _seed: AttackAgent(),  # pyright: ignore[reportAssignmentType]
}


@dataclass(frozen=True, slots=True)
class _Task:
    agent_name: str
    seed: int
    character: Character


@dataclass(frozen=True, slots=True)
class _TaskResult:
    task: _Task
    ok: bool
    outcome: RunOutcome | None
    elapsed_s: float
    error: str | None


def _log(task: _Task, message: str) -> None:
    # One line per event, prefixed by agent/seed so the parallel
    # interleave is still readable. Flush so a long-running task's
    # progress is visible immediately.
    print(f"[{task.agent_name:<8} seed={task.seed:<6}] {message}", flush=True)


def _run_one(task: _Task, out_root: Path, max_steps: int) -> _TaskResult:
    # Per-agent replay root: the host's recorder treats this as the
    # parent and creates its own `<game_version>/<timestamp>-<seed>/`
    # subtree below it (today's behaviour). Folder-by-agent is what
    # gives us `replays/<agent-name>/...` grouping for free.
    agent_root = out_root / task.agent_name
    agent_root.mkdir(parents=True, exist_ok=True)
    env = dict(os.environ)
    env["STS2_REPLAY_OUT"] = str(agent_root.resolve())

    agent = AGENT_FACTORIES[task.agent_name](task.seed)
    _log(task, f"start (out={agent_root})")
    started = time.monotonic()
    try:
        with Client.spawn(env=env) as client:
            client.run_new(
                RunNewParams(character=task.character, seed=task.seed),
                timeout=120,
            )
            outcome = play_run(client, agent, max_steps=max_steps)
    except Exception as ex:
        elapsed = time.monotonic() - started
        _log(task, f"FAILED in {elapsed:.1f}s: {ex!r}")
        return _TaskResult(
            task=task,
            ok=False,
            outcome=None,
            elapsed_s=elapsed,
            error="".join(traceback.format_exception_only(type(ex), ex)).strip(),
        )
    elapsed = time.monotonic() - started
    final = outcome.final_state
    _log(
        task,
        f"done in {elapsed:.1f}s  steps={outcome.steps}  hp={final.hp}  "
        f"floor={final.act_floor}  terminated_by={outcome.terminated_by}",
    )
    return _TaskResult(task=task, ok=True, outcome=outcome, elapsed_s=elapsed, error=None)


def _print_summary(results: list[_TaskResult]) -> None:
    print()
    print("=== summary ===")
    by_agent: dict[str, list[_TaskResult]] = {}
    for r in results:
        by_agent.setdefault(r.task.agent_name, []).append(r)
    for agent_name in sorted(by_agent):
        rows = by_agent[agent_name]
        ok = sum(1 for r in rows if r.ok)
        total = len(rows)
        total_time = sum(r.elapsed_s for r in rows)
        print(f"  {agent_name:<8}  {ok}/{total} ok  ({total_time:.1f}s total)")


def main(argv: Sequence[str] | None = None) -> int:
    default_agents = list(AGENT_FACTORIES)
    parser = argparse.ArgumentParser(
        description=(
            "Drive every Python agent against the host in parallel, "
            "recording into replays/<agent-name>/."
        ),
    )
    parser.add_argument(
        "--agents",
        nargs="+",
        choices=default_agents,
        default=default_agents,
        metavar="NAME",
        help=f"Agents to run (default: all — {', '.join(default_agents)}).",
    )
    parser.add_argument(
        "--seeds",
        nargs="+",
        type=int,
        default=[42],
        help="One run per (agent, seed). Default: 42.",
    )
    parser.add_argument(
        "--character",
        type=Character,
        choices=list(Character),
        default=Character.ironclad,
        metavar="{" + ",".join(c.value for c in Character) + "}",
        help="Character every run starts with.",
    )
    parser.add_argument(
        "--out",
        type=Path,
        default=Path("replays"),
        help="Replay root (default: ./replays). Each agent gets its own subfolder.",
    )
    parser.add_argument(
        "--workers",
        type=int,
        default=0,
        help=(
            "Parallel host subprocesses. 0 (default) picks "
            "min(tasks, 4). Each worker loads its own sts2.dll — watch RAM."
        ),
    )
    parser.add_argument(
        "--max-steps",
        type=int,
        default=500,
        help="Safety cap per run.",
    )
    args = parser.parse_args(argv)

    tasks = [
        _Task(agent_name=name, seed=seed, character=args.character)
        for name in args.agents
        for seed in args.seeds
    ]
    if not tasks:
        print("no tasks (empty --agents or --seeds)", file=sys.stderr)
        return 2

    workers = args.workers if args.workers > 0 else min(len(tasks), 4)
    out_root: Path = args.out
    out_root.mkdir(parents=True, exist_ok=True)
    print(f"running {len(tasks)} task(s) across {workers} worker(s) → {out_root.resolve()}")

    results: list[_TaskResult] = []
    # Threads, not processes: every task spends its time blocked on
    # stdio with a subprocess, so the GIL is not in the way. Threads
    # also avoid pickling agent factories across a process boundary.
    with ThreadPoolExecutor(max_workers=workers) as pool:
        futures = {pool.submit(_run_one, t, out_root, args.max_steps): t for t in tasks}
        for fut in as_completed(futures):
            results.append(fut.result())

    _print_summary(results)
    return 0 if all(r.ok for r in results) else 1


if __name__ == "__main__":
    sys.exit(main())
