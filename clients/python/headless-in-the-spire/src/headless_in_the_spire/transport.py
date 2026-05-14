"""Subprocess NDJSON transport for the C# headless host.

Spawns the host (`dotnet run --project src/Sts2Headless -- --stdio` by
default) and speaks JSON-RPC-style envelopes over stdio per AD-2. One
background thread reads stdout, parses each line as JSON, and dispatches it
either to the waiting caller (by `id`) or to the notification subscribers.

`Transport` is intentionally low-level: it accepts raw `dict` params and
returns raw `dict` results. The typed wrapper in `client.py` handles
pydantic (de)serialisation.
"""

from __future__ import annotations

import json
import os
import queue
import shutil
import subprocess
import sys
import threading
from collections.abc import Callable, Mapping, Sequence
from dataclasses import dataclass
from pathlib import Path
from typing import Any


# Default subprocess command components. Two forms:
#   1. HEADLESS_IN_THE_SPIRE_HOST=/path/to/binary  → run that binary directly.
#   2. fallback                                    → `dotnet run --project ...`
#      against the in-tree project, located by walking up to GAME_VERSION.
DEFAULT_DOTNET_PROJECT = "src/Sts2Headless/Sts2Headless.csproj"


class TransportClosedError(RuntimeError):
    """Raised when calling against a transport whose subprocess has exited."""


@dataclass(frozen=True, slots=True)
class JsonRpcError(Exception):
    code: int
    message: str
    data: Any | None = None

    def __str__(self) -> str:
        return f"JSON-RPC error {self.code}: {self.message}"


@dataclass(frozen=True, slots=True)
class Notification:
    method: str
    params: dict[str, Any] | None


