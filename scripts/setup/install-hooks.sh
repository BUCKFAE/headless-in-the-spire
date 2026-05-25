#!/usr/bin/env bash
# Symlink repo-managed git hooks into .git/hooks/. Idempotent — safe to re-run.
# Hooks themselves live in scripts/git-hooks/ so they stay versioned with the
# code that depends on them (the protocol artefacts the pre-commit hook checks).

set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
src_dir="$repo_root/scripts/git-hooks"
dst_dir="$repo_root/.git/hooks"

if [[ ! -d "$src_dir" ]]; then
    echo "install-hooks: no hook sources at $src_dir" >&2
    exit 1
fi
if [[ ! -d "$dst_dir" ]]; then
    echo "install-hooks: no .git/hooks (not a git checkout?)" >&2
    exit 1
fi

shopt -s nullglob
for src in "$src_dir"/*; do
    [[ -f "$src" ]] || continue
    name="$(basename "$src")"
    chmod +x "$src"
    # Relative symlink so the hook keeps working if the repo is moved.
    ln -sf "../../scripts/git-hooks/$name" "$dst_dir/$name"
    echo "install-hooks: installed $name"
done
