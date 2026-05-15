# agent-survival gaps

What happens when the `GreedyAgent` walks `run/new` → forward through Act 1
across a range of seeds. Generated from the diagnostic harness in
`tests/Sts2Headless.End2EndTests/DiagnoseMerchantSeedScanTests.cs` — scan
of seeds 1..25 (Ironclad, heal-between-rooms via `debug/set_hp`).

The reason this matters: **every end-to-end test depends on the agent
making forward progress.** Today only `ReachAct1BossTests` and the future
`MerchantRoomTests` are blocked, but every multi-room arc the project
adds will hit the same gaps. They're documented here as a punch list so
the next agent-survival slice has a starting point.

## summary

- Seeds 1–25 scanned. **None reached a `MerchantRoom`.**
- 19/25 seeds timed out in a combat stall at floor 0–5 (the dominant
  failure mode).
- 2/25 seeds died (legitimate game-over inside the floor budget).
- 2/25 seeds hit `MissingMethodException` on a GodotStubs gap
  (`Godot.Mathf.RoundToInt(Single)`).
- 1/25 hit a `MissingFieldException` on a GodotStubs gap (`Godot.Color.A`).
- 1/25 hit an `EventRoom` with empty options (event subtype the wire
  doesn't surface picks for).

Seed 42 — used by `ReachAct1BossTests` — was unusually clean: traversed
17 floors before stalling on the boss combat. Almost every other seed
breaks much earlier.

## 1. combat stall (dominant, 19/25 seeds)

**Symptom**: the agent is in `CombatRoom`, `IsPlayPhase=false`,
`IsInProgress=true`, hand size 0, energy 0/3, but the enemy turn never
completes. The agent calls `run/end_turn` and `run/state` repeatedly; the
engine never flips back to play phase. The 2-minute wall-clock cap in
`DriveUntilAsync` (`MaxSteps=2000`) eventually trips cancellation.

**Reproduced at**: seeds 1, 2, 3, 5, 6, 7, 8, 10, 11, 14, 15, 16, 18, 19,
20, 22, 23, 24, 25 — and seed 42 at floor 17 (boss VANTOM).

**Existing relevant code**:
`src/Sts2Headless.Runtime/Sts2Bindings.cs` `EndTurn` already pumps the
sync context and has a `ForceSwitchToEnemySide` retry. The natural-chain
probe (`just probe-natural-chain`, seed 42) reports `0 unique gaps` for
the first combat — but that's a single, simple monster encounter. The
stalls here are deeper into Act 1 (or in some seeds, the very first
combat is the one that hangs).

**Hypothesis**: the boss/elite enemy turns invoke engine paths the
existing pump doesn't drain — possibly an animation-completion future
the headless host should fulfil, or a multi-action turn whose steps
yield between `Pump()` cycles. Needs a probe analogous to
`probe-natural-chain` but pointed at later combats.

**Next-slice starting point**: extend `--probe-natural-chain` to walk to
seed N, floor M, and dump the gap waterfall from there. If the stall is
on the boss specifically, snapshot the engine's `ActionExecutor` queue
and `CombatManager` state during the stuck loop.

## 2. GodotStubs gaps (3/25 seeds)

**Symptoms**:
- Seeds 12, 13 — `run/select_map_node` fails with
  `MissingMethodException: 'Int32 Godot.Mathf.RoundToInt(Single)'`.
- Seed 21 — `run/select_event_option` fails with
  `MissingFieldException: 'Godot.Color.A'`.

Both are pure stubs growth — sts2.dll calls into a Godot symbol our
`GodotStubs` project hasn't mirrored yet. These will surface as
runtime errors on any seed whose path requires the touched code.

**Next-slice starting point**: run `just list-members Godot.Mathf` and
`Godot.Color` to confirm the surface, then add stubs. Each addition is
a one-line entry per the project's convention; CLAUDE.md's "GodotStubs
grows on demand" rule applies.

## 3. EventRoom with no surfaced options (1/25)

**Symptom (seed 17, floor 5)**: `EventRoom` with
`availableEventOptions.Count == 0`. The GreedyAgent throws
"either the wire is mid-transition or this event auto-resolves and the
wire surface hasn't routed around it yet."

**Hypothesis**: a specific event subtype (probably one that resolves
immediately on entry, like a passive blessing) leaves no picks on the
`CurrentOptions` list. The wire's `ReadAvailableEventOptions` filters
out `IsFinished` events, but this one may finish without ever
populating options.

**Next-slice starting point**: probe seed 17 floor 5 to identify the
event type, decide whether the wire should auto-advance it or surface
a "no decision needed" marker the agent can act on.

## what doesn't fix this

- **Picking a different seed.** Seed 42 was the cleanest of the 25
  scanned, and even that one stalls at the boss. The combat-stall bug
  is too widespread to seed-hunt around.
- **Healing more aggressively.** The stalls aren't damage-driven —
  the player is alive when the stuck loop starts. HP doesn't change
  during the loop.
- **A smarter agent.** The agent's combat logic is correct given the
  wire state (empty hand → end turn). The wire state itself is wrong:
  the engine should have ended the enemy turn and dealt a new hand.

## what this means for the existing skipped tests

- `MerchantRoomTests.WalkToMerchant_*` — blocked until **(1)** is
  fixed (combat-stall) so the agent can reach the floors where
  merchants live.
- `ReachAct1BossTests.GreedyAgent_Ironclad_ReachesAct1BossRoom_OnFixedSeed`
  — blocked on the same stall at floor 17. Additionally, the test's
  `stopWhen: BossRoom` never fires because the engine surfaces
  `CombatRoom` (not `BossRoom`) for the boss fight — fixing this in
  the test requires a different stop signal (e.g. monster id =
  the boss).

Until (1) ships, both stay `[Skip]`'d.
