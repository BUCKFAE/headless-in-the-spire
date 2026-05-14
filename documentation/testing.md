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
- "Full Act-1 win on seed S with character C." (future)
- Anything that spans **a player journey**, not a single wire call.

Per-test cost: tens of seconds. Runs in `just test` for now; may move to a
`just test-slow` tier if inner-loop wall time gets uncomfortable. **A red
test here is usually a regression in *the stitching* — combat→reward→map
transitions, multi-call invariants, or determinism.**

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
just test              # all C# axes + Python parity + Python typecheck/lint
just test-cs           # C# unit + integration + end2end (when present)
just test-python       # Python parity tests only
```

(Recipes are in the `justfile`; `just --list` enumerates the current set.)
