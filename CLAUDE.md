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
  stdio), AD-3 (pinned game version), AD-6 (C# is the source of behavioral
  truth) shape almost every decision in the repo.
- `documentation/testing.md` — the three-axis (Unit / Integration / End-to-end)
  test pyramid. Pick the right axis before adding a test.
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
- **Debug methods are opt-in via `--enable-debug` (AD-7).** Any wire
  method under the `debug/` namespace (`debug/give_relic`, `debug/set_hp`,
  …) is **disabled by default**. The host only serves it when started
  with the `--enable-debug` CLI flag; without it, calls return
  `WireErrorCode.DebugMethodDisabled` (-32001). When adding a new debug
  method, register it via `HostMethods.GateDebug(...)`, mark its
  `MethodCatalog` entry with `IsDebugOnly: true`, and add a positive case
  to `DebugSetHpTests`-style tests *and* a negative case to
  `DebugDisabledTests` so the gate stays a tested regression net. The
  HostSubprocess test fixture passes `--enable-debug` automatically; a
  production host must never set it. Treat any unexplained appearance of
  `--enable-debug` in process args or deployment configs as a
  security/correctness concern, not a convenience.
- **C# is the source of behavioral truth (AD-6).** Drivers, agents,
  scenarios, fixtures, replay corpora, and regression tests are authored in
  C# — `src/Sts2Headless.Agents/` for drivers / agents,
  `tests/Sts2Headless.IntegrationTests/` for single-slice scenarios,
  `tests/Sts2Headless.End2EndTests/` for multi-room arcs and replays. Do
  **not** reach for Python to author this kind of work: no Python "greedy
  agent", no Python "drive a scenario" tests, no Python "this is how the
  game should behave" assertions. Python tests live under
  `clients/python/.../tests/` and verify *parity* with the C# reference
  only — a red Python test attributes to the client or the bridge, never
  to game behavior. The cost of "I'll just write a quick pytest" is
  permanent ambiguity about who owns behavioral truth; we paid that cost
  once across the ecosystem we surveyed and won't again.

## Local setup

Per-machine config lives in `.env` (copy from `.env.example`). The only
required variable is `STS2_GAME_DIR` — the directory containing the local
`sts2.dll`. Two host tools must be on PATH: `dotnet` (the .NET SDK) and
`uv` (Python toolchain manager). uv is the only Python-side prerequisite —
it downloads its own managed CPython per `.python-version`.

```
just setup    # validate STS2 install, copy DLLs to vendor/, create uv .venv
just build    # compile the C# solution
just test     # C# unit + integration + Python suites
```

`just --list` shows everything; recipes are in `justfile`.

### Python toolchain

- **Manager:** `uv` everywhere. Don't reach for `pip`, `python -m venv`,
  `pipx`, or `poetry` — they all create state that uv doesn't track.
- **Version:** pinned to **Python 3.13** by `.python-version` (read by uv
  automatically). The matching `requires-python = ">=3.13"` is duplicated in
  both `pyproject.toml` (workspace root) and
  `clients/python/headless-in-the-spire/pyproject.toml` — bump in lockstep.
- **Workspace:** root `pyproject.toml` is a uv workspace; members live under
  `clients/python/`. A single `.venv` at the repo root serves every member.
- **Adding a dependency:** `uv add <pkg>` from inside the member directory
  for runtime deps; `uv add --dev <pkg>` at the repo root for shared dev
  tooling (datamodel-code-generator, pytest, ruff). Either way, **commit
  the resulting `uv.lock`** — reproducibility relies on it.
- **Running code:** `uv run <cmd>` from anywhere in the repo. Don't activate
  the venv manually for one-off scripts; let `uv run` do the resolution.

## Project layout

