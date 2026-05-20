using System.Reflection;
using HarmonyLib;

namespace Sts2Headless.Runtime;

// Runtime Harmony patches that neutralise sts2.dll's async pumping. Without
// these, anything that hits a Godot frame-yield or a "wait for the animation
// queue to drain" call deadlocks immediately — the headless host has no
// frame loop and no animation queue.
//
// AD-4: we never name sts2 types in C#. The Cmd.Wait and WaitUntilQueue…
// methods are discovered by reflection from the loaded assembly. The Yield
// awaiter target is in the runtime, so we name it directly.
//
// Three patches, matching the three sts2-cli interventions (see
// documentation/research/04-sts2-cli-anatomy.md):
//
//   1. YieldAwaitable.YieldAwaiter.get_IsCompleted → true.
//      `await Task.Yield()` therefore never parks, continuation runs inline.
//   2. MegaCrit.Sts2.Core.Commands.Cmd.Wait(float) → Task.CompletedTask.
//      Used for UI animation pacing; in headless mode there is nothing to
//      wait for, and leaving it intact deadlocks on certain boss moves.
//   3. *.WaitUntilQueueIsEmptyOrWaitingOnNonPlayerDrivenAction → Task.CompletedTask.
//      The game's "drain animation/effect queue" hook; same rationale.
public static class HangPatches
{
    public sealed record PatchOutcome(string Target, bool Patched, string? Detail);

    private const string HarmonyId = "headless-in-the-spire.hang-patches";

    public static IReadOnlyList<PatchOutcome> Apply(Assembly sts2)
    {
        var harmony = new Harmony(HarmonyId);
        return
        [
            PatchYieldAwaiterIsCompleted(harmony),
            PatchCmdWait(harmony, sts2),
            PatchWaitUntilQueueIsEmpty(harmony, sts2),
            PatchTalkCmdPlay(harmony, sts2),
            // CardSelectCmd.From* factories used to be patched here to return
            // Task.FromResult(default) so events that opened a card-pick
            // screen (e.g. RoomFullOfCheese.Gorge) wouldn't take the host
            // down. That band-aid stopped the synchronous crash but left
            // every card that legitimately needs a card-pick (Headbutt,
            // Armaments, Burning Pact) awaiting a null CardSelectCmd. The
            // supported fix is to install a MegaCrit.Sts2.Core.TestSupport
            // .ICardSelector via CardSelectCmd.UseSelector — that runs in
            // CardSelectorInstaller during RuntimeBootstrap and covers
            // the screen-based factories (FromSimpleGrid, FromChooseACardScreen)
            // that Headbutt uses end-to-end.
            //
            // FromHandForUpgrade (Armaments) and FromHandForDiscard
            // (Burning Pact) need a different intervention: their bodies
            // unconditionally call NPlayerHand.Instance.CancelAllCardPlay
            // (NRE in headless — Instance is null) AND, on the
            // ShouldSelectLocalCard=false branch, PlayerChoiceSynchronizer
            // .WaitForRemoteChoice (throws "Cannot wait for remote choice
            // in singleplayer!" by design). Both branches fail. The fix
            // is to replace the body entirely with a prefix that runs the
            // engine's hand-filter logic, consults our selector, and
            // returns the picked CardModel via Task.FromResult — same
            // contract as the original async method, none of the
            // UI/choice-sync side effects that headless can't satisfy.
            PatchFromHandForUpgrade(harmony, sts2),
            PatchFromHandForDiscard(harmony, sts2),
            PatchFromHand(harmony, sts2),
            PatchVantomDismemberMove(harmony, sts2),
            PatchEscapeArtistPowerAfterTurnEnd(harmony, sts2),
            PatchThievingHopperMoves(harmony, sts2),
            PatchBowlbugRockMoves(harmony, sts2),
            PatchImbalancedPowerAfterDamageGiven(harmony, sts2),
            PatchSoulNexus(harmony, sts2),
            PatchTestSubject(harmony, sts2),
            PatchCeremonialBeast(harmony, sts2),
        ];
    }

