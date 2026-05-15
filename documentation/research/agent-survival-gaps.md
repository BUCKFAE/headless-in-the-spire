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

After the 2026-05-15 combat-stall fix, VFX-stub follow-up,
card-select-screen recovery slice, and the late-Act-1 monster-stall
slice:

- **25/25 seeds reach a legitimate game-over** inside the 20-floor
  budget. Forward progress is real on every seed in the diagnostic
  range; the greedy agent just dies to standard encounters because it
  doesn't plan energy.
- **0/25 seeds host-die on a card-select NRE** (was 7/25 after the
  combat-stall fix). `Sts2Bindings.SelectEventOption` now wraps
  `EventOption.Chosen()` in a try-catch; on failure the bindings
  fire `AutoAdvanceFinishedEvent`'s `EnterRoom(MapRoom)` fallback so
  the player lands back on the map with no event effect applied.
- **0/25 seeds stall on late-Act-1 monsters.** Seed 18 (`KIN_FOLLOWER`
  at floor 17) was a missing `Node2D.Scale` getter — added to
  `GodotStubs/Nodes.cs`. Seeds 22 and 23 (`VANTOM.DismemberMove`,
  also floor 17) were an internal NRE inside the move's body with
  no MissingMethodException to name the gap; patched with a
  `HangPatches.PatchVantomDismemberMove` no-op so Vantom skips that
  one move and the enemy turn completes.

