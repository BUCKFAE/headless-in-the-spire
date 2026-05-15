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
# SlayTheSpire2.app/Contents/Resources/data_sts2_macos_<arch>/ and ships
# both arm64 and x86_64 copies; Steam libraries on secondary disks just
# change the parent. We try the root first, then the arch-matching macOS
# path, then fall back to a bounded `find`.
locate_dll() {
    local name="$1"
    local hit
    if [ -f "$STS2_GAME_DIR/$name" ]; then
        echo "$STS2_GAME_DIR/$name"
        return 0
    fi
    if [ "$(uname -s)" = "Darwin" ]; then
        local arch app
        arch="$(uname -m)"
        for app in "$STS2_GAME_DIR"/*.app; do
            [ -d "$app" ] || continue
            hit="$app/Contents/Resources/data_sts2_macos_$arch/$name"
            if [ -f "$hit" ]; then
                echo "$hit"
                return 0
            fi
        done
    fi
    hit="$(find "$STS2_GAME_DIR" -maxdepth 5 -name "$name" -type f -print -quit 2>/dev/null)"
    if [ -n "$hit" ]; then
        echo "$hit"
        return 0
    fi
    return 1
}

# Resolve every DLL up front so we fail early if anything is missing —
# better than copying half the set and discovering the rest is wrong.
# Parallel indexed arrays instead of `declare -A` for bash 3.2 (macOS).
RESOLVED_KEYS=()
RESOLVED_PATHS=()
missing=0
for dll in "${DLLS[@]}"; do
    if path="$(locate_dll "$dll")"; then
        RESOLVED_KEYS+=("$dll")
        RESOLVED_PATHS+=("$path")
    else
        echo "  ✗ $dll not found anywhere under $STS2_GAME_DIR" >&2
        missing=$((missing + 1))
    fi
done

resolved_path_for() {
    local key="$1" i
    for ((i = 0; i < ${#RESOLVED_KEYS[@]}; i++)); do
        if [ "${RESOLVED_KEYS[$i]}" = "$key" ]; then
            echo "${RESOLVED_PATHS[$i]}"
            return 0
        fi
    done
    return 1
}

if [ "$missing" -gt 0 ]; then
    echo "" >&2
    echo "$missing required DLL(s) missing from STS2_GAME_DIR. Aborting." >&2
    echo "  STS2_GAME_DIR: $STS2_GAME_DIR" >&2
    exit 3
fi

# Portable sha256 — GNU coreutils (Linux) vs. perl shasum (macOS / BSD).
sha256_of() {
    if command -v sha256sum >/dev/null 2>&1; then
        sha256sum "$1" | awk '{print $1}'
    elif command -v shasum >/dev/null 2>&1; then
        shasum -a 256 "$1" | awk '{print $1}'
    else
        echo "Neither sha256sum nor shasum is available on PATH." >&2
        return 1
    fi
}

# ── Version-pin check ──────────────────────────────────────────────────
# If GAME_VERSION exists, refuse to clobber vendor/sts2.dll with a
# different-hash copy. The version bump workflow in AD-3 (just
# check-game-compat → just test → just rerecord-snapshots) is what
# changes the pin; this script is for first-time setup and re-extraction
# of the *pinned* version on a fresh clone.
sts2_src="$(resolved_path_for sts2.dll)"
new_sha="$(sha256_of "$sts2_src")"

if [ -f "$GAME_VERSION_FILE" ]; then
    expected_sha="$(awk '/^SHA256/ {print $2}' "$GAME_VERSION_FILE" 2>/dev/null || true)"
    if [ -n "$expected_sha" ] && [ "$new_sha" != "$expected_sha" ]; then
        if [ "${STS2_SKIP_SHA_CHECK:-0}" = "1" ]; then
            # Temporary escape hatch: AD-3's single-pin scheme assumed one
            # canonical sts2.dll, but Godot's C# pipeline emits per-arch
            # binaries on macOS (arm64 ≠ x86_64) with neither matching the
            # current Linux-recorded pin. Until a per-platform pin lands
            # (AD-3 amendment), allow the user to opt out with
            # STS2_SKIP_SHA_CHECK=1 and a loud banner so the bypass cannot
            # silently rot into "we ignore the pin everywhere".
            echo "" >&2
            echo "⚠⚠⚠ STS2_SKIP_SHA_CHECK=1 — sts2.dll SHA-256 mismatch ignored ⚠⚠⚠" >&2
            echo "  expected: $expected_sha" >&2
            echo "  local:    $new_sha" >&2
            echo "  path:     $sts2_src" >&2
            echo "  This bypass is temporary; remove it once the AD-3" >&2
            echo "  per-platform pin lands." >&2
            echo "" >&2
        else
            echo "Local sts2.dll does not match the pinned SHA-256." >&2
            echo "  expected: $expected_sha" >&2
            echo "  local:    $new_sha" >&2
            echo "  path:     $sts2_src" >&2
            echo "" >&2
            echo "Your Steam copy has been updated past the pinned version," >&2
            echo "or you are on a platform whose sts2.dll bytes differ from" >&2
            echo "the pin (e.g. macOS — the Godot C# pipeline emits per-arch" >&2
            echo "binaries). Either roll back the game (Steam → Properties →" >&2
            echo "Betas), run the version-bump workflow (see AD-3 in" >&2
            echo "documentation/requirements/02-architecture-decisions.md)," >&2
            echo "or re-run with STS2_SKIP_SHA_CHECK=1 as a temporary bypass." >&2
            exit 4
        fi
    fi
fi

# ── Copy ───────────────────────────────────────────────────────────────
mkdir -p "$VENDOR_DIR"
echo "📁 Source:  $STS2_GAME_DIR"
echo "📦 Vendor:  $VENDOR_DIR"
echo ""
for dll in "${DLLS[@]}"; do
    cp "$(resolved_path_for "$dll")" "$VENDOR_DIR/$dll"
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
