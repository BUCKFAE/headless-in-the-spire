using System.Reflection;
using HarmonyLib;
using Sts2Headless.Runtime;

namespace Sts2Headless.Commands;

// Caller-finder probe — `just probe-callers Method[,Other]`. Loads sts2.dll
// and walks every declared method's CIL looking for call/callvirt to a
// method whose simple name matches one of the supplied patterns. Prints
// caller FQN -> resolved target signature (including closed generic args
// when the IL pins them). Built for the Doormaker.SwapPhasePower<T>
// investigation — Harmony can't patch the open generic, but it can patch
// each closed instantiation the IL exposes.
internal static class ProbeCallersCommand
{
    public static int Run(string vendorDir, string[] args)
    {
        var idx = Array.IndexOf(args, "--probe-callers");
        var patterns = (idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);
        if (patterns.Count == 0)
        {
            Console.Error.WriteLine("usage: --probe-callers <MethodName>[,<MethodName>...]");
            return 1;
        }

        var preamble = RuntimeBootstrap.Run(vendorDir);
        if (preamble.SetupError is not null)
        {
            Console.Error.WriteLine($"  bootstrap setup failed: {preamble.SetupError}");
            return 1;
        }

        Type?[] types;
        try { types = preamble.Sts2!.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { types = ex.Types; }

        var hits = 0;
        foreach (var t in types)
        {
            if (t is null) continue;
            MethodInfo[] methods;
            try { methods = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly); }
            catch { continue; }
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
                    if (ins.operand is not MethodBase target) continue;
                    if (!patterns.Contains(target.Name)) continue;
                    hits++;
                    Console.WriteLine($"  {t.FullName}.{m.Name} -> {Format(target)}");
                }
            }
        }
        Console.WriteLine($"-- {hits} caller hit(s) --");
        return 0;
    }

    private static string Format(MethodBase m)
    {
        var owner = m.DeclaringType?.FullName ?? "?";
        if (m is MethodInfo mi && mi.IsGenericMethod && !mi.IsGenericMethodDefinition)
        {
            var args = string.Join(",", mi.GetGenericArguments().Select(a => a.FullName ?? a.Name));
            return $"{owner}.{m.Name}<{args}>";
        }
        return $"{owner}.{m.Name}";
    }
}
