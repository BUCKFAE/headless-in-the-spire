"""Serve protocol/openrpc.json with permissive CORS and open the hosted
OpenRPC Playground (https://playground.open-rpc.org) pointed at it.

Why not a local SPA: the playground already renders OpenRPC docs better
than anything we'd hand-roll, and is updated upstream as the spec evolves.
The only friction it can't solve itself is fetching a local file — modern
browsers block client-side fetches of local files from a remote SPA. So
we run a tiny stdlib HTTP server with `Access-Control-Allow-Origin: *`
and pass the localhost URL as `?schemaUrl=...`.

Re-running `just build::export-schema` regenerates protocol/openrpc.json in
place; reload the playground tab to pick up the new shape.
"""

import http.server
import os
import socketserver
import sys
import threading
import urllib.parse
import webbrowser
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent.parent
PROTOCOL_DIR = REPO_ROOT / "protocol"
SPEC = PROTOCOL_DIR / "openrpc.json"
DEFAULT_PORT = 5179
PLAYGROUND_BASE = "https://playground.open-rpc.org/"


class CorsHandler(http.server.SimpleHTTPRequestHandler):
    def end_headers(self) -> None:
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Cache-Control", "no-store")
        super().end_headers()

    def log_message(self, format: str, *args: object) -> None:
        # Quieter default logging — one line per request to stderr, no
        # client-IP noise.
        sys.stderr.write(f"  {format % args}\n")


def main() -> int:
    if not SPEC.exists():
        print(f"error: {SPEC} not found. Run `just build::export-schema` first.", file=sys.stderr)
        return 1

    port = int(os.environ.get("PORT", str(DEFAULT_PORT)))
    local_url = f"http://localhost:{port}/openrpc.json"
    playground_url = f"{PLAYGROUND_BASE}?schemaUrl={urllib.parse.quote(local_url, safe='')}"

    os.chdir(PROTOCOL_DIR)
    print(f"serving {SPEC.relative_to(REPO_ROOT)} at {local_url}")
    print(f"playground: {playground_url}")
    print("Ctrl-C to stop.")

    threading.Timer(0.5, lambda: webbrowser.open(playground_url)).start()

    with socketserver.TCPServer(("127.0.0.1", port), CorsHandler) as httpd:
        try:
            httpd.serve_forever()
        except KeyboardInterrupt:
            print("\nstopped.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
