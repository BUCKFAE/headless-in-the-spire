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
}
