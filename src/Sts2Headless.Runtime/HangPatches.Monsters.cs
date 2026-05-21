using System.Reflection;
using HarmonyLib;

namespace Sts2Headless.Runtime;

// Per-monster patches. Every monster needs the same three-step shape:
// resolve the type by FQN, filter declared methods to a name set, prefix each
// by return-type kind. The shared PatchMonsterMethods helper at the bottom
// does the work; per-monster wrappers exist for the narrative comment
// (which call chain NREs, what gameplay cost we accept by no-op'ing it).
//
// Each (TypeFqn, MethodNames) pair lives on a private static `MonsterPatchEntry`
// field that the wrapper passes to PatchMonsterMethods. `EnumerateMonsterPatchEntries`
// reflects over those fields so MonsterPatchAuditor can walk every patched
// method's IL without duplicating the registry — adding a new entry below
// auto-extends the audit's input set.

// Data shape for one monster's patch set: the FQN to resolve from sts2.dll,
// the set of declared method names to patch on that type, and a human-
// readable label that flows through to the PatchOutcome record.
internal sealed record MonsterPatchEntry(
    string TypeFqn,
    IReadOnlySet<string> MethodNames,
    string Label);

public static partial class HangPatches
{
    // Reflection-based registry. Every `MonsterPatchEntry` static field on
    // this class is enumerated as a registry member. Cheaper to maintain
    // than a hand-curated list and impossible to forget — adding a new
    // _xEntry field is the entire wiring step.
    internal static IReadOnlyList<MonsterPatchEntry> EnumerateMonsterPatchEntries() =>
        typeof(HangPatches)
            .GetFields(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(MonsterPatchEntry))
            .Select(f => (MonsterPatchEntry)f.GetValue(null)!)
            .OrderBy(e => e.TypeFqn, StringComparer.Ordinal)
            .ToList();

    // Vantom (Act 1 elite-ish encounter) executes DismemberMove during its
    // enemy turn. The body NREs internally — not on a missing Godot stub
    // (no MissingMethodException surfaces; just a bare NRE), so reflective
    // probe-combat-stall enumeration can't name the gap. Confirmed via
    // `just probe-combat-stall 22` after the card-select recovery slice:
    //
    //   System.NullReferenceException
    //     at Vantom.DismemberMove(IReadOnlyList`1 targets)
    //     at MonsterMoveStateMachine.MoveState.PerformMove(IEnumerable`1)
    //     at MonsterModel.PerformMove()
    //     at Creature.TakeTurn()
    //
    // Swallowed by TaskHelper.LogTaskExceptions inside ExecuteEnemyTurn →
    // CombatManager left half-transitioned (IsEnemyTurnStarted=True,
    // EndingPlayerTurnPhaseTwo=True, IsPlayPhase=False, hand empty,
    // energy 0/3) — the classic combat-stall shape.
    //
    // Patch shape: void-returning prefix that skips the body. Vantom
    // simply doesn't perform DismemberMove in headless; the enemy turn
    // completes, the combat continues. Acceptable for agent survival.
    // Other Vantom moves are left intact so the encounter still threatens
    // the player; pure no-op of the whole monster would make Act 1 boring
    // rather than survivable.
    private static readonly MonsterPatchEntry _vantomEntry = new(
        TypeFqn: "MegaCrit.Sts2.Core.Models.Monsters.Vantom",
        MethodNames: new HashSet<string>(StringComparer.Ordinal) { "DismemberMove" },
        Label: "MegaCrit.Sts2.Core.Models.Monsters.Vantom.DismemberMove");
    private static PatchOutcome PatchVantomDismemberMove(Harmony harmony, Assembly sts2)
        => PatchMonsterMethods(harmony, sts2, _vantomEntry);

