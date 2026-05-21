using System.Reflection;
using HarmonyLib;

namespace Sts2Headless.Runtime;

// Per-monster patches. Every monster needs the same three-step shape:
// resolve the type by FQN, filter declared methods to a name set, prefix each
// by return-type kind. The shared PatchMonsterMethods helper at the bottom
// does the work; per-monster wrappers exist for the narrative comment
// (which call chain NREs, what gameplay cost we accept by no-op'ing it).
public static partial class HangPatches
{
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
    private static PatchOutcome PatchVantomDismemberMove(Harmony harmony, Assembly sts2)
        => PatchMonsterMethods(harmony, sts2,
            typeFqn: "MegaCrit.Sts2.Core.Models.Monsters.Vantom",
            methodNames: new HashSet<string>(StringComparer.Ordinal) { "DismemberMove" },
            label: "MegaCrit.Sts2.Core.Models.Monsters.Vantom.DismemberMove");

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
    private static PatchOutcome PatchThievingHopperMoves(Harmony harmony, Assembly sts2)
        => PatchMonsterMethods(harmony, sts2,
            typeFqn: "MegaCrit.Sts2.Core.Models.Monsters.ThievingHopper",
            methodNames: new HashSet<string>(StringComparer.Ordinal)
            {
                "ThieveryMove", "NabMove", "HatTrickMove", "FlutterMove", "EscapeMove",
            },
            label: "MegaCrit.Sts2.Core.Models.Monsters.ThievingHopper.*Move");

    // BowlbugRock (Act 2 enemy on seed 42 floor 12) has two move methods
    // — HeadbuttMove and DizzyMove. Same shape as Vantom.DismemberMove
    // and ThievingHopper.*Move: Task-returning bodies that NRE in
    // headless, exception swallowed by TaskHelper.LogTaskExceptions,
    // combat half-transitioned. Replace both with Task.CompletedTask.
    private static PatchOutcome PatchBowlbugRockMoves(Harmony harmony, Assembly sts2)
        => PatchMonsterMethods(harmony, sts2,
            typeFqn: "MegaCrit.Sts2.Core.Models.Monsters.BowlbugRock",
            methodNames: new HashSet<string>(StringComparer.Ordinal) { "HeadbuttMove", "DizzyMove" },
            label: "MegaCrit.Sts2.Core.Models.Monsters.BowlbugRock.*Move");

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
    private static PatchOutcome PatchSoulNexus(Harmony harmony, Assembly sts2)
        => PatchMonsterMethods(harmony, sts2,
            typeFqn: "MegaCrit.Sts2.Core.Models.Monsters.SoulNexus",
            methodNames: new HashSet<string>(StringComparer.Ordinal)
            {
                "SoulBurnMove", "MaelstromMove", "DrainLifeMove",
                "AfterDeath", "BeforeRemovedFromRoom",
            },
            label: "MegaCrit.Sts2.Core.Models.Monsters.SoulNexus.{*Move, lifecycle}");

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
    private static PatchOutcome PatchTestSubject(Harmony harmony, Assembly sts2)
        => PatchMonsterMethods(harmony, sts2,
            typeFqn: "MegaCrit.Sts2.Core.Models.Monsters.TestSubject",
            methodNames: new HashSet<string>(StringComparer.Ordinal)
            {
                "BiteMove", "SkullBashMove", "MultiClawMove", "Phase3LacerateMove",
                "BigPounceMove", "BurningGrowlMove", "RespawnMove",
                "Revive", "TriggerDeadState", "AfterAddedToRoom",
            },
            label: "MegaCrit.Sts2.Core.Models.Monsters.TestSubject.{*Move, AfterAddedToRoom, Revive, TriggerDeadState}");

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
    private static PatchOutcome PatchCeremonialBeast(Harmony harmony, Assembly sts2)
        => PatchMonsterMethods(harmony, sts2,
            typeFqn: "MegaCrit.Sts2.Core.Models.Monsters.CeremonialBeast",
            methodNames: new HashSet<string>(StringComparer.Ordinal)
            {
                "PlowMove", "CrushMove", "StampMove", "StompMove", "BeastCryMove",
                "SetStunned", "StunnedMove",
                "AfterAddedToRoom", "AfterDeath", "BeforeRemovedFromRoom",
            },
            label: "MegaCrit.Sts2.Core.Models.Monsters.CeremonialBeast.{*Move, SetStunned, lifecycle}");

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
    private static PatchOutcome PatchCorpseSlug(Harmony harmony, Assembly sts2)
        => PatchMonsterMethods(harmony, sts2,
            typeFqn: "MegaCrit.Sts2.Core.Models.Monsters.CorpseSlug",
            methodNames: new HashSet<string>(StringComparer.Ordinal)
            {
                "GlompMove", "GoopMove", "WhipSlapMove",
            },
            label: "MegaCrit.Sts2.Core.Models.Monsters.CorpseSlug.*Move");

