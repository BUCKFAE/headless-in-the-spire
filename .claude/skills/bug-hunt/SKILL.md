---
name: bug-hunt
description: Reproduce, generalise, and fix a bug from a user-supplied report or log. Probes the binary, reads related code, researches sts2/Godot/Harmony internals if needed, writes a red regression test next to the affected feature, hunts for sibling bugs and writes red tests for them too, then fixes each — one logical commit at a time.
---

# /bug-hunt

Invoked as `/bug-hunt <bug description / log / stack trace>`. The argument is
the bug report — a symptom paragraph, a host stderr snippet, a stack trace, a
"runs/leave_treasure_room throws X" sentence, etc. Treat it as the seed for an
investigation, not as a finished spec.

The skill runs five phases, end-to-end:

0. Parse the report
1. Investigate (probes + code + research)
2. Write the red repro test (THE CRUCIAL STEP)
3. Hunt for sibling bugs, write red tests for each
4. Fix one at a time, one commit per logical change

After every phase, post a short update to the conversation so the user can
intercept. Do not silently accumulate into a final dump.

---

## Phase 0 — Parse the report

Extract from the bug description:

- **Symptom** — what the user observed (error message, wrong DTO, hang, soft-lock, …).
- **Exact identifier** — the wire method (`run/leave_treasure_room`), the C# symbol (`HostMethods.LeaveTreasureRoom`), the sts2 type (`Godot.Colors.get_Black`), or the test name. If the report is vague, pick the most specific thing it mentions and grep for it.
- **Repro context** — seed, character, ascension, modifiers, floor, room type, deck state, anything that pins the scenario.
- **Suspected category** — combat / rewards / map / merchant / rest site / event / treasure / replay / agent driver / protocol-shape / cheats / bootstrap.

Pre-flight: `git status` must be clean. If not, stop and ask the user — never
mix a bug repro into in-progress work.

Pre-flight: read `BLOCKED.md` and search project memory
(`/Users/julianschubert/.claude/projects/-Users-julianschubert-Documents-headless-in-the-spire/memory/`)
for the symptom keywords. If the bug is already a known/resolved entry,
surface that to the user before any other work — saves a wasted investigation.

Output a one-paragraph summary of the parsed report and move on.

---

## Phase 1 — Investigate

Three parallel tracks. Use **Explore** subagents for the read-only tracks so
the main context isn't drowned in source.

### 1a — Probe the binary

Pick one or more based on the suspected category:

| Symptom shape                                         | Probe                                                       |
| ----------------------------------------------------- | ----------------------------------------------------------- |
| Hang / "snapshot identical forever" / stall           | `just probe-combat-stall <seed> <floor>`                    |
| Reward chain (claim/skip silent, missing cards/relics)| `just probe-rewards-natural-chain`                          |
| Enemy turn / monster move NREs                        | `just probe-natural-chain`                                  |
| Content missing on wire (card/relic/power id Unknown) | `just probe-modeldb` + diff against `*Id.g.cs`              |
| Bootstrap / load failure / vendor resolution          | `just probe-bootstrap` → `just probe-run-state`             |
| GodotStubs missing method                             | `just list-members <Godot.Type>` for the named type         |
| Coverage gap (content never seen)                     | `just coverage` and diff against `documentation/coverage/latest.json` |

Run the relevant probe. If it exits non-zero or writes to
`documentation/research/*-gaps.md`, capture the diff against `git HEAD` — that
is the engine-side evidence.

### 1b — Read the related code

Spawn an Explore subagent. Give it the wire method name / C# symbol from
Phase 0 and ask it to report:

- Where the method is declared (`Methods.cs`), catalogued (`MethodCatalog.cs`
  or `CheatMethodCatalog.cs`), and handled (`HostMethods.cs` or
  `CheatHostMethods.cs`).
- Which `Sts2Bindings.*` reflection call it routes through.
- Which Harmony patches in `Sts2Headless.Runtime/HangPatches.cs` (or
  siblings) touch the same engine type.
- Any existing test that already covers the green path (so we know where the
  red repro should land).

Cap the report at ~300 words; ask for file:line citations.

### 1c — Research (only if the symptom points outside the repo)

Skip if the bug is clearly self-contained. Use `WebFetch` / `WebSearch` when
the symptom hints at:

- A Godot 4.x API method missing from GodotStubs.
- A Harmony patching pattern (e.g. transpilers, `MethodNotFound` on
  generic methods, postfix vs prefix ordering).
- A .NET reflection quirk relevant to `AssemblyLoadContext` /
  `VendorAssemblyResolver`.
- A known sts2 mod / community fix for the same symptom (`external-tools/sts2-cli`
  is the OSS reference — check its source first, then external sources).

Do **not** "research" the game's content (card numbers, relic rolls, boss
movesets) — `vendor/sts2.dll` is the source of truth via `probe-modeldb`,
not a wiki.

### Triage

Merge findings into one paragraph: **what's happening, where, why**. If the
investigation is inconclusive, write a `[Trait("Category", "Diagnostic")]`
test that captures the unknown (an exception is caught, state is dumped) and
ask the user how to proceed — don't guess at a fix.

