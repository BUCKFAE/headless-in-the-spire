using System.Reflection;
using HarmonyLib;

namespace Sts2Headless.Runtime.Patches;

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

    // Vantom (Act 1 boss on seed 42) — DismemberMove was deemed
    // TestMode-safe in the first cleanup wave (2026-05-23 IL probe:
    // every UI singleton is null-gated; CreatureCmd.TriggerAnim has
    // TestMode early-exit). HOWEVER, the smoke A0 test still hangs at
    // round 3 of the Vantom boss fight after the player has taken
    // ~40 HP of damage — the body partly executes, deals damage, then
    // wedges. Likely the post-damage CardPileCmd.AddToCombatAndPreview<Wound>
    // call or the WithHitFx VFX hook fails in BossRoom context but
    // not in the encounter sweep's direct debug/start_combat path.
    // Until the exact NRE site is identified, keep DismemberMove
    // patched. See s_expectedDoormakerShape regression net.
    private static readonly MonsterPatchEntry _vantomEntry = new(
        TypeFqn: "MegaCrit.Sts2.Core.Models.Monsters.Vantom",
        MethodNames: new HashSet<string>(StringComparer.Ordinal) { "DismemberMove" },
        Label: "MegaCrit.Sts2.Core.Models.Monsters.Vantom.DismemberMove");
    private static PatchOutcome PatchVantomDismemberMove(Harmony harmony, Assembly sts2)
        => PatchMonsterMethods(harmony, sts2, _vantomEntry);

    // ThievingHopper used to need every Move method patched as "skip
    // body" — same Doormaker-shape over-reach as Vantom and BowlbugRock.
    // IL probe confirms every UI helper is null-gated on
    // NCombatRoom.Instance / NCreature lookups, and CreatureCmd.TriggerAnim
    // has TestMode early-exit. EscapeArtistPower.AfterTurnEnd stays
    // patched separately (PatchEscapeArtistPowerAfterTurnEnd).

    // BowlbugRock used to need HeadbuttMove + DizzyMove patched as
    // "skip body" — same misdiagnosis as Vantom. IL probe confirms
    // every UI helper is either null-gated on NCombatRoom.Instance or
    // routes through CreatureCmd.TriggerAnim (TestMode early-exit).
    // No leaf-helper patch needed; the bodies run cleanly with the
    // TestMode flag set in BootstrapSequence.

    // SoulNexus (Act 3 enemy on seed 42) — moves are TestMode-safe but
    // the killing-blow lifecycle hooks (AfterDeath, BeforeRemovedFromRoom)
    // unconditionally call NCombatRoom.get_Instance and dereference the
    // result without null-checking. Keep those two stripped; drop the
    // three Move methods so the boss actually attacks during combat.
    //
    // Original BeatGameOnSeed42Tests failure (Act 3 floor 7): StrikeIronclad
    // .OnPlay → AttackCommand → CreatureCmd.Kill → CombatManager.RemoveCreature
    // → SoulNexus.BeforeRemovedFromRoom NRE. AfterDeath has the same shape.
    // Both still patched as a safety net until null-gating is added there.
    private static readonly MonsterPatchEntry _soulNexusEntry = new(
        TypeFqn: "MegaCrit.Sts2.Core.Models.Monsters.SoulNexus",
        MethodNames: new HashSet<string>(StringComparer.Ordinal)
        {
            "AfterDeath", "BeforeRemovedFromRoom",
        },
        Label: "MegaCrit.Sts2.Core.Models.Monsters.SoulNexus.{AfterDeath, BeforeRemovedFromRoom}");
    private static PatchOutcome PatchSoulNexus(Harmony harmony, Assembly sts2)
        => PatchMonsterMethods(harmony, sts2, _soulNexusEntry);

    // TestSubject (Act 2 boss). IL probe confirms every move + lifecycle
    // method has TestMode-safe UI (null-gated NCombatRoom/NRunMusicController
    // singletons + CreatureCmd.TriggerAnim early-exit). The original
    // "StallDetector fires" was caused by the missing TestMode flag and is
    // now handled by BootstrapSequence.SetTestMode. Restoring the move
    // bodies means the boss actually performs SetMaxAndCurrentHp(OriginalHp)
    // and PowerCmd.Apply during AfterAddedToRoom — same significance as
    // the Doormaker fix for the Act 3 boss.

    // CeremonialBeast (Act 1 boss on seed 1). IL probe confirms all
    // moves + lifecycle methods are TestMode-safe (null-gated UI helpers
    // + CreatureCmd.TriggerAnim early-exit). Restoring the body means
    // the boss actually attacks, applies stun, and runs phase-setup
    // PowerCmd.Apply in AfterAddedToRoom.

    // ── Encounter-sweep wave ────────────────────────────────────────────
    //
    // The blob below patches the ten monsters surfaced by
    // EveryEncounterSmokeTests (`just sweep-encounters`). All match the
    // existing CeremonialBeast / SoulNexus / TestSubject shape — Task-
    // returning bodies NRE on UI/VFX state in headless. The per-monster
    // wrapper exists for the narrative comment, not for behaviour; the
    // shared PatchMonsterMethods helper does the work.

    // CorpseSlug — moves are TestMode-safe per IL probe; the originally-
    // diagnosed "slime-VFX setup NRE" was the missing TestMode flag.
    // RavenousPower stays patched separately for the on-kill listener.

    // DecimillipedeSegment — TestMode-safe. AnimSegmentsAttack has a
    // TestMode.IsOn early-exit; AfterDeath gates Godot calls similarly.
    // ReattachPower stays patched separately.

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

    // GREMLIN_MERC_NORMAL → GremlinMerc spawns a FatGremlin. GremlinMerc
    // moves are TestMode-safe per IL probe (TalkCmd already patched
    // globally; CreatureCmd.TriggerAnim safe). FatGremlin moves remain
    // patched defensively — not probed in the current pass; revisit if
    // an audit cycle confirms safety. HEIST_POWER is synchronous so no
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

    // TerrorEel — TestMode-safe per IL probe. VigorPower.AfterAttack
    // stays patched separately.

    // TUNNELER_WEAK → Tunneler. IL probe (2026-05-23) confirms BelowMove
    // gates the UI block correctly: at IL_0020 `call TestMode.get_IsOff;
    // brfalse IL_016F` jumps OVER the entire NCombatRoom + Node2D.set_Position
    // + SfxCmd + TriggerAnim + Cmd.Wait animation block and lands squarely
    // on `get_BelowDamage; DamageCmd.Attack; AttackCommand.FromMonster;
    // WithHitFx; Execute` — i.e. the gameplay-damage chain still runs in
    // headless. The prior "unconditional set_Position" diagnosis read the
    // call sites but missed the branch target. BiteMove and BurrowMove
    // had no UI block to gate in the first place. All three moves run
    // safely with no patch.

    // TheInsatiable — TestMode-safe per IL probe.

    // LagavulinMatriarch — moves are TestMode-safe per IL probe.
    // AfterAddedToRoom stays patched because its body legitimately puts
    // the monster into a sleeping state, which leaves the sweep fixture
    // with InProgress=false (no active combatant to advance). Same
    // shape as SlumberingBeetle below — the sweep can't drive a
    // sleep-locked encounter; production agents would observe the
    // intent and end-turn until WakeUpMove fires.
    private static readonly MonsterPatchEntry _lagavulinMatriarchEntry = new(
        TypeFqn: "MegaCrit.Sts2.Core.Models.Monsters.LagavulinMatriarch",
        MethodNames: new HashSet<string>(StringComparer.Ordinal)
        {
            "AfterAddedToRoom",
        },
        Label: "MegaCrit.Sts2.Core.Models.Monsters.LagavulinMatriarch.AfterAddedToRoom");
    private static PatchOutcome PatchLagavulinMatriarch(Harmony harmony, Assembly sts2)
        => PatchMonsterMethods(harmony, sts2, _lagavulinMatriarchEntry);

    // SlumberingBeetle.AfterAddedToRoom keeps an unconditional
    // NCombatRoom.get_Instance + dereference — keep it patched until a
    // proper null-gate or leaf helper lands. The two Move methods are
    // TestMode-safe.
    private static readonly MonsterPatchEntry _slumberingBeetleEntry = new(
        TypeFqn: "MegaCrit.Sts2.Core.Models.Monsters.SlumberingBeetle",
        MethodNames: new HashSet<string>(StringComparer.Ordinal)
        {
            "AfterAddedToRoom",
        },
        Label: "MegaCrit.Sts2.Core.Models.Monsters.SlumberingBeetle.AfterAddedToRoom");
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