    // DECIMILLIPEDE_ELITE → DecimillipedeSegment (parent of *Front/Middle/
    // Back). Each segment owns REATTACH_POWER:25. The segment-move bodies
    // and AfterAddedToRoom/AfterDeath walk segment-link state via UI nodes
    // that don't exist headless.
    private static PatchOutcome PatchDecimillipede(Harmony harmony, Assembly sts2)
        => PatchMonsterMethods(harmony, sts2,
            typeFqn: "MegaCrit.Sts2.Core.Models.Monsters.DecimillipedeSegment",
            methodNames: new HashSet<string>(StringComparer.Ordinal)
            {
                "BulkMove", "ConstrictMove", "ReattachMove", "WritheMove",
                "DeadMove", "AnimSegmentsAttack",
                "AfterDeath",
            },
            label: "MegaCrit.Sts2.Core.Models.Monsters.DecimillipedeSegment.{*Move, AfterDeath}");

    // DOORMAKER_BOSS → Doormaker (with HUNGER_POWER). Generic boss moves +
    // the dramatic-open intro animation that always NREs in headless.
    // SwapPhasePower is the boss's phase-transition async hook — without
    // patching it the agent can keep damaging but the boss never advances
    // past phase 1 (the sweep saw a step-limit timeout). AfterDeath is
    // patched defensively per the SoulNexus precedent.
    private static PatchOutcome PatchDoormaker(Harmony harmony, Assembly sts2)
        => PatchMonsterMethods(harmony, sts2,
            typeFqn: "MegaCrit.Sts2.Core.Models.Monsters.Doormaker",
            methodNames: new HashSet<string>(StringComparer.Ordinal)
            {
                "DramaticOpenMove", "GraspMove", "HungerMove", "ScrutinyMove",
                "SwapPhasePower", "AfterDeath",
            },
            label: "MegaCrit.Sts2.Core.Models.Monsters.Doormaker.{*Move, SwapPhasePower, AfterDeath}");

    // GREMLIN_MERC_NORMAL → GremlinMerc spawns a FatGremlin via its moves;
    // both need patching for the encounter to finish out. HEIST_POWER is a
    // synchronous power (no Task-returning hooks per `probe-types`) so no
    // power patch is needed.
    private static PatchOutcome PatchFatGremlin(Harmony harmony, Assembly sts2)
        => PatchMonsterMethods(harmony, sts2,
            typeFqn: "MegaCrit.Sts2.Core.Models.Monsters.FatGremlin",
            methodNames: new HashSet<string>(StringComparer.Ordinal)
            {
                "FleeMove", "SpawnedMove",
            },
            label: "MegaCrit.Sts2.Core.Models.Monsters.FatGremlin.*Move");

    private static PatchOutcome PatchGremlinMerc(Harmony harmony, Assembly sts2)
        => PatchMonsterMethods(harmony, sts2,
            typeFqn: "MegaCrit.Sts2.Core.Models.Monsters.GremlinMerc",
            methodNames: new HashSet<string>(StringComparer.Ordinal)
            {
                "DoubleSmashMove", "GimmeMove", "HeheMove",
            },
            label: "MegaCrit.Sts2.Core.Models.Monsters.GremlinMerc.*Move");