    private static PatchOutcome PatchYieldAwaiterIsCompleted(Harmony harmony)
    {
        const string label = "YieldAwaitable.YieldAwaiter.get_IsCompleted";
        var awaiter = typeof(System.Runtime.CompilerServices.YieldAwaitable).GetNestedType("YieldAwaiter");
        var getter = awaiter?.GetProperty("IsCompleted")?.GetGetMethod();
        if (getter is null)
        {
            return new PatchOutcome(label, Patched: false, Detail: "getter not found in runtime");
        }

        var prefix = typeof(HangPatches).GetMethod(nameof(YieldIsCompletedPrefix), BindingFlags.Static | BindingFlags.NonPublic);
        harmony.Patch(getter, prefix: new HarmonyMethod(prefix));
        return new PatchOutcome(label, Patched: true, Detail: null);
    }

    private static PatchOutcome PatchCmdWait(Harmony harmony, Assembly sts2)
    {
        const string label = "MegaCrit.Sts2.Core.Commands.Cmd.Wait(float)";
        var cmdType = sts2.GetType("MegaCrit.Sts2.Core.Commands.Cmd");
        if (cmdType is null)
        {
            return new PatchOutcome(label, Patched: false, Detail: "type MegaCrit.Sts2.Core.Commands.Cmd not found");
        }

        // Wait may have multiple overloads; we patch every static Wait(...) on the type that returns Task.
        var waits = cmdType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(m => m.Name == "Wait" && typeof(System.Threading.Tasks.Task).IsAssignableFrom(m.ReturnType))
            .ToArray();
        if (waits.Length == 0)
        {
            return new PatchOutcome(label, Patched: false, Detail: "no static Wait method returning Task on Cmd");
        }

        var prefix = typeof(HangPatches).GetMethod(nameof(ReturnCompletedTaskPrefix), BindingFlags.Static | BindingFlags.NonPublic);
        var sigs = new List<string>(waits.Length);
        foreach (var m in waits)
        {
            harmony.Patch(m, prefix: new HarmonyMethod(prefix));
            sigs.Add($"Wait({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name))})");
        }
        return new PatchOutcome(label, Patched: true, Detail: string.Join(", ", sigs));
    }

    // BygoneEffigy.WakeMove (and other intro monster moves) invokes
    // TalkCmd.Play(LocString, Creature, VfxColor, VfxDuration) to pop a speech
    // bubble over the speaker. Real Play returns NSpeechBubbleVfx (a Node-
    // derived UI object) and walks UI-only state to construct it; in headless
    // those nodes are absent, so the body NREs. The exception is swallowed by
    // TaskHelper.LogTaskExceptions inside the enemy-turn async chain, leaving
    // combat half-transitioned (EndingPlayerTurnPhaseTwo=True,
    // IsEnemyTurnStarted=True, IsPlayPhase=False) — the residual combat-stall
    // pattern after the GodotStubs gaps are filled.
    //
    // Patch shape: prefix that skips the original (returns false) and sets
    // __result to null. Caller code paths either null-check the returned VFX
    // or tween it; in headless the tween is patched to no-op separately.
    private static PatchOutcome PatchTalkCmdPlay(Harmony harmony, Assembly sts2)
    {
        const string label = "MegaCrit.Sts2.Core.Commands.TalkCmd.*";
        var talkCmdType = sts2.GetType("MegaCrit.Sts2.Core.Commands.TalkCmd");
        if (talkCmdType is null)
        {
            return new PatchOutcome(label, Patched: false, Detail: "type MegaCrit.Sts2.Core.Commands.TalkCmd not found");
        }

        var methods = talkCmdType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && !m.ReturnType.IsValueType && !typeof(System.Threading.Tasks.Task).IsAssignableFrom(m.ReturnType))
            .ToArray();
        if (methods.Length == 0)
        {
            return new PatchOutcome(label, Patched: false, Detail: "no reference-returning methods on TalkCmd to no-op");
        }

