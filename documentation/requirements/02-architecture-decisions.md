# Architecture Decisions

This document records the foundational architectural decisions for the project,
the alternatives considered, and the reasoning behind each choice. Update it
when a decision is revisited or reversed; do not silently rewrite history.

Format is light-ADR — each decision has Status, Context, Decision, and
Consequences.

---

## AD-1 — Core language: C# only

**Status**: Accepted (2026-05-13)

**Context**

The headless runner has two natural halves:

- An *unavoidably C# / in-process* part: the Godot mod that loads inside the
  game, applies Harmony patches, reads game state via reflection, and
  dispatches actions on the main thread.
- A *flexible orchestrator* part: process management, parallel instance
  control, replay recording, the wire-protocol server.

The orchestrator could plausibly live in Python (like `wuhao21/sts2-cli`),
in C# (like `longkerdandy/STS2-Cli-Mod`), or somewhere else entirely.

Goal 4 — clean high-level API, idiomatic bindings in Python / Kotlin / etc —
*sounds* like it argues for a polyglot core, but it doesn't. The right way to
support many client languages is a typed wire protocol with generated client
bindings, not to make every internal language first-class.

**Decision**

The core is C# only. This includes the in-game mod, the headless host that
embeds the game, and the orchestrator that manages parallel instances and
records replays.

External clients (Python, Kotlin, future others) are *consumers* of the wire
protocol. They are generated from the published schema, not maintained in
parallel.

**Consequences**

- Single language, single type system, single build, single test framework
  for the entire core.
- Schemas authored once in C# (records / `JsonDerivedType` / annotated DTOs)
  flow naturally to JSON Schema → generated client bindings.
- No marshalling layer between mod and orchestrator inside the core.
- We forgo Python-native conveniences (REPL, dynamic types) for the
  orchestrator. We don't think that's load-bearing — the orchestrator's job
  is structured, not exploratory.
- If the fuzzing / replay-analysis tooling ever grows into a substantial
  Python codebase, we may revisit; for now it stays on the *consumer* side
  of the wire boundary.

---

## AD-2 — Wire protocol: NDJSON over stdio with JSON-RPC-style envelope

**Status**: Accepted (2026-05-13)

**Context**

The wire protocol is on the critical path for four goals at once: typed APIs
(1), parallel execution (3), clean bindable interface (4), and human-readable
replays (5).

Options considered, with the trade-offs that matter here:

| Option | Typed | Human-readable | Parallel-friendly | Notes |
| --- | --- | --- | --- | --- |
| **NDJSON over stdio** | via schema | yes (line-by-line) | trivially (per-process) | Each line is one message; the log *is* the replay. |
| JSON-RPC over stdio | via schema | yes | yes | Standard envelope; libraries exist. Layers naturally on NDJSON. |
| JSON over WebSocket / HTTP | via schema | yes | needs dynamic ports | Hardcoded ports antifeature for parallelism. |
| JSON over Unix socket / named pipe | via schema | yes | yes (per-instance socket path) | Like WS but file-addressable; one more thing to name. |
| gRPC (protobuf) | yes, codegen | no (binary) | needs ports | Best typing, worst readability — loses replay diffability. |
| MessagePack / CBOR | via schema | no (binary) | yes | Marginally faster JSON; readability tax. |
| Cap'n Proto / FlatBuffers | yes | no | yes | Overkill. |

Goal 5 explicitly requires replays be diffable and human-readable. That alone
rules out the binary options. Goal 3 requires zero port / socket-name
collisions across N parallel instances, which rules out anything with a
network address that has to be assigned.

**Decision**

NDJSON over stdio, with a JSON-RPC-style message envelope on top:

```
{ "id": 42, "method": "play_card", "params": { ... } }
{ "id": 42, "result": { ... } }
{ "method": "state_changed", "params": { ... } }      // notification, no id
```

- Schema authored in C# (records, sealed hierarchies, enums).
- Exported as JSON Schema as part of the build.
- Python and Kotlin client bindings generated from the JSON Schema
  (`datamodel-codegen` / `quicktype` or similar).
- `id` is mandatory on requests so concurrent in-flight calls can be
  correlated.
- Notifications (no `id`) carry async events (state-changed pushes, log
  lines, game-over).
- Each game instance is a child process; its stdio *is* its transport. Replays
  are recorded by `tee`ing the stream.

**Consequences**

- Replays are trivially loggable, greppable, diffable.
- Parallel runs are free of port / socket coordination — N processes, N
  pipes, done.
- All clients are generated; there is one schema, no hand-written wrappers.
- If we later want network access, NDJSON maps 1:1 to WebSocket text frames
  — same protocol, different envelope.
- JSON parsing overhead is non-zero; if profiling later shows it matters,
  we can introduce a binary fast-path *alongside* NDJSON without breaking
  the protocol shape.
- We do not get gRPC's automatic codegen for free; we invest in our own
  schema→bindings pipeline. That investment pays dividends in readability
  and replay tooling.

---

## AD-3 — Game version: pinned, with explicit bump workflow

**Status**: Accepted (2026-05-13)

**Context**

STS2 is in Early Access and patching roughly weekly on the beta channel,
plus periodic "major update" main-branch releases. Mod APIs and reflective
symbols churn with these patches. We have to choose between:

- Tracking the latest game version automatically and absorbing breakage as
  it comes;
- Pinning a single version per branch and managing bumps as deliberate
  events.

The first option is hostile to determinism, golden replays, and CI
stability. The second introduces a manageable maintenance cost in exchange
for a stable foundation.

**Decision**

A single game version is pinned per branch via:

- `vendor/sts2.dll` (the actual bytes — distribution / licensing strategy
  TBD; at minimum, gitignored with a checked-in extraction script).
- `GAME_VERSION` file at the repo root containing the version string and
  expected SHA-256 of the DLL.
- Replays and golden snapshots stored under
  `snapshots/<game-version>/...`. Comparisons against snapshots from a
  different version refuse to run.

Version bumps follow a three-stage compat check:

1. **Reflection-manifest diff.** Every reflective access to `sts2.dll`
   (types, fields, methods) is registered in one central manifest. On bump,
   dump the new manifest and diff. Missing / renamed / signature-changed
   targets are reported up-front. Runs in milliseconds.
2. **Compile-time check.** Anything we reference by direct symbol breaks
   the build if it disappears.
3. **Harmony-apply smoke test.** Load DLL, apply all patches, boot a no-op
   run. Catches signature changes the manifest didn't see (parameter
   types, generics) and confirms patches still attach.

Bump workflow:

1. Update `vendor/sts2.dll` and `GAME_VERSION`.
2. Run the reflection-manifest diff (stage 1 above) → fix any reported
   breakages in core.
3. `just validation::test` (fast tier) → must pass.
4. Re-record every scenario and replay snapshot under
   `snapshots/<new-version>/`. Diff is reviewed by a human and compared
   against published patch notes:
   - **Content drift** (numeric changes in HP / damage / cost / card text)
     is normally accepted as-is.
   - **Structural drift** (a field went from present to null, an enum
     gained a variant, a state shape changed) is investigated — it may
     indicate a regression in *our* serialiser, not the game.
5. Commit. The new version is the pin.

**Consequences**

- Determinism, reproducibility, and replay validity are preserved.
- Patches that don't break the API and don't change balance are zero-effort
  to absorb.
- Patches that change content cost one human review per affected snapshot.
- Patches that break reflective access are detected before any test
  output is interpreted, so we don't waste hours debugging "regressions"
  that are actually missing fields.
- Centralising reflection in one registry is a constraint on the core,
  but a useful one — it forces explicit thinking about which game internals
  we depend on.
- A snapshot-diff classifier (content drift vs. structural drift) is a
  worthwhile but secondary investment; we can do it manually until volume
  demands automation.

---

## AD-4 — Game-symbol access: reflection only, no compile-time references to sts2.dll

**Status**: Accepted (2026-05-13)

**Context**

`sts2.dll` is proprietary and gitignored (AD-3). Everything in `vendor/` is
populated at first-run by `just setup::setup` from the user's Steam install. The
host needs to call into sts2 (instantiate `Player`, register `ModelDb`
subtypes, find `Cmd.Wait` for a Harmony patch, etc.). Two ways to do that:

- **Compile-time reference.** Add `<Reference Include="vendor/sts2.dll"/>`
  in `Sts2Headless.csproj`. Game types resolve to typed symbols — clean
  call sites, IDE completion, stage-2 compat check (AD-3) works
  automatically. This is what `wuhao21/sts2-cli` does.
- **Reflection only.** Game types are never named in C# source. Every
  access goes through `Type.GetType`, `MethodInfo.Invoke`, etc., grouped
  in a curated surface that AD-3's stage-1 reflection manifest naturally
  describes.

The compile-time path is more ergonomic but has a hard cost: `dotnet
build` requires a populated `vendor/` directory, which requires a Steam
install of the game. Contributors without the game can't compile, and
CI (GitHub Actions, etc.) can't build or run unit tests without us
either checking in proprietary bytes (forbidden by AD-3) or wiring a
Steam runner into CI (expensive, fragile, licence-questionable).

**Decision**

The host accesses sts2 symbols by reflection only. `Sts2Headless.csproj`
has no `<Reference>` to anything under `vendor/`, and no `using
MegaCrit.Sts2.…` directives appear anywhere in `src/`.

Reflection access is funneled through a small set of helpers (initially
ad-hoc, hardening into the AD-3 reflection manifest as it matures) so
the surface stays inventoried.

`GodotStubs` is unaffected — it's our own typed library, referenced
normally. The constraint is only on sts2-defined types.

