# agent-survival gaps

What happens when the `GreedyAgent` walks `run/new` → forward through Act 1
across a range of seeds. Generated from the diagnostic harness in
`tests/Sts2Headless.End2EndTests/DiagnoseMerchantSeedScanTests.cs` and the
in-process `--probe-combat-stall` walk over seeds 1..25 (Ironclad, heal-
between-rooms via `debug/set_hp`).

The reason this matters: **every end-to-end test depends on the agent
making forward progress.** `ReachAct1BossTests` and `MerchantRoomTests`
both ship today; the gaps below are documented as a punch list so the
next agent-survival slice has a starting point when new multi-room arcs
land.

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
  also floor 17) were a `NullReferenceException` on the unguarded
  `NGame.Instance.DoHitStop(2, 1)` call inside the move's MoveNext
  state machine. Fix is an IL transpiler
  (`HangPatches.PatchVantomDismemberMoveDoHitStop` in
  `src/Sts2Headless.Runtime/Patches/HangPatches.NGame.cs`) that
  rewrites the four-instruction window
  `call NGame.get_Instance; ldc.i4 ldc.i4; callvirt NGame.DoHitStop`
  to four `Nop`s. Receiver-null callvirt fails before any Harmony
  prefix could run, so the leaf-helper recipe doesn't help here — the
  surrounding `AttackCommand.Execute` and `AddToCombatAndPreview<Wound>`
  await chain runs untouched. Coverage in
  `tests/Sts2Headless.IntegrationTests/VantomDismemberHangTests.cs`.

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

2. **`HangPatches.PatchTalkCmdPlay`** (`src/Sts2Headless.Runtime/Patches/HangPatches.Cards.cs`).
   `BygoneEffigy.WakeMove` and similar intro moves call
   `TalkCmd.Play(LocString, Creature, VfxColor, VfxDuration)`, which
   returns `NSpeechBubbleVfx` and walks UI-only state to construct it.
   In headless those nodes are absent, so the body NREs. Harmony prefix
   skips the body and returns null; the NRE never fires, the enemy turn
   completes cleanly. Vantom.DismemberMove is a different story — its
   NRE fires on a receiver-null `callvirt` before any prefix could run,
   so it's patched via the IL transpiler described in the summary
   above (not a body-skip prefix). Vantom's other moves stay intact so
   the encounter still threatens the player.

3. **`ProbeCombatStallCommand`** (`src/Sts2Headless.Commands/probe/`).
   The diagnostic probe that walks a seed in-process, drives the
   agent's logic via the bindings, and on the first stalled combat
   dumps engine state plus a short list of "WaitX"/"YieldX" methods in
   sts2.dll. Run via `just runner::probe::combat-stall <seed> [floor]`. This is
   the tool to reach for whenever a new combat-stall regression
   surfaces — `Method not found:` in the stderr names the next stub
   gap.

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

1. **`HeadlessCardSelector` + `HeadlessCardSelectorBridge`**
   (`src/Sts2Headless.Runtime/CardSelection/`). The engine's own
   `CardSelectCmd.UseSelector(ICardSelector)` hook lets us install a
   headless `ICardSelector` implementation (via `DispatchProxy`, AD-4
   safe) that returns a pre-queued pick instead of opening a screen.
   Hand-side factories (`CardSelectCmd.FromHandForUpgrade`,
   `FromHandForDiscard`, `FromHand`) each get a per-factory Harmony
   prefix in `HangPatches.Cards.cs` that resolves the pick through the
   same queue. This replaced the original blanket `From*`-factory
   prefix (commit `7622137`): with the bridge the engine's async
   state machine completes normally, so card-pick cards work
   end-to-end (Armaments, BurningPact, Headbutt, …) instead of just
   surviving.

2. **`GreedyAgent.StepEventAsync`** now picks the *last* unlocked
   option. By sts2 convention the "leave / decline / safe" choice
   tends to be last (e.g. `ROOM_FULL_OF_CHEESE.SEARCH` after `.GORGE`),
   so for a fraction of affected events the agent threads through a
   handler that doesn't card-select at all. Not robust — some events
   have the broken option last (`SELF_HELP_BOOK.READ_PASSAGE`,
   `WELLSPRING.BATHE`, `WOOD_CARVINGS.TORUS`) — but it's a cheap
   heuristic that costs nothing on the failure path.

