# Testing

This repo runs a three-axis test pyramid. The axes describe **scope** — what
a test pins down — not duration. A fast test in the wrong axis is still in
the wrong axis.

Behavioral source-of-truth is C#-only per [AD-6](requirements/02-architecture-decisions.md#ad-6--behavioral-source-of-truth-c-only-clients-verify-parity);
every layer below is C# / xUnit. The Python tree (`clients/python/...`) does
not appear on this pyramid — it carries parity tests against the C#
reference, not behavioral assertions.

## Axes

### Unit — `tests/Sts2Headless.UnitTests/`

**Scope:** host-only logic, in-process. No `sts2.dll`, no subprocess.

What lives here:

- Envelope encoding / decoding.
- Method catalogue ↔ dispatch-table parity (AD-5).
- AD-4 invariant guards (no compile-time sts2 reference).
- Schema export shape and OpenRPC validity.
- Anything that exercises C# code with no game involvement.

Per-test cost: milliseconds. Always in the inner loop.

### Integration — `tests/Sts2Headless.IntegrationTests/`

**Scope:** one wire-call slice against a real host subprocess + real
`sts2.dll`. The host is typically shared per test class (xUnit
`IClassFixture<HostSubprocess>`); each test does a `run/new` plus a small
number of follow-up calls and asserts on a single slice of the contract.

What lives here:

- "Wire call X returns the expected DTO shape."
- "Card Y plays in combat and surfaces the right snapshot delta."
- "Reward Z appears with the right options after combat."
- "Relics on snapshots stay consistent between back-to-back calls."

Per-test cost: seconds. Acceptable inner-loop; parallelism helps. **A red
test here is a regression in *one* wire surface.**

### End-to-end — `tests/Sts2Headless.End2EndTests/`

**Scope:** multi-room arcs. A driver / agent / replay walks a complete
player journey end-to-end and asserts on the trajectory.

What lives here:

- "Greedy agent reaches the Act-1 boss room on seed S."
- "Replay R re-executes byte-for-byte against a fresh host."
- "Full game on seed S with character C runs to the Architect terminus"
  (`BeatGameOnSeed42Tests.cs`).
- Anything that spans **a player journey**, not a single wire call.

Per-test cost: tens of seconds. Runs in `just validation::test` for now; may move
into the `just validation::test-full` tier (which already pulls in gaps,
benchmarks, and every MechanicSweep) if inner-loop wall time gets
uncomfortable. **A red
test here is usually a regression in *the stitching* — combat→reward→map
transitions, multi-call invariants, or determinism.**

### Wrap long-running drives in `StallDetector`

A multi-act drive iterates tens of thousands of wire round-trips; if any
one of them hangs (a monster move method NREs internally, the exception
is swallowed by sts2's `TaskHelper.LogTaskExceptions`, the engine ends
up half-transitioned), the symptom on the wire is "every subsequent
snapshot is identical." Naive tests detect this only after the whole
cancellation budget expires — minutes for what's effectively an instant
failure.

`src/Sts2Headless.Agents/Driving/StallDetector.cs` is the reusable watchdog,
wired automatically by `AgentDriver.PlayRunAsync` — every IAgent gets
stall detection for free, structurally impossible to forget. It
fingerprints each snapshot (room + act/floor + hp/gold/deck + combat
round/phase/inProgress/energy/block/hand + per-enemy hp/powers) and throws
`StallDetectedException` when K consecutive snapshots have an identical
fingerprint. Default threshold 8 catches hangs within ~8 seconds. The
exception's fingerprint message points the operator at the exact
combat / enemy / power that's stuck — pair with `HangPatches.cs` to
add a Harmony prefix that no-ops the hanging method.

## Where things live

| Concern | Home |
| --- | --- |
| Drivers / agents (greedy, future MCTS, replay re-executor) | `src/Sts2Headless.Agents/` |
| Single-slice scenarios | `tests/Sts2Headless.IntegrationTests/` |
| Multi-room arcs, replay corpora | `tests/Sts2Headless.End2EndTests/` |
| Python parity tests | `clients/python/headless-in-the-spire-agents/tests/` (parity only — never behavioral) |

## Pyramid layer ↔ axis cross-reference

The [e2e-testing-and-self-feedback research note](research/e2e-testing-and-self-feedback.md)
sketches a four-layer pyramid (unit → scenario → golden-replay → fuzz).
That maps onto the three axes above as:

- Layer 1 (unit) → **Unit** axis.
- Layer 2 (scenario fixtures, single-slice) → **Integration** axis.
- Layer 3 (golden replay, full arcs) → **End-to-end** axis.
- Layer 4 (fuzz / property-based) → not materialised yet. When it lands,
  it either lives under End-to-end as a slow tier or earns its own project;
  decide at the time, not now.

## Replays

Replays are end-to-end tests where the decision source is a recorded NDJSON
stream rather than a live agent. Same project, same scaffolding —
`IDriver`-pluggable. The recording substrate is already free per AD-2 (the
host's stdout *is* a replay). What's deferred until a concrete consumer
asks for it: the header record (game version, DLL sha256, seed, schema
version, reflection-manifest hash), the persistence layout, and the
re-executor. Doing them speculatively costs us complexity now; deferring
costs us nothing because AD-2 preserved the option.

## Running the suite

```
just validation::test                    # all C# axes + Python parity + Python typecheck/lint
just validation::dotnet::test-unit       # C# unit tests only (no vendor/sts2.dll required)
just validation::dotnet::test-integration  # C# integration tests (single-slice scenarios)
just validation::dotnet::test-end2end    # C# end-to-end tests (multi-room arcs / replays)
just validation::test-python             # Python parity tests only
just validation::test-sequential         # all axes, sequential (live logs per suite)
```

(The root `justfile` is a thin orchestrator; recipes live in
`scripts/<module>/justfile` modules and shared variables live in
`scripts/common.just`. `just --list` enumerates the current set.)
