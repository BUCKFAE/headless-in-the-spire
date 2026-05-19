"""headless-in-the-spire-mcp — MCP server over the wire client.

Exposes the C# host's NDJSON / JSON-RPC surface (AD-2) as Model Context
Protocol tools so AI assistants can drive a Slay the Spire 2 run end-to-end.
The MCP server is a *wire consumer* — it owns no behavioural truth (AD-6),
just a thin adapter from MCP `tools/call` to `Client.run_*`.

Lifecycle: one MCP server process == one C# host subprocess == one game.
Calling `run_new` resets the run within that host.

Debug methods (AD-7) are only registered when the MCP server is started with
`--enable-debug`. The same flag is propagated to the spawned host, so debug
tooling can never appear in the MCP catalogue without the host actually
honouring it.
"""

from headless_in_the_spire_mcp.server import build_server

__version__ = "0.0.1"

__all__ = [
    "__version__",
    "build_server",
]
