# Evaluation Harness & Leaderboard

This document captures the *what* of the planned evaluation harness — the
feature that drives many agents through many seeds in parallel, collects
results, records replays, and (eventually) publishes leaderboards. It is
deliberately scoped to behaviour and contracts. The *how* — concrete C# /
Python project layouts, the exact agent-subprocess wire dialect, the CI
plumbing — is pinned in
AD-9 in [02-architecture-decisions.md](./02-architecture-decisions.md).

Where this document records concrete defaults (timeouts, character set,
ascension, parallelism cap, scoring function), they are baselines exposed
through a single `EvaluationHarnessConfig` object (see [FR-1](#fr-1--matrix-execution--evaluationharnessconfig));
callers override what they care about.

## Motivation

The five goals in [01-initial-goals.md](./01-initial-goals.md) are mostly
plumbing-shaped: load the engine headless (G1, G2), run N in parallel
(G3), expose a typed wire protocol with bindings (G4), record replays
(G5). The plumbing is now in place — `HostPool` parallelises hosts,
`AgentDriver.PlayRunAsync` runs a single agent end-to-end, AD-8 captures
`.mcr` + `.run` artefacts automatically, and the OpenRPC schema (AD-5)
gives any language a typed view of the wire.

What's missing is the *consumer* of all that plumbing: a system that asks
the questions the plumbing was built to answer.

- "How good is agent X compared to agent Y across a fixed seed bank?"
- "Did this commit regress the Ironclad agent's Act 1 win rate?"
- "If I write an agent in Rust in a separate repo, can I plug it in
  here and compare it against the C# reference agents without
  rewriting it in C#?"
- "Where are the systematic crash patterns in the engine that only
  surface across a few hundred runs?"

Today, answering any of these requires hand-rolling a script
(`examples/run_all_agents.py` is the closest existing prototype). The
result is one-shot scripts, no comparability across runs, no shared
result format, no replay-linked evidence, no leaderboard.

The evaluation harness fills that gap.

## Scope

### In scope

- **Single matrix execution**: take a set of agents, a set of seeds, a
  set of characters / ascensions / modifiers; run every cell to
  termination; collect results.
- **Cross-language agent plug-in**: any agent that implements the
  documented adapter contract is a first-class participant. C#, Python,
  Kotlin, Rust, anything that can read NDJSON from stdin and write NDJSON
  to stdout. Agents from external repos plug in via a small manifest
  file, not by being vendored into this tree.
- **Crash isolation**: a crash in any agent, any host process, or the
  engine itself confines its damage to a single matrix cell. The harness
  records the crash and continues.
- **Automatic replay capture**: AD-8's `.mcr` + `.run` + `manifest.json`
  per-run substrate is wired into every eval cell, transparently. Agent
  authors do not configure paths; the harness places artefacts under a
  per-eval timestamped tree.
- **Result aggregation**: per-agent summaries (win rate, mean floor,
  crash rate, wall-clock cost) plus a per-cell row-level table suitable
  for both human reading and machine ingestion. One canonical JSON
  artefact per eval that downstream tools (plots, leaderboards) consume.
- **Reference agent suite**: the in-repo agents (Greedy, Ironclad
  battle agent, Random, …) are always available as a baseline so a new
  agent has something to compare against without separately setting it up.
- **Leaderboard surface**: a published artefact (initially: local
  static HTML; eventually: a GitHub-Pages-hosted dashboard) that ranks
  agents on a documented scoring function and links to evidence
  replays.

### Out of scope (initial; revisit later)

- **Training infrastructure for RL.** This harness *evaluates*; it does
  not provide an RL training loop. Training is the agent author's
  responsibility — they bring trained weights, we run inference. (The
  harness is a useful evaluation surface *for* RL projects without being
  one.)
- **Live tournaments / matchmaking between agents.** STS2 is a
  single-player game; there is no agent-vs-agent shape to play. Each
  agent plays its own run; ranking is across results, not across
  matches.
- **Replay rendering inside the eval pipeline.** The eval records `.mcr`
  + `.run`; viewing them is the replay viewer's job
  (`tools/replay-viewer/`) and a downstream pixel-accurate Godot mod
  (per AD-8). The leaderboard *links* to replays; it does not host a
  player.
- **Mid-run human intervention.** Agents play to completion without
  step-through. If an author wants debugging, they use the existing
  drivers + viewer on a single seed.
- **Anonymous / hidden seed banks.** No "secret evaluation set". Seeds
  are committed in this repo and any author can run the same matrix
  locally before submitting. Transparency wins over anti-overfitting
  guarantees for a hobby-scale project.

## Functional requirements

### FR-1 — Matrix execution + `EvaluationHarnessConfig`

The harness takes a single `EvaluationHarnessConfig` object and runs the
matrix it describes. A cell is one combination of `(agent, seed,
character, ascension, modifiers)`. Defaults collapse the matrix to the
minimum a caller wants to think about — an eval that says "run agents A
and B on seeds 1..10" is a 20-cell Ironclad-A0-no-modifiers matrix.

`EvaluationHarnessConfig` is the single source of truth for what an eval
*is*. The harness has no implicit globals and no hard-coded knobs;
everything tunable goes through here. Indicative fields (exact schema
deferred to the implementation ADR):

- **Matrix axes**: agent set, seed bank reference, character set
  (default: `[Ironclad]`), ascension set (default: `[A0]`), modifier
  sets (default: `[]`).
- **Budgets**: per-decision timeout, per-cell wall-clock cap,
  per-cell hard step cap. All have library defaults; all are
  caller-overridable.
- **Parallelism**: worker cap, RAM ceiling.
- **Scoring function**: which `IScoringFunction` implementation to use
  for the leaderboard sort (see FR-6).
- **Output**: eval root (default `replays/eval-harness/`), eval-id
  generator (default: wall-clock timestamp).
- **Toggles**: determinism canary on/off (FR-11), per-decision notes
  capture on/off (FR-12).

The config is a structured input (file or in-process object), not
implicit. A canonical run that publishes its `EvaluationHarnessConfig`
alongside the results is bit-identical (modulo wall-clock noise) to
anyone else's run of the same config against the same `GAME_VERSION`
pin — that is the definition of a reproducible result.

### FR-2 — Agent adapter contract

An agent is *any* process that the harness can spawn and that speaks the
documented `agent/*` wire dialect over stdio:

1. The harness spawns the agent subprocess once per cell, passes
   per-cell context (game version, character, seed, ascension,
   per-action timeout) in an `agent/init` message.
2. The harness drives the host (existing `AgentDriver` loop, existing
   `run/state` snapshots) and forwards each snapshot to the agent as an
   `agent/decide` request.
3. The agent replies with one `AgentAction` (the same closed union the
   in-repo `Sts2Headless.Agents` already uses) plus an optional free-text
   `notes` field.
4. The harness applies the action against the host and proceeds until
   termination.
5. The harness sends `agent/teardown` and waits a bounded time for the
   agent to exit cleanly.

What this buys us:

- **Language-agnostic.** Anything that reads stdin and writes stdout
  can be an agent. Existing in-repo C# / Python clients already carry
  the typed DTOs; the adapter layer for them is a thin loop.
- **Crash-isolated.** The agent is a separate OS process. A
  segfault, an unhandled exception, an OOM kill — none of these can
  hurt the host process or any other cell.
- **Stateful by design.** The agent process is kept alive for the
  full cell so a planner can cache search trees, opponent models,
  pre-computed lookups, partial MCTS rollouts, etc. across decisions.
  An agent that wants to plan ahead pays the cost once at
  `agent/init` and amortises it across every `agent/decide` for that
  cell.
- **Tractable for external authors.** "Implement this on stdio" is
  the same contract every Slay-the-Spire-1 bot author already knows
  from `CommunicationMod`. Familiar precedent.
- **Cohesive with AD-2.** The existing host wire is NDJSON over
  stdio; the agent wire is the *mirror* dialect over stdio. Two halves
  of the same shape.

The exact set of `agent/*` methods, the snapshot envelope, and the
action envelope are deferred to the implementation ADR. The
*requirement* is that the dialect is published, schema'd (an OpenRPC
sibling to `protocol/openrpc.json`), and stable enough that an external
adapter can be written against a versioned contract.

### FR-3 — Agent registration

Each agent declares itself via a small manifest file, addressable by a
filesystem path (in-repo) or a git URL (external repo). The manifest
carries:

- **Identity**: name, version, optional author / repo URL, optional
  free-text description.
- **Spawn instructions**: command line, working directory, environment
  variables. Sufficient for the harness to run the agent without
  language-specific code paths in the harness itself.
- **Declared capabilities**: which characters the agent supports,
  which ascensions, optional per-character notes (e.g. "trained for
  A0, undefined behaviour above A10").
- **Resource hints** (optional): expected per-decision wall-clock, RAM
  footprint. The harness uses these to schedule (e.g. don't run two
  4-GB-RAM agents simultaneously on an 8-GB box).

The manifest is the *single registration unit*. In-repo reference agents
ship with their manifest committed under (proposed)
`documentation/eval/agents/<name>.json`. External agents are added by
dropping a manifest into the same directory (or pointing the harness at
an external manifest path).

### FR-4 — Seed banks

Seeds are committed into the repo, partitioned into named banks:

- **`smoke`** (~5–10 seeds) — fast inner-loop check; used by PR CI and
  by an author probing "did I break the basic plumbing".
- **`reference`** (~50 seeds) — the everyday comparability bank; sized
  to run a full matrix of reference agents in under an hour on a
  workstation.
- **`deep`** (~500+ seeds) — the published-leaderboard bank;
  multi-hour, run on a schedule, not in PR CI.

Each bank is a versioned JSON file under (proposed)
`documentation/eval/seeds/<bank>.json`. Seeds may be added but not
removed or reordered, so a result from yesterday remains comparable to a
result from today on the same bank. (When a bank's content materially
changes, it's a new bank with a new name, not a silent edit.)

Per-bank metadata: name, creation date, generation method (e.g.
"first 50 seeds where Ironclad A0 has at least one Neow choice with a
boss relic"), pinned `GAME_VERSION`.

### FR-5 — Per-cell result

Each cell yields a structured result row:

- **Identity**: agent name + version, seed, character, ascension,
  modifiers, eval-id, game-version SHA-256, host-process PID, agent
  git-SHA (if discoverable).
- **Terminus**: a closed enum — `Victory`, `Death`, `Abandoned`,
  `Stalled` (StallDetector tripped), `MaxSteps` (hard step cap),
  `Timeout` (wall-clock budget), `EngineCrash` (host returned a wire
  error indistinguishable from a genuine engine bug), `HostCrash` (host
  process died), `AgentCrash` (agent process died), `HarnessError` (the
  harness itself failed to set up the cell).
- **Run metrics**: floor reached, final HP, max HP, gold, deck size,
  relic count, combat count, elite count, boss count, total turns spent
  in combat, total wall-clock.
- **Resource accounting**: peak RSS of the host process, peak RSS of
  the agent process, total CPU-time. Sourced from /proc-equivalent on
  the harness side; "best-effort, not load-bearing".
- **Evidence**: relative path to the cell's replay subdirectory
  (containing `manifest.json`, `run.json`, `combats/*.mcr` per AD-8)
  and the failing wire error / stack trace if applicable.

Rows are written incrementally to `runs.jsonl` as cells complete, so a
partial eval is inspectable mid-flight.

### FR-6 — Per-eval aggregate

After (or during) a matrix run, the harness emits per-agent aggregates:

- **Win rate** (% of cells reaching `Victory` — i.e. beating the Act 3
  boss per `sts2-game-facts.md`).
- **Survival floor**: mean / median / p25 of floors reached.
- **Crash rate**, split by attribution (engine / host / agent /
  harness).
- **Timeout rate**.
- **Cost**: median per-cell wall-clock and peak RSS.
- **Per-cohort breakdown**: per character, per ascension, per
  modifier-set. Hidden when the matrix only spans one value.

Aggregates land in:

- **`summary.md`** — sorted leaderboard table, deterministic-ordered for
  diffability, intended for humans (and for `cat`-friendly CI logs).
- **`summary.json`** — same data, structured; the single artefact
  downstream tools consume.

The leaderboard sort is delegated to a pluggable `IScoringFunction`
implementation, selected via `EvaluationHarnessConfig`. The interface
takes the full per-cell row collection and returns an ordered list of
per-agent aggregate rows; ties are broken deterministically by the
implementation. The harness ships a default implementation
(**lex-sort(win-rate desc, mean-floor desc, mean-wall-clock asc)** —
correctness first, depth second, efficiency as tiebreak) and a
slot for callers to register their own — weighted-sum, character-
stratified, ascension-weighted, whatever — without touching the
orchestrator. Every published result records the name + version of
the scoring function it used, so a leaderboard isn't ambiguous about
its own sort rule.

### FR-7 — Replay capture

Replays are automatic and addressable.

- The harness sets `STS2_REPLAY_OUT` and `STS2_REPLAY_AGENT` per cell
  before spawning the host (the existing AD-8 contract). Per-cell
  output lands under `<eval-root>/<eval-id>/cells/<agent>/<seed>/...`.
- **Default `eval-root`**: `replays/eval-harness/` at the repo root.
  The top-level `replays/` directory is gitignored. Distinct from
  AD-8's default `replays/manual/` (which is the bucket for ad-hoc /
  `record-all`-style runs) — keeping eval output in its own bucket
  prevents an eval-id from ever colliding with a manual recording and
  makes the eval tree safe to `rm -rf` without touching anything else.
- `<eval-id>` is wall-clock timestamp at eval start (sortable,
  collision-free across humans on different machines). Caller can
  override the eval-id generator via `EvaluationHarnessConfig` for
  CI scenarios that prefer a build-number or git-SHA stamp.
- Cell results carry a relative path to the cell's replay subdirectory
  so a leaderboard row can link to evidence directly.
- AD-8's `manifest.json` already carries `seed`, `character`,
  `game_version`, agent name. The harness writes one *additional*
  sibling file per cell, `cell.json`, that joins those to the
  eval-specific context (eval-id, ascension, modifiers, agent
  version, terminus, scoring metrics) — i.e. it's a denormalised
  forward index from `cells/<agent>/<seed>/` back to the row in
  `runs.jsonl`. This is the only file the harness owns inside the
  replay subdir; everything else is AD-8's bytes.

### FR-8 — Parallelism

The harness exploits multiple cores by running cells concurrently. The
implementation reuses (or extends) `HostPool`'s bounded-concurrency
model — one OS process per active host, one OS process per active
agent, capped by the `EvaluationHarnessConfig` worker cap.

- **Default cap**: conservative — `min(matrix-size, ⌊cores / 2⌋)`. Each
  cell holds *two* processes (host + agent) and the host alone is
  hundreds of MB resident.
- **Throughput target**: a 50-seed × 4-agent reference matrix completes
  within "a coffee" on an 8-core workstation. A 500-seed deep matrix
  completes overnight.
- **No global serialisation point.** Cells share the eval root for
  output but nothing more — no shared file lock, no shared port. The
  only contention is RAM and the filesystem.

Parallelism is opt-up, not opt-down: a `--workers 1` invocation is
the supported reproducer for "I want to step through what the
harness is doing".

### FR-9 — Robustness boundaries

The harness commits to specific failure containment.

- **An agent that throws / segfaults / OOMs** fails *its cell* with
  terminus `AgentCrash`, with the captured stderr in the row. Other
  cells run unaffected.
- **A host that crashes** fails *its cell* with terminus `HostCrash`
  (detected via stdout EOF, per the existing transport's behaviour).
  Other cells run unaffected. The harness does *not* restart the host
  for that cell — a crashed host is a result, not a retry trigger.
- **A wire error returned by the host** (engine NRE wrapped as
  `InternalError`, etc.) fails *its cell* with terminus `EngineCrash`,
  with the wire-error payload captured. Other cells unaffected.
- **A stall** (StallDetector trips) fails *its cell* with terminus
  `Stalled`, with the offending fingerprint captured. The detector is
  already wired automatically by `AgentDriver.PlayRunAsync` and gets
  inherited for free.
- **A wall-clock timeout** is enforced per-cell at two layers: a soft
  per-decision budget (the agent must reply to an `agent/decide`
  within N seconds) and a hard per-cell budget. Both are pulled from
  `EvaluationHarnessConfig`; both can also be overridden per-agent via
  the agent manifest (so an MCTS-heavy planner can ask for a longer
  per-decision window without raising the whole matrix's budget).
  Library defaults are a starting point for the implementation ADR,
  not load-bearing here — every caller can tune them.
- **A harness bug** (e.g. failure to write `runs.jsonl`) is logged
  loudly and fails the entire eval with a clear distinguishing exit
  code. The harness does not silently degrade.

The exception is the *harness orchestrator process itself*: if it
dies, the eval is over. Mitigations are appropriate-for-batch-tools
(e.g. `runs.jsonl` is append-only and inspectable even after a kill;
already-recorded replays are intact under AD-8). A resumable eval is
not promised in the MVP.

### FR-10 — Reproducibility & provenance

Every published result is independently re-runnable.

- **Game-version pinning** is non-negotiable. Every cell row records
  `game_version` + `sts2_dll_sha256`. A result row from version A
  cannot be compared row-for-row against version B; the aggregate
  layer refuses to mix.
- **Agent versioning** is recorded best-effort. For in-repo agents,
  the eval-time git SHA. For external agents, whatever the manifest
  declares — at minimum the agent's self-reported `version` string.
- **Seed bank versioning**: every aggregate row records which named
  bank it ran against. Cross-bank comparisons are explicit, not silent.
- **`EvaluationHarnessConfig` capture**: the exact config that drove
  the eval is serialised into `<eval-id>/config.json` alongside the
  results. Re-running with that file is the canonical reproduction
  step.

The combination is sufficient to reproduce any published number locally
(assuming the same `GAME_VERSION` pin and the same agent versions).

### FR-11 — Determinism canary (optional, low priority)

For the in-repo reference agents, the harness optionally re-runs each
cell twice and asserts the `RunHistory` is identical. AD-8's
`NetFullCombatState` checksums are the primitive that makes this
detection cheap.

This is *not* a default-on feature — it doubles cell wall-clock. It is
the kind of thing that lands in nightly CI on a small "canary" subset
to surface determinism regressions in the host or in the agent.

### FR-12 — Notes / annotations (deferred to v2)

Agents may attach a free-text `notes` field per decision response. The
harness collects them into a `decisions.jsonl` per-cell sidecar.
**Default off**, opt-in via `EvaluationHarnessConfig` (to avoid bloating
replay trees with NDJSON the author never reads).

This is intentionally minimal in v1 — just a free string. A structured
"explain my move" surface (linked to specific cards / enemies / hooks)
is a v2 question. The hook needs to exist now so v2 can fill it in
without a protocol bump.

## Non-functional requirements

### NFR-1 — Output is the API

The contract a downstream tool depends on is the JSON shape of
`summary.json` + `runs.jsonl`, not the harness binary. Adding columns is
allowed; removing or renaming columns is a breaking change with the
same discipline as the wire protocol (AD-5).

### NFR-2 — Scales to the largest seed bank in one job

A `deep` bank run (~500 seeds × N agents) is a single invocation, not a
hand-managed batch. The harness owns batching internally.

### NFR-3 — Fits the existing tooling shape

- `just eval::...` recipes follow the existing module-per-concern
  pattern of `just runner::`, `just validation::`, `just build::`.
- Reports land at `replays/eval-harness/<eval-id>/`. The top-level
  `replays/` directory is gitignored (the per-cell replay subdirs
  under it carry game bytes derived from `vendor/sts2.dll`, same
  proprietary-derivative posture AD-8 takes for `replays/manual/`).
- The harness obeys AD-7: the host it spawns runs *without*
  `--enable-debug` by default. An eval with `--enable-debug` is a
  diagnostic affordance, never the leaderboard pipeline.

### NFR-4 — CI-friendly

The harness's exit code is meaningful (`0` = matrix executed cleanly,
non-zero = at least one cell hit `HarnessError`, never non-zero on cell
crashes — those are *expected results*, not harness failures). The
distinction matters: a CI gate that fires on agent crashes is hostile
to development on the harness itself.

## Constraints inherited from existing decisions

The harness is *built on top of* the AD chain and cannot relax any of:

- **AD-1**: orchestrator core is C#. Cross-language plug-in is via the
  wire boundary, not by hosting Python or Kotlin in-process.
- **AD-2**: NDJSON-over-stdio is the wire shape. The agent adapter
  dialect is a mirror, not a parallel format.
- **AD-3**: `GAME_VERSION` pin defines a "comparable result class".
  Aggregates do not mix versions.
- **AD-4**: harness code never references `sts2.dll` symbols at
  compile time.
- **AD-5**: any new wire surface (the agent dialect) gets its own
  OpenRPC schema, mirroring the host protocol's discipline.
- **AD-6**: the harness *itself* is authored in C#, lives under
  `src/Sts2Headless.Eval/`. The reference agents are C#. Python
  consumers exist downstream of the JSON artefacts (plots,
  leaderboard rendering, ad-hoc analysis); they read `summary.json`,
  they do not author canonical eval scenarios. Python eval scripts
  like `examples/run_all_agents.py` are user tools, not the source of
  truth.
- **AD-7**: production eval invocations never pass `--enable-debug`.
  The debug gate is sacred; a leaderboard built off cheating runs is
  worthless.
- **AD-8**: `.mcr` + `.run` + `manifest.json` is the replay format.
  The harness writes only `cell.json` on top of that, never a
  parallel replay format.

## Resolved choices: language split and CI shape

The two specific uncertainties surfaced in the original brief are
resolved as follows. The full reasoning is kept here because each
involves a real tradeoff worth recording.

### Q1 — Language for the harness: C# (with Python consumers)

The orchestration layer (matrix spec parsing, agent-subprocess management,
result collection, replay layout, parallel scheduling) belongs in C#:

- **AD-6 already says so.** The behaviour-truth boundary AD-6 draws
  applies as strongly to "what is a correct run" as to "what is a
  correct game state".
- **The plumbing is already C#.** `HostPool`, `AgentDriver`,
  `MechanicSweep`'s sweep-then-aggregate pattern are all here.
  Re-implementing them in Python would mean either drift (two
  implementations) or re-shelling out (defeats the point).
- **Crash handling discipline.** The C# transport layer's wire-error
  taxonomy is exactly the discrimination an eval harness needs.
  Surfacing it through Python would re-encode it.

The downstream layer (plots, leaderboard rendering, statistical
analysis, GitHub Pages site generation) belongs in Python:

- **Python's tooling is better here.** matplotlib / plotly / pandas /
  jinja2 are mature; equivalent C# tooling exists but is markedly less
  ergonomic for the "plot a histogram, render an HTML page" job.
- **It's downstream of the JSON.** A Python plot script reading
  `summary.json` is not authoring behaviour, so AD-6 doesn't
  preclude it.
- **External contributors can fork the plotting layer** without
  touching the orchestrator. This is the right division: the
  contract is the JSON, the renderer is interchangeable.

Practically: new C# project `src/Sts2Headless.Eval/` for the
orchestrator (plus `tests/Sts2Headless.EvalTests/`); new Python
workspace member `clients/python/headless-in-the-spire-leaderboard/`
for plots + static site. Both arrive in the same milestone; the JSON
contract between them is the API.

### Q2 — CI integration: nightly scheduled, results published to GitHub Pages

The realistic shape:

- **PR-gated smoke eval**: every PR runs the `smoke` seed bank against
  the in-repo reference agents (no external agents) and posts a
  delta comment vs. main. Fast (≲5 min) and free in standard GHA;
  catches gross regressions in the agents.
- **Nightly / on-tag `reference` eval**: runs the `reference` bank
  against in-repo + opted-in external agents. Publishes the resulting
  `summary.json` + `summary.md` + selected interesting replays to the
  `gh-pages` branch. Total cost ≲1 hour.
- **Manual `deep` eval**: triggered by a workflow_dispatch (or run
  locally). Publishes to a separate `deep/` path on gh-pages.

The hard infrastructure question is **how the runner gets
`sts2.dll`**. Standard GitHub-hosted runners can't run sts2 (no Steam
install, and AD-3 forbids checking in the bytes). Options, ordered by
plausibility:

1. **Self-hosted runner** with a Steam install. One machine, the same
   one that runs `just validation::test` today. This is the path of
   least resistance.
2. **Scheduled job on a workstation** that does the run locally and
   pushes results to gh-pages from a token. Doesn't depend on GH
   Actions runner infra at all; just needs a cron-like trigger.
3. **Vendor-mirror access from a self-hosted runner.** See
   [vendor-mirror-setup.md](../runbooks/vendor-mirror-setup.md) for
   the existing pattern.

The CI plumbing is *strictly downstream* of FR-1..FR-12. None of those
requirements change based on the CI choice. The implementation ADR
should ship them and treat CI publication as a follow-up.

## Resolved foundational choices

In addition to the language split (C# orchestrator + Python leaderboard
renderer) and the CI shape (nightly scheduled + gh-pages publication)
covered above, the following are decided:

1. **Agents are stateful.** One process per cell, kept alive for the
   full cell, so a planner can pre-compute and cache across decisions.
2. **All operational knobs live on `EvaluationHarnessConfig`.** No
   hard-coded defaults that callers can't override — timeouts,
   per-cell wall-clock cap, worker count, output root, character
   set, ascension set, scoring function. The config is one object;
   the harness has no implicit globals.
3. **The scoring function is a pluggable `IScoringFunction`.** The
   harness ships a default implementation
   (`lex-sort(win-rate desc, mean-floor desc, mean-wall-clock asc)`)
   and accepts caller-supplied alternatives — weighted sums,
   character-stratified rankings, whatever — through the config. The
   leaderboard always records which scoring function produced it.
4. **Default character set is `[Ironclad]`.** Multi-character matrices
   are opt-in via the config's character-set field. Agent manifests
   declare which characters they support; the harness skips cells
   for unsupported characters.
5. **Default ascension is `A0`.** Higher ascensions are opt-in via the
   config's ascension-set field.
6. **Output tree is `replays/eval-harness/<eval-id>/`** at the repo
   root; the top-level `replays/` directory is gitignored. Distinct
   from AD-8's `replays/manual/` (ad-hoc / `record-all` bucket) so the
   eval tree is safe to wipe in isolation.
7. **MVP cut: FR-1 through FR-10.** FR-11 (determinism canary) and
   FR-12 (notes / annotations on decisions) slip to v2. They are
   sketched in this document so v2 can pick them up without a
   protocol bump.
8. **The agent adapter dialect is specified in the implementation ADR,
   not here.** This document commits to *having* one; the exact method
   names, payload shapes, error codes, and OpenRPC sibling schema land
   in AD-9 in [02-architecture-decisions.md](./02-architecture-decisions.md).

## Resolved by AD-9

AD-9 in [02-architecture-decisions.md](./02-architecture-decisions.md)
pins the *how* this document deferred:

- The agent adapter dialect — `agent/init`, `agent/decide`,
  `agent/teardown`, and the agent-side error code range
  (-32200..-32299).
- The `EvaluationHarnessConfig` schema, `HarnessBudgets` defaults
  (per-decision 30s, per-cell 10min, max-steps 4000), and the auto
  worker cap (⌊cores/2⌋).
- The `IScoringFunction` interface shape and the default
  `LexSortScoring(WinRate desc, MeanFloor desc, MedianWallClock asc)`.
- The `AgentManifest` + `BundledAgent` abstract-class hierarchy
  (with `CreateAgent()` for hand-written agent construction) and
  the `BuiltinAgents` registry.
- The seed bank JSON file format (committed under
  `documentation/eval/seeds/<bank>.json`).
- The `summary.json` + `runs.jsonl` + `cell.json` schemas and the
  `CellTerminus` closed set.
- C# project layout: new `src/Sts2Headless.Eval/`,
  `src/Sts2Headless.Eval.Manifests/`,
  `src/Sts2Headless.AgentRunner/`,
  `tests/Sts2Headless.EvalTests/`, and example exes under
  `examples/Eval{Smoke,Reference,Deep}/`.
- Python leaderboard package boundary:
  `clients/python/headless-in-the-spire-leaderboard/`.
- CI workflows and gh-pages layout (PR-gated smoke on self-hosted
  runner, nightly reference, manual-dispatch deep).

This document is the *what*; AD-9 is the *how*.
