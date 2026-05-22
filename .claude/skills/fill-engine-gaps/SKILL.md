---
name: fill-engine-gaps
description: Long-running autonomous pass to find and implement not-yet-wired engine features. Combines catalog/TODO scans, coverage deltas, variant-agent sweeps, probe re-runs, and ModelDb reflection to surface unknown gaps; fixes mechanical ones end-to-end (one commit each) and appends bigger choices to BLOCKED.md. Uses subagents heavily to keep main context light.
---

# /fill-engine-gaps

A long-running, autonomous pass. Run until the candidate queue is exhausted.
Use subagents for almost every step — main context only orchestrates.

## Mental model

The repo is **already heavily instrumented** for gap detection. Don't reinvent;
combine existing signals:

- `src/Sts2Headless.Protocol/Methods/*Id.g.cs` (+ matching `*Id.Fallback.cs`)
  — generated enumerations of every card, relic, power, monster, encounter,
  event, potion, enchantment, modifier, affliction, orb. Each exposes
  `AllWireNames`. Counts drift on engine bumps — read the file, don't trust a
  memorised number.
- `src/Sts2Headless.MechanicSweep/` — per-kind smoke sweeps
  (`CardSweep`, `RelicSweep`, `PotionSweep`, `PowerSweep`, `EventSweep`,
  `EncounterSweep`, `AfflictionSweep`, `EnchantmentSweep`). Each runs every
  id in its `*IdNames.AllWireNames` through a minimal fixture and classifies
  rows as Played / Crashed / Timeout / Unreachable / Unplayable /
  KnownUnsafe. Reports land in `documentation/coverage/sweep-<kind>.{md,json}`
  (gitignored). Wrappers live under `tests/Sts2Headless.MechanicSweepTests/`
  with the `[Trait("Category", "MechanicSweep")]` opt-in.
- `src/Sts2Headless.MechanicSweep/SweepKnownIssues.cs` — per-kind allowlist
  of "this id crashes for a reason we already understand". An id that
  drops off this list (engine fix) flips Crashed → Played; an id that
  shows up Crashed and *isn't* in the list is a fresh gap.
- `src/Sts2Headless.MechanicSweep/SweepRegistry.cs` — `ImplementedSweeps`
  vs `PlannedSweeps`. The Planned list is a punch-card of kinds we know
  we want a sweep for but haven't built (today: Modifier, Orb, Monster).
- `tests/Sts2Headless.IntegrationTests/Coverage/` — parity / drift tests
  (`ContentManifestDriftTests`, `EveryKindHasASweepTest`,
  `KnownIssuesParityTest`, `NewContentKindTests`, `HookSurfaceSnapshotTest`,
  `InstrumentationKindParityTest`). These fail when a manifest, sweep
  registry, or hook surface drifts out of sync — read their output first
  before launching detection passes.
- `src/Sts2Headless.Commands/probe/Probe*Command.cs` — `just probe-natural-chain`,
  `just probe-rewards-natural-chain`, `just probe-combat-stall`,
  `just probe-modeldb`, `just probe-method-body`, `just probe-callers`,
  `just probe-types`. Each surfaces a different class of gap and exits
  non-zero when found.
- `src/Sts2Headless.Agents/Driving/StallDetector.cs` + `CombatBudgetGuard.cs`
  — fingerprint-based hang detector + per-combat step budget, wrapped
  around every agent run by `AgentDriver`.
- `tests/Sts2Headless.IntegrationTests/HarnessGaps/` — red-on-purpose tests
  that document open gaps and graduate out when fixed. Today the "Open
  gaps" section is empty; the folder still holds the convention + a
  graduation history.
- `tests/Sts2Headless.IntegrationTests/MonsterPatchAuditTests.cs` +
  `MonsterPatchAuditor` — locked-in snapshot of every monster move/lifecycle
  method whose Harmony patch strips a gameplay mutation. New patches that
  expand the strip surface fail the audit; per-monster fixes shrink it.
  See BLOCKED.md "Move-body patches" entry for the active workflow.
- `BLOCKED.md` — running list of decisions the user must make before work
  can land. Same file you append "big choices" to.

A "gap" is anything in one of these categories:

1. A `MethodCatalog` / `CheatMethodCatalog` summary that says "not wired",
   "future slice", "not yet", "stub", "TODO".
