# End-to-End Testing and the Self-Feedback Loop

Snapshot date: 2026-05-13. This is a research / thinking note, not a design doc.
It captures how reliable end-to-end tests can work for this project, and how to
structure the codebase so an LLM (Claude, or any other automated contributor)
can make a change and immediately verify whether it worked.

The two questions are tightly coupled: a good self-feedback loop is just a good
test suite plus a one-command runner that reports structured results.

## Why this matters

Initial goal (3) is parallel execution, but the deeper reason we want parallelism
is that **the test suite is the product**, almost more than the runner itself.
Existing STS2 automation projects have effectively zero test coverage; if we want
to be confident that an enum rename, a protocol tweak, or a game-version bump
hasn't broken combat correctness, we need a test corpus dense enough to catch
those classes of regressions.

## What makes e2e tests reliable here

Several properties of the STS2 environment shape what "reliable" means:

1. **Bit-identical RNG.** STS2 is deterministic given a seed. We can rely on
   that as long as we always test against a pinned game version. Tests should
   record the game version they were captured under, and fail loudly when run
   against a different version (rather than silently producing different state).
2. **The real game logic, not a re-implementation.** Following `sts2-cli`'s
   `GodotStubs` approach, the same `sts2.dll` runs in tests as in production
   use. There is no "mock combat engine" to drift out of sync. This is a strong
   reliability property and we should not give it up.
3. **No renderer.** Headless tests aren't subject to frame timing, animations,
   or fast-mode toggles. A turn either resolves to the same state or it doesn't.
4. **State is fully observable.** Via reflection / Harmony we can serialise the
   `GameState` / `CombatState` etc. into a canonical JSON snapshot. Tests can
   assert on whole-state diffs rather than picking individual fields.
5. **Save format is JSON.** That means we can author tests as save-game fixtures
   — load a hand-crafted JSON, then assert that after action `X`, the state
   becomes the expected snapshot. No need to play 30 turns to get into the
   scenario you want to test.

## Suggested test pyramid

A roughly four-layer model fits this domain well.

### Layer 1 — Unit tests of pure code

The boring layer: enum codecs, JSON schema validation, IPC framing, replay
serialisation. Standard `xUnit` / `pytest` work. Should run in **seconds total**.

### Layer 2 — Scenario tests from authored fixtures

Each test is a tuple: `(starting state, action sequence, expected resulting state)`.