```
Directory.Build.props          shared csproj settings (net10.0, nullable, etc.)
src/
  Sts2Headless/                exe — entry, CLI/probe commands, stdio loop (TBD)
  Sts2Headless.Runtime/        lib — vendor resolver, sts2 load, sync ctx,
                                     Harmony hang patches, bootstrap walker.
                                     Everything that talks to a live sts2.dll.
  Sts2Headless.Protocol/       lib — JSON-RPC-style envelope, method records,
                                     MethodCatalog (single source of truth).
  Sts2Headless.Agents/         lib — drivers / agents that talk to a running
                                     host via ITransport (AD-6). GreedyAgent
                                     is the first one.
  Sts2Headless.SchemaExport/   exe — emits protocol/openrpc.json from Protocol
                                     records (AD-5). Run via `just export-schema`.
  GodotStubs/                  lib — no-op GodotSharp.dll replacement (grown on demand)
tests/
  Sts2Headless.UnitTests/      xUnit — host-only logic, no sts2.dll.
  Sts2Headless.IntegrationTests/  xUnit — single-slice scenarios against the
                                     real host subprocess + sts2.dll.
  Sts2Headless.End2EndTests/   xUnit — multi-room arcs / replays. See
                                     documentation/testing.md.
Sts2Headless.slnx              solution at repo root
scripts/                       bootstrap shell scripts (bash)
protocol/openrpc.json          generated wire-protocol schema (AD-5)
vendor/                        game DLLs (gitignored; populated by `just pull-game-libs`)
GAME_VERSION                   pinned version string + SHA-256 of vendor/sts2.dll
pyproject.toml                 uv workspace root (no installable package itself)
uv.lock                        resolved Python deps — committed for reproducibility
.python-version                pinned Python toolchain (read by uv)
clients/python/
  headless-in-the-spire/       wire client — generated pydantic v2 DTOs + transport
  headless-in-the-spire-agents/ algorithms / drivers on top of the wire client (AD-5)
```

## Conventions

- Target framework: **net10.0** (latest installed; can downgrade to net8 if
  game-DLL load forces it).
- Wire protocol authored in C# records; payloads carried as `JsonNode` at the
  envelope layer, deserialised to concrete records at the method-dispatch
  layer. See `src/Sts2Headless.Protocol/Envelope.cs` and AD-2.
- **Prefer enums over strings on the wire and in code.** Any field with a
  fixed set of values (room type, character, map-node type, …) gets a C#
  enum with a `JsonStringEnumConverter` and an `Unknown` sentinel. Grow the
  enum when an integration test surfaces a new value rather than widening
  the parse — see `RoomType` / `MapNodeType` in `Methods.cs` for the
  canonical pattern.
- **Integration tests must use those enums end-to-end.** Assert against
  `RoomType.MapRoom` etc., not the string `"MapRoom"`; build params from the
  DTO records (`new RunNewParams(Character: Character.Ironclad, …)`), not
  hand-written JSON. A wire rename then surfaces as a compile error in the
  tests instead of a passing-but-wrong assertion.
- Vendor DLL resolution goes through `Sts2Headless.Runtime.VendorAssemblyResolver`
  → `AssemblyLoadContext.Default.Resolving`. We don't probe the game's full
  data directory at runtime; `vendor/` is the curated set.
- AD-4 (no compile-time sts2 reference) is guarded by
  `tests/Sts2Headless.Runtime.Tests/Ad4InvariantTests.cs`; the bootstrap walk
  is locked in by `BootstrapSequenceTests.cs`. Run via `just test`.
- New `just` recipes get a one-line doc comment that fits in `just --list`
  output. Multi-line comments are clipped.
- Don't reference `external-tools/` in code — it's a research clone of
  `wuhao21/sts2-cli` for reading only, gitignored, and may be absent.

### Python rules

- **Every function and every parameter is type-annotated.** No untyped
  signatures, including private helpers and tests. Pyright runs in strict
  mode (`just typecheck-python`); ruff's `ANN` ruleset catches missing
  annotations before pyright does. Return-type annotations on
  `def test_xxx()` are exempted via ruff's per-file-ignores — pytest gives
  us the return-type contract for free.
- **No `from __future__ import annotations`.** We're on Python 3.13;
  modern syntax (`X | Y`, `list[int]`, `Self`) is native. The future import
  turns *every* annotation into a string, which silently breaks runtime
  introspection used by pydantic, dataclasses, typer, and anything calling
  `typing.get_type_hints()`. Self-references go through `typing.Self`;
  genuine forward refs use a `TYPE_CHECKING`-gated import. The generator
  passes `--disable-future-imports` so even regenerated DTOs stay clean.
- **Lint + format + typecheck before commit.** `just lint-python` (ruff
  check + format --check), `just typecheck-python` (pyright strict),
  `just test-python` (pytest). The `just test` umbrella runs all three
  plus the C# suites.