**Consequences**

- `dotnet build` works for anyone who clones the repo, with no Steam
  install required. CI can build and run host-only tests (anything that
  doesn't try to invoke sts2 itself) without proprietary bytes.
- Per-call-site verbosity at every reflective access. We accept this cost
  in exchange for the build-without-vendor property and as a forcing
  function for the reflection manifest.
- AD-3's stage-2 compile-time compat check does not apply to game
  symbols — they're invisible to the C# compiler. Stage 1 (the
  reflection manifest diff) becomes load-bearing as a result; it has
  to catch what the compiler would otherwise have caught.
- Source-generated typed wrappers over the manifest (a "stage 1.5") are
  a plausible future investment. They'd recover the ergonomics of the
  compile-time path without sacrificing offline-buildability: the
  generator runs on the manifest, which is a checked-in JSON file. Not
  pursued in the initial iteration.
- Tests that need to actually invoke sts2 (smoke tests for hang patches,
  end-to-end runs) require a populated `vendor/` and must be marked /
  gated so they're skipped in CI environments without it.

---

## AD-5 — Schema description format: OpenRPC

**Status**: Accepted (2026-05-14)

**Context**

AD-2 commits us to NDJSON-over-stdio with a JSON-RPC envelope, schema authored
in C# records, and client bindings generated from an exported schema. It does
not name the *format* of the exported schema. This decision picks one.

Goal 4 names two first-class binding targets — Python (for ML / RL) and
Kotlin (for our own consumers) — plus open-ended future targets. The schema
format has to:

1. Describe a JSON-RPC method catalogue, not just data shapes. Method names,
   notifications, and per-method errors are first-class concepts on our wire;
   HTTP verbs and status codes are not.
2. Have working codegen for Python and Kotlin today, or with bounded one-time
   effort.
3. Embed JSON Schema for the DTOs so the substrate stays durable even if the
   wrapper format stagnates.

The candidates with non-zero ecosystem support:

| Option | Native fit | Python codegen | Kotlin codegen | Honest about protocol |
| --- | --- | --- | --- | --- |
| **OpenRPC** | designed for JSON-RPC | `datamodel-code-generator` + dispatch glue, or `openrpcclientgenerator` (pydantic) | none actively maintained — DIY template against `open-rpc/generator` | yes |
| OpenAPI | designed for HTTP REST | mature `openapi-generator` (HTTP transport, swappable) | mature | no — protocol misrepresented as HTTP |
| Plain JSON Schema + private method index | partial — DTOs only | mature DTO codegen, hand-roll dispatch | mature DTO codegen, hand-roll dispatch | yes, but readers learn our private format |
| protobuf / gRPC | no — binary, breaks Goal 5 | n/a | n/a | no |

The OpenAPI route looks attractive at first — its codegen is the most polished
in the industry, especially for Kotlin. The cost is that OpenAPI describes
HTTP, and we don't speak HTTP. Using it as our canonical schema means:

- Every method becomes a fictitious `POST /method/name`. Anyone reading the
  schema reasonably believes they can curl the service; they can't.
- Generated clients are HTTP clients (`httpx`, `OkHttpClient`, …). We strip
  the HTTP transport and graft on our NDJSON-over-stdio adapter on every
  binding, every language, forever. The DTO codegen is the only piece that
  survives unchanged.
- Notifications and per-method JSON-RPC errors don't fit HTTP's verb / status
  model — they get described as common-error responses or out-of-band events
  documented in prose.

These costs are not one-time. They are paid every time a new contributor reads
the schema, every time a doc tool renders it, every time we add a binding. We
decline to pay rent on a permanent misrepresentation in exchange for codegen
polish.

OpenRPC fits the protocol natively. Methods, notifications, and per-method
errors are first-class in the spec; slash-namespaced names like `run/new` are
spec-legal; DTOs embed JSON Schema verbatim. The cost is on the tooling side,
not the modelling side. See
[schema-description-format.md](../research/schema-description-format.md) for
the full ecosystem and tooling survey behind this decision.

**Decision**

OpenRPC is the canonical schema description format for the wire protocol.

A single C# tool, `src/Sts2Headless.SchemaExport/`, walks the records in
`Sts2Headless.Protocol.Methods` and emits `protocol/openrpc.json`. The
artefact is checked in (so contributors without a .NET build can regenerate
language bindings) and validated against `open-rpc/meta-schema` in CI. The
emitter sits on top of .NET 10's `System.Text.Json.Schema.JsonSchemaExporter`
and adds the method-catalogue layer on top — realistic size ~300–400 LOC for
the current method set.

The wire name ↔ params/result type mapping is centralised in
`Sts2Headless.Protocol.MethodCatalog`. Both the host dispatch table
(`HostMethods.Build`) and the schema emitter read from it; the host asserts
parity at startup so the catalogue can't drift from the registered handlers.

All language bindings derive from `protocol/openrpc.json`:

- **Python**: package name **`headless-in-the-spire`** on PyPI (import
  `headless_in_the_spire`). DTOs emitted by `datamodel-code-generator`
  targeting **pydantic v2**; method dispatch wraps the subprocess transport
  via an in-repo template. Lives at `clients/python/headless-in-the-spire/`.
- **Python (agents)**: separate sibling package
  **`headless-in-the-spire-agents`** at
  `clients/python/headless-in-the-spire-agents/`. Depends on the client
  package; carries algorithm dependencies (numpy / torch / etc.) so the
  thin client doesn't. Independent release cadence: algorithm churn does
  not force a new wire client.
- **Kotlin**: an in-repo template component for `open-rpc/generator`,
  written once. Likely the first OpenRPC Kotlin generator in the ecosystem;
  upstream contribution if the template lands cleanly.
- **Future languages**: prefer off-the-shelf `open-rpc/generator` components
  (TypeScript and Rust ship in the box) before hand-rolling.

OpenAPI is **not** dual-published. Anyone who wants OpenAPI can derive it
from `openrpc.json` themselves; we do not bake the protocol misrepresentation
into our canonical contract.

