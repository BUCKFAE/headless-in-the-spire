# HarnessGaps

Integration tests that document **known gaps in our headless harness** — wire
or runtime capabilities that sts2 exposes but our implementation doesn't (yet)
handle. Each test in this folder is **born red on purpose**: it asserts the
behaviour we want once the gap is filled. The day the implementation lands,
the test goes green and graduates out of this folder.

This is **not** a "regression tests" folder. Regressions of working features
live next to their feature (`CombatTests.cs`, `MerchantRoomTests.cs`, …). And
it is **not** a place to pin upstream sts2 bugs — that's a separate convention
(`KnownSts2Bugs/`, to be added if/when we find one).

## How to add a gap test

1. Add `[Fact, Trait("Category", "Gap")]` (or `[Theory, ...]`) to every test.
   The `Gap` trait is what excludes the test from the default `just test`
   suite — gap tests are red on purpose, so they should not poison CI.
2. Write the test as if the gap were already fixed. Assert success, expected
   snapshot shape, etc. The failing call surfaces what's missing.
3. Class-level XML doc names the gap, points at the file/line where the
   limitation lives in our code, and links any upstream reference (e.g. how
   `external-tools/sts2-cli` solves it).

## How to run

```
just test-gaps        # runs only Gap-traited tests across every project
just test             # runs everything EXCEPT Gap-traited tests (green-only)
```

## Lifecycle

```
gap discovered ──► add test here (red)
                          │
       harness fix lands ─┤
                          ▼
            drop the Gap trait
                          │
                          ▼
        move file out of HarnessGaps/
        into the feature folder it belongs to
        (now a Kind-1 regression test — green forever)
```

A gap that turns out to be unsolvable (or a deliberate non-goal) gets
**deleted** rather than left rotting under a Skip attribute.

## Open gaps

- [`CardSelectionTests.cs`](./CardSelectionTests.cs) — cards that prompt the
  player to choose another card (Headbutt, Armaments, …) currently crash the
  host because `HangPatches.PatchCardSelectCmdFactories` returns a `null`
  `CardSelectCmd`. Fix sketch: install a `MegaCrit.Sts2.Core.TestSupport.ICardSelector`
  in `Sts2Headless.Runtime` (mirror `external-tools/sts2-cli/src/Sts2Headless/RunSimulator.cs`
  → `HeadlessCardSelector`), then narrow or remove the Harmony patch.
