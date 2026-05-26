# Cross-language battle simulator feasibility

Snapshot date: 2026-05-26.

## The question

Can we build a "BattleSimulator" library that calls the actual `sts2.dll`,
exposes a clean cross-language interface, and supports MCTS-style branched
simulation efficiently — so that a Python / Rust / Kotlin agent can do
ground-truth forward search without porting `Sts2Headless.BattleAgent.Core`
into its own language?

The question matters because `BattleAgent.Core` (pure C#, no `sts2.dll`
dependency) is the only forward-simulator the project owns, and it can't
be reused from outside the C# tree. An external agent author who wants
MCTS today has to either (a) reimplement combat math from scratch in their
language, or (b) skip search and rely on shallow policies.

This note records the conclusions of a deliberate research pass into
whether a shared, engine-backed `BattleSimulator` is achievable. Short
version: the framing of the question turns out to be wrong. There are
two distinct goals tangled inside it, and one of them is structurally
unachievable.

## TL;DR

Two answers, depending on the goal:

1. **"Use `sts2.dll` ground truth from any language"** — yes, achievable
   and mostly already built. The existing NDJSON wire (AD-2) + `HostPool`
   already are this, in skeleton form. A new `combat/predict_action`-style
   wire method on the host surface would let any wire client ask "apply
   this action against the current state, return the result" and get the
   engine's answer. Cheap to ship on top of the existing infrastructure;
   language-agnostic for free.

2. **"…fast enough to drive MCTS"** — no, structurally. The engine has
   process-global state (`RunManager` singleton, Godot scene tree, RNG)
   and per-step cost in the milliseconds-to-seconds range. Tree search at
   the modest scale of 1k rollouts × ~20 steps × <30s/decision needs
   microseconds per state transition. That gap is not a transport issue.
   It holds identically whether you call `sts2.dll` via JSON-RPC, a
   NativeAOT shared library + FFI, Python.NET embedding, or WASM.

So the offline simulator (`BattleAgent.Core`) is not a redundant
re-implementation; given engine speed, it is the only way to do search at
all. The right architecture is hybrid — offline simulator for tree
expansion, real engine for ground-truth verification of the action
actually taken (AlphaZero pattern).

## What `sts2.dll` forces on us

The hard constraint is **one combat per OS process**. It comes from
several reinforcing pieces of engine architecture, none of which can be
worked around from outside:

- `RunManager` is a static singleton. Every state-modifying engine call
  routes through `RunManager.Instance`
  (`src/Sts2Headless.Runtime/Bindings/Sts2Bindings.cs`).
- Godot's `SceneTree`, async scheduler, sync context, and `TestMode.IsOn`
  are all process-globals
  (`src/Sts2Headless.Runtime/Loading/RuntimeBootstrap.cs`).
- RNG state is process-global — *not* part of any saved combat state and
  not externally addressable.
- No `DeepCopy` / `Cloneable` on `Combat` or `RunState`.
- `Session.cs` is explicitly single-slot; the "no runId routing" comment
  flags multi-session as future work, not near-term.

**Crucially, this constraint is transport-agnostic.** It holds:

