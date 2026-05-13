# Initial Goals

This document captures the motivation and high-level requirements for the project.
It is the canonical "why are we building this" reference and should be updated
whenever priorities shift.

## Context

Slay the Spire 2 is an Early-Access C#/.NET game running on Godot 4.x. A small
community of headless / automation projects exists already (`wuhao21/sts2-cli`,
`Gennadiyev/STS2MCP`, `CharTyr/STS2-Agent`, `longkerdandy/STS2-Cli-Mod` and others
— see [existing-headless-libraries.md](../research/existing-headless-libraries.md)
for the full survey). I have evaluated two of them in detail and decided to build
our own rather than fork.

The reasons follow.

## Goals

### 1. Engineered to a higher standard than what exists today

The existing implementations don't feel well engineered. Two specific complaints:

- **Stringly-typed APIs.** Cards, relics, enemies, events, intents, screens and
  similar finite domains are passed around as strings rather than enums / sealed
  types. This makes typos silent, refactors painful, and tooling worthless.
- **God files.** Several projects have single logical classes spanning many files
  or thousands of lines of code (e.g. STS2MCP's `McpMod.*.cs` partial classes,
  individual files in the 4k+ LOC range). This is a maintainability red flag.

Our project should:

- Prefer **enums and sealed types** for every finite domain we surface. Generate
  them where feasible from a trusted source (`spire-codex` is the obvious one).
- Cap file size as a soft norm; split by responsibility, not by `partial class`.
- Treat unknown card / relic / enemy / event IDs as **errors**, not as opaque
  strings to be passed through.

### 2. A large variety of end-to-end tests

Existing projects have essentially no test coverage (only `sts2-cli` even has a
tests directory). Combat correctness, RNG determinism, map generation, event
trees, and the IPC protocol are all things we want covered by automated tests.

Concretely:

- End-to-end tests should drive the **real game logic** via the headless runner
  and assert on observed state.
- Tests should be **deterministic** (seed-controlled) and **fast** (no renderer).
- Test scenarios should cover: combat (basic attacks, blocks, powers, multi-enemy
  encounters, intent prediction), map traversal (each room type), card rewards,
  events, shops, rest sites, boss encounters, ascension scaling, and at minimum
  one full Act 1 run.
- A separate research note ([02-e2e-testing-and-self-feedback.md](../research/02-e2e-testing-and-self-feedback.md))
  goes deeper on how this works in practice.

### 3. Parallel execution — run N instances at once

None of the surveyed projects support running multiple instances in parallel.
Every IPC mechanism in the ecosystem hardcodes a port or pipe name, and the
embedded Godot runtime is assumed to be a singleton.

For RL training, large-batch replay validation, and parametric testing, we want
to be able to spin up `N` independent headless instances on one machine without
ports colliding or game state leaking between runs. Specifically:

- Each instance is its own OS process with isolated working directory and config.
- IPC transports must be addressable (per-instance pipe name, dynamic port, or
  child-process stdio).
- Shared state (save files, profile data, log files) is parameterised per
  instance.
- Throughput target: realistic enough to drive thousands of full runs per day
  on a single workstation.

### 4. A clean, high-level API with first-class language bindings

We want **one canonical interface** to the headless game, with thin wrappers
exposing it idiomatically to other languages. The wire protocol and the in-host
C# API both need to be considered "public surface" worthy of design.

Target bindings (initial set):

- **Python** — first-class, because most ML / RL work lives there.
- **Kotlin** — first-class, because that's where our own consumers are.
- Future: other JVM languages, TypeScript, Rust if there's demand.

Design constraints:

- The API should be **typed** end-to-end (no string-typed enums on the wire).
- The API should be **discoverable** — autocomplete in an IDE should be enough
  to write a client.
- The API should be **stable across game patches** where possible, with explicit
  versioning when it isn't. Adding a new relic shouldn't break clients.
- The API should be **easy to wrap**: prefer schemas (e.g. JSON Schema /
  Protobuf / similar) over hand-written bindings.

### 5. Record replays

We want every run recorded as a structured, deterministic replay artefact:

- A replay should contain the **seed**, game version, character, ascension,
  the full sequence of decisions (player actions), and ideally periodic state
  snapshots for cheap seeking.
- Replays should be **reproducible**: replaying produces the same state stream,
  assuming the same game version.
- Replays must be **persistent** — not auto-deleted after a few days, as
  `sts2-cli` does today.
- Replays should be **diffable**: human-readable enough to inspect, structured
  enough to compare programmatically.
- Use cases: regression testing across game patches, post-hoc debugging of bot
  behaviour, RL data collection, sharing interesting runs with collaborators.

## Non-goals (initial)

- Visual rendering. We target the headless engine pattern (stub `GodotSharp` à la
  `sts2-cli`). If anyone wants to watch a run, they can replay it in the real
  game.
- Reimplementing game logic in another language (`sts_lightspeed`-style). We
  reuse the real `sts2.dll`. A reimplementation is a separate, much larger
  project.
- Steam Workshop publishing. We may package as a mod, but distribution is not a
  priority.
- Multiplayer / co-op support, for now. Single-player only.

## Resolved foundational decisions

The architectural choices below were resolved before implementation started.
The reasoning is recorded in [02-architecture-decisions.md](./02-architecture-decisions.md);
this section is a one-line summary of each.

- **Language**: C# only for the core (mod + headless host + orchestrator).
  External clients in Python / Kotlin are generated from the wire schema.
- **Wire protocol**: NDJSON over stdio, with a JSON-RPC-style envelope
  (`id`, `method`, `params`, `result`/`error`, plus notifications). Schema
  authored in C#, exported as JSON Schema, client bindings generated from
  that.
- **Version pinning**: a single pinned `sts2.dll` per branch, with a
  three-stage compat check on bump (reflection-manifest diff → compile →
  Harmony-apply smoke) and a bulk-rerecord workflow for golden snapshots.