**Python toolchain pin.** The Python clients are managed as a single
[`uv`](https://docs.astral.sh/uv/) workspace rooted at the repo's
`pyproject.toml`, with members under `clients/python/`. Python itself is
pinned to **3.13** via `.python-version` (uv downloads a managed CPython
if the host lacks one), and `requires-python = ">=3.13"` is mirrored in
both the workspace root and each member. A single `.venv/` at the repo
root serves every member; `uv.lock` is committed for reproducible
installs. `just setup::setup` runs `uv sync --all-packages`; `just build::generate-python`
and `just validation::test-python` go through `uv run`. We pick uv over pip/poetry/pipx
because (a) it manages the Python toolchain itself, removing a class of
"works on my machine" failures; (b) workspace support is first-class, so
the agents package can drop in next to the wire client without bespoke
plumbing; (c) it's the fastest of the modern options and is what the
broader Python tooling ecosystem has converged on.

**Consequences**

- The schema artefact accurately describes the wire protocol. No HTTP
  fiction; nobody reading the docs is misled about what they're talking to.
- One source of truth (`Methods.cs` records) → one canonical artefact
  (`openrpc.json`) → N generated bindings. Methods or DTOs added outside
  `Methods.cs` cannot enter the protocol by construction.
- The `Unknown` sentinel pattern (every wire enum carries a sentinel; the
  host emits it for variants the schema hasn't catalogued, so clients keep
  parsing across game patches — see Goal 1) is **not expressible** in
  OpenRPC or any other schema we evaluated. Each such enum carries a
  `description` note documenting the contract, and generated clients must
  not perform exhaustive matches on these enums.
- Slash-namespaced method names (`run/new`, `debug/give_relic`) are
  spec-legal but generators sanitise differently per language (`/` becomes
  `_`, or nested namespaces). The first generated client per language is
  pinned via an integration test so a generator behavioural change can't
  silently rename our public API.
- We carry the maintenance of one C# emitter (~300–400 LOC) and one
  per-language dispatch template. Writing the Kotlin generator component is
  a multi-day one-time investment; once written, it sits in the repo and
  runs on every protocol bump.
- If OpenRPC the spec ever stagnates, the embedded JSON Schema for the DTOs
  survives a format migration trivially; only the thin method-catalogue
  wrapper needs replacement. We are not betting the project on the format's
  long-term health.
- We are the first STS project (1 or 2) to ship a formal protocol
  description — the ecosystem survey found none. There is no de-facto
  community schema to align with and no compatibility pressure either way.

---

## AD-6 — Behavioral source of truth: C# only; clients verify parity

**Status**: Accepted (2026-05-14)

**Context**

AD-1 made the *core* C#-only — the in-game mod, the headless host, and the
orchestrator that drives `sts2.dll`. External clients (Python, Kotlin, …) are
*consumers* generated from the wire schema (AD-5).

That decision covered the production wire layer cleanly, but left the *test*
layer ambiguous. Tests, drivers, scenarios, agents, and replays are all
things that could plausibly live in *either* the C# core or in the Python
client tree:

- A "greedy agent that drives Act 1 to the boss" could be a C# test fixture
  or a Python script.
- A "fixture run from `run/new` to a particular game state" could be authored
  in either language.
- A regression test for "play this card does X" could be a C# xUnit test
  or a `pytest` against the wire.

If both are allowed, "what the game is supposed to do" has two co-authors.
When the C# suite and the Python suite disagree, neither is canonical; we
spend cycles arbitrating. Worse, behavioral truth slowly drifts into
whichever side is easier to write at the moment, and the canonical answer
to "does this commit regress combat?" depends on whose tests ran.

The same logic that justified AD-1 for the runtime applies to the test
estate. Letting Python tests author canonical scenarios silently makes
Python a producer of behavioral contracts — exactly the polyglot core
AD-1 rejected.

**Decision**

C# is the single source of behavioral truth. Concretely:

- **Drivers, agents, scenarios, fixtures, and replay corpora are authored
  in C#.** Drivers and agents live in `src/Sts2Headless.Agents/`; the
  scenarios that exercise them live in `tests/Sts2Headless.End2EndTests/`
  (multi-room arcs) and `tests/Sts2Headless.IntegrationTests/`
  (single-slice scenarios). See [testing.md](../testing.md) for the
  three-axis test split.
- **Regression tests that assert "the game should behave like X" are C#
  tests** (xUnit, against a real host subprocess). A red C# test is a
  real regression; its meaning does not depend on the Python tree.
- **Python client tests do exactly one thing: verify parity.** Given the
  same wire scenario, the Python client must produce the same outcome
  (deserialised DTOs, decoded events, …) as the C# reference. A red
  Python test attributes to the client or the bridge, *never* to the
  game.
- **Python never authors canonical scenarios.** A useful Python script
  that happens to drive a scenario can live in the agents package as a
  user tool, but it is not part of the regression net and is not
  consulted to answer "what is the game supposed to do here?".

This applies recursively to any future client (Kotlin, TS, Rust): each is
a parity consumer of the C#-authored canon, not a co-author.

**Consequences**

- Bug attribution is mechanical. C# red → game / host regression. Python
  red while C# green → client / bridge regression. There is no third case.
- The Python tree is allowed to be smaller. It needs the parity tests and
  the DTO codegen, not a parallel scenario corpus.
- Future replay corpora are C# artefacts. A "replay" is recorded by the
  C# host, asserted by a C# end-to-end test, and re-played by Python
  clients only as a parity check.
- Adding a new client language costs one parity-test scaffold, not a new
  scenario authoring story.
- We forgo the ergonomics of "just write a quick pytest for this" as a
  regression mechanism. Quick pytests still work as ad-hoc tools for the
  engineer writing them; they just don't enter the net.
- This decision relies on the C# suite being fast and ergonomic enough to
  absorb all scenario authoring. If that becomes false, we revisit — the
  answer is to fix the C# suite, not to relax this AD.

---

## AD-7 — Debug methods are opt-in via `--enable-debug`

**Status**: Accepted (2026-05-14)

**Context**

The wire protocol includes test affordances that bypass normal game
mechanics: `debug/give_relic` grants a relic without an in-game source
event, `debug/set_hp` writes the engine's HP backing field directly. These
are load-bearing for end-to-end tests (e.g. healing the greedy agent
between rooms so it can reach the act boss) but actively destructive to
authoritative state — a run where `debug/*` has been called is no longer
a faithful replay of the underlying game.

The first cuts of these methods were registered in the host's dispatch
table unconditionally. That posture has two failure modes:

1. **Accidental production use.** A client of the wire that copies an
   example from a test (or fat-fingers a method name into a script)
   silently mutates state in a non-game-authoritative way. Bugs surface
   late, far from the cheat.
2. **Replay corruption.** A replay file recorded with debug calls
   intermixed looks identical to a clean replay; downstream analysis that
   trusts the stream draws conclusions from a poisoned run without
   knowing.

Both fail silently in the existing AD-2 wire setup, and neither is
recoverable after the fact. The AD-6 "behavioral truth lives in C#"
posture relies on the wire being a faithful narrative of the game; debug
calls being available by default puts that property on a soft footing.

**Decision**

Debug methods are gated by an explicit `--enable-debug` flag on the host
process. With the flag absent — the default in every production posture
— every `debug/*` call returns `WireErrorCode.DebugMethodDisabled`
(-32001) regardless of params; the dispatch table still registers the
method handler (so `MethodCatalog.AssertParity` stays valid) but the
handler refuses to run.

Operational rules:

- **The flag is a CLI argument, not an env var.** It surfaces in every
  process listing (`ps`, container logs, systemd unit files) so an
  operator cannot accidentally enable debug without it being visible.
- **The flag is logged to stderr when the host starts with it.** A
  conspicuous banner — `"sts2-headless: debug methods ENABLED via
  --enable-debug (development/test only — never use in production)"` —
  ensures that any captured log shows the capability was on.
- **The integration-test fixture (`HostSubprocess`) passes the flag
  unconditionally.** Tests are the canonical "I want debug methods"
  context, and every other test would have to pass the flag explicitly
  if the fixture didn't. The negative-side fixture (`NoDebugHost` in
  `DebugDisabledTests`) deliberately omits it to pin the gate behaviour
  from the other side; the gate test is the regression net.
- **The wire schema marks debug methods with `x-debugOnly: true`.** This
  is documentation, not enforcement — it lets generated clients
  segregate, hide, or label debug methods, and lets schema-aware tooling
  flag a replay containing debug calls. The host gate is what actually
  refuses calls.
- **The error code is distinct from MethodNotFound and InternalError.**
  Clients can branch on `DebugMethodDisabled` specifically. A
  `MethodNotFound` (-32601) reply would imply the method doesn't exist,
  hiding the policy decision; an `InternalError` (-32603) would suggest
  a bug. The custom code (-32001) makes the gate an observable, named
  policy.

The flag is intentionally per-method-class, not per-method. Granular
per-method opt-in adds operational surface area without changing the
threat model (an operator who enables `--enable-debug` to use one debug
method has already crossed the "I am running a non-production host"
boundary; enabling another method behind it is no additional risk).

**Consequences**

- A production host is debug-locked by construction. Forgetting to
  remove a debug call from client code surfaces immediately as a typed
  wire error rather than as silently-corrupted state.
- Replays produced by a debug-enabled host can be detected by any
  consumer that watches for `debug/*` request lines; the wire itself
  doesn't add new framing, but the namespace is reserved and visible.
- Every debug method added in the future inherits the gate by default —
  the registration helper (`GateDebug` in `HostMethods`) wraps every
  `debug/*` handler. Adding a debug method without going through this
  helper is the failure mode the unit + integration-suite parity tests
  (`DebugDisabledTests.NonDebugMethod_StillWorks_WithoutEnableDebugFlag`)
  exist to catch.
- We accept the small ergonomic cost of one CLI flag for every
  development / CI invocation — `just validation::dotnet::test-integration` passes it via
  the fixture; manual `dotnet run` invocations against the host must add
  it. The cost is a one-time learning hit for new contributors and
  measurably zero for automation.
- This AD applies recursively: any future namespace whose semantics are
  "this should never run in production" (e.g. a hypothetical `cheat/`
  or `fuzz/` family) inherits the same posture, with its own gate flag
  and the same error-code discipline.

## AD-8 — Replay artefacts: adopt the game's `.mcr` + `.run` verbatim

**Status**: Accepted (2026-05-16)

**Context**

[Goal 5](../requirements/01-initial-goals.md) calls for replays as
first-class artefacts — seed + version + decisions + snapshots, diffable,
reproducible, persistent. The initial research note
([replay-recording-and-viewing.md](../research/replay-recording-and-viewing.md),
2026-05-14) sketched an answer based on `tee`-ing our AD-2 NDJSON stdio
into a `.ndjson` replay file and authoring header / snapshot-index records
in our own shape. That note assumed we'd be inventing the canonical replay
format ourselves.

Probing `vendor/sts2.dll` and `vendor/sample-saves/` on 2026-05-16
surfaced a different reality: **the game already ships two complete,
mature replay formats**, used internally for multiplayer desync detection
and the in-game "View Run History" screen.

- **`.mcr`** — `MegaCrit.Sts2.Core.Multiplayer.Replay.CombatReplay`,
  binary, one file per combat. Authored by `CombatReplayWriter`, which
  subscribes to `ActionQueueSet.ActionEnqueued`, `ActionResumed`,
  `PlayerChoiceSynchronizer.PlayerChoiceReceived`, and
  `ChecksumTracker.ChecksumGenerated`. Header pins `version`, `gitCommit`,
  `modelIdHash` (`ModelIdSerializationCache.Hash` — 1357847701 at
  v0.103.2); body is the serialised initial `RunState`, the choice / hook
  / action id high-water marks, the event stream
  (`CombatReplayEventType.{GameAction, HookAction, ResumeAction,
  PlayerChoice}`), and **per-event `NetFullCombatState` checksums**. The
  retail loader (`NMultiplayerTest.RunReplay`) drives those events back
  through the live engine to reproduce the combat pixel-for-pixel,
  verifying checksums as it goes (`CheckAgainstReplayChecksum`).
- **`.run`** — `RunHistory` JSON, one file per run, `schema_version: 9`
  at v0.103.2. Authored by `SaveManager.SaveRun`. Floor-level audit
  trail: `acts`, `ascension`, `build_id`, `game_mode`, `seed`,
  `start_time`, `was_abandoned`, `win`, `killed_by_encounter/event`,
  `modifiers`, `platform_type`, plus a rich `map_point_history[][]` (per
  map node: `rooms[].room_type / model_id / turns_taken`, `player_stats[]`
  with `ancient_choice / cards_gained / cards_transformed / cards_removed
  / event_choices / relic_choices / gold_* / hp_* / max_hp_*` …) and a
  final `players[].deck / relics / potions / badges` snapshot. It is what
  the in-game History screen renders.