    // TERROR_EEL_ELITE → TerrorEel (with VIGOR_POWER). VigorPower.AfterAttack
    // is patched separately. StunMove is the move the engine sequences into
    // after a stun-shaped action; without it patched the agent still stalls
    // on the round the eel's state machine picks Stun.
    private static PatchOutcome PatchTerrorEel(Harmony harmony, Assembly sts2)
        => PatchMonsterMethods(harmony, sts2,
            typeFqn: "MegaCrit.Sts2.Core.Models.Monsters.TerrorEel",
            methodNames: new HashSet<string>(StringComparer.Ordinal)
            {
                "CrashMove", "TerrorMove", "ThrashMove", "StunMove",
            },
            label: "MegaCrit.Sts2.Core.Models.Monsters.TerrorEel.*Move");

    // TUNNELER_WEAK → Tunneler. No named power in the stall fingerprint;
    // the hang is purely move-side.
    private static PatchOutcome PatchTunneler(Harmony harmony, Assembly sts2)
        => PatchMonsterMethods(harmony, sts2,
            typeFqn: "MegaCrit.Sts2.Core.Models.Monsters.Tunneler",
            methodNames: new HashSet<string>(StringComparer.Ordinal)
            {
                "BelowMove", "BiteMove", "BurrowMove",
            },
            label: "MegaCrit.Sts2.Core.Models.Monsters.Tunneler.*Move");

    // THE_INSATIABLE_BOSS → TheInsatiable. STRENGTH_POWER in the fingerprint
    // is vanilla and already works; the hang is the move bodies.
    private static PatchOutcome PatchTheInsatiable(Harmony harmony, Assembly sts2)
        => PatchMonsterMethods(harmony, sts2,
            typeFqn: "MegaCrit.Sts2.Core.Models.Monsters.TheInsatiable",
            methodNames: new HashSet<string>(StringComparer.Ordinal)
            {
                "BiteMove", "LiquifyMove", "SalivateMove", "ThrashMove",
            },
            label: "MegaCrit.Sts2.Core.Models.Monsters.TheInsatiable.*Move");

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
    private static PatchOutcome PatchLagavulinMatriarch(Harmony harmony, Assembly sts2)
        => PatchMonsterMethods(harmony, sts2,
            typeFqn: "MegaCrit.Sts2.Core.Models.Monsters.LagavulinMatriarch",
            methodNames: new HashSet<string>(StringComparer.Ordinal)
            {
                "DisembowelMove", "Slash2Move", "SlashMove", "SoulSiphonMove",
                "WakeUpMove", "SleepMove", "AfterAddedToRoom",
                // First-blood path: Hellraiser → AttackCommand → CreatureCmd.Damage →
                // Hook.AfterDamageReceived. Same shape as the Crusher fix.
                "AfterDamageReceived", "AfterDeath",
            },
            label: "MegaCrit.Sts2.Core.Models.Monsters.LagavulinMatriarch.{*Move, lifecycle, AfterDamageReceived}");

    private static PatchOutcome PatchSlumberingBeetle(Harmony harmony, Assembly sts2)
        => PatchMonsterMethods(harmony, sts2,
            typeFqn: "MegaCrit.Sts2.Core.Models.Monsters.SlumberingBeetle",
            methodNames: new HashSet<string>(StringComparer.Ordinal)
            {
                "RolloutMove", "WakeUpMove", "AfterAddedToRoom",
            },
            label: "MegaCrit.Sts2.Core.Models.Monsters.SlumberingBeetle.{*Move, AfterAddedToRoom}");

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
    private static PatchOutcome PatchCrusher(Harmony harmony, Assembly sts2)
        => PatchMonsterMethods(harmony, sts2,
            typeFqn: "MegaCrit.Sts2.Core.Models.Monsters.Crusher",
            methodNames: new HashSet<string>(StringComparer.Ordinal)
            {
                "AdaptMove", "BugStingMove", "EnlargingStrikeMove",
                "GuardedStrikeMove", "ThrashMove",
                "AfterAddedToRoom", "AfterCurrentHpChanged", "BeforeDeath",
            },
            label: "MegaCrit.Sts2.Core.Models.Monsters.Crusher.{*Move, AfterAddedToRoom, AfterCurrentHpChanged, BeforeDeath}");

