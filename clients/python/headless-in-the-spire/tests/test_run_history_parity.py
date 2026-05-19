"""Parity test for `RunHistoryDocument` against real game-written `.run` files.

The C# `RunHistoryDocumentTests` validate parsing on the C# side
(`vendor/sample-saves/`). This test mirrors that posture on the Python
side: walk every `.run` file under `vendor/replays/`, parse via the
generated pydantic model, assert it doesn't raise and that the load-
bearing fields populate.

The schema-export pipeline has a few sharp edges around this record
(AD-8 makes it snake_case, the game also writes one field — `TextKey` —
in PascalCase). Both broke the Python side silently before
2026-05-19; this test is the regression net.

Skipped when no replays present (CI / fresh clones).
"""

from pathlib import Path

import pytest

from headless_in_the_spire._models import RunHistoryDocument


def _repo_root() -> Path:
    here = Path(__file__).resolve()
    for p in [here, *here.parents]:
        if (p / "GAME_VERSION").is_file():
            return p
    raise FileNotFoundError("no GAME_VERSION upwards from " + str(here))


def _run_json_files() -> list[Path]:
    root = _repo_root() / "vendor" / "replays"
    if not root.is_dir():
        return []
    return sorted(root.rglob("run.json"))


_RUN_JSONS = _run_json_files()


@pytest.mark.skipif(not _RUN_JSONS, reason="no vendor/replays/**/run.json present")
@pytest.mark.parametrize("path", _RUN_JSONS, ids=lambda p: str(p.relative_to(_repo_root())))
def test_run_history_document_parses(path: Path) -> None:
    """A real game-written `.run` must round-trip through the generated
    pydantic model without raising. Regression net for AD-8's
    snake_case wire shape vs the PascalCase default the rest of the
    protocol uses."""
    doc = RunHistoryDocument.model_validate_json(path.read_text())
    # Schema-pin: bumping the game version may bump this; surface the
    # change loudly rather than silently.
    assert doc.schema_version == 9, f"unexpected schema_version {doc.schema_version}"
    assert doc.seed, "seed must be non-empty"
    assert doc.build_id, "build_id must be non-empty"


@pytest.mark.skipif(not _RUN_JSONS, reason="no vendor/replays/**/run.json present")
def test_history_choice_entry_text_key_populates() -> None:
    """Specific regression: `HistoryChoiceEntry.TextKey` is the one field in
    the History tree the game writes as PascalCase inside an otherwise
    snake_case record. Before the 2026-05-19 fix this silently parsed to
    an empty string on BOTH sides (the C# parser dropped it under the
    `SnakeCaseLower` policy; the Python alias was wrong-cased).
    Find any `.run` that recorded an ancient-choice and assert
    text_key populated.
    """
    for path in _RUN_JSONS:
        doc = RunHistoryDocument.model_validate_json(path.read_text())
        for act in doc.map_point_history:
            for entry in act:
                for stats in entry.player_stats or []:
                    for choice in stats.ancient_choice or []:
                        assert choice.text_key, (
                            f"ancient_choice.text_key was empty in {path} — "
                            f"likely a regression of the TextKey mixed-case "
                            f"fix"
                        )
                        return
    pytest.skip("no run had an ancient_choice — coverage gap, not a failure")
