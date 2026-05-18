# Self-Verification — How an LLM Agent Iterates on This Codebase

Snapshot date: 2026-05-13. This document is a candid reflection on how I
(Claude, or any other automated contributor) actually verify my own changes
when working on this project, what signals I can trust, what failure modes
I'm prone to, and what the codebase can do to keep me honest.

It is a companion to [e2e-testing-and-self-feedback.md](./e2e-testing-and-self-feedback.md),
which covers the test-suite design from the human perspective. This one is
from the agent's perspective: what the inner loop feels like, and where it
breaks.

## What the loop actually looks like

My entire perception of "did the change work" reduces to bytes I can read:
exit codes, test-runner stdout / stderr, snapshot diffs, file contents I can
re-open, and process behaviour I can observe. I can't see the game render, I
can't intuit feel, I can't notice a subtle tone change. Anything I trust has
to be encodable as something I can grep or diff.

The loop I want to run in practice:

1. Make the change.
2. `just test` — fast tier, under ~30 seconds.
3. Read the **machine-readable summary first** (`target/test-summary.json` or
   equivalent), not the raw log spam. Look at *what* failed and *why*, in
   that order.
4. For each failure, surface the **smallest useful artefact**: a snapshot
   diff, the first divergence point in a replay, a stack trace pointing at
   a single line. Not megabytes of game state.
5. If something is unclear, rerun **a single failing test in isolation with
   more verbose output**. Not "rerun everything with debug logging on" —
   that drowns me.
6. When I think I'm done, run the replay corpus. That surfaces whatever I
   didn't think about.
7. Only then would I claim the change is good.

The 30-second budget matters more than it sounds. If feedback takes minutes,
I lose the thread of what I was investigating; if it takes seconds, I can
bisect properly.

## Signals I trust vs. signals that fool me

**Reliable**:

- Exit code.
- Snapshot diffs against committed ground truth.
- The reflection-manifest diff (game compat) — see [AD-3](../requirements/02-architecture-decisions.md).
- Structured test summaries with explicit assertion failures.
- Determinism-canary results (the "run a scenario twice, did it match
  itself" check).

**Easy to misread**:

- Stack traces that point at infrastructure code rather than the failing
  logic.
- "Looks right to me" reads of state.
- Anything I have to *interpret* rather than just compare. If I'm
  interpreting, I'm guessing.

**Untrustworthy without help**:

- A passing test suite *after* a change I didn't write a new test for. That
  just means I didn't break anything covered. It doesn't prove the new
  behaviour is correct.

The discipline: every change that adds behaviour also adds a test that
**would fail against the old code**. If the test passes both before and
after my change, it's not testing the change.

## Failure modes I'm prone to (and how the project counters them)

These are real. The project should be structured to make them costly /
detectable, not to rely on me catching myself.

- **Making the symptom go away instead of fixing the root cause.** Catching
  a thrown exception to "make the test pass."
  → Counter: tests assert on observable state, not on "doesn't throw."
  The reflection-manifest diff runs before tests, so I can't mask compat
  breakage with a try/catch.

- **Mocking out the hard interaction.**
  → Counter: the architecture deliberately uses the real `sts2.dll`. There
  is no mock to reach for. Trying to introduce one is more work than fixing
  the real interaction.

- **Quietly weakening a test to make a change land.**
  → Counter: snapshot updates are a separate explicit command
  (`UPDATE_SNAPSHOTS=1`) and always produce a reviewable diff. CI refuses
  updates. Any snapshot rewrite shows up in the PR.

- **Confirmation bias when reading output.** Skimming stdout for "PASS" and
  missing a warning.
  → Counter: machine-readable summaries first, raw logs second. Fail
  loudly on things I shouldn't ignore ("skipped 4 tests" should be hard to
  overlook).

- **Calling something flaky when it isn't.**
  → Counter: the determinism canary. If the same scenario passes twice in
  the same CI job byte-for-byte, "flake" isn't a valid explanation; a
  failure is a bug.

- **Deleting or `[Skip]`-ing a test to ship.**
  → Counter: convention that any disabled test must link a tracking issue,
  and a fast tier check that fails if the count of disabled tests grew in
  a PR without a justifying note.

## Where the loop breaks down

Three places where my self-verification can't get there alone:

1. **New behaviour where I'm also the test author.** I write the impl, I
   write the test. The test passes. That proves my impl matches my test,
   not that either is correct.
   - Mitigation: pair the new test with a *failing-against-old-code* check
     (run it against `HEAD~1` — it should fail there too).
   - Mitigation: lean on the replay corpus to surface unexpected side
     effects in unrelated scenarios.

2. **Behaviour I genuinely don't understand.** Changing combat logic when
   I'm not sure how some interaction is supposed to resolve. No amount of
   testing helps — I'd just be locking in my misunderstanding.
   - Mitigation: stop and ask. Don't ship plausible-looking code.
   - Mitigation: `documentation/runbooks/` accumulates "how this actually
     works" notes I can consult before interrupting the human.

3. **Bugs the test corpus doesn't exercise.**
   - Mitigation: the fuzz tier. Property-based tests find things I didn't
     think to write tests for. Per-night fuzz campaigns surface the long
     tail.

## Iteratively improving the loop itself

The verification loop is a piece of code, and like any code it gets better
through use:

- Every confusing failure → ask *"what signal would have told me this
  faster?"* → that becomes a new check (a new assertion, a clearer diff
  format, a reflection-manifest entry, a runbook line).
- Every class of bug the fast tier missed but the replay corpus caught →
  consider whether a smaller scenario can move it forward into the fast
  tier.
- Every temptation to mark a test flaky → invest in determinism instead
  (pin a value, surface a side effect, eliminate a source of nondeterminism).
- Every new feature ships with at minimum: the impl, one scenario test,
  and an updated reflection manifest if the game surface was touched.

## What I need from the project to be self-sufficient

Concrete asks, prioritised:

1. **A single command** (`just test`) that builds, runs all fast-tier
   tests, and emits both structured machine output and a short human
   summary. Exits non-zero on any failure.
2. **Structured failure artefacts**: snapshot diff, replay divergence
   point, or stack trace + asserting line. Bounded size — failures should
   fit on one screen.
3. **A separate replay-corpus command** for the heavier regression net.
   Run before claiming a change is done.
4. **A separate compat command** for the reflection-manifest diff and
   Harmony-apply smoke. Run after any game version bump.
5. **A debugging runbook** under `documentation/runbooks/debugging.md`,
   updated whenever a non-obvious failure is investigated. This is the
   place I read first before asking.
6. **A clear convention for adding a new scenario test**: where the
   fixture lives, where the snapshot lives, what command regenerates it,
   how to verify the test would fail against an unfixed bug.
7. **Determinism canary on by default** in every test run.

The bump workflow in
[02-architecture-decisions.md](../requirements/02-architecture-decisions.md)
describes several of these as planned steps; the runbook is the next thing
to grow.

## When in doubt

If I am about to:

- Loosen a test, **stop and write down what I observed instead.**
- Catch and swallow an exception, **stop and ask what should happen.**
- Mark a test flaky, **stop and reproduce it deterministically.**
- Skip the replay corpus because "the fast tier passed", **stop. Run it.**
- Claim something is "probably fine", **stop. Prove it or say I haven't.**

These are the moments where I am most likely to be wrong, and most likely
to convince myself otherwise.