    // ThievingHopper (Act 2 enemy on seed 42 floor 3) carries five move
    // methods on the monster type — ThieveryMove, NabMove, HatTrickMove,
    // FlutterMove, EscapeMove. After patching EscapeArtistPower.AfterTurnEnd
    // the agent's end-turn still produced an infinite end-turn loop, so the
    // hang is in the move-execution body (same shape as Vantom.DismemberMove
    // in Act 1) rather than the post-turn power hook. Discovered via
    // DiagnoseAct2WalkTests on seed 42, Act 2 floor 3.
    //
    // Patch shape: replace every Task-returning Move body with
    // Task.CompletedTask. The hopper still threatens the agent via wire-
    // surfaced intent damage (the engine reports its NextMove correctly),
    // but the actual move execution is a no-op — the enemy turn unblocks
    // and the engine flips back to play phase. With the 999/999 HP cheat
    // the agent doesn't actually take damage anyway, so the loss of move
    // effects is acceptable for the goal-state multi-act drive.
    private static readonly MonsterPatchEntry _thievingHopperEntry = new(
        TypeFqn: "MegaCrit.Sts2.Core.Models.Monsters.ThievingHopper",
        MethodNames: new HashSet<string>(StringComparer.Ordinal)
        {
            "ThieveryMove", "NabMove", "HatTrickMove", "FlutterMove", "EscapeMove",
        },
        Label: "MegaCrit.Sts2.Core.Models.Monsters.ThievingHopper.*Move");
    private static PatchOutcome PatchThievingHopperMoves(Harmony harmony, Assembly sts2)
        => PatchMonsterMethods(harmony, sts2, _thievingHopperEntry);

    // BowlbugRock (Act 2 enemy on seed 42 floor 12) has two move methods
    // — HeadbuttMove and DizzyMove. Same shape as Vantom.DismemberMove
    // and ThievingHopper.*Move: Task-returning bodies that NRE in
    // headless, exception swallowed by TaskHelper.LogTaskExceptions,
    // combat half-transitioned. Replace both with Task.CompletedTask.
    private static readonly MonsterPatchEntry _bowlbugRockEntry = new(
        TypeFqn: "MegaCrit.Sts2.Core.Models.Monsters.BowlbugRock",
        MethodNames: new HashSet<string>(StringComparer.Ordinal) { "HeadbuttMove", "DizzyMove" },
        Label: "MegaCrit.Sts2.Core.Models.Monsters.BowlbugRock.*Move");
    private static PatchOutcome PatchBowlbugRockMoves(Harmony harmony, Assembly sts2)
        => PatchMonsterMethods(harmony, sts2, _bowlbugRockEntry);

    // SoulNexus (Act 3 enemy on seed 42) carries three Task-returning
    // move methods (SoulBurnMove, MaelstromMove, DrainLifeMove) and a
    // void AfterDeath(Creature) hook. The first observed failure on
    // this monster was a host-side NRE on run/play_card when SOUL_NEXUS
    // was at 6/234 HP — the killing-blow card triggered the
    // AfterDeath hook, which NRE'd. The Move bodies follow the same
    // shape as every other monster move we've patched.
    //
    // Patch shape:
    //   * Three Move methods → Task.CompletedTask via ReturnDefaultTaskPrefix.
    //   * AfterDeath (void) → SkipVoidPrefix (same as Vantom.DismemberMove's
    //     void overload).
    // BeforeRemovedFromRoom is the actual offender on the killing-blow path
    // observed in BeatGameOnSeed42Tests Act 3 floor 7 — the sts2 call chain
    // is StrikeIronclad.OnPlay → AttackCommand → CreatureCmd.Kill →
    // CombatManager.RemoveCreature → this method, which NREs walking
    // UI-only state. Patched alongside the Move methods + AfterDeath for
    // defense in depth.
    private static readonly MonsterPatchEntry _soulNexusEntry = new(
        TypeFqn: "MegaCrit.Sts2.Core.Models.Monsters.SoulNexus",
        MethodNames: new HashSet<string>(StringComparer.Ordinal)
        {
            "SoulBurnMove", "MaelstromMove", "DrainLifeMove",
            "AfterDeath", "BeforeRemovedFromRoom",
        },
        Label: "MegaCrit.Sts2.Core.Models.Monsters.SoulNexus.{*Move, lifecycle}");
    private static PatchOutcome PatchSoulNexus(Harmony harmony, Assembly sts2)
        => PatchMonsterMethods(harmony, sts2, _soulNexusEntry);