- If you talk to the host via JSON-RPC over stdio (today's shape).
- If you compile a `BattleSimulator.dll` via NativeAOT and load it from
  Python via `ctypes`. (Each "simulator handle" still has to own its own
  OS process for the singleton state.)
- If you embed .NET runtime in Python (Python.NET) and call sts2 directly.
  (Same singleton-per-process collision.)
- If you compile to WASM. (WASM has its own process model, but the
  engine wasn't designed to run inside one and the Godot dependencies
  would have to be stripped or stubbed.)

Parallelism therefore lives at the *process* level — which is exactly
what `HostPool` already does
(`src/Sts2Headless.Agents/Hosting/HostPool.cs`). Branching lives at
"spawn another process and replay to a checkpoint," not in-memory fork.

## The replay system is asymmetric

`src/Sts2Headless.Replay/` reads and writes the engine's `.mcr` binary
format, but the read path is asymmetric:

- ✅ `CombatReplayBytes.Write` — live combat → `.mcr` bytes via the
  engine's own `PacketWriter<CombatReplay>`.
- ✅ `CombatReplayReader` — `.mcr` bytes → in-memory `CombatReplay`
  object (used for timeline emission and inspection).
- ❌ No "load this `.mcr` into a fresh combat and continue stepping."
  The engine's own `NMultiplayerTest.RunReplay` does this internally,
  but it needs the Godot main loop pumping, which the headless host
  deliberately does not do.

`NetFullCombatState` (the FR-11 determinism-canary primitive) is also
not a restore-able state. It carries creature HP / block and player
energy / gold (`src/Sts2Headless.Replay/CombatTimelineEmitter.cs`) —
just enough for checksum-based divergence detection, not enough to
reconstruct hand, deck, powers, or RNG.

**Today, "branch from this combat state" means: spawn a worker,
`run/new` with the same seed, replay every action, then apply your
hypothetical action.** Per-rollout cost scales with full run length.

This could in principle be improved by reverse-engineering an
intra-process replay path that bypasses the Godot frame loop, or by
finding an engine-internal save/restore API exposed by `SaveManager`.
Neither is investigated. Both are non-trivial.

## Engine cost rules out MCTS-against-real-engine

Even if branching were free, per-step cost rules out search:

- `Sts2Bindings.Combat.EndTurn`
  (`src/Sts2Headless.Runtime/Bindings/Sts2Bindings.Combat.cs`) pumps the
  engine with up to 500 iterations × 2ms sleeps to settle async work.
  Realistic steady-state EndTurn cost is well below the 1s ceiling, but
  not microseconds.
- `MechanicSweep` measures 5–20 seconds for the full per-card fixture
  (setup + 5 turns + card play) — most of that is setup, but per-action
  cost is still in the milliseconds-to-tenths-of-seconds range, not
  microseconds.

Modest MCTS workloads:

| Rollouts | Steps/rollout | Total transitions | At 10ms each | At 1ms each |
|---------:|--------------:|------------------:|-------------:|------------:|
| 1,000 | 20 | 20,000 | 200s | 20s |
| 10,000 | 20 | 200,000 | 2000s | 200s |

The AD-9 per-decision budget is 30s. The math does not work at any
plausible step cost, on any plausible MCTS scale. This is independent
of the wire / FFI question.

**This is why `BattleAgent.Core` exists.** Its csproj literally says
"Pure C#, no host or sts2.dll dependency." It's not a redundant fork;
it's the architectural admission that real-engine search is infeasible.

## Cross-language analysis (why FFI doesn't escape the constraints)

There are roughly four candidate shapes for cross-language access. None
of them improve on the fundamental tradeoffs:

| Shape | Per-call overhead | Singleton-escape? | Engineering cost | Useful for |
|---|---|---|---|---|
| **JSON-RPC over stdio** (today's wire + new `combat/predict`) | ~1 ms wire + replay-to-state | No | Days | Single-action queries, debugging, calibration |
| **NativeAOT shared library + per-language FFI** | ~µs (no JSON) | **No** — one combat per FFI-loaded process | Weeks (C ABI design, NativeAOT reflection limits, cross-platform builds) | Same things as wire, marginally faster, much more setup |
| **Embedded .NET runtime** (Python.NET, Rust .NET hosting) | ~µs | **No** — same process-global state | Weeks per host language | Same |
| **WASM** | Native-ish | **No** — Godot deps would need stripping | Many weeks | Hypothetically: in-browser replay analysis |

The pattern: **whatever transport you pick, the per-instance unit is
still "one OS process holding one combat."** That's the binding
constraint. JSON-RPC happens to be the shape that costs the least
engineering for the most language coverage. FFI / WASM / embedding buy
nothing the wire doesn't already buy, because the bottleneck isn't
serialization.

## What can actually be shipped (tiers)

Given the constraints, here's the tier list of useful artefacts, easiest
first:

### Tier 1 — `combat/predict_action` wire method

Add a host method that applies one hypothetical action in a *forked
sibling worker*, returns the resulting state, and discards the sibling.
Per-call cost: spin up a worker, `run/new`, replay-to-state, apply
action. Order of seconds for short runs; longer for late-game.

- Language-agnostic via the existing JSON-RPC wire ✓
- Ground truth via real `sts2.dll` ✓
- Parallelism via `HostPool`'s existing pool ✓
- Not viable for tree search.

Useful for: debugging tools, "what does this card actually do?"
introspection, the FR-12 explainability surface, calibrating
`BattleAgent.Core` against engine reality.

### Tier 2 — warm sibling pool with bulk-replay

`HostPool` keeps N workers warm. Each worker supports
`clone_from(other_worker)` by bulk-replaying a snapshot without
spawning a fresh process. Per-call latency drops from seconds to
tens-of-ms for short combats. Still not MCTS-viable.

Requires either an intra-process replay path that bypasses the Godot
frame loop (research not done) or aggressive pump-skipping in the
existing replay system. Larger engineering investment than Tier 1.

### Tier 3 — hybrid (what MCTS actually wants)

Two simulators, used for different jobs:

- **Tree expansion** runs `BattleAgent.Core` (pure C#, microseconds per
  step, lossy).
- **Ground-truth verification** runs Tier-1/2 against `sts2.dll` —
  *only* for the action actually played. Did the offline model agree
  with reality?
- **Drift correction** logs divergences and patches `BattleAgent.Core`'s
  card / relic / power logic.

This is the AlphaZero pattern — fast learned/heuristic model for
search, ground truth for moves actually taken. Given the engine speed
floor, it's the architecturally honest answer to "how do you do MCTS
on `sts2.dll`."

For non-C# agents to do this they'd still need an offline simulator in
their language. That's a real cost the harness can't paper over. The
mitigation is to make `BattleAgent.Core`'s behaviour
*specification-grade* (well-documented transitions, comprehensive test
coverage) so porting becomes mechanical rather than reverse-engineering.

## Distribution / AD-3 / AD-4 implications

Whichever tier ships, none of the existing ADs block it:

- **AD-3** (game-version pin): each user's `vendor/sts2.dll` is their
  own. A `BattleSimulator` artefact (whether `.dll`, native shared lib,
  or wire method) loads `sts2.dll` from `vendor/` the same way the host
  exe does today. Game-version-pinning behaviour transfers cleanly.
- **AD-4** (no compile-time `sts2.dll` reference): the existing
  reflection-based loading pattern (`Sts2Headless.Runtime`) is exactly
  what a shipped `BattleSimulator` would need. No new compile-time
  reference; everything via `System.Reflection` /
  `AssemblyLoadContext.Default.Resolving`.
- **AD-6** (C# is source of behavioural truth): unchanged. Whether the
  battle simulator is a C# wire method or a NativeAOT shared library,
  the C# source remains canonical. Python/Rust/etc. consumers read the
  result; they don't author it.

**Precedent**: `Sts2Headless` (the host exe) is exactly this kind of
artefact today. It ships as `Sts2Headless.dll`, expects `sts2.dll` at
runtime from a known location, and never references sts2 symbols at
compile time. A `BattleSimulator` library does the identical dance.

## Recommendations

1. **Drop "MCTS against the real engine" as a goal.** It's unachievable
   regardless of language or transport. Keep `BattleAgent.Core` as the
   search simulator.

2. **Ship `combat/predict_action`** (Tier 1) when there's demand. Cheap
   on top of `HostPool`, lives on the existing wire, every language
   gets it for free, useful for the cases sts2.dll-grade ground truth
   actually matters for: debugging, explainability, calibration.

3. **Frame offline + engine as collaborators**, not as competing
   simulators. The engine is the oracle; `BattleAgent.Core` is the
   search head. They have different jobs and should be documented as
   such.

4. **Invest in `BattleAgent.Core` as a porting target.** If we want
   non-C# agents to do search, the realistic lever is to make
   `BattleAgent.Core`'s combat math well-specified and well-tested so
   a Python or Rust port is mechanical, not interpretive. (Adjacent to
   the "behavioural truth is C#" posture from AD-6 — externalising
   that truth into something portable is a meaningful follow-up.)

## Open questions

- **Intra-process replay without Godot pump.** Is there an
  engine-internal path that re-executes recorded actions against a live
  combat without going through the frame loop? If yes, Tier 2 becomes
  much cheaper. Investigation would mean reading the engine's
  `NMultiplayerTest.RunReplay` and the `SaveManager` surface via the
  probe commands.

- **Mid-run RNG state access.** The RNG is process-global and not
  externally addressed today. If a probe pass found the RNG object and
  its state were addressable via reflection, snapshot/restore of a
  *full* combat (including RNG) becomes possible — which would unlock
  cheaper branching. Not investigated.

- **Whether `combat/predict_action` belongs on the host wire or the
  agent adapter wire.** Today's agent adapter (FR-2 in
  [04-evaluation-harness.md](../requirements/04-evaluation-harness.md))
  is pure stimulus → response — agents don't call back into the host.
  If predict is added, the question becomes: does the harness forward
  predict calls between agent and host, or does the agent get its own
  host endpoint? Out of scope for this note; flagged for whichever AD
  introduces predict.

## See also

- [02-architecture-decisions.md](../requirements/02-architecture-decisions.md)
  — AD-1 / AD-3 / AD-4 / AD-6 / AD-8 / AD-9 are the constraints this
  note threads through.
- [04-evaluation-harness.md](../requirements/04-evaluation-harness.md)
  — FR-2 (agent contract) and FR-9 (per-agent timeout overrides for
  MCTS planners). The harness is unaffected by this analysis; agent
  internals are out of scope.
- [replay-recording-and-viewing.md](./replay-recording-and-viewing.md)
  — companion note on how the engine's own replay format is hooked.
