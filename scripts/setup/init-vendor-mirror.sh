#!/usr/bin/env bash
# Interactive one-time bootstrap of the encrypted vendor mirror.
#
# Walks you through:
#   1. Dependency check (age, gh, git).
#   2. Confirming vendor/ is populated locally (we encrypt FROM that).
#   3. Creating the private mirror repo on GitHub (via gh).
#   4. Generating an age keypair.
#   5. Telling you exactly where to make the PAT, and reading it back.
#   6. Writing all the env vars to .env.
#   7. Running the first push-vendor to seed the mirror.
#   8. Optionally setting the GitHub Actions secrets / variables on the
#      main repo so CI works on the next push.
#
# Safe to re-run: detects existing repo, existing keypair in .env, and
# asks before overwriting.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$REPO_ROOT"

ENV_FILE="$REPO_ROOT/.env"
VENDOR_DIR="$REPO_ROOT/vendor"

# ── Pretty output helpers ──────────────────────────────────────────────
bold()  { printf '\033[1m%s\033[0m\n' "$*"; }
ok()    { printf '  \033[32m✓\033[0m %s\n' "$*"; }
warn()  { printf '  \033[33m⚠\033[0m %s\n' "$*"; }
fail()  { printf '  \033[31m✗\033[0m %s\n' "$*" >&2; }
step()  { printf '\n\033[36m▸ %s\033[0m\n' "$*"; }

confirm() {
    # confirm "Question?" — returns 0 for yes, 1 for no. Default: no.
    local prompt="$1" reply
    read -r -p "$prompt [y/N] " reply
    [[ "$reply" =~ ^[Yy]$ ]]
}

prompt_default() {
    # prompt_default "Question" "default-value" → echoes the answer.
    local prompt="$1" default="$2" reply
    read -r -p "$prompt [$default]: " reply
    echo "${reply:-$default}"
}

read_secret() {
    # Read a secret line without echoing it.
    local prompt="$1" reply
    read -r -s -p "$prompt: " reply
    echo "" >&2
    printf '%s' "$reply"
}

# ── 1. Dependencies ────────────────────────────────────────────────────
step "Checking dependencies"

missing_deps=()
for cmd in age git gh; do
    if command -v "$cmd" >/dev/null 2>&1; then
        ok "$cmd ($(command -v "$cmd"))"
    else
        fail "$cmd not found"
        missing_deps+=("$cmd")
    fi
done

if [ "${#missing_deps[@]}" -gt 0 ]; then
    echo ""
    fail "Install the missing tools and re-run:"
    for d in "${missing_deps[@]}"; do
        case "$d" in
            age) echo "    age:  apt install age   |  brew install age" >&2 ;;
            gh)  echo "    gh:   https://cli.github.com/" >&2 ;;
            git) echo "    git:  apt install git   |  brew install git" >&2 ;;
        esac
    done
    exit 2
fi

if ! gh auth status >/dev/null 2>&1; then
    fail "gh is installed but not logged in. Run 'gh auth login' first."
    exit 2
fi
ok "gh is authenticated as $(gh api user --jq .login)"

# ── 2. Local vendor/ must be populated ─────────────────────────────────
step "Checking local vendor/"

if [ ! -f "$VENDOR_DIR/sts2.dll" ]; then
    fail "vendor/sts2.dll missing. Run 'just setup::pull-game-libs' first"
    fail "(needs STS2_GAME_DIR set in .env, see .env.example)."
    exit 2
fi
ok "vendor/ is populated"

# ── 3. Mirror repo ─────────────────────────────────────────────────────
step "Mirror repository"

current_user="$(gh api user --jq .login)"
default_repo_name="headless-in-the-spire-vendor"

repo_owner="$(prompt_default "GitHub user or org for the mirror" "$current_user")"
repo_name="$(prompt_default "Mirror repo name" "$default_repo_name")"
full_repo="${repo_owner}/${repo_name}"

if gh repo view "$full_repo" >/dev/null 2>&1; then
    ok "Repo already exists: $full_repo"
else
    if confirm "Create private repo $full_repo now?"; then
        gh repo create "$full_repo" --private --description "Encrypted vendor mirror for headless-in-the-spire" >/dev/null
        ok "Created $full_repo (private)"
    else
        fail "Cannot continue without the mirror repo. Create it manually or re-run."
        exit 2
    fi
fi

# ── 4. age keypair ─────────────────────────────────────────────────────
step "age keypair"

existing_priv=""
existing_pub=""
if [ -f "$ENV_FILE" ]; then
    existing_priv="$(grep -E '^STS2_VENDOR_PRIVKEY=' "$ENV_FILE" 2>/dev/null | cut -d= -f2- | sed 's/^"//;s/"$//' || true)"
    existing_pub="$(grep -E '^STS2_VENDOR_PUBKEY=' "$ENV_FILE" 2>/dev/null | cut -d= -f2- | sed 's/^"//;s/"$//' || true)"
fi

if [ -n "$existing_priv" ] && [ -n "$existing_pub" ]; then
    ok "Found existing keypair in .env"
    if confirm "Use the existing keypair (recommended)?"; then
        age_priv="$existing_priv"
        age_pub="$existing_pub"
    else
        warn "Generating a fresh keypair will require re-running push-vendor"
        warn "and rotating the secret in GitHub Actions."
        age_priv=""
    fi
fi

