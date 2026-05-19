# MCP integration

**Status**: shipped 2026-05-19 (`clients/python/headless-in-the-spire-mcp`).

A short note recording why the [Model Context Protocol](https://modelcontextprotocol.io)
server lives where it does and why the surface looks the way it does. This is
*not* an architecture decision — every choice below sits comfortably under
the existing ADs (AD-1, AD-5, AD-6, AD-7). It's a placement record.

## What it is

`headless-in-the-spire-mcp` is an MCP server that exposes the wire protocol
(`protocol/openrpc.json`) to AI assistants. Each `MethodCatalog` entry maps
1:1 to an MCP tool; `summarize_state` is the only convenience tool we own.
A connected AI (Claude Desktop / Claude Code / any MCP host) can drive a
Slay the Spire 2 run end-to-end via tool calls.

## Placement: a Python uv workspace member

The server is a fourth member of the repo-root uv workspace, next to
`headless-in-the-spire` (the wire client), `headless-in-the-spire-agents`,
and `headless-in-the-spire-utils`.

The choice between **C# (new `src/Sts2Headless.McpServer/`)** and **Python
(new `clients/python/headless-in-the-spire-mcp/`)** turned on what kind of
component the MCP server is:

- AD-1 makes the *core* C# only — the in-game mod, the headless host, the
  orchestrator that talks to `sts2.dll`. AD-1 does not say "every component
  is C#"; it says "the core is C#".
- The MCP server is a *consumer* of the wire protocol. It does not touch
  `sts2.dll`, does not embed the game, does not author scenarios. AD-5
  explicitly names "Python (for ML / RL)" as a first-class binding target;
  AI assistants driving a game via MCP are exactly that.
- The existing Python client (`headless-in-the-spire`) already owns the
  subprocess transport and the generated pydantic DTOs. A C# MCP server
  would re-implement both — pointless duplication, and the wire client's
  Python DTOs are the only reason adding 18 tools costs ~400 LOC instead
  of ~1500.

Python wins on cost; nothing in the ADs argues against it. The official
`mcp` Python SDK (FastMCP, MIT, pydantic v2 native) is the standard choice.

## Tool surface

One MCP tool per `MethodCatalog` core entry (17 today) plus `summarize_state`,
which renders the current `RunStateResult` as compact text. Three reasons:

1. **AD-6.** A 1:1 mirror keeps the wire protocol the single authoritative
   surface. Higher-level "play card by name" tools would introduce a second
   behavioural layer that decides what player-facing nouns mean — that's
   exactly the polyglot truth AD-6 rejects.
2. **Token economics.** An AI assistant polling `run_state` every turn
   blows context on the full pydantic DTO (~1–2 KB JSON). `summarize_state`
   condenses the relevant slice to a few hundred bytes of plain text.
   Without it, the LLM either pays the JSON tax or guesses; with it, the
   wire stays canonical *and* the assistant has a cheap option.
3. **Discoverability.** Tool names mirror the Python client methods
   (`run_play_card`, `debug_set_hp`, …), which mirror the wire names
   (`run/play_card`, `debug/set_hp`). Anyone who reads `openrpc.json`
   already knows the MCP surface.

## Debug-method gating

AD-7 makes `debug/*` opt-in via `--enable-debug` on the host. The MCP
server inherits this two ways:

1. **Tool registration** — `debug_*` tools are only registered in the MCP
   catalogue when the MCP server is itself started with `--enable-debug`.
   Without the flag, the AI assistant cannot even *discover* a debug tool,
   let alone call it.
2. **Host gate** — the same `--enable-debug` flag is propagated to the
   spawned host. So even if a future bug let a debug tool slip into the
   non-debug catalogue, the host would still refuse the call with
   `WireErrorCode.DebugMethodDisabled` (-32001).

Two locks on the same door, gated by one flag. The flag is a CLI argument,
not an env var, so `ps`/log inspection makes it visible.

## Lifecycle

One MCP server process == one C# host subprocess == one game. The host is
spawned **lazily** on the first tool call (so `tools/list` works without
`dotnet` or `vendor/sts2.dll`); shut down via FastMCP's lifespan callback
when the MCP server exits.

Considered and rejected: explicit `start_host` / `stop_host` tools. An AI
that forgets to call `start_host` sees every other tool fail; that's a
state machine the LLM doesn't need to track. `run_new` already resets the
*game* within the same host, which covers every "I want a fresh start"
use case.

## Adoption

Claude Code picks up `.mcp.json` at the repo root automatically when run
from inside the repo. Claude Desktop and other MCP hosts wire up the same
config in their respective settings — see
[`clients/python/headless-in-the-spire-mcp/README.md`](../../clients/python/headless-in-the-spire-mcp/README.md)
for ready-to-paste snippets.

## Tests

Mirrors the wire client's posture:

- **Catalogue parity** (`test_tool_catalog_parity.py`) — assert the MCP
  tool set equals `METHOD_NAMES` (core + `summarize_state`); debug tools
  registered only with `--enable-debug`.
- **Summary unit tests** (`test_summary.py`) — synthetic `RunStateResult`
  values pin the *shape* of the compact view so a refactor doesn't
  silently change what the LLM sees.
- **Live smoke** (`test_server_smoke.py`) — spawn the host via FastMCP's
  tool dispatch, round-trip `host_ping`, and exercise `summarize_state`
  end-to-end on seed 42 Ironclad. Skipped without `vendor/sts2.dll` and
  `dotnet`, same gate as the wire client's smoke test.

All three run inside `just test-python`.
