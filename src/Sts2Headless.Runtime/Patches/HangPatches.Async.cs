using System.Reflection;
using HarmonyLib;

namespace Sts2Headless.Runtime.Patches;

// Async/dispatch primitives. These are the three foundational interventions
// (sts2-cli parity) that keep the engine's async surface from deadlocking in
// headless: never park on Task.Yield, no-op Cmd.Wait, no-op the "drain
// animation/effect queue" hook.
public static partial class HangPatches
{
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
        const string label = "MegaCrit.Sts2.Core.Commands.Cmd.{Wait,CustomScaledWait}";
        var cmdType = sts2.GetType("MegaCrit.Sts2.Core.Commands.Cmd");
        if (cmdType is null)
        {
            return new PatchOutcome(label, Patched: false, Detail: "type MegaCrit.Sts2.Core.Commands.Cmd not found");
        }

        // Every wait-shaped static helper on Cmd returns Task and parks
        // on a SceneTreeTimer (or scaled variant). They all deadlock in
        // headless for the same reason and want the same treatment —
        // return Task.CompletedTask. Doormaker.DramaticOpenMove /
        // HungerMove / ScrutinyMove / GraspMove all use CustomScaledWait
        // for the animation-timing pause; without patching it, those
        // moves' state machines hang at the first await even though
        // their gameplay (HP setter, PowerCmd.Apply/Remove) had already
        // run earlier in the body. The Doormaker patches that used to
        // skip the move bodies entirely (stripping HP/power mutations)
        // were a sledgehammer for a problem this fine-grained patch
        // resolves at the actual deadlock site.
        var waits = cmdType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(m => (m.Name == "Wait" || m.Name == "CustomScaledWait")
                && typeof(System.Threading.Tasks.Task).IsAssignableFrom(m.ReturnType))
            .ToArray();
        if (waits.Length == 0)
        {
            return new PatchOutcome(label, Patched: false, Detail: "no static Wait/CustomScaledWait method returning Task on Cmd");
        }

        var prefix = typeof(HangPatches).GetMethod(nameof(ReturnCompletedTaskPrefix), BindingFlags.Static | BindingFlags.NonPublic);
        var sigs = new List<string>(waits.Length);
        foreach (var m in waits)
        {
            harmony.Patch(m, prefix: new HarmonyMethod(prefix));
            sigs.Add($"{m.Name}({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name))})");
        }
        return new PatchOutcome(label, Patched: true, Detail: string.Join(", ", sigs));
    }

    private static PatchOutcome PatchWaitUntilQueueIsEmpty(Harmony harmony, Assembly sts2)
    {
        const string name = "WaitUntilQueueIsEmptyOrWaitingOnNonPlayerDrivenAction";
        const string label = $"*.{name}";

        // Fast path: the method has lived on CombatManager since the project
        // was first scaffolded against sts2 v0.103.x. Probing that FQN first
        // saves ~60ms per process start because the slow path below has to
        // call sts2.GetTypes() (which materialises every top-level type in
        // the assembly) and then per-type GetMethod() — a tax that's
        // unnecessary when the known location is still correct.
        //
        // If a future GAME_VERSION bump moves the method, the fast path
        // misses cleanly and we fall through to the original reflective
        // scan, so the patch never silently goes missing. The slow path
        // also covers the (unlikely) case of multiple declaring types.
        var fastType = sts2.GetType("MegaCrit.Sts2.Core.Combat.CombatManager");
        var fastMethod = fastType?.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
        if (fastMethod is not null && typeof(System.Threading.Tasks.Task).IsAssignableFrom(fastMethod.ReturnType))
        {
            var prefixFast = typeof(HangPatches).GetMethod(nameof(ReturnCompletedTaskPrefix), BindingFlags.Static | BindingFlags.NonPublic);
            harmony.Patch(fastMethod, prefix: new HarmonyMethod(prefixFast));
            return new PatchOutcome(label, Patched: true, Detail: fastMethod.DeclaringType?.FullName ?? "<unknown>");
        }

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
}