The two are not interchangeable: `.mcr` is *frame-level deterministic
combat replay*; `.run` is *per-floor decision summary*. Both are needed
to reconstruct a full run; neither alone is sufficient. Together they are
materially richer than anything we could have authored in NDJSON, because
they include semantic events (card enchant / transform / ancient choice)
that our wire protocol only surfaces indirectly via state diffs.

Three problems with the original "author our own NDJSON" plan, now that
the alternative is visible:

1. **Schema split.** Maintaining a parallel NDJSON-of-decisions on top of
   game formats that already capture the same information means two
   schemas to bump on every game pin. AD-3's three-stage compat check
   would need a fourth: "our replay schema still maps to the game's
   record shape." That bookkeeping pays no dividend — every consumer
   benefits more from a typed mirror of the canonical format than from a
   parallel one.
2. **No free determinism canary.** `CombatReplayWriter` records
   per-event checksums of `NetFullCombatState`. A re-executor that drives
   those events back through our headless host can compare checksums and
   pinpoint the first divergent action. We do not have to design that
   mechanism; we have to *adopt* it. An NDJSON replay would need its own
   diff strategy invented from scratch.
3. **No path to pixel-accurate viewing.** A `.mcr` loaded by the retail
   game replays inside the real `NRun` scene with actual cards / animations
   / sound. There is no realistic NDJSON replacement for that capability.
   Any "watchable replay" goal degrades to a JSON viewer if we don't
   adopt the game's format.

**Decision**

The canonical replay artefacts for this project ARE the game's `.mcr` +
`.run` formats, adopted verbatim. We are not introducing a parallel
format.

Concretely:

- **One `.mcr` per combat** written by `CombatReplayWriter.WriteReplay`,
  triggered from our recording layer via a Harmony post-hook on
  combat-end. The game's writer is enabled by default
  (`CombatReplayWriter.IsEnabled = !TestMode.IsOn`); we own the policy
  explicitly so a version bump can't silently turn it off.
- **One `.run` per run** written by the game's `SaveManager.SaveRun`
  path. Our headless host sets `SetUpNewSinglePlayer(state,
  shouldSave: true)` so `.run` emission is the default for any recorded
  run.
- **One additional file we author** — `manifest.json` — ties them
  together. Header fields: `game_version`, `sts2_dll_sha256` (from
  `GAME_VERSION`), `model_id_hash`, `git_commit`, `runhistory_schema_version`,
  `seed`, `character`, `ascension`, `modifiers`, `start_time`,
  `protocol_version`. Index: per-combat entry with `mcr_path`,
  `room_coordinate` (act + floor), `encounter_id`, `outcome`, action
  count, checksum count. The manifest is the only file in the layout we
  own; everything else is the game's bytes.