    // KAISER_CRAB_BOSS spawns a Crusher and a Rocket together (see the
    // sweep fingerprint: `enemies=[CRUSHER:..., ROCKET:...]`). Rocket
    // carries BACK_ATTACK_RIGHT_POWER + CRAB_RAGE_POWER and has the same
    // background-NRE shape as Crusher. Patch every Task-returning method.
    private static PatchOutcome PatchRocket(Harmony harmony, Assembly sts2)
        => PatchMonsterMethods(harmony, sts2,
            typeFqn: "MegaCrit.Sts2.Core.Models.Monsters.Rocket",
            methodNames: new HashSet<string>(StringComparer.Ordinal)
            {
                "ChargeUpMove", "LaserMove", "PrecisionBeamMove",
                "RechargeMove", "TargetingReticleMove",
                "AfterAddedToRoom", "AfterCurrentHpChanged", "BeforeDeath",
            },
            label: "MegaCrit.Sts2.Core.Models.Monsters.Rocket.{*Move, AfterAddedToRoom, AfterCurrentHpChanged, BeforeDeath}");

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
    private static PatchOutcome PatchMonsterMethods(
        Harmony harmony,
        Assembly sts2,
        string typeFqn,
        HashSet<string> methodNames,
        string label)
    {
        var monsterType = sts2.GetType(typeFqn);
        if (monsterType is null)
            return new PatchOutcome(label, Patched: false, Detail: $"type {typeFqn} not found");

        var methods = monsterType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(m => methodNames.Contains(m.Name) && !m.IsSpecialName)
            .ToArray();
        if (methods.Length == 0)
            return new PatchOutcome(label, Patched: false, Detail: $"no target methods on {typeFqn}");

        var taskPrefix = typeof(HangPatches).GetMethod(nameof(ReturnDefaultTaskPrefix), BindingFlags.Static | BindingFlags.NonPublic)!;
        var voidPrefix = typeof(HangPatches).GetMethod(nameof(SkipVoidPrefix), BindingFlags.Static | BindingFlags.NonPublic)!;
        var nullPrefix = typeof(HangPatches).GetMethod(nameof(ReturnNullPrefix), BindingFlags.Static | BindingFlags.NonPublic)!;

        var sigs = new List<string>(methods.Length);
        foreach (var m in methods)
        {
            // Harmony can't patch open generic methods (Doormaker.SwapPhasePower
            // is the surfacing example — the compiler-generated state machine
            // type has one generic parameter, and Harmony's IL-rewrite path
            // fails at MMReflectionImporter.ImportGenericParameter). Skip with
            // a visible note rather than crash bootstrap.
            if (m.IsGenericMethodDefinition || m.ContainsGenericParameters)
            {
                sigs.Add($"{m.Name} → {m.ReturnType.Name} (skipped: open-generic, not Harmony-patchable)");
                continue;
            }
            MethodInfo prefix;
            if (typeof(System.Threading.Tasks.Task).IsAssignableFrom(m.ReturnType)) prefix = taskPrefix;
            else if (m.ReturnType == typeof(void)) prefix = voidPrefix;
            else if (!m.ReturnType.IsValueType) prefix = nullPrefix;
            else
            {
                sigs.Add($"{m.Name} → {m.ReturnType.Name} (skipped: unsupported value-type return)");
                continue;
            }
            harmony.Patch(m, prefix: new HarmonyMethod(prefix));
            sigs.Add($"{m.Name}({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name))}) → {m.ReturnType.Name}");
        }
        return new PatchOutcome(label, Patched: true, Detail: string.Join(", ", sigs));
    }
}