---

## Phase 2 — Write the red repro test (CRUCIAL)

### Pick the axis

| Bug scope                                               | Axis                                                   |
| ------------------------------------------------------- | ------------------------------------------------------ |
| Pure host logic / DTO / envelope / catalog parity       | `tests/Sts2Headless.UnitTests/`                        |
| One wire call against the real host + sts2.dll          | `tests/Sts2Headless.IntegrationTests/`                 |
| Multi-room arc, agent driver, replay re-execution       | `tests/Sts2Headless.End2EndTests/`                     |

### Pick the home — **next to the feature, NOT HarnessGaps**

Per `tests/Sts2Headless.IntegrationTests/HarnessGaps/README.md`:

> This is **not** a "regression tests" folder. Regressions of working features
> live next to their feature (`CombatTests.cs`, `MerchantRoomTests.cs`, …).

So:

- Bug in `run/leave_treasure_room` → add to `TreasureRoomTests.cs` (or a
  sibling file if the existing one is full).
- Bug in combat reward shape → `CombatSelectRewardTests.cs` /
  `CombatRewardShapeTests.cs`.
- Bug in agent driver / stall detection → an End2End file.
- Bug that surfaces a wire method that doesn't exist yet (a *missing
  feature*, not a *regression*) → HarnessGaps + `[Trait("Category", "Gap")]`,
  but flag this explicitly to the user. If they confirm "feature gap, not
  bug", consider redirecting to `/fill-engine-gaps`.

### Write the test using house conventions

Mandatory:

- `public class <Feature>BugRepro_<ShortSlug> : IClassFixture<HostSubprocess>`
  (or reuse an existing class if it already covers the feature).
- Build params from DTO records (`new RunNewParams(Character: Character.Ironclad, Seed: 42uL)`),
  never hand-written JSON.
- Assert on enum values (`Assert.Equal(RoomType.MapRoom, …)`), never raw
  strings.
- Use `AgentDriver.PlayRunAsync(transport, agent, stopWhen: …, ct: cts.Token)`
  with `GreedyAgent` (or `PotionDrinkingAgent`, etc.) to walk to the slice.
- Use `CheatClient` extensions for deterministic state setup
  (`ReplaceDeckAsync`, `SetHpAsync`, `GiveRelicAsync`, `ReadDeckAsync`, …) —
  reach a known scenario instead of relying on RNG.
- Wire-only debug methods need a positive AND negative case (the negative
  case goes in `DebugDisabledTests.cs` per AD-7).

A class-level XML doc comment names the bug, links the symptom, and pins
the **what we want to be true** assertion. The doc stays with the test
forever as documentation of why this regression matters.

### Confirm it's red

```
just test-integration --filter "FullyQualifiedName~<NewTest>"
```

(Or the corresponding `test-unit` / `test-end2end` filter.) The failure mode
must match the bug description from Phase 0 — same exception type, same
wrong value, same hang. If it fails for a *different* reason, the repro is
wrong; iterate.

### Commit the red repro

```
git add tests/<...>
git commit -m "$(cat <<'EOF'
test: red repro for <one-line bug summary>

<2–3 sentence body: where the bug surfaces, what the test pins>

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

A red commit on its own makes the fix's diff legible: the next commit
turns this exact test green.

---

## Phase 3 — Hunt for sibling bugs

Most bugs in this repo come in families:

- A wire method that mishandles X probably also mishandles Y (same handler
  shape).
- A missing GodotStubs entry probably has cousins in the same `Godot.*`
  type.
- A snapshot field that's wrong on `RoomType.A` is often wrong on `RoomType.B`.
- A Harmony patch that misses an enemy class often misses the others.

Process:

1. **Grep for the root cause.** If the bug is `MissingMethodException` on
   `Godot.Colors.get_Black`, grep for other `Godot.Colors.*` references in
   the bootstrap walk and `inspect-sts2` output.
2. **Walk the catalog.** If a `MethodCatalog` entry mishandles its summary's
   guarantee, read every nearby entry for the same shape.
3. **Coverage delta.** Run `just coverage` if it isn't already fresh and
   diff the `seen` set against the `*Id.g.cs` manifest — content that's
   reachable but never seen is a candidate sibling.
4. **Variant seeds.** If the bug surfaced on seed 42, try seeds 1 / 7 / 100
   via an ad-hoc `[Theory]` to see whether the symptom is seed-specific or
   universal.

For each suspected sibling, **write a red test in the same Phase-2 style**.
Each lives in the appropriate feature folder; each commits as its own
`test: red repro for <sibling>`.

Cap this phase: **at most 5 siblings**. If you find more, write the rest
to `BLOCKED.md` and ask the user which to prioritise. Saturating Phase 4
on a 30-bug hunt is not the goal.

If no sibling surfaces after a focused look, say so explicitly and move on
— "no siblings found" is a valid Phase-3 outcome.

---

## Phase 4 — Fix one at a time

For each red test from Phases 2–3, in order:

### Step 1 — Pre-check

`git status` must be clean. If not, stop and ask.

### Step 2 — Implement the fix

Implement the smallest change that turns this specific test green. Follow
the house workflow (see `/fill-engine-gaps` Phase 3 for the canonical version):

- DTO / enum changes go in `Methods.cs` with `[JsonPropertyName]` snake_case
  + an enum + `Unknown` sentinel for any new finite-domain field.
- Catalog entries (`MethodCatalog.cs` for core; `CheatMethodCatalog.cs`
  with `IsDebugOnly: true` for debug).
- Handlers in `HostMethods.cs` (core) or `CheatHostMethods.cs` (debug,
  registered via `HostMethods.GateDebug`).
- GodotStubs additions get a `// from: <type>.<member>` comment naming
  the caller in sts2.dll. Do not speculatively widen the stub surface
  beyond what the symptom requires.
