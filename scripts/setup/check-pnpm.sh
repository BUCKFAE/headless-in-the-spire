#!/usr/bin/env bash
# Fail with a clear, copy-pasteable install hint if `pnpm` is not on PATH.
# The replay viewer (tools/replay-viewer) uses pnpm; the lockfile checked in
# is pnpm-lock.yaml, so npm/yarn would diverge from the resolved tree.

set -euo pipefail

if command -v pnpm >/dev/null 2>&1; then
    exit 0
fi

cat >&2 <<'EOF'
error: `pnpm` is not on PATH.

The replay viewer under tools/replay-viewer is a pnpm workspace and ships a
pnpm-lock.yaml. Install pnpm once per machine, then re-run the just recipe:

    # Official installer (Linux / macOS / WSL):
    curl -fsSL https://get.pnpm.io/install.sh | sh -

    # Or via corepack (ships with modern Node):
    corepack enable && corepack prepare pnpm@latest --activate

    # Or via a system package manager:
    #   Arch:   pacman -S pnpm
    #   macOS:  brew install pnpm
EOF
exit 1