    // TestSubject is the Act 2 boss. Its enemy-phase moves walk UI-only
    // state (animation queues, VFX setup) and NRE in headless — the
    // exceptions are swallowed by TaskHelper.LogTaskExceptions and the
    // engine never advances past round 1's enemy phase, leaving the
    // StallDetector to fire. Same pattern as the SoulNexus / Vantom /
    // ThievingHopper / BowlbugRock patches above.
    //
    // The full set of declared methods observed in BeatGameOnSeed42Tests
    // when the Pommel/Hellraiser combo reaches Act 2 floor 15:
    //   * BiteMove, SkullBashMove, MultiClawMove, Phase3LacerateMove,
    //     BigPounceMove, BurningGrowlMove — the boss's attacks.
    //   * Revive, RespawnMove — phase-transition / second-life moves.
    //   * TriggerDeadState, AfterAddedToRoom — lifecycle hooks invoked
    //     from CombatManager when the boss enters / dies. Patching these
    //     defensively (same as SoulNexus.BeforeRemovedFromRoom) covers
    //     the killing-blow path.
    private static readonly MonsterPatchEntry _testSubjectEntry = new(
        TypeFqn: "MegaCrit.Sts2.Core.Models.Monsters.TestSubject",
        MethodNames: new HashSet<string>(StringComparer.Ordinal)
        {
            "BiteMove", "SkullBashMove", "MultiClawMove", "Phase3LacerateMove",
            "BigPounceMove", "BurningGrowlMove", "RespawnMove",
            "Revive", "TriggerDeadState", "AfterAddedToRoom",
        },
        Label: "MegaCrit.Sts2.Core.Models.Monsters.TestSubject.{*Move, AfterAddedToRoom, Revive, TriggerDeadState}");
    private static PatchOutcome PatchTestSubject(Harmony harmony, Assembly sts2)
        => PatchMonsterMethods(harmony, sts2, _testSubjectEntry);

    // CeremonialBeast is the Act 1 boss reachable on seed 1. Same shape as
    // TestSubject / SoulNexus: Task-returning move bodies walk UI-only state
    // (animation triggers _stunTrigger / _unstunTrigger / _stunSfx, VFX setup)
    // and NRE in headless. The exception is swallowed by
    // TaskHelper.LogTaskExceptions; CombatManager is left half-transitioned
    // (IsPlayPhase=False, hand empty, round counter frozen), and the
    // StallDetector fires after 8 identical snapshots.
    //
    // The wedging move on the observed repro is the stun-self path:
    // CEREMONIAL_BEAST telegraphs intent=Stun, then enters SetStunned →
    // StunnedMove, which references UI animation infrastructure that
    // doesn't exist headless. Other moves (Plow / Crush / Stamp / Stomp /
    // BeastCry) are patched defensively — they have the same UI-dependent
    // shape and would trip on the round they happen to execute.
    //
    // Lifecycle hooks (AfterDeath, BeforeRemovedFromRoom, AfterAddedToRoom)
    // are patched defensively per the SoulNexus precedent — the killing-
    // blow path needs them to no-op rather than NRE.
    private static readonly MonsterPatchEntry _ceremonialBeastEntry = new(
        TypeFqn: "MegaCrit.Sts2.Core.Models.Monsters.CeremonialBeast",
        MethodNames: new HashSet<string>(StringComparer.Ordinal)
        {
            "PlowMove", "CrushMove", "StampMove", "StompMove", "BeastCryMove",
            "SetStunned", "StunnedMove",
            "AfterAddedToRoom", "AfterDeath", "BeforeRemovedFromRoom",
        },
        Label: "MegaCrit.Sts2.Core.Models.Monsters.CeremonialBeast.{*Move, SetStunned, lifecycle}");
    private static PatchOutcome PatchCeremonialBeast(Harmony harmony, Assembly sts2)
        => PatchMonsterMethods(harmony, sts2, _ceremonialBeastEntry);

    // ── Encounter-sweep wave ────────────────────────────────────────────
    //
    // The blob below patches the ten monsters surfaced by
    // EveryEncounterSmokeTests (`just sweep-encounters`). All match the
    // existing CeremonialBeast / SoulNexus / TestSubject shape — Task-
    // returning bodies NRE on UI/VFX state in headless. The per-monster
    // wrapper exists for the narrative comment, not for behaviour; the
    // shared PatchMonsterMethods helper does the work.

