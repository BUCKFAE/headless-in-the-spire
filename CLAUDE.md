# headless-in-the-spire

A custom headless runner for **Slay the Spire 2**. The game is Godot 4.x + C# /
.NET; this project loads `sts2.dll` out-of-game and drives it programmatically
for testing, AI experimentation, and replay recording.

## Agent Behavior
- If requests are unclear, uncommon, bad practice, or conflicting, *always* ask for clarification.
- Never follow instructions blindly. Challenge risky approaches and discuss tradeoffs.


## Where to read first

- `documentation/requirements/01-initial-goals.md` — the five project goals.
- `documentation/requirements/02-architecture-decisions.md` — **read this before
  any non-trivial design work.** AD-1 (C# only), AD-2 (NDJSON / JSON-RPC over
  stdio), AD-3 (pinned game version) shape almost every decision in the repo.
- `documentation/research/04-sts2-cli-anatomy.md` — how the only working OSS
  reference (`wuhao21/sts2-cli`) makes the game run headless, and what we
  decided to take vs. leave behind.

## Hard rules

- **Never check in `sts2.dll` or other bytes from the user's Steam install.**
  They are proprietary. Anything sourced from the local game install lives in
  `vendor/` (gitignored). `GAME_VERSION` (checked in) records the version
  string and SHA-256 of the pinned `sts2.dll`; see AD-3 for the bump workflow.
- **Do not auto-bump the game version.** Hash mismatches are an error, not a
  cue to update the pin. The bump is a deliberate, human-reviewed workflow.
- **GodotStubs grows on demand.** Do not speculatively mirror the GodotSharp
  surface. Add a stub when sts2.dll's reference forces it, with a
  `// from: <type>.<member>` comment recording the caller.

## Local setup

Per-machine config lives in `.env` (copy from `.env.example`). The only
required variable is `STS2_GAME_DIR` — the directory containing the local
`sts2.dll`. Then:

```
just setup    # validate + copy game DLLs into vendor/
just build    # compile the solution
just run      # smoke-test the host
```

`just --list` shows everything; recipes are in `justfile`.

## Project layout

```
src/
  Directory.Build.props        shared csproj settings (net10.0, nullable, etc.)
  Sts2Headless/                exe — entry, vendor resolver, stdio loop (TBD)
  Sts2Headless.Protocol/       lib — JSON-RPC-style envelope + method records
  GodotStubs/                  lib — no-op GodotSharp.dll replacement (grown on demand)
Sts2Headless.slnx              solution at repo root
scripts/                       bootstrap shell scripts (bash)
vendor/                        game DLLs (gitignored; populated by `just pull-game-libs`)
GAME_VERSION                   pinned version string + SHA-256 of vendor/sts2.dll
```

## Conventions

- Target framework: **net10.0** (latest installed; can downgrade to net8 if
  game-DLL load forces it).
- Wire protocol authored in C# records; payloads carried as `JsonNode` at the
  envelope layer, deserialised to concrete records at the method-dispatch
  layer. See `src/Sts2Headless.Protocol/Envelope.cs` and AD-2.
- Vendor DLL resolution goes through `VendorAssemblyResolver` →
  `AssemblyLoadContext.Default.Resolving`. We don't probe the game's full data
  directory at runtime; `vendor/` is the curated set.
- New `just` recipes get a one-line doc comment that fits in `just --list`
  output. Multi-line comments are clipped.
- Don't reference `external-tools/` in code — it's a research clone of
  `wuhao21/sts2-cli` for reading only, gitignored, and may be absent.