2. A `HostMethods.cs` handler that throws
   `ArgumentException("...not yet supported...")` or
   `NotImplementedException`.
3. Content in an `*Id.g.cs` that the matching `MechanicSweep` reports
   as Crashed / Timeout / Unreachable / Unplayable *and* is not already
   covered by a `SweepKnownIssues.<Kind>` row.
4. A `--probe-*` re-run that exits with code 2 and writes new entries to
   `documentation/research/*-gaps.md`.
5. A new seed/agent combo that crashes the agent driver with a stall,
   unhandled exception, or an unexpected `WireErrorCode.InvalidParams`
   from a path that should work.
6. A `[Trait("Category", "Gap")]` test in `HarnessGaps/` whose
   implementation is now within reach.
7. A `ModelDb.AllX` property surfacing a content kind that has no
   `*Id.g.cs` manifest yet.

---

## Phase 0 — Orient (main context, ~5 min)

Read, in order:

1. `BLOCKED.md` — anything already deferred for human decision.
2. `documentation/coverage/sweep-*.md` if any exist (one per kind that
   has been swept recently; gitignored). Each lists Crashed / Timeout /
   KnownUnsafe rows with engine-side stack heads.
3. `tests/Sts2Headless.IntegrationTests/HarnessGaps/README.md` —
   the "Open gaps" section (often empty; the file still records the
   convention + graduation log).
4. `src/Sts2Headless.MechanicSweep/SweepRegistry.cs` — `PlannedSweeps`
   list. Each entry is a pre-scored candidate kind.
5. `git log --oneline -30` — what's recently shipped (avoid re-doing it).
6. `CLAUDE.md` "Hard rules" section (refresher).

If `BLOCKED.md`, `HarnessGaps/`, or `SweepRegistry.PlannedSweeps`
already lists open candidates, prefer those over fresh discovery —
they're pre-scored by the user or by an existing audit.

Pre-flight: `git status` must be clean. If not, stop and report.

---

## Phase 1 — Detect (parallel subagents)

Launch six investigations **in parallel** in a single message. Each writes
findings to `/tmp/fill-engine-gaps-<topic>.md`; main thread merges into a
single candidate list afterward.

### 1a — Catalog summary scan (Explore subagent)

```
grep -nE '(not wired|future slice|not yet|stub|TODO|FIXME)' \
  src/Sts2Headless.Protocol/MethodCatalog.cs \
  src/Sts2Headless.Cheats/CheatMethodCatalog.cs
```
Plus inspect the `Summary:` text of every `MethodEntry`. Each hit is a
`wire-surface` candidate. Output to
`/tmp/fill-engine-gaps-catalog.md` with file:line + the catalog summary +
a one-line proposed fix shape.

### 1b — Codebase TODO + handler stub scan (Explore subagent)

```
rg -n --no-heading -t cs \
  '\b(TODO|FIXME|HACK|NotImplementedException)\b|"not yet"|"not wired"|"not yet supported"' \
  src tests
```
Special focus on `src/Sts2Headless/HostMethods.cs` for `throw
ArgumentException(...)` patterns that block specific inputs (e.g.
characters, ascensions, modifiers). Output to
`/tmp/fill-engine-gaps-todos.md`.

### 1c — Sweep delta (Bash + Explore subagent)

Run a fast pass across every kind:

```
just sweep-sample 30        # MECHANIC_SWEEP_SAMPLE=30 across all kinds, ~10–20 min
```

For a focused re-sweep of one kind: `just sweep-cards` /
`just sweep-relics` / etc. (full universe, hours). Don't launch
`just sweep-all` from this skill unless the user asks — that's a
multi-hour pass meant for `GAME_VERSION` bumps.

Then a subagent parses the reports:

- Read every `documentation/coverage/sweep-*.json` that exists.
- Group rows by `Outcome`. Of interest:
  - **Crashed / Timeout** — a fresh entry that is NOT in
    `SweepKnownIssues.<Kind>` is a candidate. Include id, stack head,
    and a one-line proposed fix shape (fixture extension, cheat plumb,
    leaf-helper patch, …).
  - **Unreachable / Unplayable** — the sweep couldn't stage the id at
    all. Surface the reason from `row.Detail`; if the blocker is a
    missing cheat or wire surface, that's the candidate.
