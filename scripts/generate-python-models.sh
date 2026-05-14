#!/usr/bin/env bash
# Regenerate the Python client's pydantic DTOs (AD-5).
#
# Bootstraps a .venv under clients/python/headless-in-the-spire/ on first run,
# installs datamodel-code-generator, then runs the generator script.
# Idempotent — re-runs just reuse the venv.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PKG_DIR="$REPO_ROOT/clients/python/headless-in-the-spire"
VENV="$PKG_DIR/.venv"

if [[ ! -x "$VENV/bin/python" ]]; then
    echo "[generate-python-models] bootstrapping .venv at $VENV"
    python3 -m venv "$VENV"
    "$VENV/bin/python" -m pip install --upgrade pip >/dev/null
fi

if ! "$VENV/bin/python" -c "import datamodel_code_generator" 2>/dev/null; then
    echo "[generate-python-models] installing datamodel-code-generator"
    "$VENV/bin/pip" install "datamodel-code-generator[http]>=0.26" >/dev/null
fi

exec "$VENV/bin/python" "$PKG_DIR/scripts/generate_models.py" "$@"