if [ -z "${age_priv:-}" ]; then
    # `mktemp -u` returns a unique path WITHOUT creating the file —
    # age-keygen refuses to overwrite an existing file, so we must let
    # it create the file itself.
    keyfile="$(mktemp -u)"
    trap 'rm -f "$keyfile"' EXIT
    # age-keygen prints "Public key: …" to stderr; let it through so the
    # user gets confirmation in their terminal.
    age-keygen -o "$keyfile"
    age_priv="$(grep '^AGE-SECRET-KEY-' "$keyfile")"
    age_pub="$(grep '^# public key:' "$keyfile" | awk '{print $4}')"
    rm -f "$keyfile"
    trap - EXIT
    ok "Generated new age keypair (public: $age_pub)"
fi

# ── 5. PAT ─────────────────────────────────────────────────────────────
step "Fine-grained PAT for the mirror repo"

cat <<EOF
Open this URL in a browser:

  https://github.com/settings/personal-access-tokens/new

Fill in:
  • Token name:           headless-in-the-spire vendor mirror
  • Resource owner:       $repo_owner
  • Expiration:           up to you (90 days is a good default)
  • Repository access:    Only select repositories → $full_repo
  • Repository permissions:
      Contents:           Read and write
      Metadata:           Read-only (auto-selected)

Click "Generate token", then paste it below. It is not echoed.

EOF

existing_token=""
if [ -f "$ENV_FILE" ]; then
    existing_token="$(grep -E '^STS2_VENDOR_TOKEN=' "$ENV_FILE" 2>/dev/null | cut -d= -f2- | sed 's/^"//;s/"$//' || true)"
fi

if [ -n "$existing_token" ] && confirm "Keep existing STS2_VENDOR_TOKEN from .env?"; then
    pat="$existing_token"
    ok "Using existing PAT"
else
    pat="$(read_secret "Paste PAT")"
    if [ -z "$pat" ]; then
        fail "Empty PAT — aborting."
        exit 2
    fi
    ok "PAT received ($(printf '%s' "$pat" | wc -c | tr -d ' ') chars)"
fi

# ── 6. Write .env ──────────────────────────────────────────────────────
step "Updating .env"

if [ ! -f "$ENV_FILE" ]; then
    cp "$REPO_ROOT/.env.example" "$ENV_FILE"
    ok "Created .env from .env.example"
fi

# Strip any prior vendor-mirror lines, then append fresh ones. Keeps the
# rest of .env (STS2_GAME_DIR etc.) untouched.
tmp_env="$(mktemp)"
grep -vE '^STS2_VENDOR_(REPO|TOKEN|PUBKEY|PRIVKEY|REF)=' "$ENV_FILE" > "$tmp_env" || true
cat >> "$tmp_env" <<EOF

# ── Vendor mirror (managed by scripts/setup/init-vendor-mirror.sh) ──
STS2_VENDOR_REPO="$full_repo"
STS2_VENDOR_TOKEN="$pat"
STS2_VENDOR_PUBKEY="$age_pub"
STS2_VENDOR_PRIVKEY="$age_priv"
STS2_VENDOR_REF="main"
EOF
mv "$tmp_env" "$ENV_FILE"
chmod 600 "$ENV_FILE"
ok "Wrote vendor-mirror vars to .env (mode 600)"

# ── 7. First push ──────────────────────────────────────────────────────
step "Seeding the mirror"

if confirm "Run 'just setup::push-vendor' now to encrypt vendor/ and push?"; then
    set -a; . "$ENV_FILE"; set +a
    bash "$REPO_ROOT/scripts/setup/push-vendor-remote.sh"
else
    warn "Skipped. Run 'just setup::push-vendor' yourself when ready."
fi

# ── 8. GitHub Actions secrets on the main repo ─────────────────────────
step "GitHub Actions secrets on the main repo"

# Detect the main repo from the current checkout's origin.
main_repo=""
if main_repo="$(gh repo view --json nameWithOwner --jq .nameWithOwner 2>/dev/null)"; then
    echo "Detected main repo: $main_repo"
    if confirm "Set the secrets / variables on $main_repo via gh?"; then
        gh secret set STS2_VENDOR_TOKEN --repo "$main_repo" --body "$pat"
        ok "secret STS2_VENDOR_TOKEN"
        gh secret set STS2_VENDOR_PRIVKEY --repo "$main_repo" --body "$age_priv"
        ok "secret STS2_VENDOR_PRIVKEY"
        gh variable set STS2_VENDOR_REPO --repo "$main_repo" --body "$full_repo"
        ok "variable STS2_VENDOR_REPO"
        gh variable set STS2_VENDOR_REF --repo "$main_repo" --body "main"
        ok "variable STS2_VENDOR_REF"
    else
        warn "Skipped. Set these manually under Settings → Secrets and variables → Actions:"
        echo "    Secret    STS2_VENDOR_TOKEN     = <the PAT>" >&2
        echo "    Secret    STS2_VENDOR_PRIVKEY   = $age_priv" >&2
        echo "    Variable  STS2_VENDOR_REPO      = $full_repo" >&2
        echo "    Variable  STS2_VENDOR_REF       = main" >&2
    fi
else
    warn "Couldn't detect the main repo (gh repo view failed)."
    warn "Set the GitHub Actions secrets manually — see documentation/runbooks/vendor-mirror-setup.md."
fi

echo ""
bold "🎉 Done."
echo "Next CI run will fetch vendor automatically. On a GAME_VERSION bump,"
echo "re-run 'just setup::push-vendor' to update the mirror."