- **Wire protocol surfaces a typed `run/history` method.** The `.run`
  JSON schema (schema_version 9 at the v0.103.2 pin) is mirrored as
  C# records in `Sts2Headless.Protocol/Methods/Methods.cs` with the same
  enum-codec discipline used for `RoomType` / `MapNodeType` / `CardId`
  etc. (`Unknown` sentinels, `JsonStringEnumConverter`). Clients see the
  artefact through schema, not via hand-rolled JSON parsing. AD-5
  (OpenRPC export) carries the contract; AD-6 (C# is behavioural truth)
  keeps the enums grounded.
- **Wire NDJSON stays the live protocol, not a replay format.** AD-2's
  per-line envelopes remain the runtime channel between clients and the
  host. `tee`'d stdio captures are useful for protocol-level debugging
  and golden-replay regression of the *wire*, but the canonical replay
  of the *game* is the `.mcr` + `.run` + `manifest.json` triple.
- **Determinism canary** uses Mega Crit's own checksums. The in-process
  re-executor (`Sts2Headless.Replay`) loads a `.mcr`, calls
  `RunManager.Instance.SetUpReplay(state, replay)`, fast-forwards the
  id counters, drives events through
  `ActionQueueSet.EnqueueWithoutSynchronizing` on our
  `InlineSynchronizationContext`, and compares `NetFullCombatState`
  checksums at each `ReplayChecksumData` point. First-divergence
  reporting is the failure artefact. The seed=42 corpus already in
  `End2EndTests` runs this every CI; hard failure on the corpus, info
  signal elsewhere.
- **Persistence layout** mirrors AD-3 `snapshots/<game-version>/`:

  ```
  replays/<bucket>/<game-version>/<run-id>/
      manifest.json                     (authored by us)
      run.json                          (game's RunHistory writer)
      combats/
          act1-floor3-monster.mcr       (one per combat)
          act1-floor7-elite.mcr
          …
  ```

  Single gitignored root (`replays/`), one bucket per kind:
  `replays/manual/` is the default (`ReplayLayout.DefaultRootRelative`)
  for ad-hoc / MCP / `record-all` runs; `replays/sample/` is the demo
  data `just runner::record-sample-replay` produces and the
  replay-viewer's tests point at; `replays/eval-harness/<eval-id>/` is
  the orchestrator (AD-9) output. Bytes derive from `vendor/sts2.dll`
  so the whole tree is gitignored. Tests opt into a `--replay-out=<path>`
  (or `STS2_REPLAY_OUT=<path>`) for fixture-controlled locations.

- **Cross-version posture matches AD-3.** A `.mcr` recorded against
  v0.103.2 may not be re-executed against v0.103.3 — the retail
  loader's `modelIdHash` check is exactly the gate we want, and our
  in-process replayer enforces it identically. Viewing across versions
  is the retail game's problem; for in-process re-execution, version
  mismatch is a hard refuse, not a warning.

**Consequences**

- **Zero schema drift from the game.** When Mega Crit bumps
  `RunHistory.schema_version` from 9 to 10, our typed mirror bumps with
  it; the change surfaces at codegen time in
  `Sts2Headless.Protocol/Methods/Methods.cs` rather than as a silent
  serialisation breakage. The same applies to `.mcr`'s binary layout —
  if `CombatReplay.Serialize` changes, our reader breaks loudly at the
  next pin bump, which is exactly the AD-3 workflow.
- **Goal 5 lands without inventing a replay system.** Seed, version,
  decisions, snapshots, reproducibility, diffability, persistence — all
  already present in the game's formats. We add only the manifest and
  the typed wire mirror.
- **Determinism is the game's discipline, not ours.** We do not maintain
  a separate canonical-state computation, hash, or diff. If our
  replayer's checksums diverge from the recorded ones, the bug is in
  *our* host's deviation from the engine — exactly where AD-6 says
  behavioural truth lives.
- **Pixel-accurate replay viewing is unlocked but out of scope.** A
  downstream Godot mod can wrap `NMultiplayerTest.LoadReplay` to render
  a recorded `.mcr` inside the retail game's `NRun` scene. That mod is
  not in this repo; it depends on the recording substrate this AD
  establishes but is otherwise independent. Designing for it means
  keeping `.mcr` byte-compatibility with retail — which is automatic
  given we don't author the bytes.
- **No parallel "summary" or "compressed" replay format.** A run on
  disk is one directory of game bytes plus our manifest. Tools that
  want compression wrap the directory (`tar | zstd`); they do not
  re-encode the contents. This keeps "is this file the canonical
  replay or a derivative?" unambiguous.
- **Privacy inherits the game's own discipline.**
  `CombatReplay.Anonymized()` strips player ids via
  `IdAnonymizer.Anonymize`; the manifest adds no new identifying
  tokens (`seed`, `character`, `ascension` are gameplay metadata, not
  user identity). A replay safe to share between Mega Crit's
  multiplayer peers is safe to share between our test fixtures.
- **The runtime-recording-vs-on-demand decision is bypassed.** The
  game writes `.run` automatically when `shouldSave: true`, and
  `CombatReplayWriter` records every combat in-memory; the only
  trigger we own is `WriteReplay(stopRecording: false)` at combat end.
  No new policy surface.
- **Cross-version replay viewing is the retail game's problem, not
  ours.** A replay recorded at v0.103.2 may or may not load in
  v0.103.3 — the retail loader's existing model-id gate decides, and
  the user sees the same warning a Mega Crit playtester sees. We do
  not add a viewer-side compatibility shim.

The supersession of the 2026-05-14 research note's "build option B
first, spike option C2" recommendation is explicit: that note assumed
we'd be choosing between authoring NDJSON or building a viewer; we are
doing neither now. The recording substrate goes first; viewing is a
downstream mod, not a viewer we build.

---

## AD-9 — Evaluation harness: project layout, wire dialect, and config shape

**Status**: Accepted (2026-05-26)

**Context**

[04-evaluation-harness.md](./04-evaluation-harness.md) is
the *what* — FR-1..FR-12, NFR-1..NFR-4, the language split (C#
orchestrator + Python leaderboard), and the eight resolved foundational
choices. It deliberately deferred *how* to a follow-up ADR. Between the
spec landing and this ADR being written, the call-site ergonomics were
validated through a four-document design-by-example exercise under
`documentation/evaluation-harness/` (an eval Program.cs in four
variants, the AgentManifest hierarchy, the result-tree shape). Nothing
in that folder builds; the goal was to feel the call site before
committing to the bytes. Outcomes from that exercise that this ADR
incorporates verbatim:

- **No JSON manifest files for agents.** Every agent is a typed C#
  class — either a `BundledAgent` subclass (for in-repo agents,
  spawned through a shared AgentRunner exe) or an `AgentManifest`
  subclass (for everything else). The "smart enum" `BuiltinAgents`
  static class is the registry; adding an agent is one line plus a
  manifest class.
- **`BundledAgent.CreateAgent()` is hand-written**, not generic
  `where TAgent : IAgent, new()`. The non-trivial constructors on
  `IroncladAgent` (five injected policies) and the future RL-backed
  agents need a hand-wiring hook; this also unlocks "variants of the
  same agent class" as separate ranked rows on the leaderboard
  (`IroncladManifest` vs `IroncladConservativeManifest`).
- **The agent dialect is the mirror of the host dialect over stdio.**
  Same NDJSON-over-stdio envelope as AD-2; same JSON-RPC reserved
  codes; same `MethodCatalog` discipline; same AssertParity check at
  startup. Stateful by FR-2: one process per cell, kept alive across
  all `agent/decide` calls for the cell so a planner can cache.
- **Defaults are tuned data-first against existing measurements**, not
  invented from scratch. `AgentDriver.DefaultMaxSteps = 4000` and
  `StallDetector.DefaultMaxIdentical = 8` carry over verbatim;
  `ParallelHostThroughputBenchmark` and `BeatGameOnSeed42Tests` are
  the calibration anchors for per-cell wall-clock and worker cap.
- **Replay capture rides AD-8 unchanged.** The harness sets
  `STS2_REPLAY_OUT` and `STS2_REPLAY_AGENT` per cell before spawning
  the host; everything under `cells/<agent>/<seed>/` other than
  `cell.json` is the engine's own writers. The harness owns the four
  top-level files (`config.json`, `summary.{md,json}`, `runs.jsonl`)
  plus `cell.json` per cell, and nothing else.

The decisions below pin the surfaces this work needs in order to
implement; they do not introduce any concept the spec doesn't already
promise.

**Decision**

### Project boundaries

Three new C# projects and one Python workspace member. Each justified
by a real boundary, not speculative tidiness:

- **`src/Sts2Headless.Eval/`** — the orchestrator library.
  `EvaluationHarness.RunAsync(config)`, the `AgentManifest` /
  `BundledAgent` base classes, the `IScoringFunction` interface and
  default `LexSortScoring`, the `EvaluationHarnessConfig` record,
  `HarnessBudgets`, `OutputLayout`, `SeedBank` / `SeedBanks`,
  `CellResult`, `AgentAggregates`, `AgentRanking`, `CellTerminus`, the
  agent-dialect DTOs (`AgentInitParams` / `AgentInitResult` /
  `AgentDecideParams` / `AgentDecideResult` / `AgentTeardownResult`)
  and `AgentMethodCatalog`. References `Sts2Headless.Agents`
  (for `HostPool`, `AgentDriver`, `IAgent`, `ITransport`, `AgentAction`,
  `StallDetector`), `Sts2Headless.Protocol` (for `RunStateResult`,
  `Character`, `ModifierId`, `MethodEntry`, `WireErrorCode`,
  `MethodCatalog.AssertParity`), and `Sts2Headless.Replay` (for
  `ReplayLayout` constants and `ReplayManifest` field shapes).
- **`src/Sts2Headless.Eval.Manifests/`** — concrete `BundledAgent`
  subclasses (`GreedyManifest`, `IroncladManifest`,
  `IroncladConservativeManifest`, `RandomManifest`, `AttackManifest`,
  `BlockManifest`, `EagerEliteManifest`) and the `BuiltinAgents`
  static registry. Lives in its own assembly because the
  `Sts2Headless.AgentRunner` exe needs to resolve manifest types by
  FQN at startup *without* dragging in `Sts2Headless.Eval`'s
  orchestrator surface (`HostPool`, `EvaluationHarness`, etc.) —
  manifests are tiny, the orchestrator is not. References
  `Sts2Headless.Eval` (for the base classes) and whichever agent
  libraries the manifests wrap (`Sts2Headless.Agents`,
  `Sts2Headless.BattleAgent`).
- **`src/Sts2Headless.AgentRunner/`** — exe. Generic stdio agent host:
  takes `--manifest <FQN>` on the command line, reflects the
  parameterless constructor on the manifest type, calls
  `manifest.CreateAgent()` to materialise the `IAgent`, and runs the
  `agent/init` → `agent/decide`\* → `agent/teardown` loop against
  stdin / stdout. References `Sts2Headless.Eval` (for the dialect
  DTOs and `AgentMethodCatalog`) and `Sts2Headless.Eval.Manifests`
  (so the FQNs in `BundledAgent.Command` are resolvable). Does **not**
  reference `Sts2Headless.Runtime` or any sts2-touching code — it
  speaks the agent dialect, not the host dialect, and never loads
  `sts2.dll`. This is what keeps the runner lightweight, fast to
  spawn, and runnable on a workstation without a Steam install (only
  the *host* needs `vendor/sts2.dll`; an agent process just needs
  the wire DTOs).
- **`tests/Sts2Headless.EvalTests/`** — xUnit. End-to-end smoke
  scenarios: a 2-agent × 3-seed matrix completes in < 60s and emits
  the full `config.json` + `summary.{md,json}` + `runs.jsonl` +
  per-cell directory; an intentionally-crashing test agent surfaces
  as `terminus: AgentCrash` without taking down sibling cells; a
  scoring-function plug-in replaces the default ranking;
  `cells/<agent>/<seed>/cell.json` round-trips through serialisation.
  Lives under `tests/` next to the other xUnit projects, gated on the
  same `[Trait("Category", "Integration")]` discipline so it's
  included in `just validation::test` only when `vendor/sts2.dll` is
  present.

Examples (the user-facing console exes that call
`EvaluationHarness.RunAsync`) land in a new top-level **`examples/`**
folder, also added to `Sts2Headless.slnx`:

- `examples/EvalSmoke/` — the 5-seed × Greedy minimal eval.
- `examples/EvalReference/` — the 50-seed × in-repo agents eval.
- `examples/EvalDeep/` — the 500-seed × everyone-opted-in eval,
  plus a sample custom `WeightedScoring : IScoringFunction`.

`examples/` is added to the solution but kept separate from
`src/` so it doesn't pollute `dotnet pack` outputs; the convention
matches `tests/` (tested against, not shipped).

Python side, exactly one new workspace member:

- **`clients/python/headless-in-the-spire-leaderboard/`** — reads
  `summary.json` + `runs.jsonl`, renders plots (matplotlib /
  plotly), generates a static HTML site (jinja2), publishes to
  gh-pages. Depends on `headless-in-the-spire` (the wire client,
  transitively for the `Character` / `ModifierId` enum
  serialisations) and on `headless-in-the-spire-utils`. Independent
  release cadence from the wire client. **Does not** depend on
  `headless-in-the-spire-agents` — the leaderboard is downstream of
  the eval JSON, not of the agent surface, and the agents package
  pulls in numpy / RL deps that the leaderboard does not need.

### `AgentManifest` hierarchy — canonical contract

The sketches' shape is adopted verbatim. Reproduced here so the AD is
self-contained and a future reader doesn't need to chase the
documentation/evaluation-harness sketches:

```csharp
// src/Sts2Headless.Eval/Agents/AgentManifest.cs
namespace Sts2Headless.Eval;

public abstract class AgentManifest
{
    // Required.
    public abstract string Name    { get; }
    public abstract string Version { get; }
    public abstract IReadOnlyList<string> Command { get; }

    // Optional with sensible defaults.
    public virtual string?  Language    => null;       // "csharp" | "python" | …
    public virtual string?  Cwd         => null;       // null ⇒ repo root
    public virtual string?  Description => null;
    public virtual IReadOnlyDictionary<string, string>? Env => null;

    public virtual IReadOnlyList<Character>   SupportedCharacters => [Character.Ironclad];
    public virtual IReadOnlyList<int>         SupportedAscensions => [0];
    public virtual IReadOnlyList<ModifierId>? SupportedModifiers  => null;

    // Per-agent budget overrides. null ⇒ inherit EvaluationHarnessConfig.Budgets.
    public virtual HarnessBudgets? Budgets => null;
}

public abstract class BundledAgent : AgentManifest
{
    // Called by Sts2Headless.AgentRunner after it resolves this
    // manifest type by --manifest <FQN>. Hand-wired so agents with
    // non-trivial constructors (IroncladAgent's five policies, future
    // RL-backed weights) compose explicitly; no new() constraint, no
    // DI container, no reflection on the agent's parameter list.
    public abstract IAgent CreateAgent();

    public sealed override string Language => "csharp-bundled";

    public sealed override IReadOnlyList<string> Command =>
    [
        "dotnet", "run",
        "--project", "src/Sts2Headless.AgentRunner",
        "--no-build", "--",
        "--manifest", GetType().FullName!,
    ];
}
```

A `BundledAgent`'s subclass:

```csharp
public sealed class IroncladConservativeManifest : BundledAgent
{
    public override string Name    => "ironclad-conservative";
    public override string Version => "0.5.1";
    public override IAgent CreateAgent() =>
        new IroncladAgent(
            draftPolicy:    new BossAwareDraftPolicy(),
            pathPolicy:     new ElitePreferringPathPolicy(),
            restPolicy:     new HpThresholdRestPolicy(threshold: 0.7),
            eventPolicy:    new GreedyEventPolicy(),
            merchantPolicy: new BudgetMerchantPolicy());
}
```

Abstract class rather than interface: virtual properties give bundled
authors near-zero boilerplate; `sealed override` on `BundledAgent`'s
`Command` + `Language` prevents an accidental rewrite from breaking
the AgentRunner contract; the class form lets us add `[Description]`
or metadata attributes later without a breaking change. Default-method
interfaces could approximate this in C# 8+, but the readability of
`virtual` properties wins on hover and in diffs.

### Agent dialect — wire bytes

The harness owns *both* ends of every cell's wire: it talks to the
host (existing AD-2 dialect) over one stdio pair and to the agent
(this AD's dialect) over a sibling stdio pair. Both share the
JSON-RPC envelope from AD-2; only the method namespace and DTOs differ.

Method catalogue, declared once in `Sts2Headless.Eval` and asserted at
both the harness *and* the AgentRunner sides via
`MethodCatalog.AssertParity` so a drift between catalogue and dispatch
fails startup:

```csharp
// src/Sts2Headless.Eval/Protocol/AgentMethodCatalog.cs
namespace Sts2Headless.Eval;

public static class AgentMethodCatalog
{
    public static IReadOnlyList<MethodEntry> All { get; } =
    [
        new("agent/init",
            ParamsType: typeof(AgentInitParams),
            ResultType: typeof(AgentInitResult),
            Summary: "Initialize the agent for one cell. Sent exactly once, before any agent/decide."),

        new("agent/decide",
            ParamsType: typeof(AgentDecideParams),
            ResultType: typeof(AgentDecideResult),
            Summary: "Decide one action against the current snapshot. Sent once per host step."),

        new("agent/teardown",
            ParamsType: null,
            ResultType: typeof(AgentTeardownResult),
            Summary: "Drain caches, flush logs, exit cleanly. Final wire call before SIGTERM."),
    ];
}

public sealed record AgentInitParams(
    string GameVersion,
    string Sts2DllSha256,
    Character Character,
    ulong Seed,
    int Ascension,
    IReadOnlyList<ModifierId> Modifiers,
    HarnessBudgets Budgets,
    string EvalId);

public sealed record AgentInitResult(
    string Name,
    string Version,
    string? Notes = null);

public sealed record AgentDecideParams(
    RunStateResult Snapshot);

public sealed record AgentDecideResult(
    AgentAction Action,
    string? Notes = null);

public sealed record AgentTeardownResult(
    bool Ok,
    string? Reason = null);
```

Sequence (one cell):

```text
[harness → agent]  {"id":1,"method":"agent/init","params":{ … }}
[harness ← agent]  {"id":1,"result":{"name":"my-agent","version":"0.1.0"}}
[harness → agent]  {"id":2,"method":"agent/decide","params":{"snapshot":{ … }}}
[harness ← agent]  {"id":2,"result":{"action":{"kind":"PlayCard","cardIndex":3,"targetIndex":0}}}
…
[harness → agent]  {"id":N,"method":"agent/teardown"}
[harness ← agent]  {"id":N,"result":{"ok":true}}
```

Error codes — agent-side server-defined range is **-32200..-32299**,
deliberately distinct from the host's -32000..-32099 (AD-7's
`DebugMethodDisabled = -32001`) so a log line is unambiguously
classifiable by code prefix. Three named codes for v1:

| Code | Constant | When |
| --- | --- | --- |
| -32200 | `AgentDeclinedToInit` | Agent emitted an error envelope on `agent/init` instead of an `AgentInitResult` — typically "I don't support this character / ascension / modifier combination." The harness records the cell with `terminus: HarnessError` rather than `AgentCrash` because the agent exited cleanly with a typed refusal. |
| -32201 | `AgentDecisionRefused` | Agent emitted an error envelope on `agent/decide` it doesn't know how to handle. The harness records the cell with `terminus: AgentCrash` and the error payload in `runs.jsonl`. |
| -32202 | `AgentSnapshotInvalid` | Agent received a snapshot that fails its own validation (missing fields after a wire bump it hadn't been re-generated against). Tagged separately from `AgentDecisionRefused` so a cross-version regression is visible as a category. |

JSON-RPC reserved codes (`ParseError` -32700, `InvalidRequest` -32600,
`MethodNotFound` -32601, `InvalidParams` -32602, `InternalError`
-32603) carry their standard meanings; an agent returning
`InternalError` from `agent/decide` lands in `runs.jsonl` as
`terminus: AgentCrash` with the wrapped exception. A stdout EOF mid-request
is also `AgentCrash` (detected the same way `HostProcess.SendAsync`
detects host crashes today: empty `ReadLineAsync` result).

The harness records the *first* hard error per cell; subsequent
errors from the same cell are not collected (the cell is already
attributed). Sibling cells run unaffected (FR-9).

`AgentAction` is `Sts2Headless.Agents.Contracts.AgentAction` — the
existing closed union of `PlayCard`, `EndTurn`, `UsePotion`,
`SelectMapNode`, `SelectEventOption`, `SelectRestSiteOption`,
`TakeTreasure`, `SkipTreasure`, `BuyMerchantItem`,
`LeaveMerchantRoom`, `EnterNextAct`, `ProceedEvent`,
`SelectReward`, `SkipReward`, and `StopRun`. The polymorphic
serialisation already established in the protocol (sealed union with
`kind` discriminator via `JsonDerivedType`) covers it.
`StopRun` from an agent is received by the harness, treated as a
voluntary cell exit, and recorded as `terminus: Abandoned` — the
existing `AgentDriver.PlayRunAsync` semantics carry over.

Per-decision soft budget enforcement: the harness's
`AgentTransport.SendAsync<AgentDecideResult>` wraps the underlying
read with `CancellationTokenSource(Budgets.PerDecision)`. A timeout
fails the cell with `terminus: Timeout`, not `AgentCrash` — the agent
is alive but unresponsive within the contracted window. The harness
then SIGTERMs the agent, waits 2s, and SIGKILLs if still up.

OpenRPC export (AD-5): a sibling artefact `protocol/agent-openrpc.json`
is generated by extending `Sts2Headless.SchemaExport` to walk
`AgentMethodCatalog.All` alongside `MethodCatalog.Core`. The file is
checked in, validated against `open-rpc/meta-schema` in CI, and is
the input source for any future non-C# / non-Python adapter (Rust,
Kotlin, …) author who wants typed bindings instead of hand-rolled
NDJSON.

### `EvaluationHarnessConfig` — schema and defaults

```csharp
// src/Sts2Headless.Eval/EvaluationHarnessConfig.cs
namespace Sts2Headless.Eval;

public sealed record EvaluationHarnessConfig
{
    // Required matrix axes.
    public required IReadOnlyList<AgentManifest> Agents { get; init; }
    public required SeedBank                     Seeds  { get; init; }

    // Defaulted matrix axes.
    public IReadOnlyList<Character>  Characters { get; init; } = [Character.Ironclad];
    public IReadOnlyList<int>        Ascensions { get; init; } = [0];
    public IReadOnlyList<ModifierId> Modifiers  { get; init; } = [];

    // Per-cell budgets. Per-manifest Budgets win when non-null.
    public HarnessBudgets Budgets { get; init; } = HarnessBudgets.Default;

    // Parallelism. null ⇒ auto: min(matrixSize, max(1, Environment.ProcessorCount / 2)).
    public int? Workers { get; init; } = null;

    // Pluggable scoring. Default is LexSortScoring.
    public IScoringFunction Scoring { get; init; } = ScoringFunctions.Default;

    // Output layout. EvalIdGenerator default: utc-timestamp.
    public OutputLayout Output { get; init; } = OutputLayout.Default;

    // FR-11 (deferred to v2 in spec; field exists so v2 can fill it in).
    public bool EnableDeterminismCanary { get; init; } = false;

    // FR-12 (deferred to v2; same rationale).
    public bool CaptureAgentNotes { get; init; } = false;
}

public sealed record HarnessBudgets(
    TimeSpan PerDecision,
    TimeSpan PerCell,
    int      MaxSteps)
{
    public static HarnessBudgets Default { get; } =
        new(PerDecision: TimeSpan.FromSeconds(30),
            PerCell:     TimeSpan.FromMinutes(10),
            MaxSteps:    AgentDriver.DefaultMaxSteps);  // 4000
}

public sealed record OutputLayout(
    string         EvalRoot,
    Func<DateTimeOffset, string> EvalIdGenerator)
{
    public static OutputLayout Default { get; } =
        new(EvalRoot: "replays/eval-harness",
            EvalIdGenerator: t => t.UtcDateTime.ToString("yyyy-MM-ddTHH-mm-ssZ"));
}
```

Default calibration notes:

- **`PerDecision = 30s`** is generous for the existing in-repo agents
  (`GreedyAgent` decides in microseconds; `IroncladAgent`'s MCTS-style
  combat planner runs in tens to hundreds of milliseconds in the
  current `BeatGameOnSeed42Tests`). 30s leaves headroom for external
  agents to do real planning. Callers tighten to 5s for fast agents
  and loosen to minutes for MCTS-heavy ones via per-manifest
  `Budgets`.
- **`PerCell = 10 min`** is calibrated against
  `BeatGameOnSeed42Tests`'s 30-min cancellation budget (which covers
  the full Architect-terminus path with mid-run cheats firing on
  every decision — i.e. the hardest case in the suite). A real
  Greedy-Ironclad-A0 run terminates well inside 5 min on the existing
  hardware per `ParallelHostThroughputBenchmark`. 10 min is the
  conservative line under which a `Timeout` is genuinely "this agent
  isn't making progress."