- Harmony patches go in `Sts2Headless.Runtime/HangPatches.cs` (or a
  sibling) with a comment explaining what engine method is being silenced
  and why.

Run `just regen` after any change to `Methods.cs` — the pre-commit hook
will block otherwise.

### Step 3 — Confirm green + no regressions

```
just test-integration --filter "FullyQualifiedName~<TheRedTest>"   # was red, is now green
just test                                                          # full suite, no regressions
```

If any other test goes red, **stop**. Don't patch the regression by
expanding the fix's scope. Revert (`git reset --hard HEAD`), narrow the
fix, and try again. A bug-hunt that breaks two green tests to close one
red one is a net loss.

### Step 4 — Commit the fix

```
git commit -m "$(cat <<'EOF'
<scope>: fix <one-line summary>

<2–4 sentence body: what was wrong, why it broke, why this fix is
narrow. Reference the red-repro commit's subject so the pair is
greppable.>

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

Scope examples: `runtime`, `protocol`, `cheats`, `host`, `agents`,
`stubs`, `harmony`. The red repro commit + the fix commit form a
2-commit pair; a future bisect or revert can target either independently.

### Step 5 — Cross off and continue

Mark the test done. Move to the next red test. Loop until the queue is
empty.

If a red test from Phase 3 turns out to need a design decision rather
than a mechanical fix (new wire method, new character, new
`GAME_VERSION` pin), **leave the red commit in place** and append a
BLOCKED.md entry instead of trying to fix it. The red test is now
documentation of the open problem.

---

## Phase 5 — Wrap up

One short summary to the user:

- Bug as understood (1 sentence).
- Red repro commits (SHA + subject, one bullet each).
- Fix commits (SHA + subject, one bullet each).
- Siblings deferred to BLOCKED.md (entry titles, one bullet each).
- Anything weird the investigation surfaced (often the most valuable line).

Then stop. Do not push; the user pushes.

---

## Hard guardrails

1. **AD-3** — Never auto-bump `GAME_VERSION`. Hash mismatch = stop, surface.
2. **AD-4** — Never add a compile-time reference to sts2.dll. Reflection
   only. The fix lives in `Sts2Headless.Runtime/Sts2Bindings*` or a Harmony
   patch, never `using MegaCrit.Sts2…`.
3. **AD-6** — The repro test is C#. Do not write a Python "this is the bug"
   test. Python parity tests verify the client matches C# behavior; they
   are not the regression net for game behavior.
4. **AD-7** — Any new debug method ships with `HostMethods.GateDebug`, a
   positive integration test, AND a negative case in `DebugDisabledTests`.
5. **HarnessGaps is for features, not regressions** — the bug repro lives
   next to the feature it pins. If the symptom is "the feature doesn't exist
   yet", redirect to `/fill-engine-gaps`.
6. **Never `--no-verify`** — the pre-commit hook regenerates
   `protocol/openrpc.json` and `_models.py`. If it edits files, restage and
   create a new commit (do NOT amend).
7. **Never amend, never squash, never force-push** — one logical change per
   commit. Red repro + fix is the canonical 2-commit pair.
8. **Never commit anything under `vendor/`** — proprietary content, gitignored.
9. **One commit per logical change** — red repro is its own commit, each
   sibling repro is its own commit, each fix is its own commit. Bisect-friendly.
10. **Don't push** — leave commits local.

---

## Subagent cheatsheet

- **Explore** — Phase 1b (read related code), Phase 3 (grep for siblings).
  Read-only, fast, cap output.
- **general-purpose** — Phase 4 fix implementation, especially when the
  fix is mechanical and the test already pins the expected behavior.
- **Plan** — when Phase 2 turns ambiguous (which axis? which fixture?),
  spawn Plan before writing the test.

Run independent subagents in parallel by bundling them in a single message.

---

## When to STOP and ask the user

- `git status` not clean at any pre-check.
- `GAME_VERSION` hash mismatch.
- Phase 2 repro reproduces a *different* symptom than the user reported
  (the report may be wrong, or you're looking in the wrong place).
- Phase 3 finds >5 sibling candidates — let the user prioritise.
- A fix would break a currently-green test that isn't a sibling of the
  bug. The "right" fix probably needs design input.
- The bug needs a new wire method, a new character, or a `GAME_VERSION`
  bump — surface it, don't decide unilaterally.
