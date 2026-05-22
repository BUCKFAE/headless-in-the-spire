using System.Reflection;
using HarmonyLib;

namespace Sts2Headless.Runtime.Patches;

// Per-power patches. Same shape as the monster-move patches in
// HangPatches.Monsters.cs — Task-returning hooks (AfterTurnEnd, AfterAttack,
// AfterApplied …) that walk UI state and NRE in headless. Generalised via
// PatchPowerMethods to keep the call sites declarative.
public static partial class HangPatches
{
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

    // CORPSE_SLUG carries RAVENOUS_POWER:4 — the "spawn slimed when killed"
    // listener that runs AfterDeath and StunnedMove async hooks.
    private static PatchOutcome PatchRavenousPower(Harmony harmony, Assembly sts2)
        => PatchPowerMethods(harmony, sts2,
            typeFqn: "MegaCrit.Sts2.Core.Models.Powers.RavenousPower",
            methodNames: new HashSet<string>(StringComparer.Ordinal) { "AfterDeath", "StunnedMove" },
            label: "MegaCrit.Sts2.Core.Models.Powers.RavenousPower.{AfterDeath, StunnedMove}");

    // DECIMILLIPEDE_SEGMENT carries REATTACH_POWER:25 — re-attaches the
    // segment on death. DoReattach + AfterDeath both walk segment-link
    // UI nodes that don't exist headless.
    private static PatchOutcome PatchReattachPower(Harmony harmony, Assembly sts2)
        => PatchPowerMethods(harmony, sts2,
            typeFqn: "MegaCrit.Sts2.Core.Models.Powers.ReattachPower",
            methodNames: new HashSet<string>(StringComparer.Ordinal)
            {
                "AfterDeath", "DoReattach", "PlayVfxAndThenRemoveNodes",
            },
            label: "MegaCrit.Sts2.Core.Models.Powers.ReattachPower.{AfterDeath, DoReattach, PlayVfxAndThenRemoveNodes}");

    // DOORMAKER carries HUNGER_POWER:1 — async hooks fire on Afflict /
    // AfterApplied / AfterCardEnteredCombat. The card-entered-combat hook
    // is what trips on the agent's first Hellraiser play, before the
    // stall fingerprint is captured.
    private static PatchOutcome PatchHungerPower(Harmony harmony, Assembly sts2)
        => PatchPowerMethods(harmony, sts2,
            typeFqn: "MegaCrit.Sts2.Core.Models.Powers.HungerPower",
            methodNames: new HashSet<string>(StringComparer.Ordinal)
            {
                "Afflict", "AfterApplied", "AfterCardEnteredCombat",
            },
            label: "MegaCrit.Sts2.Core.Models.Powers.HungerPower.{Afflict, AfterApplied, AfterCardEnteredCombat}");

    // TERROR_EEL carries VIGOR_POWER:6 — fires AfterAttack on every
    // attack-shaped move. Hangs the enemy turn after the eel's first hit.
    private static PatchOutcome PatchVigorPower(Harmony harmony, Assembly sts2)
        => PatchPowerMethods(harmony, sts2,
            typeFqn: "MegaCrit.Sts2.Core.Models.Powers.VigorPower",
            methodNames: new HashSet<string>(StringComparer.Ordinal) { "AfterAttack" },
            label: "MegaCrit.Sts2.Core.Models.Powers.VigorPower.AfterAttack");

    // CRAB_RAGE_POWER is the on-death listener for the KAISER_CRAB_BOSS
    // arms. The encounter has no `KaiserCrab` monster type — the boss
    // is implemented as a pair of `Crusher` monsters (see PatchCrusher
    // in HangPatches.Monsters.cs) with this power. Patched defensively
    // per the SoulNexus precedent.
    private static PatchOutcome PatchCrabRagePower(Harmony harmony, Assembly sts2)
        => PatchPowerMethods(harmony, sts2,
            typeFqn: "MegaCrit.Sts2.Core.Models.Powers.CrabRagePower",
            methodNames: new HashSet<string>(StringComparer.Ordinal) { "AfterDeath" },
            label: "MegaCrit.Sts2.Core.Models.Powers.CrabRagePower.AfterDeath");

    // Shared helper for the per-power patches above. Mirror of
    // PatchMonsterMethods — resolve the type by FQN, filter declared
    // methods by name and return-type kind, prefix each with the
    // appropriate "return default" body.
    private static PatchOutcome PatchPowerMethods(
        Harmony harmony,
        Assembly sts2,
        string typeFqn,
        HashSet<string> methodNames,
        string label)
    {
        var powerType = sts2.GetType(typeFqn);
        if (powerType is null)
            return new PatchOutcome(label, Patched: false, Detail: $"type {typeFqn} not found");

        var methods = powerType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
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
