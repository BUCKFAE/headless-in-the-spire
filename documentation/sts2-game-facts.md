# STS2 game facts (≠ STS1)

Ground truth about *Slay the Spire 2* that an agent trained mostly on STS1
lore is likely to get wrong. Consult this before reasoning about run flow,
acts, characters, or boss progression. Game-mechanics assumptions in tests
and agents should match what's written here, not STS1 intuition.

Pinned game build: see `GAME_VERSION` at the repo root. The facts below
are accurate for that build and may drift as the beta evolves — when a
new build lands and contradicts something here, update this file in the
same commit that bumps `GAME_VERSION`.

## Run flow

- **Neow is opt-in on `run/new`.** When opted into via `withNeow=true`,
  Neow offers a choice of one of three relics (no STS1-style "four
  blessings" menu — it's a flat relic pick). The wire default is
  `withNeow=false`, which lands the player straight at MapRoom with
  `StartedWithNeow=false`; agents/tests that need a fresh-run Neow
  pick as the first interactive step must opt in explicitly. See
  `RunLifecycleTests.cs` for both default-and-opt-in coverage.
- **Architect is not a fightable boss (in the current beta).** After you
  beat the Act 3 boss, the very next room is a scripted Architect
  encounter that kills you. There is no Heart-style 4th-act fight to win
  against. End-of-run agents should treat "beat Act 3 boss" as the
  terminal success state, not "survive Act 4."
- **Only Act 1 → Act 10 is implemented.** "A10" here means ascension
  level 10 in the current beta progression. **On A10 the boss room
  contains two bosses fought back-to-back (or together — confirm before
  asserting).** Anything claiming higher acts is STS1 muscle memory.

## Characters

Five playable characters in the current beta:

| Character    | Origin     | Notes                                                              |
| ------------ | ---------- | ------------------------------------------------------------------ |
| Ironclad     | returning  | Default starting character, mechanically closest to the STS1 version. |
| Silent       | returning  | Speed/precision focus, returning from STS1.                        |
| Defect       | returning  | Last character unlocked in the beta progression.                   |
| Regent       | new        | Dual-resource design: Stars + Forge.                               |
| Necrobinder  | new        | Fights alongside an animated skeletal hand named **Osty** — the only character with a permanent companion entity. Anything iterating over "the player's characters" or "combatants on the player side" needs to account for Osty. |

The codebase enum mirroring this list lives in `Sts2Headless.Protocol`
(grep for `Character`); grow that enum from this table, not from STS1
class names.