- Cross-check the universe count against `<Kind>IdNames.AllWireNames` —
  a mismatch means a stale manifest (`just generate-content-ids` fix).

Output to `/tmp/fill-engine-gaps-coverage.md`. Cap each section at 30
items — the goal is a candidate seed, not a full audit.

### 1d — Variant-agent sweep (Bash subagent)

Existing C# agents (under `src/Sts2Headless.Agents/Examples/`):
`GreedyAgent`, `PotionDrinkingAgent`, `CheatingHellRaisingSeed42Agent`
(special-case). All three derive from `HeuristicAgent` /
`IAgent`. Python has Random/Attack/Block but **AD-6 forbids using
those for behavioral truth**. The variant sweep here means:

- Iterate ~10 new seeds with `GreedyAgent` via the host subprocess
  (write a temporary integration test, not a script — keeps it in-house).
- Cap each at 60–90s.
- Collect `StallDetectedException`, `CombatBudgetExceededException`, any
  unhandled exception in the agent driver, and "stuck on" room/floor
  fingerprints.
- For each crash, run `just probe-combat-stall <seed> <floor>` to get
  the engine-internals dump.

Output to `/tmp/fill-engine-gaps-seeds.md`: one entry per failure with
seed, floor, exception type, and stack-trace head.

**If you want NEW agent diversity:** porting a Random / Attack / Block
agent to C# (in `src/Sts2Headless.Agents/`) is itself a high-value gap.
Note this candidate explicitly — it unlocks future detection passes.

### 1e — Probe re-runs (Bash)

```
just probe-natural-chain
just probe-rewards-natural-chain
```
Both write to `documentation/research/*-gaps.md` and exit 0 (converged)
or 2 (gaps found). Diff the regenerated files against `git HEAD`. Each
new gap is a candidate.

Skip `probe-combat-stall` here — pass 1d invokes it on demand.

### 1f — ModelDb reflection diff (Bash + Explore subagent)

```
just probe-modeldb
```
Writes `documentation/research/modeldb/*.txt`. Subagent compares:

- ModelDb `AllX` content count vs. `*Id.g.cs` enum count. If ModelDb
  has more, `just generate-content-ids` is stale.
- ModelDb kinds that have NO matching `*Id.g.cs` — wire surface
  candidate (add a new `Foo.g.cs` generator slice).

Output to `/tmp/fill-engine-gaps-modeldb.md`.

---

## Phase 2 — Triage (main context)

Merge the six `/tmp/fill-engine-gaps-*.md` files into one candidate list.
Dedupe (multiple passes may point at the same gap). For each candidate,
classify and write next to it:

### MECHANICAL — fix autonomously this pass

- A catalog "not wired" entry where the selector pattern already exists
  (see SMITH commit `83514c3` as the canonical template — queue indices
  into `HeadlessCardSelector`, add cheat for state setup, integration
  test).
- A `HarnessGaps/` test that becomes green after 1–3 file edits.
- Exposing an existing engine field on an existing DTO (snake_case + enum
  if applicable). Non-breaking.
- Extending a per-kind sweep fixture (e.g. `CardSweep.cs`,
  `RelicSweep.cs`) so a previously-Unreachable id can be staged — when
  diagnosis shows the path exists but the sweep doesn't reach it.
- Adding a row to `SweepKnownIssues.<Kind>` with a one-line reason
  when diagnosis confirms the crash is catalog-grade (off-class shape,
  missing reward pool, …) and not a real bug.
- Removing a row from `SweepKnownIssues.<Kind>` when a sweep now
  classifies the id as Played (the engine or harness fix landed
  separately).
- A debug method that surfaces a property already read internally
  (must follow AD-7: GateDebug + positive test + negative test).
- A graduating gap: `*Tests.cs` file moves out of `HarnessGaps/` and
  drops the `[Trait("Category", "Gap")]`.
- Adding a missing `*Id.g.cs` for a ModelDb kind that has a generator
  template already (look at `GenerateContentIdsCommand.cs` for the
  pattern; new kinds with no template are a BIG CHOICE).
- Regenerating stale `*Id.g.cs` via `just generate-content-ids` when
  pass 1f shows the manifest is behind the pin.

### BIG CHOICE — append to BLOCKED.md, do not implement