- **`MaxSteps = 4000`** mirrors `AgentDriver.DefaultMaxSteps`. Already
  the value the in-repo driver uses; choosing the same number for
  the harness means the driver's step cap and the harness's step cap
  trip simultaneously rather than the harness adding an extra
  guard-rail nobody asked for.
- **`Workers = ⌊cores/2⌋`** keeps the resident-set budget honest:
  each cell holds two processes (host + agent), the host alone is
  hundreds of MB of `sts2.dll`-loaded heap, and the workstations the
  team uses range from 8 to 32 cores. The full-throttle
  `Workers = cores` mode is one explicit override away — we don't
  default to it because most users running an eval don't want their
  IDE to swap.

The harness has no implicit globals; *every* knob is on
`EvaluationHarnessConfig`. There is no env-var path that changes
harness behaviour. (Env vars are still used to talk to the host:
`STS2_REPLAY_OUT` and `STS2_REPLAY_AGENT` per cell, per AD-8. Those
are an implementation detail; they're set by the harness from
`config.Output`, never read by user code.)

### Seed bank file format

JSON, one bank per file, committed under
`documentation/eval/seeds/<bank>.json`:

```json
{
  "name":             "reference",
  "version":          "1",
  "createdAt":        "2026-05-14",
  "gameVersion":      "v0.103.2",
  "generationMethod": "first 50 seeds where Ironclad A0 has at least one Neow choice with a boss relic",
  "seeds":            [1, 2, 3, 5, 8, 13, 21, 34, 42, ...]
}
```

