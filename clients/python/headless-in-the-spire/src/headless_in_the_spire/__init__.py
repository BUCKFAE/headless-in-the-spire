"""headless-in-the-spire — Python client for the C# headless runner.

The wire protocol (NDJSON + JSON-RPC envelope) is described by
`protocol/openrpc.json` at the repo root. DTOs in `_models` are generated
from that artefact; transport and the typed `Client` are hand-rolled.
"""

from headless_in_the_spire.client import Client
from headless_in_the_spire.transport import (
    JsonRpcError,
    Notification,
    Transport,
    TransportClosedError,
)

__version__ = "0.0.1"

__all__ = [
    "Client",
    "JsonRpcError",
    "Notification",
    "Transport",
    "TransportClosedError",
    "__version__",
]