    // CORPSE_SLUGS_NORMAL → CorpseSlug (with RAVENOUS_POWER). The slug
    // moves NRE on the engine's slime-VFX setup; Ravenous is the on-kill
    // listener that handles "spawn a Slimed when corpse dies" — its
    // AfterDeath hook is the killing-blow path failure mode.
    private static readonly MonsterPatchEntry _corpseSlugEntry = new(
        TypeFqn: "MegaCrit.Sts2.Core.Models.Monsters.CorpseSlug",
        MethodNames: new HashSet<string>(StringComparer.Ordinal)
        {
            "GlompMove", "GoopMove", "WhipSlapMove",
        },
        Label: "MegaCrit.Sts2.Core.Models.Monsters.CorpseSlug.*Move");
    private static PatchOutcome PatchCorpseSlug(Harmony harmony, Assembly sts2)
        => PatchMonsterMethods(harmony, sts2, _corpseSlugEntry);

    // DECIMILLIPEDE_ELITE → DecimillipedeSegment (parent of *Front/Middle/
    // Back). Each segment owns REATTACH_POWER:25. The segment-move bodies
    // and AfterAddedToRoom/AfterDeath walk segment-link state via UI nodes
    // that don't exist headless.
    private static readonly MonsterPatchEntry _decimillipedeEntry = new(
        TypeFqn: "MegaCrit.Sts2.Core.Models.Monsters.DecimillipedeSegment",
        MethodNames: new HashSet<string>(StringComparer.Ordinal)
        {
            "BulkMove", "ConstrictMove", "ReattachMove", "WritheMove",
            "DeadMove", "AnimSegmentsAttack",
            "AfterDeath",
        },
        Label: "MegaCrit.Sts2.Core.Models.Monsters.DecimillipedeSegment.{*Move, AfterDeath}");
    private static PatchOutcome PatchDecimillipede(Harmony harmony, Assembly sts2)
        => PatchMonsterMethods(harmony, sts2, _decimillipedeEntry);

    // DOORMAKER_BOSS → Doormaker. Originally this patch no-op'd every
    // move method + SwapPhasePower, which silently stripped the boss
    // of its mechanics: HP stayed at the engine-design sentinel
    // (999999999, the MaxHp set before DramaticOpenMove runs
    // CreatureCmd.SetMaxAndCurrentHp(OriginalHp)) and HungerPower
    // never got applied. Boss combat then "deadlocked" at sentinel HP
    // until CombatBudgetGuard tripped at round 81.
    //
    // IL inspection (`just probe-method-body Doormaker DramaticOpenMove`)
    // showed each move actually does:
    //   * gameplay state mutations (CreatureCmd.SetMaxAndCurrentHp,
    //     PowerCmd.Apply/Remove, DamageCmd.Attack, set_IsPortalOpen,
    //     SwapPhasePower<T>) — MUST run for the fight to progress
    //   * a small fixed UI surface (Cmd.CustomScaledWait,
    //     Doormaker.UpdateVisual, TalkCmd.Play, NRunMusicController) —
    //     would NRE in headless
    //
    // SwapPhasePower<T>'s body is pure PowerCmd.Remove/Apply, no UI at
    // all — patching it was an over-reach.
    //
    // The proper fix patches the helpers (Cmd.CustomScaledWait via
    // PatchCmdWait + UpdateVisual here) and lets the move bodies and
    // SwapPhasePower run normally. AfterDeath stays patched
    // defensively per the SoulNexus precedent (its body may walk
    // post-mortem UI state).
    private static readonly MonsterPatchEntry _doormakerEntry = new(
        TypeFqn: "MegaCrit.Sts2.Core.Models.Monsters.Doormaker",
        MethodNames: new HashSet<string>(StringComparer.Ordinal)
        {
            "UpdateVisual", "AfterDeath",
        },
        Label: "MegaCrit.Sts2.Core.Models.Monsters.Doormaker.{UpdateVisual, AfterDeath}");
    private static PatchOutcome PatchDoormaker(Harmony harmony, Assembly sts2)
        => PatchMonsterMethods(harmony, sts2, _doormakerEntry);

