"""Pin the MCP tool catalogue against the wire client's MethodCatalog.

If `protocol/openrpc.json` (or its mirror in `client.METHOD_NAMES`) gains,
drops, or renames a method, this test fails until `server.py` is updated to
match. Same posture as the wire client's own catalogue parity test — the
MCP layer must never silently lag behind the wire.

Two surfaces are pinned in lockstep:

1. Core wire methods → MCP tools 1:1, plus `summarize_state` (the only
   convenience tool the MCP server owns).
2. Debug wire methods → MCP tools 1:1, but only when `enable_debug=True`.
"""

import asyncio

from headless_in_the_spire_mcp import build_server

from headless_in_the_spire.client import METHOD_NAMES

# The MCP server adds exactly one convenience tool on top of the wire
# methods. Adding more requires updating this set deliberately.
_EXTRA_CORE_TOOLS = {"summarize_state"}

# Wire-name prefix that marks the AD-7 debug methods.
_DEBUG_PREFIX = "debug/"


def _wire_method_names_by_class() -> tuple[set[str], set[str]]:
    """Returns (core_python_names, debug_python_names) derived from
    `METHOD_NAMES`. Splits on the `debug/` wire-name prefix so a new debug
    method is automatically recognised here."""
    core: set[str] = set()
    debug: set[str] = set()
    for wire_name, py_name in METHOD_NAMES.items():
        bucket = debug if wire_name.startswith(_DEBUG_PREFIX) else core
        bucket.add(py_name)
    return core, debug


def _registered_tool_names(*, enable_debug: bool) -> set[str]:
    server = build_server(enable_debug=enable_debug)
    tools = asyncio.run(server.list_tools())
    return {t.name for t in tools}


def test_core_tools_match_wire_plus_summary():
    wire_core, _ = _wire_method_names_by_class()
    expected = wire_core | _EXTRA_CORE_TOOLS
    got = _registered_tool_names(enable_debug=False)
    missing = expected - got
    extra = got - expected
    assert not missing and not extra, (
        f"MCP tool catalogue out of sync with METHOD_NAMES. "
        f"Missing: {sorted(missing)}; unexpected extras: {sorted(extra)}."
    )


def test_debug_tools_omitted_without_flag():
    _, wire_debug = _wire_method_names_by_class()
    got = _registered_tool_names(enable_debug=False)
    leaked = got & wire_debug
    assert not leaked, (
        f"debug tools must not be registered without --enable-debug "
        f"(AD-7). Leaked: {sorted(leaked)}"
    )


def test_debug_tools_present_with_flag():
    wire_core, wire_debug = _wire_method_names_by_class()
    expected = wire_core | wire_debug | _EXTRA_CORE_TOOLS
    got = _registered_tool_names(enable_debug=True)
    missing = expected - got
    extra = got - expected
    assert not missing and not extra, (
        f"With --enable-debug, the MCP catalogue should expose every wire "
        f"method plus summarize_state. Missing: {sorted(missing)}; "
        f"unexpected extras: {sorted(extra)}."
    )


def test_every_tool_has_a_description():
    """A blank description is invisible to MCP clients — the model has no
    way to pick the tool. Make that an explicit invariant so we never ship
    a silently-undocumented tool."""
    server = build_server(enable_debug=True)
    tools = asyncio.run(server.list_tools())
    blank = [t.name for t in tools if not (t.description or "").strip()]
    assert not blank, f"tools without a description: {blank}"