Affected events that exercise the recovery path (each one has the same
shape: chosen option's handler awaits a `CardSelectCmd.From*` factory,
the factory's pre-first-await `Create()` synchronously NREs on
`ResourceLoader.Load(...) == null`, the engine NREs again at the model
layer's null-collection dereference): `RoomFullOfCheese.Gorge`,
`SapphireSeed.Eat`, `SelfHelpBook.ReadPassage`, `WoodCarvings.*`,
`Wellspring.Bathe`, `AromaOfChaos.MaintainControl`, and possibly more —
the agent now picks the last unlocked option in events, so some events
land on a safe option instead of triggering recovery.

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
   - `Godot.Node2D.Scale` (KIN_FOLLOWER/KIN_PRIEST at floor 17,
     surfaced after the card-select slice unblocked deeper Act 1)

2. **`HangPatches.PatchTalkCmdPlay`** (`src/Sts2Headless.Runtime/HangPatches.cs`).
   `BygoneEffigy.WakeMove` and similar intro moves call
   `TalkCmd.Play(LocString, Creature, VfxColor, VfxDuration)`, which
   returns `NSpeechBubbleVfx` and walks UI-only state to construct it.
   In headless those nodes are absent, so the body NREs. Harmony prefix
   skips the body and returns null; the NRE never fires, the enemy turn
   completes cleanly. The same shape applies to
   `HangPatches.PatchVantomDismemberMove`, added in the late-Act-1
   stall slice — `Vantom.DismemberMove` is a void method whose body
   NREs with no surfacing `MissingMethodException`, so the prefix
   simply skips the body. Vantom's other moves stay intact so the
   encounter still threatens the player.

3. **`ProbeCombatStallCommand`** (`src/Sts2Headless/`). The diagnostic
   probe that walks a seed in-process, drives the agent's logic via the
   bindings, and on the first stalled combat dumps engine state plus a
   short list of "WaitX"/"YieldX" methods in sts2.dll. Run via
   `just probe-combat-stall <seed> [floor]`. This is the tool to reach
   for whenever a new combat-stall regression surfaces — `Method not
   found:` in the stderr names the next stub gap.

## what the card-select-screen NRE actually was (recovered, not fixed)

Seeds 11, 12, 14, 17, 22, 23, 24 tripped a `NullReferenceException`
inside an event handler whose chosen option opens a card-select screen.
The engine path is:

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

The fix bundles three pieces of slightly different shapes:

1. **`HangPatches.PatchCardSelectCmdFactories`** — every
   `MegaCrit.Sts2.Core.Commands.CardSelectCmd.From*` factory is
   `static async Task<CardSelectCmd>` whose pre-first-await body calls
   the screen-`Create` that NREs. A Harmony prefix returns
   `Task.FromResult<CardSelectCmd>(default)` and skips the original.
   Without this patch the NRE bubbles synchronously past the async
   state machine and aborts the host; with it, the caller's await
   yields null and the dereference NRE lands at the event-model layer.

2. **`GreedyAgent.StepEventAsync`** now picks the *last* unlocked
   option. By sts2 convention the "leave / decline / safe" choice
   tends to be last (e.g. `ROOM_FULL_OF_CHEESE.SEARCH` after `.GORGE`),
   so for a fraction of affected events the agent threads through a
   handler that doesn't card-select at all. Not robust — some events
   have the broken option last (`SELF_HELP_BOOK.READ_PASSAGE`,
   `WELLSPRING.BATHE`, `WOOD_CARVINGS.TORUS`) — but it's a cheap
   heuristic that costs nothing on the failure path.

3. **`Sts2Bindings.SelectEventOption` recovery** — the real fix.
   `_eventOptionChosen.Invoke(...)` is wrapped in try-catch; on
   exception we still call `AutoAdvanceFinishedEvent`, whose
   `EnterRoom(MapRoom)` fallback flips the room type even if
   `IsFinished` is still false. The bindings then verify the room
   actually left `EventRoom`; if not, the original exception is
   re-thrown wrapped in an `InvalidOperationException`. Net effect:
   any event whose handler crashes mid-`Chosen` becomes a no-effect
   "land back on the map" instead of a host-killing exception.

The gameplay effects of broken events are entirely skipped — neither
HP cost nor reward applies. That's the right tradeoff for an
agent-survival fix; the engine remains in a self-consistent state and
the agent can keep walking the run.

**Future slice — proper card-select screen stand-in.** If/when an
end-to-end test wants the event's gameplay effect to land (e.g. "after
picking SAPPHIRE_SEED.EAT, the deck contains the chosen upgrade"), the
right answer is option 3 from the earlier sketch: stub
`NSimpleCardSelectScreen.Create` / `NDeckUpgradeSelectScreen.ShowScreen`
to return a screen whose "selected" field is pre-populated with a
default pick. Until such a test exists, the recovery path is enough.

### B. `EventRoom` with no surfaced options (1 seed in prior scan)

Seed 17 previously hit `EventRoom` with `availableEventOptions.Count == 0`.
That symptom hasn't recurred in the post-fix sweep — likely covered by
the combat-stall fix changing the seed's path before it reaches the
unsurfaced event. Re-investigate only if the symptom re-appears.

## what the boss test still needs

`ReachAct1BossTests.GreedyAgent_Ironclad_ReachesAct1BossRoom_OnFixedSeed`
now passes — the boss-room stop signal landed in this revision. The
remaining loose end is the agent-survival concern below; the boss-room
detection itself is closed.

### resolved: boss-room stop signal

sts2 itself has no dedicated `BossRoom` type — the act boss is a regular
`CombatRoom` whose monster is the act boss — so callers using
`currentRoomType == BossRoom` as a stop condition never triggered.
`Sts2Bindings.BuildSnapshot` now flips `CombatRoom → BossRoom` when the
player's current `MapPoint.PointType` is `"Boss"`, which is the engine's
own enum-member name for the top-row act-boss node (validated against
seed 42 row=16's only child). The `RoomType` enum gains a comment naming
itself a "wire-level synthetic" so future readers don't go looking for a
`BossRoom : Room` type that doesn't exist; `MapNodeType.Boss` is no
longer marked speculative.

### resolved: combatState was null in BossRoom

A direct consequence of the wire-level synthetic. `BuildSnapshot` flipped
the room label from `CombatRoom` to `BossRoom` *before* the combat-state
read gate, which only fired on `roomType == CombatRoom`. The engine's
actual room is still `CombatRoom` with a live `CombatManager.Instance`,
but the wire returned `combatState: null` for every boss snapshot, so any
agent stepping into the boss fight threw "in BossRoom but combatState is
null" on its very first read. Fix is one line: gate combat-state reads on
`roomType == CombatRoom || roomType == BossRoom`. Surfaced by
`Seed42ReconTests` driving the greedy agent through to the act boss —
without this fix the boss combat itself was unobservable.

### still open: agent survival to the boss

The greedy agent doesn't survive the early-Act-1 combats long enough to
reach floor 17 from every seed even with heal-between-rooms. The
in-process probe shows seed 42 game-overs at floor 8 (probe biases
toward Monster nodes and skips rest sites); the `ReachAct1BossTests`
path uses `GreedyAgent.StepMapAsync`'s richer routing (including rest
sites) plus debug-set_hp resurrection inside the test loop, and that's
enough for seed 42 to reach the boss. A smarter agent — or extra heals
between rounds inside a combat, not just between rooms — would be
needed to consistently reach the boss on arbitrary seeds. *Separate
slice from "fix the combat stall."*