    // GREMLIN_MERC_NORMAL → GremlinMerc spawns a FatGremlin via its moves;
    // both need patching for the encounter to finish out. HEIST_POWER is a
    // synchronous power (no Task-returning hooks per `probe-types`) so no
    // power patch is needed.
    private static readonly MonsterPatchEntry _fatGremlinEntry = new(
        TypeFqn: "MegaCrit.Sts2.Core.Models.Monsters.FatGremlin",
        MethodNames: new HashSet<string>(StringComparer.Ordinal)
        {
            "FleeMove", "SpawnedMove",
        },
        Label: "MegaCrit.Sts2.Core.Models.Monsters.FatGremlin.*Move");
    private static PatchOutcome PatchFatGremlin(Harmony harmony, Assembly sts2)
        => PatchMonsterMethods(harmony, sts2, _fatGremlinEntry);

    private static readonly MonsterPatchEntry _gremlinMercEntry = new(
        TypeFqn: "MegaCrit.Sts2.Core.Models.Monsters.GremlinMerc",
        MethodNames: new HashSet<string>(StringComparer.Ordinal)
        {
            "DoubleSmashMove", "GimmeMove", "HeheMove",
        },
        Label: "MegaCrit.Sts2.Core.Models.Monsters.GremlinMerc.*Move");
    private static PatchOutcome PatchGremlinMerc(Harmony harmony, Assembly sts2)
        => PatchMonsterMethods(harmony, sts2, _gremlinMercEntry);

    // TERROR_EEL_ELITE → TerrorEel (with VIGOR_POWER). VigorPower.AfterAttack
    // is patched separately. StunMove is the move the engine sequences into
    // after a stun-shaped action; without it patched the agent still stalls
    // on the round the eel's state machine picks Stun.
    private static readonly MonsterPatchEntry _terrorEelEntry = new(
        TypeFqn: "MegaCrit.Sts2.Core.Models.Monsters.TerrorEel",
        MethodNames: new HashSet<string>(StringComparer.Ordinal)
        {
            "CrashMove", "TerrorMove", "ThrashMove", "StunMove",
        },
        Label: "MegaCrit.Sts2.Core.Models.Monsters.TerrorEel.*Move");
    private static PatchOutcome PatchTerrorEel(Harmony harmony, Assembly sts2)
        => PatchMonsterMethods(harmony, sts2, _terrorEelEntry);

    // TUNNELER_WEAK → Tunneler. No named power in the stall fingerprint;
    // the hang is purely move-side.
    private static readonly MonsterPatchEntry _tunnelerEntry = new(
        TypeFqn: "MegaCrit.Sts2.Core.Models.Monsters.Tunneler",
        MethodNames: new HashSet<string>(StringComparer.Ordinal)
        {
            "BelowMove", "BiteMove", "BurrowMove",
        },
        Label: "MegaCrit.Sts2.Core.Models.Monsters.Tunneler.*Move");
    private static PatchOutcome PatchTunneler(Harmony harmony, Assembly sts2)
        => PatchMonsterMethods(harmony, sts2, _tunnelerEntry);

    // THE_INSATIABLE_BOSS → TheInsatiable. STRENGTH_POWER in the fingerprint
    // is vanilla and already works; the hang is the move bodies.
    private static readonly MonsterPatchEntry _theInsatiableEntry = new(
        TypeFqn: "MegaCrit.Sts2.Core.Models.Monsters.TheInsatiable",
        MethodNames: new HashSet<string>(StringComparer.Ordinal)
        {
            "BiteMove", "LiquifyMove", "SalivateMove", "ThrashMove",
        },
        Label: "MegaCrit.Sts2.Core.Models.Monsters.TheInsatiable.*Move");
    private static PatchOutcome PatchTheInsatiable(Harmony harmony, Assembly sts2)
        => PatchMonsterMethods(harmony, sts2, _theInsatiableEntry);

