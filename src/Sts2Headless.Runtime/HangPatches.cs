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
            PatchCardSelectCmdFactories(harmony, sts2),
            PatchVantomDismemberMove(harmony, sts2),
            PatchEscapeArtistPowerAfterTurnEnd(harmony, sts2),
            PatchThievingHopperMoves(harmony, sts2),
            PatchBowlbugRockMoves(harmony, sts2),
            PatchImbalancedPowerAfterDamageGiven(harmony, sts2),
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

    // Defensive backstop for any code path that calls CardSelectCmd.From*
    // factories in headless. Each factory is `static async Task<CardSelectCmd>`
    // and synchronously calls NSimpleCardSelectScreen.Create /
    // NDeckUpgradeSelectScreen.ShowScreen *before* the first await — which
    // Load() the .tscn from the Godot asset cache (empty in headless) and
    // NRE on the null result. Since the throw is pre-first-await, it
    // surfaces synchronously, bubbles out of run/select_event_option, and
    // aborts the host:
    //
    //   System.NullReferenceException
    //     at NSimpleCardSelectScreen.Create(IReadOnlyList`1, CardSelectorPrefs)
    //     at CardSelectCmd.FromSimpleGridForRewards(...)
    //     at RoomFullOfCheese.Gorge()
    //     at EventOption.Chosen()
    //
    // Patch shape: prefix returns Task.FromResult<TInner>(default) where
    // TInner is the unwrapped return type. The caller awaits a null
    // CardSelectCmd — most existing callers then dereference it and NRE
    // *again* at the event-model layer. The agent-side fix
    // (GreedyAgent.StepEventAsync prefers the last unlocked option, which
    // is conventionally "Leave" / "Decline") avoids those handlers
    // entirely. This patch remains as defense in depth: if a wire
    // consumer or future agent picks a gorge-style option, the
    // synchronous host-killing NRE is replaced with a softer
    // null-deref-in-handler that an integration test can attribute to
    // the event rather than the bridge.
    private static PatchOutcome PatchCardSelectCmdFactories(Harmony harmony, Assembly sts2)
    {
        const string label = "MegaCrit.Sts2.Core.Commands.CardSelectCmd.From*";
        var cmdType = sts2.GetType("MegaCrit.Sts2.Core.Commands.CardSelectCmd");
        if (cmdType is null)
        {
            return new PatchOutcome(label, Patched: false, Detail: "type MegaCrit.Sts2.Core.Commands.CardSelectCmd not found");
        }

        var methods = cmdType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName
                        && m.Name.StartsWith("From", StringComparison.Ordinal)
                        && typeof(System.Threading.Tasks.Task).IsAssignableFrom(m.ReturnType))
            .ToArray();
        if (methods.Length == 0)
        {
            return new PatchOutcome(label, Patched: false, Detail: "no Task-returning From* methods on CardSelectCmd");
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
    {
        const string label = "MegaCrit.Sts2.Core.Models.Monsters.Vantom.DismemberMove";
        var vantomType = sts2.GetType("MegaCrit.Sts2.Core.Models.Monsters.Vantom");
        if (vantomType is null)
        {
            return new PatchOutcome(label, Patched: false, Detail: "type MegaCrit.Sts2.Core.Models.Monsters.Vantom not found");
        }

        var methods = vantomType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(m => m.Name == "DismemberMove" && !m.IsSpecialName)
            .ToArray();
        if (methods.Length == 0)
        {
            return new PatchOutcome(label, Patched: false, Detail: "DismemberMove not found on Vantom");
        }

        var sigs = new List<string>(methods.Length);
        foreach (var m in methods)
        {
            MethodInfo prefix;
            if (typeof(System.Threading.Tasks.Task).IsAssignableFrom(m.ReturnType))
            {
                prefix = typeof(HangPatches).GetMethod(nameof(ReturnDefaultTaskPrefix), BindingFlags.Static | BindingFlags.NonPublic)!;
            }
            else if (m.ReturnType == typeof(void))
            {
                prefix = typeof(HangPatches).GetMethod(nameof(SkipVoidPrefix), BindingFlags.Static | BindingFlags.NonPublic)!;
            }
            else if (!m.ReturnType.IsValueType)
            {
                prefix = typeof(HangPatches).GetMethod(nameof(ReturnNullPrefix), BindingFlags.Static | BindingFlags.NonPublic)!;
            }
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
    {
        const string label = "MegaCrit.Sts2.Core.Models.Monsters.ThievingHopper.*Move";
        var monsterType = sts2.GetType("MegaCrit.Sts2.Core.Models.Monsters.ThievingHopper");
        if (monsterType is null)
        {
            return new PatchOutcome(label, Patched: false, Detail: "type ThievingHopper not found");
        }

        var moveNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "ThieveryMove", "NabMove", "HatTrickMove", "FlutterMove", "EscapeMove",
        };
        var methods = monsterType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(m => moveNames.Contains(m.Name) && typeof(System.Threading.Tasks.Task).IsAssignableFrom(m.ReturnType))
            .ToArray();
        if (methods.Length == 0)
        {
            return new PatchOutcome(label, Patched: false, Detail: "no Task-returning Move methods on ThievingHopper");
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

    // BowlbugRock (Act 2 enemy on seed 42 floor 12) has two move methods
    // — HeadbuttMove and DizzyMove. Same shape as Vantom.DismemberMove
    // and ThievingHopper.*Move: Task-returning bodies that NRE in
    // headless, exception swallowed by TaskHelper.LogTaskExceptions,
    // combat half-transitioned. Replace both with Task.CompletedTask.
    private static PatchOutcome PatchBowlbugRockMoves(Harmony harmony, Assembly sts2)
    {
        const string label = "MegaCrit.Sts2.Core.Models.Monsters.BowlbugRock.*Move";
        var monsterType = sts2.GetType("MegaCrit.Sts2.Core.Models.Monsters.BowlbugRock");
        if (monsterType is null)
        {
            return new PatchOutcome(label, Patched: false, Detail: "type BowlbugRock not found");
        }

        var moveNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "HeadbuttMove", "DizzyMove",
        };
        var methods = monsterType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(m => moveNames.Contains(m.Name) && typeof(System.Threading.Tasks.Task).IsAssignableFrom(m.ReturnType))
            .ToArray();
        if (methods.Length == 0)
        {
            return new PatchOutcome(label, Patched: false, Detail: "no Task-returning Move methods on BowlbugRock");
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
