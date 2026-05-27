---
name: sts2-identity
description: Record the loaded sts2.dll's identity in artefacts the right way. Activates whenever you're about to write a SHA-256 or version label for the loaded game into any per-run artefact (replay manifest, eval summary/config, host/ping wire response, cell result), or are about to call `GameVersionPin.Read(...).Sha256` from any code path that isn't `scripts/setup/pull-game-libs.sh`. The trap is that `GAME_VERSION` is Linux-canonical and on macOS the pinned SHA never matches the bytes actually loaded (Godot's C# pipeline emits per-arch DLLs; `STS2_SKIP_SHA_CHECK=1` is the documented setup-time bypass) — so recording the pin's SHA in a per-run artefact records bytes that did not run.
---

# Recording sts2.dll identity the right way

There is exactly one helper for this: `Sts2Headless.Utils.Sts2Identity`. Use
it from every site that writes the loaded game's identity into an artefact.

```csharp
using Sts2Headless.Utils;

var identity = Sts2Identity.Current;
//   identity.GameVersion    — pin label ("v0.103.2"), platform-independent
//   identity.Sts2DllSha256  — SHA-256 of the live vendor/sts2.dll bytes
```

`Current` is process-cached; vendor bytes don't change mid-run. Use
`Sts2Identity.From(repoRoot)` when you need to compute identity for an
arbitrary repo root (tools, tests that operate on a specific path).

## Why two sources

The two fields look symmetric but aren't:

| Field           | Source                            | Why                                                                              |
| --------------- | --------------------------------- | -------------------------------------------------------------------------------- |
| `GameVersion`   | `GAME_VERSION` pin (the label)    | A human release marker. Travels with the game release, not the byte sequence.    |
| `Sts2DllSha256` | Live hash of `vendor/sts2.dll`    | Determinism gate for replay. Must describe the bytes that actually executed.     |

On Linux the pin's recorded SHA happens to equal the live hash, so the
distinction is invisible. On macOS — Godot's C# pipeline emits per-arch
binaries (arm64 ≠ x86_64), neither matches the Linux-canonical pin — the
pin's SHA describes bytes that have never run on the developer's machine.
Recording it in a manifest is a small lie that masquerades as
determinism metadata. `Sts2Identity` fixes this by giving the SHA field a
correct, platform-honest source while keeping the label sourced from the
pin (where labels belong).

The eventual AD-3 per-platform-pin amendment will give the pin file a
shape that can record multiple SHAs. When that lands, this helper's
implementation may simplify, but its **interface stays the same** — call
sites won't need to change.

## What this skill is for

- Writing or editing code that records `Sts2DllSha256` in:
  - replay headers (`ReplayHeader`, `ReplayManifest`)
  - eval artefacts (`EvaluationSummary`, `SerialisedConfig`, `CellResult`,
    `AgentInitParams`)
  - the `host/ping` wire response
  - any new wire/artefact type that adds a SHA field
- Writing or editing code that records the game's version label alongside
  the SHA in those artefacts (use `Sts2Identity.GameVersion` for symmetry
  even though `pin.Version` would technically work — one helper, one
  call, no drift between fields).
- Reviewing diffs that touch any of the above and the author reached for
  `GameVersionPin.Read(...).Sha256` instead of the helper.

## The one remaining legitimate use of `GameVersionPin.Sha256`

`scripts/setup/pull-game-libs.sh` reads the pin's SHA to detect upstream
drift at setup time. That's the pin's *actual* job — surfacing "Steam
shipped new bytes under the same version label" so a human can run the
AD-3 bump workflow. The bypass (`STS2_SKIP_SHA_CHECK=1` in `.env`) is
scoped to that script; it does not flow into any other consumer.

If you find yourself adding a second consumer of the pin's SHA, stop and
ask: "is this setup-time bump detection?" If no, you want `Sts2Identity`.

## What this skill is NOT for

- **`Sts2Headless.SchemaExport`**: reads only the version *label* from the
  pin, not the SHA. That's fine — labels are platform-independent.
- **The pin file itself**: `GAME_VERSION` keeps its current shape and its
  current responsibilities. This skill changes how *consumers* read it,
  not the file.
- **Engine `modelIdHash` / `gitCommit`**: those come from the loaded
  `Assembly` via reflection in `ReplayHeaderFactory`. Different concept
  (engine schema / build), different home, stays where it is.

## When you find a new call site

The first new write site that adds a `Sts2DllSha256` field will be tempted
to copy-paste the closest existing pattern. If the pattern is right, it
goes through `Sts2Identity`. If the pattern is wrong (someone left
`GameVersionPin.Read(...).Sha256` somewhere this skill didn't catch),
fix both the new site and the old one in the same change — the value of
having a single helper collapses fast once a second source-of-SHA path
exists in the codebase.
