#!/usr/bin/env bash
# Encrypt the local vendor/ directory with age and push it to the private
# vendor mirror repo. Inverse of fetch-vendor-remote.sh.
#
# Typical use:
#   - Initial setup: run once after the first `just setup::pull-game-libs`.
#   - On a GAME_VERSION bump: re-extract DLLs locally, then run this to
#     update the mirror so CI / Claude containers see the new pin.
#
# Inputs (env):
#   STS2_VENDOR_REPO    — owner/repo, e.g. "julians/headless-in-the-spire-vendor"
#   STS2_VENDOR_TOKEN   — PAT with write access to the mirror repo
#   STS2_VENDOR_PUBKEY  — age recipient (public key), e.g. "age1abc…"
#   STS2_VENDOR_BRANCH  — optional; branch to push to (default: main)
#
# The pubkey is *not* secret — anyone holding it can only encrypt.
# Decryption requires STS2_VENDOR_PRIVKEY, which lives only in your .env
# and in the deployment secrets stores (GitHub Actions, mobile sessions).

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
VENDOR_DIR="$REPO_ROOT/vendor"
GAME_VERSION_FILE="$REPO_ROOT/GAME_VERSION"

: "${STS2_VENDOR_REPO:?STS2_VENDOR_REPO is not set}"
: "${STS2_VENDOR_TOKEN:?STS2_VENDOR_TOKEN is not set (needs write scope)}"
: "${STS2_VENDOR_PUBKEY:?STS2_VENDOR_PUBKEY is not set (age recipient, public key)}"
STS2_VENDOR_BRANCH="${STS2_VENDOR_BRANCH:-main}"

if ! command -v age >/dev/null 2>&1; then
    echo "age is not installed. Install via your package manager (e.g. 'apt install age')." >&2
    exit 2
fi

if [ ! -f "$VENDOR_DIR/sts2.dll" ]; then
    echo "vendor/sts2.dll missing — run 'just setup::pull-game-libs' first." >&2
    exit 2
fi

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

tmpdir="$(mktemp -d)"
trap 'rm -rf "$tmpdir"' EXIT

clone_url="https://x-access-token:${STS2_VENDOR_TOKEN}@github.com/${STS2_VENDOR_REPO}.git"
echo "📦 Cloning vendor mirror: ${STS2_VENDOR_REPO}@${STS2_VENDOR_BRANCH}"

# Branch may not exist yet on a fresh mirror. Try to clone the branch; if
# that fails, clone whatever HEAD is and create the branch.
if ! git clone --quiet --depth 1 --branch "$STS2_VENDOR_BRANCH" "$clone_url" "$tmpdir/mirror" 2>/dev/null; then
    git clone --quiet --depth 1 "$clone_url" "$tmpdir/mirror" 2>/dev/null || {
        # Empty repo — initialize.
        mkdir -p "$tmpdir/mirror"
        cd "$tmpdir/mirror"
        git init -q -b "$STS2_VENDOR_BRANCH"
        git remote add origin "$clone_url"
        cd - >/dev/null
    }
    cd "$tmpdir/mirror"
    git checkout -B "$STS2_VENDOR_BRANCH"
    cd - >/dev/null
fi

cd "$tmpdir/mirror"

# Clear any old ciphertext so a removed DLL doesn't linger in the mirror.
rm -f ./*.dll.age

echo "🔒 Encrypting with age (recipient: $STS2_VENDOR_PUBKEY)"
for dll in "${DLLS[@]}"; do
    age -r "$STS2_VENDOR_PUBKEY" -o "$dll.age" "$VENDOR_DIR/$dll"
    echo "  ✓ $dll.age"
done

# Also commit the GAME_VERSION pin so the mirror is self-describing —
# fetchers can sanity-check that the mirror matches the main repo's pin.
cp "$GAME_VERSION_FILE" ./GAME_VERSION

# A tiny README, plaintext, so anyone who stumbles into the mirror knows
# what they're looking at. Contains no secrets.
cat > README.md <<'EOF'
# headless-in-the-spire vendor mirror

Encrypted mirror of the `vendor/` DLLs used by
[`headless-in-the-spire`](https://github.com/julians/headless-in-the-spire).

The DLLs are proprietary Slay the Spire 2 bytes — they are stored here
only as **age-encrypted ciphertext** (`*.dll.age`). Decryption requires
the project's `STS2_VENDOR_PRIVKEY`, held in maintainer environments and
deployment secrets only.

Do not push plaintext DLLs to this repo. Use
`just setup::push-vendor` in the main repo, which encrypts before pushing.
EOF

git config user.name "vendor-mirror"
git config user.email "vendor-mirror@localhost"
git add ./*.dll.age GAME_VERSION README.md

if git diff --cached --quiet; then
    echo ""
    echo "✅ Mirror already up to date — no changes to push."
    exit 0
fi

# Commit message records the pin we just pushed, for easy log inspection.
pin_version="$(awk '/^VERSION/ {print $2}' "$GAME_VERSION_FILE")"
pin_sha="$(awk '/^SHA256/ {print $2}' "$GAME_VERSION_FILE")"
git commit -q -m "Update vendor mirror to $pin_version (sts2.dll $pin_sha)"

echo "🚀 Pushing to ${STS2_VENDOR_REPO}@${STS2_VENDOR_BRANCH}"
git push -q origin "HEAD:$STS2_VENDOR_BRANCH"

echo ""
echo "✅ Mirror updated: $pin_version ($pin_sha)"
