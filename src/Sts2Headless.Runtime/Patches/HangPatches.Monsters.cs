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

    // Vantom (Act 1 boss on seed 42) — DismemberMove had one unguarded
    // `NGame.Instance.DoHitStop(2, 1)` call at IL 198-201 of its async
    // state machine that NRE'd on the null-receiver callvirt in
    // headless. The original whole-body strip (PatchVantomDismemberMove)
    // killed the move's gameplay (DamageCmd.Attack on the player, three
    // Wound cards to the discard pile) along with the offending call;
    // PatchVantomDismemberMoveDoHitStop in HangPatches.NGame.cs replaces
    // it with a four-instruction Nop window via Harmony transpiler so
    // the surrounding gameplay runs untouched.

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

    // LagavulinMatriarch / SlumberingBeetle — `AfterAddedToRoom` on both
    // monsters legitimately puts the creature into a sleeping state by
    // applying PlatingPower + a sleep power (AsleepPower /
    // SlumberPower). The body then runs an unguarded UI fall-through:
    //   `NCombatRoom.Instance.GetCreatureNode(this.Creature)`
    // which `callvirt`s on a null `Instance` in headless and NREs the
    // whole `AfterCreatureAdded` loop — `CombatManager.StartCombatInternal`
    // then never reaches `set_IsInProgress(true)` and `start_combat`
    // surfaces `InProgress=false`.
    //
    // Original cleanup wave just stripped the bodies wholesale, same as
    // the Doormaker pre-fix shape: the powers vanished, the encounters
    // were trivially "Playable" (no intent, no plating, no sleep) but
    // the headless win-rate corpus was being calibrated against a
    // toothless boss. The audit (s_expectedDoormakerShape) caught both.
    //
    // Doormaker-style leaf fix — replace the body with a synthetic
    // prefix that:
    //   1. Awaits the base-class hook (`<>n__0`)
    //   2. Applies the canonical sleep-power set via
    //      `PowerCmd.Apply(PowerModel, Creature, Decimal, Creature,
    //      CardModel, Boolean)` — the engine path, fully equivalent to
    //      the IL's `Apply<T>` calls (only the type-param plumbing
    //      changes).
    //   3. Returns `Task.CompletedTask`, skipping the post-power UI VFX
    //      block that NREs in headless.
    //
    // The original IL still references gameplay calls, but the patch
    // PRE-EMPTS them with equivalent calls of our own — the audit list
    // drops both entries because the MethodNames sets are empty (same
    // Crusher/Rocket shape: the entry exists for narrative + reflection
    // surface, but no methods are passed to PatchMonsterMethods).
    //
    // After the fix, the encounter naturally drives in headless: the
    // monster starts asleep, plays SnoreMove/SleepMove (both
    // `Task.CompletedTask`) for the first turn, Strike removes Plating,
    // AsleepPower/SlumberPower's AfterDamageReceived wakes the monster
    // (engine path), and subsequent turns run normal Move bodies.
    private static readonly MonsterPatchEntry _lagavulinMatriarchEntry = new(
        TypeFqn: "MegaCrit.Sts2.Core.Models.Monsters.LagavulinMatriarch",
        // Empty MethodNames — the body-replacement happens in
        // PatchLagavulinMatriarchAfterAddedToRoom below, NOT through
        // PatchMonsterMethods. Matches the Crusher/Rocket entry shape.
        MethodNames: new HashSet<string>(StringComparer.Ordinal),
        Label: "MegaCrit.Sts2.Core.Models.Monsters.LagavulinMatriarch.AfterAddedToRoom (body-replacement: apply PlatingPower+AsleepPower, skip UI VFX)");
    private static PatchOutcome PatchLagavulinMatriarch(Harmony harmony, Assembly sts2)
        => PatchSleepingMonsterAfterAddedToRoom(
            harmony, sts2,
            typeFqn: "MegaCrit.Sts2.Core.Models.Monsters.LagavulinMatriarch",
            entry: _lagavulinMatriarchEntry,
            platingAmount: () => 12,  // hardcoded in the IL (ldc.i4.s 12 / newobj Decimal)
            sleepPowerId: "ASLEEP_POWER",
            sleepAmount: 3);

    private static readonly MonsterPatchEntry _slumberingBeetleEntry = new(
        TypeFqn: "MegaCrit.Sts2.Core.Models.Monsters.SlumberingBeetle",
        MethodNames: new HashSet<string>(StringComparer.Ordinal),
        Label: "MegaCrit.Sts2.Core.Models.Monsters.SlumberingBeetle.AfterAddedToRoom (body-replacement: apply PlatingPower+SlumberPower, skip UI VFX, wire Died→AfterDeath)");
    private static PatchOutcome PatchSlumberingBeetle(Harmony harmony, Assembly sts2)
        => PatchSleepingMonsterAfterAddedToRoom(
            harmony, sts2,
            typeFqn: "MegaCrit.Sts2.Core.Models.Monsters.SlumberingBeetle",
            entry: _slumberingBeetleEntry,
            // Per IL probe: SlumberingBeetle's PlatingAmount is a property
            // (AscensionHelper.GetValueIfAscension(8, 18, 15)) — call the
            // monster's own getter so an ascension bump tracks.
            platingAmount: null,  // → resolved via reflection from monster instance
            sleepPowerId: "SLUMBER_POWER",
            sleepAmount: 3);

    // KAISER_CRAB_BOSS spawns a Crusher + Rocket pair (revealed via
    // `--probe-encounter KAISER_CRAB_BOSS`). The encounter has no
    // `KaiserCrab` monster type — the boss is rendered by
    // NKaiserCrabBossBackground and powered by the Crusher+Rocket
    // creatures that share the boss-background node.
    //
    // The original play_card NRE captured in the probe was:
    //   System.NullReferenceException
    //     at Crusher.get_Background()
    //     at Crusher.AfterCurrentHpChanged_Patch1(Crusher, Creature, Decimal)
    //     at Hook.AfterCurrentHpChanged(...)
    //     at CreatureCmd.Damage(...)
    //     at AttackCommand.Execute(...)
    //     at PommelStrike.OnPlay(...)
    //
    // `get_Background()`'s body does
    //   _background ??= NCombatRoom.Instance.Background
    //                       .GetNode<NKaiserCrabBossBackground>("%KaiserCrab")
    // In headless `NCombatRoom.Instance` is null so the lazy initializer
    // NREs the moment any move/lifecycle hook reads `this.Background`.
    //
    // The first fix wave stripped every Task-returning method on
    // Crusher / Rocket as "skip body, return CompletedTask". That
    // silenced the NRE but silently removed every DamageCmd.Attack and
    // PowerCmd.Apply call inside those bodies (Crusher's BugStingMove
    // damage + Weak/Frail application, Rocket's LaserMove damage, both
    // monsters' AfterAddedToRoom BackAttack*/Surrounded power setup, ...).
    // Surfaced by MonsterPatchAuditTests after the 2026-05-21 Doormaker
    // graduation pattern was generalised — same Doormaker shape, same
    // misdiagnosis.
    //
    // Doormaker-style leaf fix:
    //   1. Stub `Crusher.get_Background` and `Rocket.get_Background`
    //      to return a shared uninitialized `NKaiserCrabBossBackground`
    //      instance (see PatchKaiserCrabBackgroundGetters). The
    //      instance bypasses Godot construction via
    //      `RuntimeHelpers.GetUninitializedObject`; no field on it is
    //      ever read because every method gets patched (next step).
    //   2. No-op every Task / void method on NKaiserCrabBossBackground
    //      so calls against the stub are safe
    //      (PatchKaiserCrabBossBackground). Task methods return
    //      Task.CompletedTask; void methods just skip.
    //
    // Move bodies then run cleanly: `await this.Background.PlayX()`
    // resolves to `await Task.CompletedTask` and falls through to the
    // DamageCmd / PowerCmd calls that were getting stripped.
    //
    // `AfterAddedToRoom` on both monsters has no Background or UI calls
    // at all (Crusher: PowerCmd.Apply<BackAttackLeftPower>; Rocket:
    // PowerCmd.Apply<SurroundedPower> + Apply<BackAttackRightPower>) —
    // it was patched defensively in the original wave and never needed
    // to be.
    //
    // Entries deliberately left empty so the reflection-based registry
    // (EnumerateMonsterPatchEntries) still sees "Crusher / Rocket were
    // considered" without patching any of their methods; the entries
    // double as narrative docs for the next bug hunter who lands here.

    private static PatchOutcome PatchCrusher(Harmony harmony, Assembly sts2)
        => PatchMonsterMethods(harmony, sts2, _crusherEntry);
    private static readonly MonsterPatchEntry _crusherEntry = new(
        TypeFqn: "MegaCrit.Sts2.Core.Models.Monsters.Crusher",
        MethodNames: new HashSet<string>(StringComparer.Ordinal),
        Label: "MegaCrit.Sts2.Core.Models.Monsters.Crusher.{} (Background-stubbed)");

    private static PatchOutcome PatchRocket(Harmony harmony, Assembly sts2)
        => PatchMonsterMethods(harmony, sts2, _rocketEntry);
    private static readonly MonsterPatchEntry _rocketEntry = new(
        TypeFqn: "MegaCrit.Sts2.Core.Models.Monsters.Rocket",
        MethodNames: new HashSet<string>(StringComparer.Ordinal),
        Label: "MegaCrit.Sts2.Core.Models.Monsters.Rocket.{} (Background-stubbed)");

    // The shared stub instance returned by both `Crusher.get_Background`
    // and `Rocket.get_Background` after patching. Allocated lazily via
    // `RuntimeHelpers.GetUninitializedObject` so we never run the
    // NKaiserCrabBossBackground Godot constructor (which would walk
    // engine UI state that's absent in headless). Safe because every
    // method on the type is Harmony-patched to no-op by
    // `PatchKaiserCrabBossBackground` — none of the uninitialized
    // fields are ever read.
    private static object? _kaiserCrabBackgroundStub;

    private static PatchOutcome PatchKaiserCrabBossBackground(Harmony harmony, Assembly sts2)
    {
        const string typeFqn = "MegaCrit.Sts2.Core.Nodes.Vfx.Backgrounds.NKaiserCrabBossBackground";
        const string label = $"{typeFqn}.{{PlayAttackAnim, PlayHurtAnim, PlayArmDeathAnim, PlayBodyDeathAnim, PlayRightRecharge, PlayRightSideChargeUpAnim, PlayRightSideHeavy, AddEmptyReactionAnimation, ...}}";
        var bgType = sts2.GetType(typeFqn);
        if (bgType is null)
            return new PatchOutcome(label, Patched: false, Detail: $"type {typeFqn} not found");

        // Pre-allocate the stub instance now. Once method prefixes are
        // installed below, callvirts against this instance are safe;
        // Crusher and Rocket share the same boss-background node in
        // real combat so a single instance is correct.
        _kaiserCrabBackgroundStub = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(bgType);

        // Patch every Task / void declared method on the boss-background
        // type. Skip special-name (property/event accessors — none with
        // bodies that NRE), generic open definitions (none present per
        // probe), and the Godot-bridge overrides whose signatures can't
        // be loaded reflectively (they throw TypeLoadException for
        // Godot.NativeInterop.* args; never called from Crusher/Rocket
        // move bodies).
        var taskPrefix = typeof(HangPatches).GetMethod(nameof(ReturnDefaultTaskPrefix), BindingFlags.Static | BindingFlags.NonPublic)!;
        var voidPrefix = typeof(HangPatches).GetMethod(nameof(SkipVoidPrefix), BindingFlags.Static | BindingFlags.NonPublic)!;

        var sigs = new List<string>();
        foreach (var m in bgType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            if (m.IsSpecialName) continue;
            if (m.IsGenericMethodDefinition || m.ContainsGenericParameters) continue;

            // Skip any method whose signature can't be loaded: the
            // Godot-bridge overrides (GetGodotMethodList,
            // InvokeGodotClassMethod, …) reference
            // Godot.Bridge.MethodInfo / Godot.NativeInterop.* types
            // that aren't present in our GodotStubs replacement, so
            // both `m.ReturnType` and `m.GetParameters()` throw
            // TypeLoadException on first access. None of these are
            // called from Crusher/Rocket move bodies — safe to skip.
            Type returnType;
            ParameterInfo[] pars;
            try
            {
                returnType = m.ReturnType;
                pars = m.GetParameters();
            }
            catch (TypeLoadException) { continue; }

            MethodInfo prefix;
            if (typeof(System.Threading.Tasks.Task).IsAssignableFrom(returnType)) prefix = taskPrefix;
            else if (returnType == typeof(void)) prefix = voidPrefix;
            else continue; // value-type / reference returns: none called by Crusher/Rocket bodies (per probe).

            harmony.Patch(m, prefix: new HarmonyMethod(prefix));
            sigs.Add($"{m.Name}({string.Join(",", pars.Select(p => p.ParameterType.Name))}) → {returnType.Name}");
        }
        if (sigs.Count == 0)
            return new PatchOutcome(label, Patched: false, Detail: $"no Task/void methods on {typeFqn}");
        return new PatchOutcome(label, Patched: true, Detail: string.Join(", ", sigs));
    }

    private static PatchOutcome PatchKaiserCrabBackgroundGetters(Harmony harmony, Assembly sts2)
    {
        const string label = "MegaCrit.Sts2.Core.Models.Monsters.{Crusher,Rocket}.get_Background";
        if (_kaiserCrabBackgroundStub is null)
            return new PatchOutcome(label, Patched: false, Detail: "kaiser-crab background stub not yet allocated — PatchKaiserCrabBossBackground must run first");

        var crusherType = sts2.GetType("MegaCrit.Sts2.Core.Models.Monsters.Crusher");
        var rocketType = sts2.GetType("MegaCrit.Sts2.Core.Models.Monsters.Rocket");
        if (crusherType is null || rocketType is null)
            return new PatchOutcome(label, Patched: false, Detail: "Crusher or Rocket type not found");

        // `Background` is a property; `get_Background` is the
        // compiler-emitted accessor. Patch the accessor so the body
        // never runs (it would NRE on
        // NCombatRoom.Instance → null .Background.GetNode(...)).
        var prefix = typeof(HangPatches).GetMethod(nameof(ReturnKaiserCrabBackgroundStubPrefix), BindingFlags.Static | BindingFlags.NonPublic)!;
        var hosts = new List<string>(2);
        foreach (var t in new[] { crusherType, rocketType })
        {
            var getter = t.GetMethod("get_Background", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (getter is null)
                return new PatchOutcome(label, Patched: false, Detail: $"get_Background not found on {t.FullName}");
            harmony.Patch(getter, prefix: new HarmonyMethod(prefix));
            hosts.Add(t.FullName ?? t.Name);
        }
        return new PatchOutcome(label, Patched: true, Detail: $"return shared NKaiserCrabBossBackground stub on {string.Join(", ", hosts)}");
    }

    // Prefix shared by both Crusher.get_Background and
    // Rocket.get_Background. Returning false suppresses the original
    // body so it never reaches the `NCombatRoom.Instance.Background
    // .GetNode<...>("%KaiserCrab")` NRE; `__result` is set to the
    // shared stub allocated in `PatchKaiserCrabBossBackground`.
    private static bool ReturnKaiserCrabBackgroundStubPrefix(ref object? __result)
    {
        __result = _kaiserCrabBackgroundStub;
        return false;
    }

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

        // Empty MethodNames is a deliberate registry-only entry: the type is
        // mentioned for the narrative + the reflection-based audit registry
        // (EnumerateMonsterPatchEntries), but the actual patching happens
        // elsewhere — e.g. Crusher / Rocket are patched via
        // PatchKaiserCrabBackgroundGetters, LagavulinMatriarch / SlumberingBeetle
        // via PatchSleepingMonsterAfterAddedToRoom. Report Ok so the
        // bootstrap snapshot doesn't flag these as missing patches.
        if (entry.MethodNames.Count == 0)
            return new PatchOutcome(entry.Label, Patched: true, Detail: "no methods patched here (registry-only entry; patched elsewhere)");

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

    // ── Sleeping-monster body-replacement (LagavulinMatriarch /
    //    SlumberingBeetle) ─────────────────────────────────────────────────
    //
    // Per-monster bundle that PatchSleepingMonsterAfterAddedToRoom
    // captures into a Harmony prefix. The prefix:
    //   1. Awaits the base-class hook (`<>n__0`), which mirrors what the
    //      original IL does at offset 9–13.
    //   2. Applies PlatingPower (amount per `PlatingAmountFromInstance`)
    //      via PowerCmd.Apply(PowerModel, target, Decimal, source, null,
    //      false) — the non-generic equivalent of the IL's
    //      `Apply<PlatingPower>(...)`.
    //   3. Applies the sleep power (`SleepPowerModel`, `SleepAmount`)
    //      the same way.
    //   4. Optionally subscribes the monster's `AfterDeath(Creature)`
    //      handler to `Creature.Died` (matches SlumberingBeetle IL
    //      offsets 159–164).
    //   5. Sets `__result = Task.CompletedTask` from the prefix and
    //      returns `false`, skipping the IL's UI VFX block
    //      (NCombatRoom.Instance.GetCreatureNode → NRE in headless).
    //
    // PatchSleepingMonsterAfterAddedToRoom's `platingAmount` parameter
    // being null means "read the monster's get_PlatingAmount property"
    // (SlumberingBeetle, which scales by ascension). A non-null
    // delegate returns the hardcoded value (LagavulinMatriarch's 12).
    private sealed record SleepingMonsterBundle(
        Func<object, int> PlatingAmountFromInstance,
        Type PlatingPowerModel,
        Type SleepPowerModel,
        int SleepAmount,
        MethodInfo BaseHook,
        PropertyInfo CreatureProp,
        MethodInfo PowerCmdApply,
        EventInfo? DiedEvent,
        MethodInfo? AfterDeathHandler,
        Type? AfterDeathDelegateType);

    // The bundles keyed by patched method. Harmony's prefix delegate is
    // a static method that doesn't take per-call context beyond what
    // Harmony injects (`__originalMethod`, `__instance`, `__result`),
    // so we use the original MethodInfo as the keying dimension.
    private static readonly Dictionary<MethodBase, SleepingMonsterBundle> _sleepingMonsterBundles = new();

    private static PatchOutcome PatchSleepingMonsterAfterAddedToRoom(
        Harmony harmony,
        Assembly sts2,
        string typeFqn,
        MonsterPatchEntry entry,
        Func<int>? platingAmount,
        string sleepPowerId,
        int sleepAmount)
    {
        var label = entry.Label;
        var monsterType = sts2.GetType(typeFqn);
        if (monsterType is null)
            return new PatchOutcome(label, Patched: false, Detail: $"type {typeFqn} not found");

        var afterAddedToRoom = monsterType.GetMethod(
            "AfterAddedToRoom",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly,
            binder: null, types: Type.EmptyTypes, modifiers: null);
        if (afterAddedToRoom is null)
            return new PatchOutcome(label, Patched: false, Detail: $"AfterAddedToRoom() not declared on {typeFqn}");

        // The base-class hook the original IL invokes at offset 9 via
        // `<>n__0`. The compiler emits this as a NonPublic instance
        // forwarder when an async override calls `base.AfterAddedToRoom()`.
        var baseHook = monsterType.GetMethod(
            "<>n__0",
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly,
            binder: null, types: Type.EmptyTypes, modifiers: null);
        if (baseHook is null)
            return new PatchOutcome(label, Patched: false, Detail: $"compiler-emitted base-hook <>n__0() not found on {typeFqn}");
        if (!typeof(Task).IsAssignableFrom(baseHook.ReturnType))
            return new PatchOutcome(label, Patched: false, Detail: $"{typeFqn}.<>n__0 returns {baseHook.ReturnType.Name}, not Task");

        var monsterModelType = sts2.GetType("MegaCrit.Sts2.Core.Models.MonsterModel");
        if (monsterModelType is null)
            return new PatchOutcome(label, Patched: false, Detail: "MonsterModel type not found");
        var creatureProp = monsterModelType.GetProperty("Creature", BindingFlags.Public | BindingFlags.Instance);
        if (creatureProp is null)
            return new PatchOutcome(label, Patched: false, Detail: "MonsterModel.Creature not found");

        var powerModelType = sts2.GetType("MegaCrit.Sts2.Core.Models.PowerModel");
        if (powerModelType is null)
            return new PatchOutcome(label, Patched: false, Detail: "PowerModel type not found");

        // PowerCmd.Apply(PowerModel, Creature, Decimal, Creature, CardModel, Boolean) — the
        // non-generic 6-arg form. The closed generic Apply<T> the IL uses
        // is semantically identical; the non-generic spelling is just
        // easier to bind reflectively at patch time.
        var powerCmdType = sts2.GetType("MegaCrit.Sts2.Core.Commands.PowerCmd");
        if (powerCmdType is null)
            return new PatchOutcome(label, Patched: false, Detail: "PowerCmd type not found");
        var applyMethod = powerCmdType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "Apply"
                && !m.IsGenericMethodDefinition
                && m.GetParameters().Length == 6
                && m.GetParameters()[0].ParameterType.IsAssignableFrom(powerModelType));
        if (applyMethod is null)
            return new PatchOutcome(label, Patched: false, Detail: "PowerCmd.Apply(PowerModel, Creature, Decimal, Creature, CardModel, Boolean) not found");

        var platingPowerModel = sts2.GetType("MegaCrit.Sts2.Core.Models.Powers.PlatingPower");
        if (platingPowerModel is null)
            return new PatchOutcome(label, Patched: false, Detail: "PlatingPower type not found");
        var sleepPowerModel = sts2.GetType($"MegaCrit.Sts2.Core.Models.Powers.{(sleepPowerId == "ASLEEP_POWER" ? "AsleepPower" : "SlumberPower")}");
        if (sleepPowerModel is null)
            return new PatchOutcome(label, Patched: false, Detail: $"sleep-power CLR type for id={sleepPowerId} not found");

        // Plating-amount resolver: hardcoded constant for LagavulinMatriarch
        // (12 per IL), getter lookup for SlumberingBeetle (PlatingAmount).
        // The IL probe shows `call SlumberingBeetle.get_PlatingAmount`
        // (a property accessor); we resolve it as a method instead of a
        // property to skirt accessibility/visibility quirks (the
        // declared-only NonPublic search catches both shapes).
        Func<object, int> platingFromInstance;
        if (platingAmount is not null)
        {
            var fixedAmt = platingAmount();
            platingFromInstance = _ => fixedAmt;
        }
        else
        {
            var getter = monsterType.GetMethod(
                "get_PlatingAmount",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly,
                binder: null, types: Type.EmptyTypes, modifiers: null);
            if (getter is null)
                return new PatchOutcome(label, Patched: false, Detail: $"{typeFqn}.get_PlatingAmount accessor not found");
            platingFromInstance = instance =>
            {
                var v = getter.Invoke(instance, null);
                return v is null ? 0 : Convert.ToInt32(v);
            };
        }

        // SlumberingBeetle's IL ends with a Died += AfterDeath subscription.
        // The handler is the monster's own AfterDeath(Creature) method (a
        // public instance method, not an async hook). Optional — only
        // wire it if the monster declares it as a 1-arg Creature handler.
        // LagavulinMatriarch's AfterDeath has a different signature
        // (PlayerChoiceContext, Creature, Boolean, Single), so it does
        // NOT subscribe here.
        EventInfo? diedEvent = null;
        MethodInfo? afterDeathHandler = null;
        Type? afterDeathDelegateType = null;
        var creatureClrType = sts2.GetType("MegaCrit.Sts2.Core.Entities.Creatures.Creature");
        if (creatureClrType is not null)
        {
            diedEvent = creatureClrType.GetEvent("Died", BindingFlags.Public | BindingFlags.Instance);
            var candidateHandler = monsterType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .FirstOrDefault(m => m.Name == "AfterDeath"
                    && m.GetParameters().Length == 1
                    && m.GetParameters()[0].ParameterType == creatureClrType
                    && m.ReturnType == typeof(void));
            if (diedEvent is not null && candidateHandler is not null)
            {
                afterDeathHandler = candidateHandler;
                afterDeathDelegateType = diedEvent.EventHandlerType
                    ?? typeof(Action<>).MakeGenericType(creatureClrType);
            }
        }

        var bundle = new SleepingMonsterBundle(
            PlatingAmountFromInstance: platingFromInstance,
            PlatingPowerModel: platingPowerModel,
            SleepPowerModel: sleepPowerModel,
            SleepAmount: sleepAmount,
            BaseHook: baseHook,
            CreatureProp: creatureProp,
            PowerCmdApply: applyMethod,
            DiedEvent: diedEvent,
            AfterDeathHandler: afterDeathHandler,
            AfterDeathDelegateType: afterDeathDelegateType);
        _sleepingMonsterBundles[afterAddedToRoom] = bundle;

        var prefix = typeof(HangPatches).GetMethod(
            nameof(SleepingMonsterAfterAddedToRoomPrefix),
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("SleepingMonsterAfterAddedToRoomPrefix not found");
        harmony.Patch(afterAddedToRoom, prefix: new HarmonyMethod(prefix));

        var detail = $"powers: PlatingPower({(platingAmount is null ? "PlatingAmount" : platingAmount().ToString())}), {sleepPowerId}({sleepAmount})"
            + (afterDeathHandler is not null ? "; +Died→AfterDeath" : "");
        return new PatchOutcome(label, Patched: true, Detail: detail);
    }

    // Harmony prefix shared by both monsters. `__instance` is the
    // monster model (LagavulinMatriarch / SlumberingBeetle), `__result`
    // is the return-slot Harmony writes the replacement Task into.
    // Returns `false` to suppress the original body — the powers are
    // applied via PowerCmd.Apply, the UI VFX block is skipped, and the
    // Died subscription (if applicable) is wired manually.
    private static bool SleepingMonsterAfterAddedToRoomPrefix(
        ref Task __result,
        object __instance,
        MethodBase __originalMethod)
    {
        if (!_sleepingMonsterBundles.TryGetValue(__originalMethod, out var bundle))
        {
            // Bundle missing — defensive: fall through to original body
            // (which will NRE in headless, surfacing the patch wiring
            // failure as a sweep-time error instead of silently
            // applying nothing).
            return true;
        }
        __result = RunSleepingMonsterPrefixAsync(__instance, bundle);
        return false;
    }

    private static async Task RunSleepingMonsterPrefixAsync(object instance, SleepingMonsterBundle bundle)
    {
        // 1. Await the base-class hook the original IL invokes via <>n__0.
        if (bundle.BaseHook.Invoke(instance, null) is Task baseTask) await baseTask.ConfigureAwait(false);

        var creature = bundle.CreatureProp.GetValue(instance);
        if (creature is null) return;

        // 2. Apply PlatingPower with the resolved amount.
        await ApplyPowerByClrTypeAsync(bundle.PowerCmdApply, bundle.PlatingPowerModel, creature, bundle.PlatingAmountFromInstance(instance)).ConfigureAwait(false);

        // 3. Apply the sleep power (AsleepPower / SlumberPower).
        await ApplyPowerByClrTypeAsync(bundle.PowerCmdApply, bundle.SleepPowerModel, creature, bundle.SleepAmount).ConfigureAwait(false);

        // 4. SlumberingBeetle subscribes its AfterDeath(Creature) handler
        // to Creature.Died at the tail of AfterAddedToRoom. Mirror that.
        if (bundle.DiedEvent is not null
            && bundle.AfterDeathHandler is not null
            && bundle.AfterDeathDelegateType is not null)
        {
            var add = bundle.DiedEvent.GetAddMethod(nonPublic: true);
            if (add is not null)
            {
                var del = Delegate.CreateDelegate(bundle.AfterDeathDelegateType, instance, bundle.AfterDeathHandler);
                add.Invoke(creature, new object?[] { del });
            }
        }
    }

    private static async Task ApplyPowerByClrTypeAsync(
        MethodInfo powerCmdApply,
        Type powerClrType,
        object targetCreature,
        int amount)
    {
        var canonical = ResolveCanonicalPowerByClrType(powerClrType)
            ?? throw new InvalidOperationException($"SleepingMonsterPrefix: no canonical instance for {powerClrType.FullName}");
        var mutableClone = powerClrType.GetMethod("MutableClone", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes)
            ?? throw new InvalidOperationException($"SleepingMonsterPrefix: AbstractModel.MutableClone() not found on {powerClrType.FullName}");
        var mutable = mutableClone.Invoke(canonical, null)
            ?? throw new InvalidOperationException($"SleepingMonsterPrefix: MutableClone returned null for {powerClrType.FullName}");

        // Apply(model, target, amount, source=target, cardSource=null, useFinalAmount=false).
        // Source = target mirrors the engine's "self-applied" shape; the
        // sleep-power triggers fire on damage from external sources, not
        // self, so picking source=target avoids confusing the
        // AfterDamageReceived predicate.
        var task = powerCmdApply.Invoke(null, new object?[]
        {
            mutable,
            targetCreature,
            (decimal)amount,
            targetCreature,
            /* cardSource: */ null,
            /* useFinalAmount: */ false,
        });
        if (task is Task t) await t.ConfigureAwait(false);
    }

    // Look up the canonical PowerModel singleton for `powerClrType` from
    // the engine's ModelDb. The IL form `Apply<PlatingPower>(...)` carries
    // the CLR type as its type parameter; the engine looks up the
    // matching PowerModel and clones it inside Apply<T>. We do the
    // same lookup explicitly so the non-generic Apply gets a canonical
    // input it can clone via MutableClone (PowerCmd.Apply runs
    // AssertMutable on the model).
    //
    // ModelDb exposes `Get<T:AbstractModel>() -> T` which returns the
    // canonical singleton for the CLR type — exactly the mapping
    // generic Apply<T> uses internally. Cached per CLR type so the
    // reflection cost is paid once at first sleep-power application.
    private static readonly Dictionary<Type, object?> _canonicalPowerCache = new();

    private static object? ResolveCanonicalPowerByClrType(Type powerClrType)
    {
        if (_canonicalPowerCache.TryGetValue(powerClrType, out var cached)) return cached;

        var assembly = powerClrType.Assembly;
        var modelDbType = assembly.GetType("MegaCrit.Sts2.Core.Models.ModelDb");
        if (modelDbType is null) { _canonicalPowerCache[powerClrType] = null; return null; }

        // ModelDb.DebugPower(Type) -> PowerModel — the explicit
        // type-keyed accessor for PowerModels. Mirrors the engine's
        // own internal "look up the canonical by CLR type" shape.
        var debugPower = modelDbType.GetMethod("DebugPower", BindingFlags.Public | BindingFlags.Static, new[] { typeof(Type) });
        if (debugPower is not null)
        {
            try
            {
                var canonical = debugPower.Invoke(null, new object[] { powerClrType });
                _canonicalPowerCache[powerClrType] = canonical;
                if (canonical is not null) return canonical;
            }
            catch (TargetInvocationException) { /* fall through */ }
        }

        // ModelDb.Get<T>() — typed, returns the canonical for T directly.
        var getGeneric = modelDbType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "Get" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);
        if (getGeneric is not null)
        {
            try
            {
                var closed = getGeneric.MakeGenericMethod(powerClrType);
                var canonical = closed.Invoke(null, null);
                _canonicalPowerCache[powerClrType] = canonical;
                if (canonical is not null) return canonical;
            }
            catch (TargetInvocationException) { /* fall through */ }
        }

        // Fallback: ModelDb.Get(Type) — non-generic equivalent.
        var getByType = modelDbType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static, new[] { typeof(Type) });
        if (getByType is not null)
        {
            try
            {
                var canonical = getByType.Invoke(null, new object[] { powerClrType });
                _canonicalPowerCache[powerClrType] = canonical;
                return canonical;
            }
            catch (TargetInvocationException) { /* fall through */ }
        }

        _canonicalPowerCache[powerClrType] = null;
        return null;
    }
}
