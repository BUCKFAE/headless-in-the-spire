# Sts2Headless.Utils

The solution's **leaf utility project**: pure, dependency-free helpers that more
than one project would otherwise re-implement. No project references, no
`sts2.dll`, no Godot — so anything (Protocol on up) can depend on it without a
cycle.

## What's here

| Type | Purpose |
|---|---|
| `Paths` | Repo-root location (`LocateRepoRoot`), the gitignored `vendor/` dir (`VendorDir`), and the pinned `sts2.dll` path (`Sts2DllPath`) + the shared "missing — run `just setup`" message. |
| `GameVersionPin` | The one parser for the checked-in `GAME_VERSION` pin (AD-3): `Read(repoRoot)` → `(Version, Sha256)`. Host ping, replay headers, and schema export all read the pin through here. |
| `FileHash` | `Sha256(path)` — lowercase hex, used to cross-check the pin against the actual `vendor/sts2.dll` bytes. |
| `SetupDir` | `CleanSetupDir(path, deleteContent)` — create-or-reset a directory, mirroring the Python `clean_setup_dir`. |

## When to add here

Add a helper when a second project is about to copy the first one's
implementation of some path / version / hashing / setup chore. Keep it pure —
if a helper needs `sts2.dll`, the engine, or wire DTOs, it belongs in
`Sts2Headless.Runtime` / `Protocol`, not here.