- New character implementation (Silent / Defect / Watcher / Regent /
  Necrobinder). The HostMethods character gate (`HostMethods.cs`,
  search for `character != Character.Ironclad`) throws for these;
  expanding requires character-specific deck/relic/UI plumbing that
  needs user direction. Already on BLOCKED.md as
  "Multi-character run support".
- Building a new sweep for a kind currently in
  `SweepRegistry.PlannedSweeps` (Modifier / Orb / Monster) when the
  underlying blocker is still open. Note: a Monster sweep is marked
  optional in the registry because EncounterSweep already exercises
  monsters transitively — re-confirm necessity before building.
- Implementing a per-monster patch fix from
  `MonsterPatchAuditTests.s_expectedDoormakerShape`. Each entry is its
  own commit and may surface new `Godot.*` stub gaps; the per-monster
  workflow is documented in BLOCKED.md "Move-body patches" entry.
- Treasure room pick/skip split (currently auto-picked; previewable
  pick/skip requires DTO + protocol shape — already on BLOCKED.md).
- Neow event dismissal (no wire method yet; needs design).
- Relic dynamic state on the wire (charges, counters). Non-breaking to
  add but every new field needs a caller justification per
  `Methods.cs:223-225`.
- Any candidate that requires designing a new AD-N or changing
  `GAME_VERSION`.