- **`seeds`** is a JSON array of numbers. Wire-side the protocol uses
  `Seed: ulong`; STS2 seeds typically fit in JSON `number` exactly
  (`Number.MAX_SAFE_INTEGER = 2^53 - 1`). Seeds above 2^53 are valid
  on the wire but lose precision in JSON; we accept this for v1 and
  add a parser warning at load time. A future schema bump can switch
  to string-encoded numbers if STS2 starts publishing seeds in the
  uint64 high range.
- **`version`** is a string (not an int) so a bank can carry a
  semver-ish identifier (`"1.1"` after additive append). Seeds may be
  *appended* but not removed or reordered, so a result from yesterday
  remains comparable to a result from today on the same bank. A
  material content change (remove / reorder / replace) is a new bank
  with a new name, not a silent edit.
- **Loader behaviour**: `SeedBanks.Smoke`, `SeedBanks.Reference`,
  `SeedBanks.Deep` are `public static readonly SeedBank` fields on
  `Sts2Headless.Eval.SeedBanks` that lazily read the corresponding
  JSON. `SeedBanks.FromFile(string path)` and
  `SeedBanks.Inline(IEnumerable<ulong> seeds, string? name = null)`
  cover ad-hoc use; the latter carries `version = "inline"` and is
  refused by the `summary.json` "this is a reproducible eval" emitter
  (the reproducibility claim degrades when the bank itself isn't on
  disk).
- **`gameVersion`** ties a bank to the pin it was generated against
  (FR-10). Running a bank against a mismatched `GAME_VERSION` is
  allowed (you may want to regression-test a pin bump on the same
  seeds) but `runs.jsonl` records the *eval's* `gameVersion`, not the
  bank's, so cross-version aggregation refuses to mix.

### `IScoringFunction` and the default

```csharp
// src/Sts2Headless.Eval/Scoring/IScoringFunction.cs
namespace Sts2Headless.Eval.Scoring;

public interface IScoringFunction
{
    string Name    { get; }
    string Version { get; }
    IReadOnlyList<AgentRanking> Rank(IReadOnlyList<CellResult> cells);
}

public sealed record AgentRanking(
    int             Rank,
    AgentIdentity   Agent,
    double          Score,
    AgentAggregates Aggregates);

// Default. Lex-sort: correctness first, depth second, efficiency as
// tiebreak. Reflects the conventional STS reasoning: a 60% win-rate
// agent that occasionally crashes is worse than a 60% win-rate agent
// that doesn't; once that's equal, an agent that loses on floor 47 is
// better than an agent that loses on floor 12; once that's also
// equal, prefer the cheaper one.
public sealed class LexSortScoring : IScoringFunction
{
    public string Name    => "lex-sort";
    public string Version => "1.0";

    public IReadOnlyList<AgentRanking> Rank(IReadOnlyList<CellResult> cells) =>
        cells
            .GroupBy(c => c.Agent.Name)
            .Select(g =>
            {
                var aggs = AgentAggregates.From(g);
                return new AgentRanking(
                    Rank: 0,                          // assigned post-sort
                    Agent: g.First().Agent,
                    Score: aggs.WinRate,              // displayed; ties broken below
                    Aggregates: aggs);
            })
            .OrderByDescending(r => r.Aggregates.WinRate)
            .ThenByDescending(r => r.Aggregates.MeanDepth)
            .ThenBy(r => r.Aggregates.MedianWallClockMs)
            .Select((r, i) => r with { Rank = i + 1 })
            .ToList();
}

public static class ScoringFunctions
{
    public static IScoringFunction Default { get; } = new LexSortScoring();
}
```

`AgentAggregates.From(IEnumerable<CellResult>)` is a static helper —
computes the per-agent rollups (`Wins`, `WinRate`, `MeanDepth`,
`P25Depth` / `P50Depth` / `P75Depth`, `EngineCrashes`, `HostCrashes`,
`AgentCrashes`, `Timeouts`, `MedianWallClockMs`, `PeakRssMbP95`). The
"depth" axes are a sort ordinal — `act × 100 + floor` — so cells in
act 2 outrank deep act-1 cells without scoring functions doing the
math.
Custom scoring functions consume aggregates; they don't recompute
them.

Every emitted leaderboard (`summary.md` / `summary.json`) records the
scoring function's `Name` + `Version`. Two leaderboards with the same
agents but different scoring functions are not silently comparable;
the file says which is which.

### Output schemas

The harness owns four top-level files per eval plus one per cell.
Schemas pinned below; concrete examples in the design-by-example
sketches under
[../evaluation-harness/03-results.md](../evaluation-harness/03-results.md).

**`config.json`** — full serialised `EvaluationHarnessConfig`,
including each `AgentManifest`'s `manifestType` (FQN) for traceability
and the harness's own metadata block (`harnessVersion`, `gameVersion`,
`sts2DllSha256`). Re-feeding a `config.json` into a future
`EvaluationHarness.RunAsync` is the canonical reproducer; manifest
FQNs that no longer resolve on the classpath fail loudly with a typed
error rather than silently substituting (FR-10).

**`runs.jsonl`** — one JSON object per line, append-only, written
incrementally as each cell finishes. Each line is a `CellResult`
record:

```csharp
public sealed record CellResult(
    string           EvalId,
    AgentIdentity    Agent,                // {Name, Version, Language, ManifestType}
    ulong            Seed,
    Character        Character,
    int              Ascension,
    IReadOnlyList<ModifierId> Modifiers,
    CellTerminus     Terminus,
    int              Act,                  // 1..10 (A1..A10)
    int              Floor,                // floor index within `Act`
    int              FinalHp,
    int              MaxHp,
    int              Gold,
    int              DeckSize,
    int              RelicCount,
    int              CombatCount,
    int              EliteCount,
    int              BossCount,
    int              TurnsInCombat,
    int              Steps,
    long             WallClockMs,
    int?             PeakRssMb,            // best-effort
    int?             AgentPeakRssMb,       // best-effort
    long?            CpuTimeMs,            // best-effort
    string           ReplayPath,           // relative to eval root, e.g. "cells/greedy/42"
    string           GameVersion,
    string           Sts2DllSha256,
    ScoringMetrics   Scoring,              // {Score: double}
    WireErrorPayload? Error = null);        // populated when Terminus ∈ {EngineCrash, HostCrash, AgentCrash, HarnessError}

public sealed record AgentIdentity(string Name, string Version, string Language, string ManifestType);
public sealed record ScoringMetrics(double Score);
public sealed record WireErrorPayload(int Code, string Message, string? Stack = null);
```

**`summary.json`** — the per-eval rollup:

```csharp
public sealed record EvaluationSummary(
    string                       EvalId,
    string                       GameVersion,
    string                       Sts2DllSha256,
    SeedBankReference            SeedBank,         // {Name, Version, Count}
    IReadOnlyList<Character>     Characters,
    IReadOnlyList<int>           Ascensions,
    IReadOnlyList<ModifierId>    Modifiers,
    ScoringFunctionReference     Scoring,          // {Name, Version}
    long                         ElapsedMs,
    int                          CellCount,
    int                          Workers,
    IReadOnlyList<AgentRanking>  Ranking,
    IReadOnlyList<CellResult>    NotableCells);    // auto-populated with crashes
```

**`summary.md`** — human-readable mirror of `summary.json`, formatted
to feel sibling-shaped with `documentation/coverage/sweep-*.md`
(same header, same table style). Always deterministic-ordered so
diffs across runs only reflect content changes.

**`cells/<agent>/<seed>/cell.json`** — denormalised forward index
from the AD-8 cell directory back to its row in `runs.jsonl`. The
shape is a strict subset of `CellResult` (no resource accounting, no
error payload — those live in `runs.jsonl`). The point is that a
tool walking `cells/` can read each cell's `cell.json` without
joining against `runs.jsonl` first.

NFR-1 (output is the API): adding fields is allowed; removing or
renaming is a breaking change with the same OpenRPC-style discipline
the wire protocol takes (AD-5).

### `CellTerminus` — closed set

```csharp
public enum CellTerminus
{
    Victory,        // agent beat the Act 3 boss (per sts2-game-facts.md)
    Death,          // agent died in combat
    Abandoned,      // agent emitted StopRun mid-game
    Stalled,        // StallDetector tripped
    MaxSteps,       // Budgets.MaxSteps cap reached
    Timeout,        // Budgets.PerCell wall-clock cap expired
    EngineCrash,    // host returned a wire error
    HostCrash,      // host process died (stdout EOF before response)
    AgentCrash,     // agent process died (stdout EOF before response)
    HarnessError,   // orchestrator-side failure
}
```

