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