    // LAGAVULIN_MATRIARCH_BOSS and SLUMBERING_BEETLE_NORMAL share the
    // sleeping-monster shape: the encounter starts with the monster
    // asleep, and AfterAddedToRoom builds the sleep state. In headless
    // that hook NREs on UI bits, leaving the engine with IsInProgress=false
    // after debug/start_combat (the symptom in the sweep report). Patching
    // AfterAddedToRoom + the WakeUpMove path is what gets the combat to
    // actually enter and progress.
    //
    // The agent doesn't experience the asleep→awake transition (the
    // monster is functionally awake from turn 1 with intent reported via
    // the wire), but the encounter still completes and the engine no
    // longer wedges. Acceptable cost for the sweep's coverage signal.
    private static readonly MonsterPatchEntry _lagavulinMatriarchEntry = new(
        TypeFqn: "MegaCrit.Sts2.Core.Models.Monsters.LagavulinMatriarch",
        MethodNames: new HashSet<string>(StringComparer.Ordinal)
        {
            "DisembowelMove", "Slash2Move", "SlashMove", "SoulSiphonMove",
            "WakeUpMove", "SleepMove", "AfterAddedToRoom",
            // First-blood path: Hellraiser → AttackCommand → CreatureCmd.Damage →
            // Hook.AfterDamageReceived. Same shape as the Crusher fix.
            "AfterDamageReceived", "AfterDeath",
        },
        Label: "MegaCrit.Sts2.Core.Models.Monsters.LagavulinMatriarch.{*Move, lifecycle, AfterDamageReceived}");
    private static PatchOutcome PatchLagavulinMatriarch(Harmony harmony, Assembly sts2)
        => PatchMonsterMethods(harmony, sts2, _lagavulinMatriarchEntry);

    private static readonly MonsterPatchEntry _slumberingBeetleEntry = new(
        TypeFqn: "MegaCrit.Sts2.Core.Models.Monsters.SlumberingBeetle",
        MethodNames: new HashSet<string>(StringComparer.Ordinal)
        {
            "RolloutMove", "WakeUpMove", "AfterAddedToRoom",
        },
        Label: "MegaCrit.Sts2.Core.Models.Monsters.SlumberingBeetle.{*Move, AfterAddedToRoom}");
    private static PatchOutcome PatchSlumberingBeetle(Harmony harmony, Assembly sts2)
        => PatchMonsterMethods(harmony, sts2, _slumberingBeetleEntry);

    // KAISER_CRAB_BOSS spawns two `Crusher` monsters (revealed via
    // `--probe-encounter KAISER_CRAB_BOSS`). The encounter is one of the
    // few that has no `KaiserCrab` monster type — the boss is rendered
    // by NKaiserCrabBossBackground and powered by Crusher creatures.
    //
    // The play_card NRE captured in the probe is:
    //   System.NullReferenceException
    //     at Crusher.get_Background()
    //     at Crusher.AfterCurrentHpChanged_Patch1(Crusher, Creature, Decimal)
    //     at Hook.AfterCurrentHpChanged(IRunState, CombatState, Creature, Decimal)
    //     at CreatureCmd.Damage(...)
    //     at AttackCommand.Execute(...)
    //     at PommelStrike.OnPlay(...)
    //
    // i.e. Pommel Strike → damage → Hook.AfterCurrentHpChanged →
    // Crusher.AfterCurrentHpChanged_Patch1 walks the boss-background-
    // dependent get_Background, which is null in headless.
    //
    // Patch shape: zero out every Task-returning method on Crusher
    // (5 moves + 3 lifecycle hooks). The boss still threatens via wire-
    // surfaced intent damage and the 999 HP cheat keeps the agent alive
    // long enough to win.
    private static readonly MonsterPatchEntry _crusherEntry = new(
        TypeFqn: "MegaCrit.Sts2.Core.Models.Monsters.Crusher",
        MethodNames: new HashSet<string>(StringComparer.Ordinal)
        {
            "AdaptMove", "BugStingMove", "EnlargingStrikeMove",
            "GuardedStrikeMove", "ThrashMove",
            "AfterAddedToRoom", "AfterCurrentHpChanged", "BeforeDeath",
        },
        Label: "MegaCrit.Sts2.Core.Models.Monsters.Crusher.{*Move, AfterAddedToRoom, AfterCurrentHpChanged, BeforeDeath}");
    private static PatchOutcome PatchCrusher(Harmony harmony, Assembly sts2)
        => PatchMonsterMethods(harmony, sts2, _crusherEntry);