- Anything that requires a Python scenario test (forbidden by AD-6 —
  surface the underlying C# gap instead).

### Append-to-BLOCKED.md format (match SMITH-graduated style)

```markdown
### <Title>
- **Surface:** which file:line / which wire method / which content
- **Question:** the open decision the user needs to make
- **Cheapest unblock:** the smallest action that would let the work proceed
- **Discovered:** YYYY-MM-DD via <detection method, e.g. "coverage delta 1c">
```

After appending, commit BLOCKED.md as its own commit:
`docs: BLOCKED entry — <short title>`.

---

## Phase 3 — Fix one at a time

For each MECHANICAL candidate, in candidate-list order:

### Step 1 — Pre-check (main context)
`git status` must be clean. If not, stop and report.

### Step 2 — Spawn implementer subagent (general-purpose)

Use this prompt template verbatim (substitute the bracketed fields):

```
Implement this engine gap in the headless-in-the-spire repo. Use the
absolute path of the current working directory (do not assume macOS or
Linux layout — read pwd from the orchestrator's context).

GAP: [one-paragraph description with file:line references]
EVIDENCE: [how it was detected — pass 1a/1b/1c/...]
EXPECTED CHANGE SHAPE: [DTO + handler + test, or HarnessGaps graduation,
                       or coverage matrix extension, etc.]

House workflow (see CLAUDE.md):

1. src/Sts2Headless.Protocol/Methods.cs — add Params/Result records with
   [JsonPropertyName] in snake_case. Use enums (RoomType, Character, …)
   never strings. Add a new enum variant + Unknown sentinel if needed.

2. src/Sts2Headless.Protocol/MethodCatalog.cs (core) OR
   src/Sts2Headless.Cheats/CheatMethodCatalog.cs (debug, IsDebugOnly: true).
   Append a MethodEntry — that's the single source of truth.

3. src/Sts2Headless/HostMethods.cs (core) OR
   src/Sts2Headless.Cheats/CheatHostMethods.cs (debug, must register via
   HostMethods.GateDebug).

4. Run: just regen
   (regenerates protocol/openrpc.json + Python _models.py — pre-commit
    hook will block the commit if you skip this).

5. Add an integration test under
   tests/Sts2Headless.IntegrationTests/<Feature>Tests.cs:
   - public class : IClassFixture<HostSubprocess>
   - Build params from DTO records, assert on enum values
   - Use GreedyAgent + AgentDriver.PlayRunAsync(stopWhen:...) to walk to
     the slice (see RestSiteSmithTests.cs as the canonical example)
   - Use CheatClient extensions for state setup (debug/set_hp,
     debug/replace_deck, debug/give_relic, debug/read_deck, …)

6. If the change touches multi-room flow, add an End2End test under
   tests/Sts2Headless.End2EndTests/.

7. For debug methods: ALSO add a negative test to DebugDisabledTests.cs
   asserting WireErrorCode.DebugMethodDisabled (-32001) without the flag.

8. Run: just test
   Everything must be green before declaring done.

9. Commit with this template (HEREDOC for formatting):

   git commit -m "$(cat <<'EOF'
   <scope>: <imperative summary, ≤72 chars>

   <optional body, why-not-what, 1–3 sentences>

   Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
   EOF
   )"

   Scope examples: `protocol`, `runtime`, `cheats`, `agents`, `tests`,
   `coverage`, `harness-gap`, `docs`.

Hard rules (do not violate):
- AD-4: never reference sts2.dll at compile time (reflection only).
- AD-6: behavioral truth is C# only. Python is parity-only; never write
  a Python scenario test as the canonical assertion.
- AD-7: every debug method goes through HostMethods.GateDebug AND ships
  with both a positive and a negative test.
- Never commit anything under vendor/.
- Never auto-bump GAME_VERSION; on hash mismatch, stop and surface the
  mismatch.
- Never amend an existing commit. Never push.
- Never run with --no-verify (the pre-commit hook regenerates
  openrpc.json + _models.py; let it run).

Return to me: SHA of the new commit, files touched, test names added,
and any surprises worth noting. Keep the report under 300 words.
```

### Step 3 — Verify (main context)

After the subagent returns:

```
git log -1 --stat        # confirm the commit landed
just test                # full suite green (already done by subagent, double-check)
```

If `just test` fails or the commit looks wrong, revert
(`git reset --hard HEAD~1`) and either retry with corrected guidance or
escalate. Do not "fix forward" with a second commit unless the subagent
specifically failed cleanup.

### Step 4 — Cross off + continue

Mark the candidate done. Move to the next one. Loop until queue empty.

---

## Phase 4 — Wrap up

- Re-run `just test` from a clean shell as a final sanity check.
- One-paragraph summary to the user:
  - what landed (commit SHAs, one bullet each)
  - what went to BLOCKED.md (entry titles)
  - what was deferred and why
  - any detection pass that crashed or surfaced something surprising

---

## Hard guardrails (DO NOT VIOLATE)

1. **AD-3** — Never auto-bump `GAME_VERSION`. Hash mismatch = stop, report.
2. **AD-4** — Never add a compile-time reference to sts2.dll. Reflection
   only. `Ad4InvariantTests` guards this.
3. **AD-6** — Behavioral truth lives in C#. Don't write a Python scenario
   test, even as a quick check.
4. **AD-7** — Every debug method MUST register via
   `HostMethods.GateDebug(...)` and ship with BOTH a positive test and a
   `DebugDisabledTests`-style negative test.
5. **Vendor** — Never commit anything under `vendor/`. Never commit
   `documentation/coverage/sweep-*.{md,json}` or
   `documentation/research/modeldb/` (all gitignored).
6. **Enums on the wire** — Never accept a string for a finite domain.
   Add an enum variant + Unknown sentinel.
7. **HarnessGaps lifecycle** — If a Gap test goes green, drop the trait
   AND move the file out of `HarnessGaps/` into the feature folder.
8. **Pre-commit hook** — Let it run. Never `--no-verify`. The hook
   regenerates `protocol/openrpc.json` and `_models.py` — if it edits
   files, restage and recommit (do NOT amend; new commit).
9. **One commit per logical change** — never amend, never squash, never
   force-push.
10. **Don't push** — leave commits local. The user pushes.

---

## Subagent usage cheatsheet

- **Explore** — read-only. Use for detection passes 1a, 1b, 1c (the
  parser half), 1f (the parser half). Brief with absolute paths and an
  explicit "report under N words" cap.
- **general-purpose** — full tool access. Use for Phase 3 implementation
  AND for the `just sweep-sample N` / `just probe-*` Bash steps if you
  want the multi-minute wait to happen off main context.
- **Plan** — when a mechanical candidate turns out to be ambiguous
  mid-implementation; spawn Plan to design before editing.

When sending parallel subagents (Phase 1 detection), bundle them all in
ONE message with multiple tool calls — that's the only way they run
concurrently.

---

## When to STOP and ask the user

- Game-version hash mismatch (immediate).
- Two consecutive implementer subagents fail with the same root cause
  (e.g. probe crashes on bootstrap → infrastructure issue, not a gap).
- A candidate sits ambiguously between MECHANICAL and BIG CHOICE — write
  it to BLOCKED.md, then continue with the next candidate.
- Queue exhausted: write the Phase 4 summary and stop.
