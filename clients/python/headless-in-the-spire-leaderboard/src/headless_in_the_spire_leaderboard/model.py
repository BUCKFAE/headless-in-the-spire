"""Typed models mirroring the C# emitter's ``summary.json`` shape.

The C# side is the canonical authority on the field set (NFR-1: adding
fields is fine, removing/renaming is a breaking change with the same
discipline as the wire protocol). We use ``dataclasses`` over pydantic
here to keep this package stdlib-only — the orchestrator never depends
on this code path, so we treat any deserialisation failure as a clear
"C# emitter changed, refresh the model" signal.
"""

import json
from dataclasses import dataclass
from pathlib import Path
from typing import Any


@dataclass(frozen=True, slots=True)
class AgentIdentity:
    name: str
    version: str
    language: str | None
    manifest_type: str


@dataclass(frozen=True, slots=True)
class AgentAggregates:
    cells: int
    wins: int
    win_rate: float
    mean_floor: float
    p25_floor: int
    p50_floor: int
    p75_floor: int
    engine_crashes: int
    host_crashes: int
    agent_crashes: int
    harness_errors: int
    timeouts: int
    stalled: int
    max_steps: int
    median_wall_clock_ms: int
    mean_wall_clock_ms: int


@dataclass(frozen=True, slots=True)
class AgentRanking:
    rank: int
    agent: AgentIdentity
    score: float
    aggregates: AgentAggregates


@dataclass(frozen=True, slots=True)
class SeedBankReference:
    name: str
    version: str
    count: int


@dataclass(frozen=True, slots=True)
class ScoringFunctionReference:
    name: str
    version: str


@dataclass(frozen=True, slots=True)
class WireErrorPayload:
    code: int
    message: str
    stack: str | None = None


@dataclass(frozen=True, slots=True)
class NotableCell:
    agent: str
    seed: int
    terminus: str
    floor: int
    replay_path: str
    error: WireErrorPayload | None = None


@dataclass(frozen=True, slots=True)
class EvaluationSummary:
    eval_id: str
    game_version: str
    sts2_dll_sha256: str
    seed_bank: SeedBankReference
    characters: list[str]
    ascensions: list[int]
    modifiers: list[str]
    scoring: ScoringFunctionReference
    elapsed_ms: int
    cell_count: int
    workers: int
    ranking: list[AgentRanking]
    notable_cells: list[NotableCell]


def load_summary(path: Path | str) -> EvaluationSummary:
    """Read ``summary.json`` from disk and return a typed
    :class:`EvaluationSummary`.

    Accepts either the eval directory (``replays/eval-harness/<eval-id>/``)
    or the ``summary.json`` file directly. Raises :class:`FileNotFoundError`
    if neither resolves to a real file.
    """
    p = Path(path)
    if p.is_dir():
        p = p / "summary.json"
    if not p.exists():
        raise FileNotFoundError(f"summary.json not found at {p}")
    raw: dict[str, Any] = json.loads(p.read_text(encoding="utf-8"))
    return _parse_summary(raw)


def _parse_summary(raw: dict[str, Any]) -> EvaluationSummary:
    seed_bank = SeedBankReference(**raw["seedBank"])
    scoring = ScoringFunctionReference(**raw["scoring"])
    ranking = [_parse_ranking(r) for r in raw["ranking"]]
    notable = [_parse_notable(n) for n in raw.get("notableCells", [])]
    return EvaluationSummary(
        eval_id=raw["evalId"],
        game_version=raw["gameVersion"],
        sts2_dll_sha256=raw["sts2DllSha256"],
        seed_bank=seed_bank,
        characters=list(raw["characters"]),
        ascensions=list(raw["ascensions"]),
        modifiers=list(raw["modifiers"]),
        scoring=scoring,
        elapsed_ms=int(raw["elapsedMs"]),
        cell_count=int(raw["cellCount"]),
        workers=int(raw["workers"]),
        ranking=ranking,
        notable_cells=notable,
    )


def _parse_ranking(raw: dict[str, Any]) -> AgentRanking:
    agent_raw: dict[str, Any] = raw["agent"]
    agent = AgentIdentity(
        name=agent_raw["name"],
        version=agent_raw["version"],
        language=agent_raw.get("language"),
        manifest_type=agent_raw["manifestType"],
    )
    aggs_raw: dict[str, Any] = raw["aggregates"]
    aggregates = AgentAggregates(
        cells=int(aggs_raw["cells"]),
        wins=int(aggs_raw["wins"]),
        win_rate=float(aggs_raw["winRate"]),
        mean_floor=float(aggs_raw["meanFloor"]),
        p25_floor=int(aggs_raw["p25Floor"]),
        p50_floor=int(aggs_raw["p50Floor"]),
        p75_floor=int(aggs_raw["p75Floor"]),
        engine_crashes=int(aggs_raw["engineCrashes"]),
        host_crashes=int(aggs_raw["hostCrashes"]),
        agent_crashes=int(aggs_raw["agentCrashes"]),
        harness_errors=int(aggs_raw["harnessErrors"]),
        timeouts=int(aggs_raw["timeouts"]),
        stalled=int(aggs_raw["stalled"]),
        max_steps=int(aggs_raw["maxSteps"]),
        median_wall_clock_ms=int(aggs_raw["medianWallClockMs"]),
        mean_wall_clock_ms=int(aggs_raw["meanWallClockMs"]),
    )
    return AgentRanking(
        rank=int(raw["rank"]),
        agent=agent,
        score=float(raw["score"]),
        aggregates=aggregates,
    )


def _parse_notable(raw: dict[str, Any]) -> NotableCell:
    err_raw = raw.get("error")
    error = WireErrorPayload(**err_raw) if err_raw is not None else None
    return NotableCell(
        agent=raw["agent"],
        seed=int(raw["seed"]),
        terminus=raw["terminus"],
        floor=int(raw["floor"]),
        replay_path=raw["replayPath"],
        error=error,
    )
