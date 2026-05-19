"""End-to-end smoke: spawn the MCP server, invoke `host_ping` through the
tool surface, verify a result.

Skipped when neither a prebuilt host binary nor a buildable .NET project
plus a populated `vendor/sts2.dll` is available — same gate as the wire
client's smoke test. Keeps this file unit-suite-friendly while still
letting devs verify the slice end-to-end via `just test-python`.

We deliberately do **not** spawn the MCP server as a subprocess and speak
MCP over stdio: that adds a layer (the MCP wire) on top of the layer we
actually want to exercise (FastMCP tool dispatch → wire client → host).
Calling the FastMCP tool dispatcher in-process gives a cleaner failure
attribution if anything breaks.
"""

import asyncio
import json
import os
import shutil
from pathlib import Path
from typing import Any, cast

import pytest
from headless_in_the_spire_mcp import build_server


def _repo_root() -> Path:
    here = Path(__file__).resolve()
    for p in [here, *here.parents]:
        if (p / "GAME_VERSION").is_file():
            return p
    raise FileNotFoundError("no GAME_VERSION upwards from " + str(here))


def _host_available() -> bool:
    # Mirrors `headless_in_the_spire/tests/test_transport_smoke.py`.
    if os.environ.get("HEADLESS_IN_THE_SPIRE_HOST"):
        return True
    if shutil.which("dotnet") is None:
        return False
    repo = _repo_root()
    if not (repo / "src" / "Sts2Headless" / "Sts2Headless.csproj").is_file():
        return False
    return (repo / "vendor" / "sts2.dll").is_file()


@pytest.mark.skipif(not _host_available(), reason="no host binary and no dotnet+sts2.dll")
def test_host_ping_tool_round_trips():
    """Round-trip `host_ping` end-to-end: spawn the host via FastMCP's tool
    dispatch, get back the same DTO shape the wire client would return."""
    server = build_server(enable_debug=False, repo_root=_repo_root())
    result = asyncio.run(server.call_tool("host_ping", {}))
    # FastMCP returns (content, structured_content) where content is a list
    # of mcp.types.TextContent. We only need the structured payload.
    structured = result[1] if isinstance(result, tuple) else result
    assert isinstance(structured, dict), f"unexpected result shape: {type(structured).__name__}"
    payload: dict[str, Any] = structured
    # FastMCP wraps single non-pydantic returns under a "result" key.
    inner = payload.get("result")
    if isinstance(inner, dict):
        payload = cast("dict[str, Any]", inner)
    assert payload.get("ok") is True
    # game_version may be the literal pinned string from GAME_VERSION; we
    # don't assert content, only that the field came back.
    assert "gameVersion" in payload or "game_version" in payload


@pytest.mark.skipif(not _host_available(), reason="no host binary and no dotnet+sts2.dll")
def test_summarize_state_returns_text():
    """`summarize_state` exercises the wire client + the summary formatter
    end-to-end. Starts a fresh Ironclad run on a fixed seed so the output
    is deterministic enough to grep for known substrings."""
    server = build_server(enable_debug=False, repo_root=_repo_root())
    asyncio.run(server.call_tool("run_new", {"character": "ironclad", "seed": 42}))
    raw = asyncio.run(server.call_tool("summarize_state", {}))
    # Unwrap FastMCP's (content, structured) tuple.
    content = raw[0] if isinstance(raw, tuple) else raw
    text = ""
    if isinstance(content, list):
        for piece in content:  # mcp.types.TextContent
            if getattr(piece, "type", None) == "text":
                text += getattr(piece, "text", "")
    elif isinstance(content, dict):
        text = json.dumps(content)
    assert "Act" in text and "Floor" in text and "HP" in text, (
        f"summary missing expected headers — got: {text[:400]!r}"
    )
