"""Parses a hand-rolled summary.json against the C# emitter's shape.

The fixture below is a literal copy of the design-by-example sketch in
``documentation/evaluation-harness/03-results.md`` so a refactor of the
C# emitter that changes any field name surfaces here as a red test.
"""

import json
from pathlib import Path

from headless_in_the_spire_leaderboard import load_summary
from headless_in_the_spire_leaderboard.render import render_markdown

FIXTURE: dict[str, object] = {
    "evalId": "2026-05-26T19-32-04Z",
    "gameVersion": "v0.103.2",
    "sts2DllSha256": "a1b2c3",
    "seedBank": {"name": "reference", "version": "1", "count": 50},
    "characters": ["Ironclad"],
    "ascensions": [0],
    "modifiers": [],
    "scoring": {"name": "lex-sort", "version": "1.0"},
    "elapsedMs": 1_104_000,
    "cellCount": 250,
    "workers": 8,
    "ranking": [
        {
            "rank": 1,
            "agent": {
                "name": "ironclad",
                "version": "0.5.1",
                "language": "csharp-bundled",
                "manifestType": "Sts2Headless.Eval.Manifests.IroncladManifest",
            },
            "score": 0.22,
            "aggregates": {
                "cells": 50,
                "wins": 11,
                "winRate": 0.22,
                "meanFloor": 31.4,
                "p25Floor": 18,
                "p50Floor": 28,
                "p75Floor": 47,
                "engineCrashes": 0,
                "hostCrashes": 0,
                "agentCrashes": 0,
                "harnessErrors": 0,
                "timeouts": 0,
                "stalled": 0,
                "maxSteps": 0,
                "medianWallClockMs": 72_000,
                "meanWallClockMs": 75_000,
            },
        },
    ],
    "notableCells": [
        {
            "agent": "greedy",
            "seed": 17,
            "terminus": "EngineCrash",
            "floor": 24,
            "replayPath": "cells/greedy/s17",
        },
    ],
}


def test_load_summary_from_dir(tmp_path: Path) -> None:
    (tmp_path / "summary.json").write_text(json.dumps(FIXTURE), encoding="utf-8")
    summary = load_summary(tmp_path)
    assert summary.eval_id == "2026-05-26T19-32-04Z"
    assert summary.seed_bank.count == 50
    assert summary.ranking[0].agent.name == "ironclad"
    assert summary.ranking[0].aggregates.win_rate == 0.22
    assert summary.notable_cells[0].terminus == "EngineCrash"


def test_load_summary_from_file(tmp_path: Path) -> None:
    path = tmp_path / "summary.json"
    path.write_text(json.dumps(FIXTURE), encoding="utf-8")
    summary = load_summary(path)
    assert summary.workers == 8


def test_render_markdown_smoke(tmp_path: Path) -> None:
    (tmp_path / "summary.json").write_text(json.dumps(FIXTURE), encoding="utf-8")
    md = render_markdown(load_summary(tmp_path))
    assert "Evaluation — 2026-05-26T19-32-04Z" in md
    assert "`ironclad`" in md
    assert "Notable cells" in md
    # Win% column rendered (correct to one decimal).
    assert "22.0%" in md