3. **`Sts2Bindings.SelectEventOption` recovery** — belt-and-braces
   safety net for the residual NREs that still fire from event paths
   the bridge doesn't cover. `_eventOptionChosen.Invoke(...)` is
   wrapped in try-catch; on exception we still call
   `AutoAdvanceFinishedEvent`, whose `EnterRoom(MapRoom)` fallback
   flips the room type even if `IsFinished` is still false. The
   bindings then verify the room actually left `EventRoom`; if not,
   the original exception is re-thrown wrapped in an
   `InvalidOperationException`. Net effect: any event whose handler
   crashes mid-`Chosen` becomes a no-effect "land back on the map"
   instead of a host-killing exception. Gameplay effects (HP cost,
   reward) are skipped on the recovery path, but the engine stays
   self-consistent and the agent keeps walking the run.

### B. `EventRoom` with no surfaced options (1 seed in prior scan)

Seed 17 previously hit `EventRoom` with `availableEventOptions.Count == 0`.
That symptom hasn't recurred in the post-fix sweep — likely covered by
the combat-stall fix changing the seed's path before it reaches the
unsurfaced event. Re-investigate only if the symptom re-appears.

## what the boss test still needs

`ReachAct1BossTests.GreedyAgent_Ironclad_ReachesAct1BossRoom_OnFixedSeed`
and `BeatAct1BossOnSeed42Tests.Seed42Agent_Ironclad_BeatsVantom_WithMaxHpCheat`
both pass — the agent reaches and kills VANTOM on seed 42 (with the
maxHp=999 cheat). The remaining loose end is a fair-start win; the
boss-room detection and combat-state surface are closed.

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

### resolved: Seed42Agent beats VANTOM (cheat-mode)

`BeatAct1BossOnSeed42Tests.Seed42Agent_Ironclad_BeatsVantom_WithMaxHpCheat`
drives the new `Seed42Agent` from `run/new` through the full seed-42
path, kills VANTOM in 12 rounds, and lands at floor 17 with no game-over.
The agent's combat play is unmodified; the only artificial input is a
`debug/set_hp(999, 999)` call at run start that papers over two
remaining engine gaps (Phrog+wriggler elite HP burn; the hp=0
select_reward NRE). End-to-end proof the wire + agent + engine reach
the boss cleanly with intent damage, potions, and SLIPPERY-aware
combat all wired through.

Engine surface that landed this slice:
- `BossRoom` snapshot now carries `combatState` (was `null` — gate fix).
- AttackIntent damage/hits surfaced on the wire via `DamageCalc:Func<int>`
  (returns Decimal in sts2, rounded to int wire-side).
- `run/use_potion` + `ownedPotions` snapshot field — agent can drink
  Block/Regen/Strength/etc. potions mid-combat.

### still open: fair-start agent (no maxHp cheat)

A "real" seed-42 win without the HP cheat is gated on two things:

1. **Floor-8 elite survivability.** Phrog → 4 wrigglers burns ~60 HP
   from a starter Ironclad deck. Seed42Agent reaches floor 15 on a fair
   start (was floor 9 before potion use) but the floor-15 Fogmog fight
   still wipes the remaining HP because the bag is empty of healing
   potions by then. A smarter combat AI (one-ply simulation,
   damage-EV ranking instead of the current priority queue) is the
   likely fix.

2. **hp=0 select_reward NRE.** When the player ends a combat at hp=0
   (engine doesn't game-over at zero), the next card-add to the deck
   triggers an internal NRE. Symptom-only fix: catch the throw in
   `Sts2Bindings.SelectReward` and skip the card. Real fix: figure out
   what engine state is misaligned at hp=0 mid-reward.

Neither blocks the boss-beating slice — both are agent-skill or
follow-up engine work for a separate iteration.
