"""Pin the slash → underscore name mapping (AD-5).

If `protocol/openrpc.json` gains, drops, or renames a method, this test fails
until `client._METHOD_NAMES` is updated to match. The deliberate sync step
prevents a generator behavioural change (or someone editing one side only)
from silently renaming the Python public API.
"""

from __future__ import annotations

import json
from pathlib import Path

from headless_in_the_spire.client import Client, _METHOD_NAMES


def _locate_repo_root(start: Path) -> Path:
    for p in [start, *start.parents]:
        if (p / "GAME_VERSION").is_file():
            return p
    raise FileNotFoundError(f"no GAME_VERSION upwards from {start}")


def _openrpc_methods() -> set[str]:
    repo = _locate_repo_root(Path(__file__).resolve())
    doc = json.loads((repo / "protocol" / "openrpc.json").read_text())
    return {m["name"] for m in doc["methods"]}


def test_method_names_match_openrpc() -> None:
    wire = _openrpc_methods()
    pinned = set(_METHOD_NAMES.keys())
    in_pin_only = pinned - wire
    in_wire_only = wire - pinned
    assert not in_pin_only and not in_wire_only, (
        f"client._METHOD_NAMES out of sync with protocol/openrpc.json. "
        f"In pin only: {sorted(in_pin_only)}; in wire only: {sorted(in_wire_only)}."
    )


def test_every_pinned_method_has_a_client_method() -> None:
    for py_name in _METHOD_NAMES.values():
        assert callable(getattr(Client, py_name, None)), (
            f"Client.{py_name} is missing — _METHOD_NAMES claims a wire entry "
            f"for it but no method exists."
        )