        var prefix = typeof(HangPatches).GetMethod(nameof(ReturnNullPrefix), BindingFlags.Static | BindingFlags.NonPublic);
        var sigs = new List<string>(methods.Length);
        foreach (var m in methods)
        {
            harmony.Patch(m, prefix: new HarmonyMethod(prefix));
            sigs.Add($"{m.Name}({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name))}) → {m.ReturnType.Name}");
        }
        return new PatchOutcome(label, Patched: true, Detail: string.Join(", ", sigs));
    }

    private static PatchOutcome PatchFromHandForUpgrade(Harmony harmony, Assembly sts2)
        => PatchFromHandFactory(
            harmony,
            sts2,
            methodName: "FromHandForUpgrade",
            // Cards in hand that aren't already upgraded. CardModel.IsUpgraded
            // returns true when the card is at max upgrade level (the engine
            // refuses to upgrade further); the filter scoping is intentionally
            // permissive so a hand with no upgradeable card just yields a
            // null pick, matching the engine's "no eligible options" case.
            filter: HeadlessCardSelectorBridge.IsNotUpgraded,
            // Method wire signature is (PlayerChoiceContext, Player,
            // AbstractModel) — no caller-supplied filter.
            playerArgIndex: 1,
            callerFilterArgIndex: -1);

    private static PatchOutcome PatchFromHandForDiscard(Harmony harmony, Assembly sts2)
        => PatchFromHandFactory(
            harmony,
            sts2,
            methodName: "FromHandForDiscard",
            // FromHandForDiscard's signature passes a caller-supplied
            // filter (Func<CardModel, bool>) at arg[3]; we apply it to
            // every hand card before picking.
            filter: null,
            playerArgIndex: 1,
            callerFilterArgIndex: 3);

    private static PatchOutcome PatchFromHand(Harmony harmony, Assembly sts2)
        => PatchFromHandFactory(
            harmony,
            sts2,
            methodName: "FromHand",
            // FromHand's signature passes a caller-supplied filter (Func<
            // CardModel, bool>) at arg[3]; BurningPact uses it to scope
            // the pickable set. We honour it so the picked card is
            // actually eligible for the caller's effect.
            filter: null,
            playerArgIndex: 1,
            callerFilterArgIndex: 3);

    private static PatchOutcome PatchFromHandFactory(
        Harmony harmony,
        Assembly sts2,
        string methodName,
        Func<object, bool>? filter,
        int playerArgIndex,
        int callerFilterArgIndex)
    {
        var label = $"MegaCrit.Sts2.Core.Commands.CardSelectCmd.{methodName}";
        var cmdType = sts2.GetType("MegaCrit.Sts2.Core.Commands.CardSelectCmd");
        if (cmdType is null)
        {
            return new PatchOutcome(label, Patched: false, Detail: "CardSelectCmd not found");
        }
        var method = cmdType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .FirstOrDefault(m => m.Name == methodName);
        if (method is null)
        {
            return new PatchOutcome(label, Patched: false, Detail: $"{methodName} not found on CardSelectCmd");
        }
        // Bind the bridge once per patched method so the harmony prefix
        // closes over the right filter+arg-indices.
        HeadlessCardSelectorBridge.RegisterFromHandFactory(method, filter, playerArgIndex, callerFilterArgIndex);
        var prefix = typeof(HeadlessCardSelectorBridge).GetMethod(
            nameof(HeadlessCardSelectorBridge.FromHandFactoryPrefix),
            BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("HeadlessCardSelectorBridge.FromHandFactoryPrefix not found");
        harmony.Patch(method, prefix: new HarmonyMethod(prefix));
        return new PatchOutcome(label, Patched: true, Detail: $"args=({string.Join(",", method.GetParameters().Select(p => p.ParameterType.Name))})");
    }

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

    // EscapeArtistPower carries an AfterTurnEnd hook (PlayerChoiceContext,
    // CombatSide) → Task. THIEVING_HOPPER (Act 2 enemy) ships with
    // ESCAPE_ARTIST_POWER:5; when the player ends their turn, the enemy
    // turn pipeline awaits this hook, and the hook hangs in headless.
    // Observed in DiagnoseAct2WalkTests on seed 42, Act 2 floor 3: every
    // subsequent run/end_turn returns a snapshot with combat still in
    // progress (the engine never flips back to play phase), and the
    // agent enters an infinite end-turn loop.
    //
    // Patch shape: same as the CardSelectCmd.From* and Vantom.DismemberMove
    // patches — replace the body with `Task.CompletedTask`. The power
    // simply doesn't fire its AfterTurnEnd effect in headless. The damage-
    // cap behaviour (the part the agent's drain strategy actually cares
    // about) lives on SlipperyPower.ModifyDamageCap, which is unaffected.
    private static PatchOutcome PatchEscapeArtistPowerAfterTurnEnd(Harmony harmony, Assembly sts2)
    {
        const string label = "MegaCrit.Sts2.Core.Models.Powers.EscapeArtistPower.AfterTurnEnd";
        var powerType = sts2.GetType("MegaCrit.Sts2.Core.Models.Powers.EscapeArtistPower");
        if (powerType is null)
        {
            return new PatchOutcome(label, Patched: false, Detail: "type EscapeArtistPower not found");
        }

        var methods = powerType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(m => m.Name == "AfterTurnEnd"
                        && typeof(System.Threading.Tasks.Task).IsAssignableFrom(m.ReturnType))
            .ToArray();
        if (methods.Length == 0)
        {
            return new PatchOutcome(label, Patched: false, Detail: "no Task-returning AfterTurnEnd method on EscapeArtistPower");
        }

        var prefix = typeof(HangPatches).GetMethod(nameof(ReturnDefaultTaskPrefix), BindingFlags.Static | BindingFlags.NonPublic);
        var sigs = new List<string>(methods.Length);
        foreach (var m in methods)
        {
            harmony.Patch(m, prefix: new HarmonyMethod(prefix));
            sigs.Add($"{m.Name}({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name))}) → {m.ReturnType.Name}");
        }
        return new PatchOutcome(label, Patched: true, Detail: string.Join(", ", sigs));
    }

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

    // ImbalancedPower carries an AfterDamageGiven hook that fires whenever
    // the holder lands damage. BowlbugRock ships with IMBALANCED_POWER:1
    // on Act 2 seed 42 floor 12; when it attacks (HeadbuttMove is patched
    // above, but other code paths still trigger damage), this hook runs
    // and hangs in headless. Same Task-returning shape as
    // EscapeArtistPower.AfterTurnEnd — replace with Task.CompletedTask.
    private static PatchOutcome PatchImbalancedPowerAfterDamageGiven(Harmony harmony, Assembly sts2)
    {
        const string label = "MegaCrit.Sts2.Core.Models.Powers.ImbalancedPower.AfterDamageGiven";
        var powerType = sts2.GetType("MegaCrit.Sts2.Core.Models.Powers.ImbalancedPower");
        if (powerType is null)
        {
            return new PatchOutcome(label, Patched: false, Detail: "type ImbalancedPower not found");
        }

        var methods = powerType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(m => m.Name == "AfterDamageGiven"
                        && typeof(System.Threading.Tasks.Task).IsAssignableFrom(m.ReturnType))
            .ToArray();
        if (methods.Length == 0)
        {
            return new PatchOutcome(label, Patched: false, Detail: "no Task-returning AfterDamageGiven method on ImbalancedPower");
        }

        var prefix = typeof(HangPatches).GetMethod(nameof(ReturnDefaultTaskPrefix), BindingFlags.Static | BindingFlags.NonPublic);
        var sigs = new List<string>(methods.Length);
        foreach (var m in methods)
        {
            harmony.Patch(m, prefix: new HarmonyMethod(prefix));
            sigs.Add($"{m.Name}({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name))}) → {m.ReturnType.Name}");
        }
        return new PatchOutcome(label, Patched: true, Detail: string.Join(", ", sigs));
    }

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

    private static PatchOutcome PatchWaitUntilQueueIsEmpty(Harmony harmony, Assembly sts2)
    {
        const string name = "WaitUntilQueueIsEmptyOrWaitingOnNonPlayerDrivenAction";
        const string label = $"*.{name}";

        // sts2-cli's Cecil patch iterates top-level types only — the method lives on
        // a top-level type. We do the same scan reflectively. If multiple matches
        // appear (unlikely), patch them all and report.
        Type?[] declaredTypes;
        try { declaredTypes = sts2.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { declaredTypes = ex.Types; }

        var matches = declaredTypes
            .Where(t => t is not null)
            .Select(t => t!.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(m => m is not null && typeof(System.Threading.Tasks.Task).IsAssignableFrom(m.ReturnType))
            .Cast<MethodInfo>()
            .ToArray();
        if (matches.Length == 0)
        {
            return new PatchOutcome(label, Patched: false, Detail: $"no method named {name} returning Task found");
        }

        var prefix = typeof(HangPatches).GetMethod(nameof(ReturnCompletedTaskPrefix), BindingFlags.Static | BindingFlags.NonPublic);
        var hosts = new List<string>(matches.Length);
        foreach (var m in matches)
        {
            harmony.Patch(m, prefix: new HarmonyMethod(prefix));
            hosts.Add(m.DeclaringType?.FullName ?? "<unknown>");
        }
        return new PatchOutcome(label, Patched: true, Detail: string.Join(", ", hosts));
    }

    // Shared helper for the monster *Move / lifecycle-hook patches below
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

    // Harmony prefix signatures: returning false skips the original method;
    // __result is the return slot the patched method will see.

    private static bool YieldIsCompletedPrefix(ref bool __result)
    {
        __result = true;
        return false;
    }

    private static bool ReturnCompletedTaskPrefix(ref System.Threading.Tasks.Task __result)
    {
        __result = System.Threading.Tasks.Task.CompletedTask;
        return false;
    }

    // Generic "skip original, return null" prefix for reference-returning
    // methods whose body NREs in headless because it walks UI-only state
    // (TalkCmd.Play and friends). Harmony copies the boxed-null into the
    // typed return slot, which JIT erases for plain `class` returns.
    private static bool ReturnNullPrefix(ref object? __result)
    {
        __result = null;
        return false;
    }

    // "Skip body entirely" prefix for void-returning methods (Vantom monster
    // moves). No __result slot — Harmony just suppresses the original.
    private static bool SkipVoidPrefix() => false;

    // Generic "skip original, return a completed Task with default result"
    // prefix for `async Task<T>` factories whose pre-first-await body NREs
    // in headless (CardSelectCmd.From*). For non-generic Task it returns
    // Task.CompletedTask; for Task<T> it returns Task.FromResult<T>(default).
    // The factories' callers `await` the result, so the synchronous NRE
    // becomes a normal `null` await. Harmony injects __originalMethod so
    // the prefix can introspect the actual return type per call site.
    private static bool ReturnDefaultTaskPrefix(ref System.Threading.Tasks.Task __result, MethodBase __originalMethod)
    {
        var rt = ((MethodInfo)__originalMethod).ReturnType;
        if (!rt.IsGenericType || rt.GetGenericTypeDefinition() != typeof(System.Threading.Tasks.Task<>))
        {
            __result = System.Threading.Tasks.Task.CompletedTask;
            return false;
        }
        var inner = rt.GetGenericArguments()[0];
        var fromResult = typeof(System.Threading.Tasks.Task)
            .GetMethod(nameof(System.Threading.Tasks.Task.FromResult), BindingFlags.Public | BindingFlags.Static)!
            .MakeGenericMethod(inner);
        var defaultValue = inner.IsValueType ? Activator.CreateInstance(inner) : null;
        __result = (System.Threading.Tasks.Task)fromResult.Invoke(null, [defaultValue])!;
        return false;
    }
}