class Transport:
    """Subprocess NDJSON pipe to the C# host.

    Usage:

        with Transport.spawn() as t:
            result = t.call("host/ping", None)
    """

    def __init__(
        self,
        process: subprocess.Popen,
        *,
        stderr_log: Callable[[str], None] | None = None,
    ) -> None:
        self._process = process
        self._stdin = process.stdin
        self._stdout = process.stdout
        self._stderr = process.stderr
        assert self._stdin and self._stdout, "stdio pipes required"

        self._next_id = 1
        self._lock = threading.Lock()
        self._pending: dict[int, queue.SimpleQueue] = {}
        self._notifications: queue.SimpleQueue[Notification] = queue.SimpleQueue()
        self._subscribers: list[Callable[[Notification], None]] = []
        self._closed = threading.Event()
        self._reader = threading.Thread(
            target=self._read_loop, name="hits-transport-reader", daemon=True,
        )
        self._reader.start()

        if self._stderr is not None:
            sink = stderr_log or (lambda line: print(f"[host stderr] {line}", file=sys.stderr))
            self._stderr_thread = threading.Thread(
                target=self._stderr_loop,
                args=(sink,),
                name="hits-transport-stderr",
                daemon=True,
            )
            self._stderr_thread.start()

    # ── Public API ────────────────────────────────────────────────────────

    @classmethod
    def spawn(
        cls,
        cmd: Sequence[str] | None = None,
        *,
        cwd: str | os.PathLike | None = None,
        env: Mapping[str, str] | None = None,
        stderr_log: Callable[[str], None] | None = None,
    ) -> "Transport":
        if cmd is None:
            cmd = _default_command(cwd)

        proc = subprocess.Popen(
            list(cmd),
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            cwd=str(cwd) if cwd else None,
            env=dict(env) if env is not None else None,
            text=True,
            bufsize=1,  # line-buffered
            encoding="utf-8",
        )
        return cls(proc, stderr_log=stderr_log)

    def call(
        self,
        method: str,
        params: Mapping[str, Any] | None,
        *,
        timeout: float | None = None,
    ) -> dict[str, Any] | None:
        if self._closed.is_set():
            raise TransportClosedError("host subprocess has exited")

        with self._lock:
            request_id = self._next_id
            self._next_id += 1
            inbox: queue.SimpleQueue = queue.SimpleQueue()
            self._pending[request_id] = inbox

        envelope: dict[str, Any] = {"id": request_id, "method": method}
        if params is not None:
            envelope["params"] = params
        line = json.dumps(envelope, separators=(",", ":"), ensure_ascii=False)

        try:
            assert self._stdin is not None
            self._stdin.write(line + "\n")
            self._stdin.flush()
        except (BrokenPipeError, ValueError) as ex:
            self._mark_closed()
            raise TransportClosedError(f"failed to write request: {ex}") from ex

        try:
            response = inbox.get(timeout=timeout)
        finally:
            with self._lock:
                self._pending.pop(request_id, None)

        if isinstance(response, _ReaderDied):
            raise TransportClosedError(
                f"host exited before responding to id={request_id} ({response.reason})",
            )
        assert isinstance(response, dict)
        if (err := response.get("error")) is not None:
            raise JsonRpcError(
                code=int(err.get("code", -32603)),
                message=str(err.get("message", "")),
                data=err.get("data"),
            )
        result = response.get("result")
        if result is not None and not isinstance(result, dict):
            raise RuntimeError(f"expected object result, got {type(result).__name__}")
        return result

    def subscribe(self, callback: Callable[[Notification], None]) -> Callable[[], None]:
        """Register a callback for server-push notifications. Returns an
        unsubscribe function."""
        with self._lock:
            self._subscribers.append(callback)

        def unsubscribe() -> None:
            with self._lock:
                try:
                    self._subscribers.remove(callback)
                except ValueError:
                    pass

        return unsubscribe

    def close(self, timeout: float = 5.0) -> int | None:
        """Close stdin (signalling EOF), wait for the host to exit, return its
        return code. Falls back to kill() after `timeout`."""
        if self._stdin is not None:
            try:
                self._stdin.close()
            except (BrokenPipeError, ValueError):
                pass
        try:
            rc = self._process.wait(timeout=timeout)
        except subprocess.TimeoutExpired:
            self._process.kill()
            rc = self._process.wait()
        self._mark_closed()
        return rc

    def __enter__(self) -> "Transport":
        return self

    def __exit__(self, exc_type, exc, tb) -> None:
        self.close()

    # ── Internals ─────────────────────────────────────────────────────────

    def _read_loop(self) -> None:
        assert self._stdout is not None
        try:
            for raw in self._stdout:
                line = raw.rstrip("\n")
                if not line:
                    continue
                try:
                    msg = json.loads(line)
                except json.JSONDecodeError:
                    # Lines we can't parse are logged but don't break the loop;
                    # the host should never emit them on stdout, but a stray
                    # Console.WriteLine somewhere is recoverable.
                    print(f"[host stdout, unparseable] {line!r}", file=sys.stderr)
                    continue

                if not isinstance(msg, dict):
                    continue

                if (request_id := msg.get("id")) is not None and isinstance(request_id, int):
                    with self._lock:
                        inbox = self._pending.get(request_id)
                    if inbox is not None:
                        inbox.put(msg)
                    # Orphan responses (no waiter) are dropped silently.
                    continue

                # No id → notification.
                method = msg.get("method")
                params = msg.get("params")
                if isinstance(method, str):
                    note = Notification(
                        method=method,
                        params=params if isinstance(params, dict) else None,
                    )
                    self._notifications.put(note)
                    with self._lock:
                        subscribers = list(self._subscribers)
                    for sub in subscribers:
                        try:
                            sub(note)
                        except Exception as ex:  # pragma: no cover
                            print(f"[notification subscriber] {ex!r}", file=sys.stderr)
        finally:
            self._fail_pending("stdout closed")

    def _stderr_loop(self, sink: Callable[[str], None]) -> None:
        assert self._stderr is not None
        for raw in self._stderr:
            sink(raw.rstrip("\n"))

    def _fail_pending(self, reason: str) -> None:
        with self._lock:
            inboxes = list(self._pending.values())
            self._pending.clear()
        sentinel = _ReaderDied(reason)
        for inbox in inboxes:
            inbox.put(sentinel)
        self._mark_closed()

    def _mark_closed(self) -> None:
        self._closed.set()


@dataclass(frozen=True, slots=True)
class _ReaderDied:
    reason: str


def _default_command(cwd: str | os.PathLike | None) -> list[str]:
    """Pick the host command:

    1. `HEADLESS_IN_THE_SPIRE_HOST` env var → that binary, plus `--stdio`.
    2. Otherwise locate the repo root and use `dotnet run --project ...`.
    """
    explicit = os.environ.get("HEADLESS_IN_THE_SPIRE_HOST")
    if explicit:
        return [explicit, "--stdio"]

    repo_root = _locate_repo_root(Path(cwd) if cwd else Path.cwd())
    project = repo_root / DEFAULT_DOTNET_PROJECT
    if not project.is_file():
        raise FileNotFoundError(
            f"Could not find {project!s}. Set HEADLESS_IN_THE_SPIRE_HOST to a "
            "prebuilt binary or run from inside the headless-in-the-spire repo."
        )
    dotnet = shutil.which("dotnet")
    if dotnet is None:
        raise FileNotFoundError(
            "`dotnet` not on PATH. Set HEADLESS_IN_THE_SPIRE_HOST to a "
            "prebuilt binary or install the .NET SDK."
        )
    return [dotnet, "run", "--project", str(project), "--no-build", "--", "--stdio"]


def _locate_repo_root(start: Path) -> Path:
    for p in [start, *start.parents]:
        if (p / "GAME_VERSION").is_file():
            return p
    raise FileNotFoundError(
        "could not locate headless-in-the-spire repo (no GAME_VERSION found "
        f"walking up from {start})."
    )