The first six are *results*; the cell ran cleanly and the agent (or
game state, in the case of MaxSteps) determined the outcome. The
three `*Crash` variants are *attribution* — somebody crashed; the row
records who. `HarnessError` is the only one that flips the harness's
own exit code; all others return zero (NFR-4: "a CI gate that fires
on agent crashes is hostile to development on the harness itself").

`Stalled` distinguishes from `Timeout`: the former trips on
fingerprint repetition (`StallDetector` already wired through
`AgentDriver.PlayRunAsync`), the latter on wall-clock. The
`StallDetectedException` from the existing driver bubbles up
as `terminus: Stalled` with the captured fingerprint in
`runs.jsonl`'s `Error.Message`.

### Exit code semantics

`Sts2Headless.Eval.EvaluationHarness.RunAsync` returns an
`EvaluationReport`. The example programs end with:

```csharp
return report.HasHarnessError ? 1 : 0;
```

`HasHarnessError` is true iff *any* cell has `Terminus =
HarnessError` (the harness itself failed to set up or finalise the
cell). A matrix where every cell finishes — including all the
`*Crash` outcomes — exits zero. This is what makes the harness
useful in CI: PR-gated smoke evals fail on harness regressions, not
on agent crashes (which are real signal but not "the harness is
broken").

### Just recipes and CLI shape

A new `scripts/eval/justfile`, imported from the root justfile,
follows the existing per-module convention:

```just
# scripts/eval/justfile
import '../common.just'
set working-directory := '../..'

# Smoke eval — 5 seeds × Greedy on Ironclad A0. Fast inner-loop check.
smoke:
    @just build::build
    @dotnet run --project examples/EvalSmoke --no-build

# Reference eval — 50 committed seeds × in-repo agents on Ironclad A0.
reference:
    @just build::build
    @dotnet run --project examples/EvalReference --no-build

# Deep eval — full 500-seed bank against everyone opted in. Multi-hour.
# Pass `--` then extra flags to forward them to the program.
deep *args:
    @just build::build
    @dotnet run --project examples/EvalDeep --no-build -- {{args}}
```

Top-level invocations: `just eval::smoke`, `just eval::reference`,
`just eval::deep`. No new CLI flags on `Sts2Headless` itself — the
host stays a stdio server (AD-2), nothing else.

The example programs are normal `dotnet` exes; nothing in the
harness library reads `args[]` directly. If a caller wants to drive
the matrix from a checked-in `config.json`, they write a
`Program.cs` that reads the file via
`EvaluationReportIo.LoadConfig(path)` and passes the resulting
record to `EvaluationHarness.RunAsync`. JSON-driven invocation is
deliberately one user-line away rather than baked into the harness.

### CI shape

Three GitHub Actions workflows, all targeting the existing
self-hosted runner with a populated `vendor/sts2.dll` (per AD-3 /
the runbook at `documentation/runbooks/vendor-mirror-setup.md`):

- **`.github/workflows/eval-smoke.yml`** — `on: [pull_request]`,
  `runs-on: [self-hosted]`. Runs `just eval::smoke`, diffs the
  resulting `summary.json` against the most recent `main` published
  artefact, posts a delta comment on the PR. Budget ≲ 5 min. Fails
  the PR on `HarnessError` (per the exit code), passes on agent
  crashes (they're signal, not a gate).
- **`.github/workflows/eval-reference.yml`** — `on: schedule (nightly
  03:00 UTC)` plus `workflow_dispatch`. Runs `just eval::reference`,
  publishes the eval directory (`replays/eval-harness/<eval-id>/`) to
  the `gh-pages` branch at `nightly/<eval-id>/`, regenerates the
  leaderboard index at `gh-pages/index.html` from the
  `headless-in-the-spire-leaderboard` Python package. Budget ≲ 1 h.
- **`.github/workflows/eval-deep.yml`** — `on: workflow_dispatch`
  only. Runs `just eval::deep`, publishes to `gh-pages/deep/<eval-id>/`,
  regenerates a separate `deep/index.html`. Operator-triggered;
  multi-hour.

gh-pages layout:

```
gh-pages/
├── index.html                      # leaderboard rendered from latest nightly
├── nightly/
│   ├── 2026-05-26T03-00-00Z/       # one dir per nightly eval
│   │   ├── summary.md
│   │   ├── summary.json
│   │   ├── runs.jsonl
│   │   ├── config.json
│   │   └── cells/                  # included for evidence-linking only — .mcr files referenced but not redistributed
│   ├── 2026-05-27T03-00-00Z/
│   └── …
└── deep/
    ├── index.html
    └── 2026-06-01T12-00-00Z/
```

`cells/` directories on gh-pages include `cell.json` and AD-8's
`manifest.json` + `run.json` for evidence-linking, but **not** the
`.mcr` binaries — those derive from `vendor/sts2.dll` (proprietary
posture per AD-8) and stay on the self-hosted runner. The HTML
leaderboard links to the runner's local artefacts for the
maintainer-side investigation flow; published replays sufficient for
a public viewer wait on the downstream Godot replay-viewer mod (also
per AD-8).

**Consequences**

- Four new C# projects land in the solution (`Sts2Headless.Eval`,
  `Sts2Headless.Eval.Manifests`, `Sts2Headless.AgentRunner`,
  `Sts2Headless.EvalTests`), plus three example console exes
  under `examples/`. The `Sts2Headless.AgentRunner` exe is the only
  one that links against the `Sts2Headless.Eval.Manifests` assembly;
  the orchestrator library and the example programs reference
  `Sts2Headless.Eval` only — manifests reach the orchestrator as
  `AgentManifest` references constructed by the user's
  `Program.cs`, not via assembly scanning.
- `HostPool`, `AgentDriver`, `StallDetector`, and the AD-8 recording
  substrate are reused unchanged. The harness is a *consumer* of the
  existing plumbing, not a replacement; FR-1..FR-12 demand zero new
  primitives in `Sts2Headless.Agents` or `Sts2Headless.Replay`.
- The agent dialect being a mirror of the host dialect keeps the
  cognitive load flat: anyone who's written against the host wire
  already knows the envelope, the polymorphic `AgentAction`
  serialisation, the `WireErrorCode` conventions. The OpenRPC export
  for the agent dialect is a small extension to
  `Sts2Headless.SchemaExport`, not a parallel pipeline.
- Library defaults are *starting values*. Operators tune them per
  manifest (`AgentManifest.Budgets`) or per eval
  (`EvaluationHarnessConfig.Budgets`); the harness never has a
  hard-coded knob a caller can't override. When the seed-42 wall-clock
  or the `IroncladAgent` p99 decision time changes meaningfully,
  these defaults get re-calibrated in a follow-up — the structure
  doesn't change.
- Adding a new in-repo agent is mechanical: one agent class, one
  `BundledAgent` subclass, one line in `BuiltinAgents`. An accidental
  typo anywhere along that chain is a compile error. Adding an
  external agent is one class (the manifest) and one `new …()` at the
  config call site. No JSON files to maintain, no path lookups, no
  string-typed factory.
- Variants of the same agent class (different policy stacks, different
  hyperparameters) are first-class leaderboard entries by writing
  another `BundledAgent` subclass with a distinct `Name` and a
  custom `CreateAgent()`. This is the long-term answer to "how do we
  compare policy stacks?" — naming is the author's responsibility;
  the harness only sees `AgentManifest` instances.
- Crash isolation is structural: the agent is a separate OS process,
  and per-cell host + agent subprocess pairs are the only resources
  that cross the cell boundary. The orchestrator process surviving
  a cell crash is a function of `Process.Start` + `await`-on-stdout,
  not new framework code.
- The Python leaderboard package is the only consumer of
  `summary.json` and `runs.jsonl` shipped in this repo. Anyone else
  who wants a renderer reads the same JSON; the schema is the
  contract (NFR-1). The Python package has no path back into the C#
  harness — it's strictly downstream, and AD-6 still applies (Python
  cannot author canonical eval scenarios; it only renders the JSON
  the C# harness emits).
- AD-3 cross-version posture inherits unchanged: a `gameVersion`
  bump invalidates a `.mcr`, which invalidates a cell's replay
  re-execution, which invalidates the determinism canary (FR-11) on
  that cell. The aggregate layer refuses to mix versions; the
  bank-version + game-version pair is what makes a published result
  re-runnable.
- AD-7 inherits unchanged: `Sts2Headless.AgentRunner` never sets
  `--enable-debug` on the host it talks to (the harness owns host
  spawn, not the agent), and `HostPool` already enforces the
  production-host invariant (see `HostProcess.Start` — debug is
  deliberately not exposed as a knob). A diagnostic eval that wants
  debug methods has to swap `HostPool` for the test-only
  `HostSubprocess` fixture, which the harness library does not link
  against.
- FR-11 (determinism canary) and FR-12 (per-decision notes) ship
  with the field-level surface in place (`EnableDeterminismCanary`,
  `CaptureAgentNotes` on `EvaluationHarnessConfig`;
  `AgentDecideResult.Notes` on the wire) but no orchestrator
  implementation. A v2 PR fills them in without a wire bump.
- The CI shape is a starting point: PR smoke on every PR, nightly
  reference on a schedule, manual deep on workflow_dispatch. If the
  smoke run turns out to be cost-prohibitive even on the self-hosted
  runner, we narrow it to "only run when files under `src/` or
  `tests/Sts2Headless.EvalTests/` change" — that change is a
  workflow tweak, not an architecture revision.
