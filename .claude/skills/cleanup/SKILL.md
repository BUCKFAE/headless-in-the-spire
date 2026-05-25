---
name: cleanup
description: Run a repo-cleanup sweep — docs freshness, large-file split candidates, magic-number/string → enum conversions, TODO/dead-code/duplicate scans, doc drift, and project-specific invariants. Reports findings before changing anything; only proposes edits for mechanical transforms.
---

# /cleanup

Run a structured cleanup pass over the repo. Split into **report-only** passes
(judgment required — surface findings, let the user decide) and **propose-edit**
passes (mechanical — show a diff, then apply on approval).

Work the passes in order. After each pass, write findings to the conversation
under a clear heading; do **not** silently accumulate into a final dump.

## Pass 1 — Docs freshness (report only)

Goal: catch drift between `README.md` / `CLAUDE.md` / `BLOCKED.md` and the
actual repo.

- Read `README.md` and `CLAUDE.md`.
- For every file path, directory, `just` recipe, env var, or symbol they
  mention, verify it still exists. Use `just --list` to confirm recipes.
- Diff the documented project layout against `ls src/ tests/ clients/python/`.
- Flag instructions that contradict each other or current code (e.g. CLAUDE.md
  says "X lives in Y/" but it's actually in Z/).
- Walk `BLOCKED.md`: each "graduated" / "DONE" trailer at the bottom is a
  candidate to archive (the live blocker list at the top is what readers
  actually need). Surface the count; let the user decide whether to prune
  or move to a `BLOCKED-archive.md`.

Report: a bullet list of stale references with file:line in the doc.
**Do not edit docs.** Surface findings, let the user decide.

## Pass 2 — Large files (report only)

Goal: find files that have outgrown their purpose and should be split.

```bash
# C# — the bulk of the repo.
find src tests -type f -name '*.cs' \
  -not -path '*/bin/*' -not -path '*/obj/*' \
  -exec wc -l {} + | sort -rn | head -20

# Python clients.
find clients/python -type f -name '*.py' \
  -not -path '*/.venv/*' -not -path '*/_models.py' \
  -exec wc -l {} + | sort -rn | head -10

# Replay viewer (TS/Vue). Exclude node_modules; exclude generated dist.
find tools/replay-viewer/src -type f \( -name '*.ts' -o -name '*.tsx' -o -name '*.vue' \) \
  -exec wc -l {} + | sort -rn | head -10
```

Skip auto-generated files when triaging: `*Id.g.cs` content manifests,
`protocol/openrpc.json`, `clients/python/headless-in-the-spire/**/_models.py`.

For each of the top ~5 files across languages:
- Read it.
- Decide: is the size justified (one cohesive responsibility) or is it doing
  several things that would read better split? Look for natural seams:
  multiple top-level types, distinct regions, helpers that don't share state
  with the main type.
- If split looks worthwhile, sketch the split (proposed new files + which
  members move) but **do not** make the change. Let the user pick which to act on.

## Pass 3 — Magic numbers / strings → enums (propose edits)

Goal: catch string/int literals that should be enum values per the CLAUDE.md
convention ("Prefer enums over strings on the wire and in code").

Spawn an Explore sub-agent (or two, in parallel, scoped to different dirs) to
find:
- String literals matching known enum values in `src/Sts2Headless.Protocol/`
  (`RoomType`, `MapNodeType`, `Character`, etc.) — e.g. raw `"MapRoom"`.
- Hand-written JSON in `tests/Sts2Headless.IntegrationTests/` instead of
  building params from DTO records.
- Repeated integer literals that look like they encode a fixed set (turn
  phases, card targets, etc.).

For each finding: propose the concrete edit (literal → enum reference, or
JSON-string → DTO-record), show the diff, apply on approval. This pass is
mechanical enough to propose edits directly.

## Pass 4 — TODO / FIXME / HACK scan (report only)

```bash
rg -n --no-heading -t cs -t md '\b(TODO|FIXME|HACK|XXX)\b' src tests documentation
```

Group by file. For each, decide: still relevant, or stale and removable?
Surface as a list; let the user prune.

## Pass 5 — Dead code (report only)

Goal: find unreferenced code and comment rot.

- Unused `using` directives and unreachable code: run `dotnet build` and
  surface analyzer warnings (`CS8019`, `IDE0005`, `CS0162`).
- Large commented-out blocks: `rg -n --multiline '^\s*//.{0,}\n(\s*//.{0,}\n){4,}' src tests`
- Public types/methods with zero references outside their declaring file:
  pick the suspicious ones and grep for usages across `src/` and `tests/`.
- `src/GodotStubs/`: per CLAUDE.md, every stub has a `// from: <type>.<member>`
  comment. For each stub, confirm the named caller still exists in
  `src/Sts2Headless*`. Flag stubs whose caller is gone.

Report findings; **do not delete**. Dead-code calls are judgment-heavy
(reflection, dynamic loading via Harmony, test-only fixtures).

## Pass 6 — Docs / comment drift (report only)

- Walk `documentation/` and grep for file paths, type names, and `just`
  recipes. Verify each still exists.
- In `src/` and `tests/`, scan comments referencing symbols
  (`<see cref=`, "see Foo", "from: Bar.Baz"). Verify the symbol still exists.
- Specifically check `documentation/requirements/` and
  `documentation/research/` — they are load-bearing for agents per CLAUDE.md.

Report drifted references with file:line.

## Pass 7 — Duplicate / near-duplicate code (report only)

Goal: small helpers reinvented in multiple files.

- Look for repeated short methods (5–20 lines) with similar bodies across
  files. Candidates: JSON helpers, path manipulation, reflection plumbing,
  test fixture setup.
- Cross-check `src/Sts2Headless/`, `src/Sts2Headless.Runtime/`, and
  `src/Sts2Headless.Protocol/` for duplicate utility code that could move to
  a shared spot.

Report candidates with the duplicated locations; promotion choices are
judgment calls — let the user decide.

## Pass 8 — Project-specific invariants

Per CLAUDE.md:
- AD-4 (no compile-time sts2 reference) — confirm
  `tests/Sts2Headless.UnitTests/Ad4InvariantTests.cs` (or wherever it lives
  now) still exists and `just validation::test` passes it.
- Bootstrap walk — confirm
  `tests/Sts2Headless.IntegrationTests/BootstrapSequenceTests.cs` exists.
- Vendor DLL hash matches `GAME_VERSION` (read the file, don't auto-bump).

Snapshot drift (each is a locked-in audit file that quietly grows stale
across engine bumps — `just validation::test` catches NEW drift but not entries
that *should now be removed*):

- `src/Sts2Headless.MechanicSweep/SweepKnownIssues.cs` — every row is a
  "known crash" with a reason. After an engine fix, the matching sweep
  flips Crashed → Played; the row is now dead weight.
  `KnownIssuesParityTest` catches rows whose id has left the manifest,
  but a row whose id stayed but whose crash no longer happens needs a
  manual re-sweep to surface. Surface row count + game version; flag
  if no recent sweep report exists under `documentation/coverage/`.
- `tests/Sts2Headless.IntegrationTests/MonsterPatchAuditTests.cs`
  (`s_expectedDoormakerShape`) — locked-in list of move/lifecycle
  methods whose Harmony patch strips a gameplay mutation. Per the
  BLOCKED.md "Move-body patches" workflow, every per-monster fix
  shrinks this list. Report the current entry count; a stable count
  across many commits is a hint that fixes have stalled.
- `tests/Sts2Headless.IntegrationTests/Coverage/known-abstract-model-hooks.txt`
  — `AbstractModel` listener-method snapshot. Regenerate via
  `just build::regen-hook-snapshot` after a deliberate sts2 listener change.
  Surface the file's last-modified date vs. the `GAME_VERSION` bump
  date; a pin newer than the snapshot is a flag.

Report status; surface anything missing or stale. **Never** edit these
files as part of cleanup — they're regenerated by their owning workflow.

## Output format

After all passes, give a short summary:
- Pass N: <count> findings, <count> proposed edits applied / pending.
- Highest-leverage next actions (top 3).

Do **not** produce a long catch-all document. The per-pass findings posted
during the run are the deliverable; the summary is the index.

## What this skill does NOT do

- Auto-edit docs (Pass 1, Pass 6) — too much judgment.
- Delete dead code (Pass 5) — reflection / Harmony / test-only fixtures bite.
- Bump `GAME_VERSION` (Pass 8) — hard rule from CLAUDE.md.
- Split files (Pass 2) — propose only.
- Hand-edit snapshot files (Pass 8: `SweepKnownIssues.cs`,
  `s_expectedDoormakerShape`, `known-abstract-model-hooks.txt`) — these
  are owned by their workflow (re-sweep, per-monster patch fix,
  `just build::regen-hook-snapshot`); cleanup only reports staleness.
- Archive BLOCKED.md entries (Pass 1) — surface candidates, let the user
  move them.
