# headless-in-the-spire-mcp

A [Model Context Protocol](https://modelcontextprotocol.io) server that exposes
the [headless-in-the-spire](../../..) wire surface as MCP tools. Plug it into
Claude Desktop / Claude Code / any MCP-aware AI, and the assistant can drive
a Slay the Spire 2 run end-to-end — pick a character, traverse the map, play
cards in combat, claim rewards, all via tool calls.

The MCP server is a *consumer* of the wire protocol (AD-2 / AD-5) — it owns no
behavioural truth (AD-6). Tools mirror `MethodCatalog` entries 1:1 plus a
`summarize_state` convenience that renders the current state as compact text
so AI assistants don't blow context on every poll.

## Layout

```
src/headless_in_the_spire_mcp/
  server.py     # FastMCP server, tool registration
  summary.py    # compact text rendering of RunStateResult
  __main__.py   # `python -m headless_in_the_spire_mcp` entry
tests/          # parity + summary unit tests
```

## Setup

This package is a member of the repo-root [uv workspace](../../../pyproject.toml).
From the repo root:

```sh
just setup::setup        # if you haven't yet — also handles game-DLL bootstrap
uv sync          # picks up this package as a new workspace member
```

## Running the server

The server spawns its own host subprocess (`dotnet run --project src/Sts2Headless …`)
on the first tool call. Locate the repo by running it from inside the repo
or by setting `HEADLESS_IN_THE_SPIRE_HOST=/path/to/prebuilt/binary`.

```sh
# Module form (works from anywhere inside the repo):
uv run python -m headless_in_the_spire_mcp

# Console script form (same thing):
uv run headless-in-the-spire-mcp

# With debug tools enabled (AD-7 — never in production):
uv run headless-in-the-spire-mcp --enable-debug
```

The server reads MCP requests on stdin and writes responses on stdout; logs
go to stderr.

## Adding it to Claude Code

Drop a `.mcp.json` at the repo root (a checked-in copy already lives there):

```json
{
  "mcpServers": {
    "headless-in-the-spire": {
      "command": "uv",
      "args": ["run", "headless-in-the-spire-mcp"],
      "cwd": "/path/to/headless-in-the-spire"
    }
  }
}
```

Inside the repo, Claude Code picks it up automatically. From elsewhere, point
`--mcp-config /path/to/.mcp.json` at it.

## Adding it to Claude Desktop

Edit `~/Library/Application Support/Claude/claude_desktop_config.json`
(macOS) or the equivalent on your platform, then restart Claude Desktop:

```json
{
  "mcpServers": {
    "headless-in-the-spire": {
      "command": "uv",
      "args": [
        "run",
        "--directory", "/absolute/path/to/headless-in-the-spire",
        "headless-in-the-spire-mcp"
      ]
    }
  }
}
```

`--directory` tells uv where the workspace lives without depending on a
working directory.

## Tool surface

Core (always registered):

| Tool | Wire method |
| --- | --- |
| `host_ping` | `host/ping` |
| `run_new` | `run/new` |
| `run_state` | `run/state` |
| `summarize_state` | (compact text view of `run/state`) |
| `run_select_map_node` | `run/select_map_node` |
| `run_select_event_option` | `run/select_event_option` |
| `run_select_rest_site_option` | `run/select_rest_site_option` |
| `run_leave_treasure_room` | `run/leave_treasure_room` |
| `run_buy_merchant_item` | `run/buy_merchant_item` |
| `run_leave_merchant_room` | `run/leave_merchant_room` |
| `run_end_turn` | `run/end_turn` |
| `run_play_card` | `run/play_card` |
| `run_use_potion` | `run/use_potion` |
| `run_select_reward` | `run/select_reward` |
| `run_skip_reward` | `run/skip_reward` |
| `run_enter_next_act` | `run/enter_next_act` |
| `run_proceed_event` | `run/proceed_event` |
| `run_history` | `run/history` |

Debug (only registered with `--enable-debug`, mirroring AD-7):

| Tool | Wire method |
| --- | --- |
| `debug_give_relic` | `debug/give_relic` |
| `debug_set_hp` | `debug/set_hp` |
| `debug_replace_deck` | `debug/replace_deck` |
| `debug_read_deck` | `debug/read_deck` |
| `debug_start_combat` | `debug/start_combat` |
| `debug_kill_all_enemies` | `debug/kill_all_enemies` |

Without `--enable-debug`, debug tools are *not registered* — they don't appear
in `tools/list`, so an AI assistant cannot discover or call them. This is
strictly stronger than the host gate alone: the host would still refuse
`-32001 DebugMethodDisabled`, but here the surface itself is invisible.

## Lifecycle

One MCP server process == one host subprocess == one game. `run_new` resets
the run *within* the same host without restarting. The host is spawned lazily
on the first tool call (so `tools/list` works without `dotnet`) and is shut
down via FastMCP's lifespan callback when the MCP server exits.

## Running tests

```sh
just validation::test-python  # includes this package's tests
```

The smoke test requires a built host + populated `vendor/sts2.dll` and is
skipped otherwise (same gate as the wire client smoke test). The parity test
runs everywhere.
