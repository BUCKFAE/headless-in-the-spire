#!/usr/bin/env bash
# Copy the game DLLs we depend on from a local Slay the Spire 2 install
# into ./vendor. The source path is taken from STS2_GAME_DIR (set in .env
# or the environment).
#
# This populates the pinned-version layout described in
# documentation/requirements/02-architecture-decisions.md (AD-3): the
# vendor/ directory is gitignored; the GAME_VERSION file (checked in)
# records the version string and the SHA-256 of sts2.dll for that pin.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VENDOR_DIR="$REPO_ROOT/vendor"
GAME_VERSION_FILE="$REPO_ROOT/GAME_VERSION"

# ── Inputs ─────────────────────────────────────────────────────────────

if [ -z "${STS2_GAME_DIR:-}" ]; then
    echo "STS2_GAME_DIR is not set." >&2
    echo "  Set it in .env (see .env.example) or export it in your shell." >&2
    exit 2
fi

if [ ! -d "$STS2_GAME_DIR" ]; then
    echo "STS2_GAME_DIR points at a directory that does not exist:" >&2
    echo "  $STS2_GAME_DIR" >&2
    exit 2
fi

# ── DLLs to copy ───────────────────────────────────────────────────────
# Authoritative list, mirrors external-tools/sts2-cli/setup.sh — these are
# the libraries the game ships under its data directory that we need
# alongside sts2.dll to load it.
DLLS=(
    "sts2.dll"
    "0Harmony.dll"
    "MonoMod.Backports.dll"
    "MonoMod.ILHelpers.dll"
    "SmartFormat.dll"
    "SmartFormat.ZString.dll"
    "Sentry.dll"
    "Steamworks.NET.dll"
    "System.IO.Hashing.dll"
)

# ── Locate each DLL inside the install ─────────────────────────────────
# Linux ships the DLLs at the game root; macOS hides them inside
# data_sts2_macos_*/; Steam libraries on secondary disks just change the
# parent. We try the root first, then fall back to a bounded `find`.
locate_dll() {
    local name="$1"
    if [ -f "$STS2_GAME_DIR/$name" ]; then
        echo "$STS2_GAME_DIR/$name"
        return 0
    fi
    local hit
    hit="$(find "$STS2_GAME_DIR" -maxdepth 4 -name "$name" -type f -print -quit 2>/dev/null)"
    if [ -n "$hit" ]; then
        echo "$hit"
        return 0
    fi
    return 1
}

# Resolve every DLL up front so we fail early if anything is missing —
# better than copying half the set and discovering the rest is wrong.
declare -A RESOLVED
missing=0
for dll in "${DLLS[@]}"; do
    if path="$(locate_dll "$dll")"; then
        RESOLVED["$dll"]="$path"
    else
        echo "  ✗ $dll not found anywhere under $STS2_GAME_DIR" >&2
        missing=$((missing + 1))
    fi
done

if [ "$missing" -gt 0 ]; then
    echo "" >&2
    echo "$missing required DLL(s) missing from STS2_GAME_DIR. Aborting." >&2
    echo "  STS2_GAME_DIR: $STS2_GAME_DIR" >&2
    exit 3
fi

# ── Version-pin check ──────────────────────────────────────────────────
# If GAME_VERSION exists, refuse to clobber vendor/sts2.dll with a
# different-hash copy. The version bump workflow in AD-3 (just
# check-game-compat → just test → just rerecord-snapshots) is what
# changes the pin; this script is for first-time setup and re-extraction
# of the *pinned* version on a fresh clone.
new_sha="$(sha256sum "${RESOLVED[sts2.dll]}" | awk '{print $1}')"

if [ -f "$GAME_VERSION_FILE" ]; then
    expected_sha="$(awk '/^SHA256/ {print $2}' "$GAME_VERSION_FILE" 2>/dev/null || true)"
    if [ -n "$expected_sha" ] && [ "$new_sha" != "$expected_sha" ]; then
        echo "Local sts2.dll does not match the pinned SHA-256." >&2
        echo "  expected: $expected_sha" >&2
        echo "  local:    $new_sha" >&2
        echo "  path:     ${RESOLVED[sts2.dll]}" >&2
        echo "" >&2
        echo "Your Steam copy has been updated past the pinned version." >&2
        echo "Either roll back the game (Steam → Properties → Betas) or run" >&2
        echo "the version-bump workflow (see AD-3 in" >&2
        echo "documentation/requirements/02-architecture-decisions.md)." >&2
        exit 4
    fi
fi

# ── Copy ───────────────────────────────────────────────────────────────
mkdir -p "$VENDOR_DIR"
echo "📁 Source:  $STS2_GAME_DIR"
echo "📦 Vendor:  $VENDOR_DIR"
echo ""
for dll in "${DLLS[@]}"; do
    cp "${RESOLVED[$dll]}" "$VENDOR_DIR/$dll"
    echo "  ✓ $dll"
done

# ── Write / update GAME_VERSION ────────────────────────────────────────
# If GAME_VERSION doesn't exist yet, scaffold it with the new hash and a
# placeholder version string. The placeholder makes it obvious in `git
# diff` that someone needs to fill in the real version number.
if [ ! -f "$GAME_VERSION_FILE" ]; then
    cat > "$GAME_VERSION_FILE" <<EOF
VERSION  <fill-in-from-game-credits-screen>
SHA256   $new_sha
EOF
    echo ""
    echo "ℹ Wrote $GAME_VERSION_FILE with SHA256 only — fill in the version" >&2
    echo "  string from the game's credits / About screen before committing." >&2
fi

echo ""
echo "✅ Done. ${#DLLS[@]} files in vendor/."
