#!/usr/bin/env bash
# Fetch vendor DLLs from the encrypted private mirror. Alternative entry
# point to `pull-game-libs.sh` for environments without a local Steam
# install — chiefly GitHub Actions (trusted-tier jobs) and ephemeral
# Claude containers.
#
# The mirror repo holds the same 10 DLLs that `pull-game-libs.sh` extracts
# from a local install, encrypted with age. Access has two independent
# gates: a GitHub PAT (to clone the private repo) and an age private key
# (to decrypt the ciphertext).
#
# Inputs (env):
#   STS2_VENDOR_REPO    — owner/repo, e.g. "julians/headless-in-the-spire-vendor"
#   STS2_VENDOR_TOKEN   — fine-grained PAT with read access to that repo
#   STS2_VENDOR_PRIVKEY — age identity (private key), full "AGE-SECRET-KEY-1…" line
#   STS2_VENDOR_REF     — optional; branch / tag / commit (default: main)
#
# Verifies the fetched sts2.dll against GAME_VERSION (same SHA-256 pin
# check as pull-game-libs.sh) so a mismatched mirror fails loudly.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
VENDOR_DIR="$REPO_ROOT/vendor"
GAME_VERSION_FILE="$REPO_ROOT/GAME_VERSION"

: "${STS2_VENDOR_REPO:?STS2_VENDOR_REPO is not set}"
: "${STS2_VENDOR_TOKEN:?STS2_VENDOR_TOKEN is not set}"
: "${STS2_VENDOR_PRIVKEY:?STS2_VENDOR_PRIVKEY is not set (age identity)}"
STS2_VENDOR_REF="${STS2_VENDOR_REF:-main}"

if ! command -v age >/dev/null 2>&1; then
    echo "age is not installed. Install via your package manager (e.g. 'apt install age')." >&2
    exit 2
fi

if [ -f "$VENDOR_DIR/sts2.dll" ]; then
    echo "vendor/sts2.dll already exists — refusing to clobber a local install." >&2
    echo "Remove vendor/ first if you really want to re-fetch from the mirror." >&2
    exit 2
fi

tmpdir="$(mktemp -d)"
# Write the age identity to a tempfile inside $tmpdir (cleaned up on exit).
# age's -i flag requires a file path; we never want this on disk long-term.
identity_file="$tmpdir/age.key"
umask 077
printf '%s\n' "$STS2_VENDOR_PRIVKEY" > "$identity_file"
trap 'rm -rf "$tmpdir"' EXIT

clone_url="https://x-access-token:${STS2_VENDOR_TOKEN}@github.com/${STS2_VENDOR_REPO}.git"
echo "📦 Cloning encrypted vendor mirror: ${STS2_VENDOR_REPO}@${STS2_VENDOR_REF}"
git clone --quiet --depth 1 --branch "$STS2_VENDOR_REF" "$clone_url" "$tmpdir/mirror"

mkdir -p "$VENDOR_DIR"

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

echo "🔓 Decrypting with age"
missing=0
for dll in "${DLLS[@]}"; do
    src="$tmpdir/mirror/$dll.age"
    if [ -f "$src" ]; then
        age -d -i "$identity_file" -o "$VENDOR_DIR/$dll" "$src"
        echo "  ✓ $dll"
    else
        echo "  ✗ $dll.age missing from mirror" >&2
        missing=$((missing + 1))
    fi
done

if [ "$missing" -gt 0 ]; then
    echo "$missing DLL(s) missing from the mirror — refusing to continue." >&2
    exit 3
fi

# Verify against the pinned hash.
sha256_of() {
    if command -v sha256sum >/dev/null 2>&1; then
        sha256sum "$1" | awk '{print $1}'
    else
        shasum -a 256 "$1" | awk '{print $1}'
    fi
}

if [ -f "$GAME_VERSION_FILE" ]; then
    expected="$(awk '/^SHA256/ {print $2}' "$GAME_VERSION_FILE")"
    actual="$(sha256_of "$VENDOR_DIR/sts2.dll")"
    if [ "$expected" != "$actual" ]; then
        echo "" >&2
        echo "Decrypted sts2.dll does not match GAME_VERSION pin." >&2
        echo "  expected: $expected" >&2
        echo "  mirror:   $actual" >&2
        echo "  ref:      $STS2_VENDOR_REF" >&2
        echo "" >&2
        echo "Either the mirror is behind, or GAME_VERSION was bumped" >&2
        echo "without re-pushing. Run 'just setup::push-vendor' locally to" >&2
        echo "update the mirror." >&2
        exit 4
    fi
    echo ""
    echo "✅ vendor/ populated from ${STS2_VENDOR_REPO}@${STS2_VENDOR_REF}, SHA-256 verified."
else
    echo ""
    echo "⚠ GAME_VERSION not found — skipping SHA-256 verification." >&2
fi
