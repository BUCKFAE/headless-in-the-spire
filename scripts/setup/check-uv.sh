#!/usr/bin/env bash
# Fail with a clear, copy-pasteable install hint if `uv` is not on PATH.
# The Python toolchain (Python 3.13 itself + every dev tool) is bootstrapped
# by uv; the only thing we expect the user to have pre-installed is uv.

set -euo pipefail

if command -v uv >/dev/null 2>&1; then
    exit 0
fi

cat >&2 <<'EOF'
error: `uv` is not on PATH.

The headless-in-the-spire Python toolchain is managed entirely by uv. Install
it once per machine, then re-run the just recipe:

    # Official installer (Linux / macOS / WSL):
    curl -LsSf https://astral.sh/uv/install.sh | sh

    # Or via a system package manager:
    #   Arch:   pacman -S uv
    #   macOS:  brew install uv

After install, ensure your shell has `~/.local/bin` (or wherever uv landed)
on PATH and re-run.
EOF
exit 1