    // KAISER_CRAB_BOSS spawns a Crusher and a Rocket together (see the
    // sweep fingerprint: `enemies=[CRUSHER:..., ROCKET:...]`). Rocket
    // carries BACK_ATTACK_RIGHT_POWER + CRAB_RAGE_POWER and has the same
    // background-NRE shape as Crusher. Patch every Task-returning method.
    private static readonly MonsterPatchEntry _rocketEntry = new(
        TypeFqn: "MegaCrit.Sts2.Core.Models.Monsters.Rocket",
        MethodNames: new HashSet<string>(StringComparer.Ordinal)
        {
            "ChargeUpMove", "LaserMove", "PrecisionBeamMove",
            "RechargeMove", "TargetingReticleMove",
            "AfterAddedToRoom", "AfterCurrentHpChanged", "BeforeDeath",
        },
        Label: "MegaCrit.Sts2.Core.Models.Monsters.Rocket.{*Move, AfterAddedToRoom, AfterCurrentHpChanged, BeforeDeath}");
    private static PatchOutcome PatchRocket(Harmony harmony, Assembly sts2)
        => PatchMonsterMethods(harmony, sts2, _rocketEntry);

    // Shared helper for the monster *Move / lifecycle-hook patches above
    // (Vantom, ThievingHopper, BowlbugRock, SoulNexus, TestSubject,
    // CeremonialBeast). Every monster needs the same three-step shape:
    // resolve the type by FQN, filter declared methods to a name set,
    // and prefix each by return-type kind (Task → CompletedTask via
    // ReturnDefaultTaskPrefix, void → SkipVoidPrefix, reference returns
    // → ReturnNullPrefix, unsupported value-type returns → skip-and-log).
    //
    // Adding a new monster is now one method that calls this helper with
    // a type FQN + a set of move/lifecycle method names. The wrapper
    // method retains its narrative comment block (which engine call
    // chain NREs, what gameplay cost we accept by no-op'ing it) — that
    // documentation is the actual value the per-monster file structure
    // preserves; the boilerplate it surrounded is what we're collapsing.
    private static PatchOutcome PatchMonsterMethods(Harmony harmony, Assembly sts2, MonsterPatchEntry entry)
    {
        var monsterType = sts2.GetType(entry.TypeFqn);
        if (monsterType is null)
            return new PatchOutcome(entry.Label, Patched: false, Detail: $"type {entry.TypeFqn} not found");

        var methods = monsterType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(m => entry.MethodNames.Contains(m.Name) && !m.IsSpecialName)
            .ToArray();
        if (methods.Length == 0)
            return new PatchOutcome(entry.Label, Patched: false, Detail: $"no target methods on {entry.TypeFqn}");

        var taskPrefix = typeof(HangPatches).GetMethod(nameof(ReturnDefaultTaskPrefix), BindingFlags.Static | BindingFlags.NonPublic)!;
        var voidPrefix = typeof(HangPatches).GetMethod(nameof(SkipVoidPrefix), BindingFlags.Static | BindingFlags.NonPublic)!;
        var nullPrefix = typeof(HangPatches).GetMethod(nameof(ReturnNullPrefix), BindingFlags.Static | BindingFlags.NonPublic)!;

        var sigs = new List<string>(methods.Length);
        foreach (var m in methods)
        {
            // Harmony can't patch the OPEN definition of a generic method
            // (Doormaker.SwapPhasePower<T:PowerModel> is the surfacing
            // example — Harmony's IL-rewrite path fails at
            // MMReflectionImporter.ImportGenericParameter). But every
            // closed instantiation IS patchable. We scan the type's own
            // methods + nested compiler-generated state machines for
            // call/callvirt sites that pin the generic args and patch
            // each closed form. Sentinel-HP boss phases (Doormaker stuck
            // at 999996514 HP because SwapPhasePower never ran) get a
            // real fix instead of a "skipped:" annotation.
            if (m.IsGenericMethodDefinition || m.ContainsGenericParameters)
            {
                var closed = FindClosedGenericCallers(monsterType, m);
                if (closed.Count == 0)
                {
                    sigs.Add($"{m.Name} → {m.ReturnType.Name} (skipped: open-generic with no in-type closed callers)");
                    continue;
                }
                var prefix = PickPrefix(m, taskPrefix, voidPrefix, nullPrefix);
                if (prefix is null)
                {
                    sigs.Add($"{m.Name} → {m.ReturnType.Name} (skipped: unsupported value-type return)");
                    continue;
                }
                foreach (var closedMethod in closed)
                {
                    harmony.Patch(closedMethod, prefix: new HarmonyMethod(prefix));
                    var gargs = string.Join(",", closedMethod.GetGenericArguments().Select(t => t.Name));
                    sigs.Add($"{m.Name}<{gargs}>() → {closedMethod.ReturnType.Name}");
                }
                continue;
            }
            var pickedPrefix = PickPrefix(m, taskPrefix, voidPrefix, nullPrefix);
            if (pickedPrefix is null)
            {
                sigs.Add($"{m.Name} → {m.ReturnType.Name} (skipped: unsupported value-type return)");
                continue;
            }
            harmony.Patch(m, prefix: new HarmonyMethod(pickedPrefix));
            sigs.Add($"{m.Name}({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name))}) → {m.ReturnType.Name}");
        }
        return new PatchOutcome(entry.Label, Patched: true, Detail: string.Join(", ", sigs));
    }

