# agent-survival gaps

What happens when the `GreedyAgent` walks `run/new` → forward through Act 1
across a range of seeds. Generated from the diagnostic harness in
`tests/Sts2Headless.End2EndTests/DiagnoseMerchantSeedScanTests.cs` and the
in-process `--probe-combat-stall` walk over seeds 1..25 (Ironclad, heal-
between-rooms via `debug/set_hp`).

The reason this matters: **every end-to-end test depends on the agent
making forward progress.** Today only `ReachAct1BossTests` and the future
`MerchantRoomTests` are blocked, but every multi-room arc the project
adds will hit the same gaps. They're documented here as a punch list so
the next agent-survival slice has a starting point.

## summary

After the 2026-05-15 combat-stall fix:

- **0/25 seeds stall in combat** (was 19/25 before the fix).
- 15/25 seeds reach a legitimate game-over inside the 20-floor budget —
  forward progress is real; the greedy agent just dies to standard
  encounters because it doesn't plan energy.
- 5/25 seeds hit downstream `GodotStubs` gaps that surface synchronously
  (`ParticleProcessMaterial.set_EmissionBoxExtents` is the current head
  of that queue). Each fix adds one stub and re-runs the probe.
- 5/25 seeds NRE inside an event-room handler. Separate bug class —
  the engine path tries to load a `simple_card_select_screen.tscn`
  scene that isn't shipped to headless.

The combat-stall pattern documented in earlier revisions of this file
("agent calls end_turn repeatedly with `IsPlayPhase=false`, hand=0,
energy=0/3") is **fully resolved**. The fix is the cluster of stubs
+ a Harmony patch in commit-of-this-revision.

## what the combat-stall actually was (resolved)

Monster moves (e.g. `Flyconid.VulnerableSporesMove`,
`BygoneEffigy.WakeMove`, anything that touches `Node2D.GlobalPosition` or
`Colors.Green`) call into Godot APIs whose stubs weren't present. The
resulting `MissingMethodException` was thrown inside the enemy-turn's
fire-and-forget async chain, where `TaskHelper.LogTaskExceptions`
swallowed it and routed to the engine's logger. From the wire's
perspective this looked like an indefinite stall: `IsInProgress=True`,
`IsEnemyTurnStarted=True`, `EndingPlayerTurnPhaseTwo=True`,
`IsPlayPhase=False`, with the hand cleared and energy at 0/3 — the
classic half-transitioned state.

The fix bundles three classes of change:

1. **GodotStubs growth** (`src/GodotStubs/`). Each gap below was added
   as a one-line stub with a `// from:` comment naming the monster move
   or engine path that forced it:
   - `Godot.Node.GetNode<T>(NodePath)` + non-generic overload
   - `Godot.Node2D.GlobalPosition`
   - `Godot.Colors.Green`
   - `Godot.Color..ctor(Single, Single, Single, Single)` + R/G/B/A
     fields
   - `Godot.Mathf.RoundToInt(Single)` + `Round(Single)` / `Round(Double)`
   - `Godot.CanvasItem.GetViewportRect()`
   - `Godot.GpuParticles2D.ProcessMaterial`
   - `Godot.Vector3..ctor(Single, Single, Single)`
   - `Godot.ResourceLoader.Load<T>` / `Load` / `Exists` + `CacheMode` enum

2. **`HangPatches.PatchTalkCmdPlay`** (`src/Sts2Headless.Runtime/HangPatches.cs`).
   `BygoneEffigy.WakeMove` and similar intro moves call
   `TalkCmd.Play(LocString, Creature, VfxColor, VfxDuration)`, which
   returns `NSpeechBubbleVfx` and walks UI-only state to construct it.
   In headless those nodes are absent, so the body NREs. Harmony prefix
   skips the body and returns null; the NRE never fires, the enemy turn
   completes cleanly.

3. **`ProbeCombatStallCommand`** (`src/Sts2Headless/`). The diagnostic
   probe that walks a seed in-process, drives the agent's logic via the
   bindings, and on the first stalled combat dumps engine state plus a
   short list of "WaitX"/"YieldX" methods in sts2.dll. Run via
   `just probe-combat-stall <seed> [floor]`. This is the tool to reach
   for whenever a new combat-stall regression surfaces — `Method not
   found:` in the stderr names the next stub gap.

## remaining gaps (next slices)

### A. `ParticleProcessMaterial.set_EmissionBoxExtents(Vector3)` (5 seeds)

Same shape as the resolved cluster — a VFX call site that needs a
one-line stub. Hits seeds 8, 15, 17, 21, 24. Each fix surfaces the next
VFX call (`EmissionShape`, `Gravity`, …); the chain ends when the move's
construction finishes without referencing a missing member.

**Next-slice starting point**: run `--probe-combat-stall --seed 8`, read
the unhandled-exception type/name, add the stub to
`src/GodotStubs/CombatStubs.cs` or `Resources.cs` next to the existing
`ParticleProcessMaterial` shell, re-run. Repeat until seed 8 reports
`GAME-OVER`.

### B. event-room NREs (5 seeds)

Seeds 11, 12, 14, 22, 23 trip a `NullReferenceException` inside event
handlers — the engine tries to load a card-select scene
(`res://scenes/screens/card_selection/simple_card_select_screen.tscn`)
via `NSimpleCardSelectScreen.Create` and then NREs on its return. This
is *not* a combat stall; it's a `ResourceLoader.Load` returning null
where the engine assumes a live scene.

**Next-slice starting point**: identify the specific event types
involved (one is `RoomFullOfCheese`). Either patch the event to skip
its card-select via Harmony (same shape as `TalkCmd.Play`), or
short-circuit the wire-side event resolution so the agent never picks
a card-select-requiring option. The agent could also be taught to
prefer non-card-select event options.

### C. `EventRoom` with no surfaced options (1 seed in prior scan)

Seed 17 previously hit `EventRoom` with `availableEventOptions.Count == 0`.
That symptom hasn't recurred in the post-fix sweep — likely covered by
the combat-stall fix changing the seed's path before it reaches the
unsurfaced event. Re-investigate only if the symptom re-appears.

## what the boss test still needs

`ReachAct1BossTests.GreedyAgent_Ironclad_ReachesAct1BossRoom_OnFixedSeed`
still fails — but no longer on a combat stall. The remaining failure
modes:

1. The engine surfaces `RoomType.CombatRoom` (not `BossRoom`) for the
   Act 1 boss fight, so the test's `stopWhen: BossRoom` never fires.
   The fix is to add a stop signal based on monster id (the boss has a
   stable id) or to flip the engine's room type to `BossRoom` at the
   wire layer when entering the boss node.
2. The greedy agent doesn't survive the early-Act-1 combats long enough
   to reach floor 17 even with heal-between-rooms. The probe shows
   seed 42 game-overs at floor 8 with the same heal policy the test
   uses. A smarter agent (or extra heals between rounds inside a
   combat, not just between rooms) would be needed to consistently
   reach the boss.

Both are *separate slices* from "fix the combat stall."
