# Vendor mirror — one-time setup

CI and ephemeral containers (mobile Claude, etc.) can't extract DLLs from
a local Steam install. Instead they fetch them from a private GitHub
repo that holds the same 10 DLLs as `vendor/`, **encrypted with age**.
Three independent gates protect them: a fine-grained PAT (clone), TLS
(transport), and the age private key (decrypt).

You do this once per machine that needs to *push* (i.e. yours).
Everything else — CI runs, mobile sessions — just consumes it.

## Fast path

```
just setup::init-vendor
```

The script checks dependencies (`age`, `gh`, `git`), creates the private
mirror repo, generates an age keypair, prompts you to create one PAT
(it tells you the exact URL + scopes), writes everything to `.env`,
pushes the first encrypted snapshot, and optionally sets the GitHub
Actions secrets on the main repo for you.

## What gets created

- A new **private GitHub repo** (default name `headless-in-the-spire-vendor`).
- One **fine-grained PAT** scoped only to that repo. Contents: read + write.
- One **age keypair**. Public key goes in `.env` (not secret). Private
  key goes in `.env` (secret) and in the main repo's GitHub Actions
  secrets.

## What you do once it's set up

- **Bump the game version:** after `just setup::pull-game-libs` updates
  the local pin, run `just setup::push-vendor` to re-encrypt and push.
- **New CI machine:** nothing — the GitHub Actions secrets are already
  set, the workflow installs `age` and calls `just setup::fetch-vendor`.
- **New mobile Claude session:** paste your saved env vars
  (`STS2_VENDOR_REPO`, `STS2_VENDOR_TOKEN`, `STS2_VENDOR_PRIVKEY`) into
  the container, then `just setup::fetch-vendor`.

## Recovery

- **Lost the age private key:** the mirror is now read-only ciphertext
  you can't decrypt. Generate a new keypair (`age-keygen`), re-run
  `just setup::push-vendor` (it overwrites the mirror with the new
  recipient), rotate the GitHub Actions secret.
- **PAT leaked:** revoke at <https://github.com/settings/tokens>, create
  a new one, update `.env` + the GitHub Actions secret.
- **Mirror diverged from `GAME_VERSION`:** `fetch-vendor` fails loudly
  with the expected vs. actual SHA. Run `just setup::push-vendor`
  locally to re-sync.

## Why encrypted, given the repo is already private?

Defense in depth. If the PAT ever leaks, the bytes are still ciphertext.
If the age key ever leaks, the ciphertext is still behind a private repo.
You need both to get plaintext. Cost is one extra `apt install age` step
in CI; benefit is a much higher floor on the worst-case leak.
