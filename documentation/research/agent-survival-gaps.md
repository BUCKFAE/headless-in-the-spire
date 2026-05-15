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

After the 2026-05-15 combat-stall fix + VFX-stub follow-up:

- **0/25 seeds stall in combat** (was 19/25 before the fix).
- 18/25 seeds reach a legitimate game-over inside the 20-floor budget —
  forward progress is real; the greedy agent just dies to standard
  encounters because it doesn't plan energy.
- 7/25 seeds NRE inside an event-room handler. Single bug class —
  the engine tries to load a card-select scene
  (`simple_card_select_screen.tscn` or `deck_upgrade_select_screen.tscn`)
  via `ResourceLoader.Load`, gets back null from our stub, and NREs
  inside `NSimpleCardSelectScreen.Create` or
  `NDeckUpgradeSelectScreen.ShowScreen`. Affected events so far:
  `RoomFullOfCheese.Gorge`, `SapphireSeed.Eat`.

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

### A. card-select-screen NREs (7 seeds)

Seeds 11, 12, 14, 17, 22, 23, 24 trip a `NullReferenceException` inside
an event handler whose chosen option opens a card-select screen. The
engine path is:

```
EventOption.Chosen()
  → <EventName>.<MethodName>()                  // e.g. RoomFullOfCheese.Gorge
    → CardSelectCmd.<FromVariant>(...)          // FromSimpleGridForRewards / FromDeckForUpgrade
      → <screen-class>.Create / ShowScreen()    // NSimpleCardSelectScreen / NDeckUpgradeSelectScreen
        → ResourceLoader.Load(<tscn path>)      // returns null in headless
          → NRE inside the screen ctor
```

`Asset not cached: res://scenes/screens/card_selection/*.tscn` in the
log preceding the NRE is the smoking gun.

**Next-slice starting points** (pick one):

1. Harmony-patch `CardSelectCmd.*` and/or the `Show*Screen` methods to
   no-op (same shape as the resolved `TalkCmd.Play` patch). Caller code
   needs to tolerate a null result — most do, since real Godot returns
   null when the scene hasn't loaded yet. The risk: some events
   short-circuit on the cmd's return value to apply their gameplay
   effect (e.g. SapphireSeed.Eat → "did the player actually upgrade a
   card?"). A blanket no-op skips the gameplay effect, not just the UI.

2. Teach `GreedyAgent.StepEventAsync` to *avoid* options whose chosen
   handler is known to crash. Stable option ids would make this
   tractable; otherwise the agent would have to discover the crash by
   try/snapshot/rollback, which isn't supported by the wire.

3. Build a minimal "headless `Show*Screen` stand-in" — a Harmony patch
   that returns a stub screen object whose tween/await chain resolves
   immediately. Most invasive but preserves the gameplay effect.

### B. `EventRoom` with no surfaced options (1 seed in prior scan)

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
