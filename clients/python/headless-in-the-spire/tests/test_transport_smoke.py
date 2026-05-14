"""Round-trip smoke test against the live C# host.

Spawns the host, sends `host/ping`, asserts the result shape. Skipped when
neither a prebuilt binary nor a buildable .NET project + `dotnet` CLI is
available — keeps this file unit-suite-friendly while still letting devs
verify the slice end-to-end.
"""

import os
import shutil
from pathlib import Path

import pytest

from headless_in_the_spire import Client, JsonRpcError


def _repo_root() -> Path:
    here = Path(__file__).resolve()
    for p in [here, *here.parents]:
        if (p / "GAME_VERSION").is_file():
            return p
    raise FileNotFoundError("no GAME_VERSION upwards")


def _host_available() -> bool:
    if os.environ.get("HEADLESS_IN_THE_SPIRE_HOST"):
        return True
    if shutil.which("dotnet") is None:
        return False
    return (_repo_root() / "src" / "Sts2Headless" / "Sts2Headless.csproj").is_file()


@pytest.mark.skipif(not _host_available(), reason="no host binary and no dotnet+project")
def test_host_ping_round_trips() -> None:
    with Client.spawn(cwd=_repo_root()) as c:
        result = c.host_ping(timeout=60.0)
    assert result.ok is True
    # game_version may be the literal pinned string from GAME_VERSION; we
    # don't assert content, only that the field came back as a string.
    assert isinstance(result.game_version, str) or result.game_version is None


@pytest.mark.skipif(not _host_available(), reason="no host binary and no dotnet+project")
def test_unknown_method_returns_jsonrpc_error() -> None:
    with Client.spawn(cwd=_repo_root()) as c, pytest.raises(JsonRpcError) as exc_info:
        c.transport.call("nope/does_not_exist", None, timeout=60.0)
    assert exc_info.value.code == -32601  # JSON-RPC "method not found"
