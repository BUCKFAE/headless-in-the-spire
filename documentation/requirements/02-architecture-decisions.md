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
2. `just check-game-compat` → fix any reported breakages in core.
3. `just test` (fast tier) → must pass.
4. `just rerecord-snapshots` → re-runs every scenario and replay, writes
   new snapshots under `snapshots/<new-version>/`. Diff is reviewed by a
   human and compared against published patch notes:
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
populated at first-run by `just setup` from the user's Steam install. The
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
installs. `just setup` runs `uv sync --all-packages`; `just generate-python`
and `just test-python` go through `uv run`. We pick uv over pip/poetry/pipx
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