- Starting state is a save-game JSON checked into the repo.
- Action sequence is a list of typed actions (play card, end turn, …).
- Expected state is a canonical JSON snapshot in the repo, compared field-by-field
  with a meaningful diff. Use snapshot-update conventions ("write CI=1 to refuse
  updates; locally `UPDATE_SNAPSHOTS=1` rewrites").

Per-test cost is one game boot plus the actions. With process reuse (one host
running many scenarios) this can be sub-second per test.

Coverage focus: combat (every keyword, intent type, multi-enemy interaction),
each room type, each event branch, each ascension scaling, each character.

### Layer 3 — Golden replay tests

A replay is the deterministic record from goal (5). A replay test re-runs a
recorded action sequence from `start_run(seed, character, ascension, version)`
and compares the produced state stream to the recorded one. If anything diverges,
the test fails and prints the first diverging snapshot.

This is the heaviest tier — a full Act run is ~hundreds of decisions — but it's
the best regression net for catching subtle changes (game version bump, refactor
of state serialisation, accidental dependency on rendering).

Build a corpus of golden replays for: each character, each ascension level we
care about, a few "weird" runs that exercise unusual cards / relics.

### Layer 4 — Property-based / fuzz tests

For invariants that should hold universally: "the game never panics", "HP never
exceeds max HP without a card that raises max HP", "the deck count after combat
equals the deck count before plus added cards minus removed cards", etc.

Hypothesis (Python) or FsCheck (C#) generate random legal action sequences and
assert invariants. Parallelism (goal 3) is what makes this practical — a useful
fuzz campaign is millions of action-steps per night.

## Determinism strategy

- **Pin game version per branch.** The repo has a `GAME_VERSION` file or env var.
  CI refuses to run if the local `sts2.dll` hash doesn't match. Replays and
  snapshots are tagged with that version.
- **Pin BaseLib / Harmony / .NET SDK versions** in the build manifest. Reproduce
  the same toolchain in CI as locally.
- **Quarantine non-deterministic state.** Wall-clock time, thread scheduling,
  hash-set iteration order, and Godot main-thread marshalling are all places
  determinism can break. Tests should fail fast if state diverges between runs
  on the same seed.

A useful canary: run **the same test twice in the same CI job** and compare the
state streams byte-for-byte. Any flake there is a determinism bug worth chasing
before it pollutes the rest of the suite.

## Test fixtures via save injection

Because STS2 saves are JSON, the highest-leverage testing pattern is:

1. Author a "setup" save by playing the game manually and editing the JSON.
2. Commit the save as a fixture.
3. Tests load the fixture, run a small action sequence, assert on the result.

Compared to "play through Act 1 to get a Searing Blow into a fight with
Hexaghost", this turns minutes of setup into milliseconds. Most scenario tests
should look like this.

Build a small tool to **diff and minimise** save fixtures so the committed JSON
contains only the fields that actually matter for the test (everything else
gets stripped to defaults). This keeps fixtures readable and reduces churn when
the save format evolves.

## The self-feedback loop

For Claude (or any automated contributor) to iterate confidently, the loop has
to be:

> change code → run **one** command → get structured pass/fail with diffs →
> act on it

Concretely:

### A single entry point

A top-level `just validation::test` / `make test` / `dotnet test` that:

- Builds the C# host and any binding shims.
- Runs all four layers of tests above.
- Emits a machine-readable summary (JUnit XML / TAP / a small JSON file in
  `target/test-summary.json`).
- Exits non-zero on any failure.
- Prints, for each failure, the **smallest** useful artefact (a snapshot diff,
  a divergence point in a replay, a stack trace). Not megabytes of game logs.

### Fast iteration

- Total wall time for the unit + scenario layers should be **under 30 seconds**
  to keep the inner loop tight. The full replay + fuzz tier runs separately.
- Process reuse: one host process running N scenarios is much faster than N
  process boots.
- Parallel test execution by default. This is exactly what goal (3) buys us at
  the runtime level — it also pays off in the test suite.

### Reproducible environment

Claude can't reliably install `sts2.dll` itself (licensing). Options:

1. **Devcontainer / docker image** with `sts2.dll` mounted from the host.
   Local-only assumption: the human contributor has Steam installed and a
   bootstrap script symlinks the right files.
2. **Vendored binary in a private LFS or out-of-tree directory** that CI
   knows how to fetch.
3. **Fail-fast bootstrap**: a `scripts/check-environment` that prints
   actionable errors ("missing `sts2.dll v0.103.2` at `vendor/...`; run
   `scripts/extract-from-steam`").

Either way, the contract for Claude is: **call `just validation::test`; if the environment
isn't set up, the error message tells you what to do.** No silent skips of
suites that need the game DLL.

### Replay-driven regressions for refactors

The strongest tool for verifying a refactor preserved behaviour is to run the
entire golden-replay corpus and report any divergence. When Claude makes a
change to combat or state serialisation, the loop is:

1. `just validation::test` (fast tier) — green.
2. Replay corpus (heavy tier) — green or "diverged at replay X turn Y,
   expected `<A>`, got `<B>`".

If a divergence is intentional (e.g. snapshot format changed), there's a
controlled regeneration path: re-record the corpus under the new code, the
diff lands in the PR, a human reviews. Without a human review, replay
regeneration never happens silently.

### Structured action and state vocabularies

The self-feedback loop only works if Claude can produce valid action sequences
mechanically. That argues for:

- An action **schema** (typed enum + parameters, validated at the boundary).
- A state **schema** (similarly typed).
- Generated client bindings (Python, Kotlin per goal 4) so an automated
  contributor's actions parse and type-check before they ever reach the game.

This is the same enum / type investment goal (1) demands — testing benefits
just as much as runtime correctness does.

## Concrete next steps

1. **Decide the IPC transport** with parallelism in mind (stdio / named pipe
   per process / Unix domain socket per process — anything but a hardcoded
   port).
2. **Build the headless host skeleton** following `sts2-cli`'s `GodotStubs`
   pattern, with the action/state schemas described above.
3. **Stand up the test runner**: `just validation::test` with the four-layer pyramid wired
   up, even if each layer starts with a single trivial test.
4. **Bake in the determinism canary**: every run of the suite runs one
   representative scenario twice and compares.
5. **Author the first ~10 scenario fixtures** by hand (one per character, a
   few per room type, a few edge-case combats). This will surface protocol
   bugs faster than any other activity.
6. **Add a top-level `documentation/testing.md`** that consolidates the
   above into instructions, once the tooling exists. (Shipped — see
   [`documentation/testing.md`](../testing.md); the four-layer pyramid
   below maps onto the three operational axes that doc documents.)

## Open questions

- Is there a meaningful "headless main-thread" in the stubbed Godot host, or
  do we need to design our own event loop? `sts2-cli` may answer this in code.
- Save-game format stability across game versions: how often does the JSON
  schema break? Need to find out before investing heavily in fixtures.
- Per-test process isolation vs. process reuse: which gives us cleaner state
  guarantees? Probably reuse is fine if we reset between tests, but worth
  validating.
- How do we surface `sts2.dll` version mismatches in test output? Probably a
  guarded preamble that prints version + hash and exits non-zero on mismatch.