    private static MethodInfo? PickPrefix(MethodInfo m, MethodInfo taskPrefix, MethodInfo voidPrefix, MethodInfo nullPrefix)
    {
        if (typeof(System.Threading.Tasks.Task).IsAssignableFrom(m.ReturnType)) return taskPrefix;
        if (m.ReturnType == typeof(void)) return voidPrefix;
        if (!m.ReturnType.IsValueType) return nullPrefix;
        return null;
    }

    // Walks the declaring type's own methods and nested compiler-generated
    // state machines for call/callvirt sites that close the open generic
    // method `openGeneric`. Returns the de-duplicated set of closed
    // instantiations. The IL of an async caller `await SwapPhasePower<X>()`
    // ends up on `Type+<MoveName>d__NN.MoveNext`, so we have to look at
    // nested types — declared-only on the parent misses every state
    // machine and would report zero callers for Doormaker.
    private static List<MethodInfo> FindClosedGenericCallers(Type declaringType, MethodInfo openGeneric)
    {
        var found = new List<MethodInfo>();
        ScanType(declaringType, declaringType, openGeneric, found);
        foreach (var nested in declaringType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
        {
            ScanType(nested, declaringType, openGeneric, found);
        }
        return found;
    }

    private static void ScanType(Type scanTarget, Type genericOwner, MethodInfo openGeneric, List<MethodInfo> sink)
    {
        MethodInfo[] methods;
        try { methods = scanTarget.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly); }
        catch { return; }
        foreach (var m in methods)
        {
            if (m.IsAbstract || m.ContainsGenericParameters) continue;
            List<CodeInstruction> instructions;
            try { instructions = PatchProcessor.GetCurrentInstructions(m); }
            catch { continue; }
            foreach (var ins in instructions)
            {
                if (ins.opcode != System.Reflection.Emit.OpCodes.Call
                    && ins.opcode != System.Reflection.Emit.OpCodes.Callvirt) continue;
                if (ins.operand is not MethodInfo target) continue;
                if (target.DeclaringType != genericOwner) continue;
                if (target.Name != openGeneric.Name) continue;
                if (!target.IsGenericMethod || target.IsGenericMethodDefinition) continue;
                if (!sink.Any(c => GenericArgsEqual(c, target))) sink.Add(target);
            }
        }
    }

    private static bool GenericArgsEqual(MethodInfo a, MethodInfo b)
    {
        var ga = a.GetGenericArguments();
        var gb = b.GetGenericArguments();
        if (ga.Length != gb.Length) return false;
        for (int i = 0; i < ga.Length; i++) if (ga[i] != gb[i]) return false;
        return true;
    }
}
