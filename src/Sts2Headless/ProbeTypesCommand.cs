using System.Reflection;
using Sts2Headless.Runtime;

namespace Sts2Headless;

// Type/method inspection probe — `just probe-types MonsterName,OtherName`.
// Loads sts2.dll, finds every type whose simple or full name contains any
// of the supplied substrings (case-insensitive), and prints its FQN plus
// every declared Task-returning method. Used when planning a new monster
// hang-patch: without it we'd write a `PatchMonsterMethods("...")` with a
// guessed FQN that silently no-ops if wrong.
internal static class ProbeTypesCommand
{
    public static int Run(string vendorDir, string[] args)
    {
        var idx = Array.IndexOf(args, "--probe-types");
        var patterns = (idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (patterns.Length == 0)
        {
            Console.Error.WriteLine("usage: --probe-types <substring>[,<substring>...]");
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

        foreach (var pat in patterns)
        {
            Console.WriteLine($"=== {pat} ===");
            var hits = types
                .Where(t => t is not null
                    && ((t.FullName ?? t.Name).Contains(pat, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(t => t!.FullName ?? t!.Name)
                .ToList();
            if (hits.Count == 0)
            {
                Console.WriteLine("  <no matches>");
                continue;
            }
            foreach (var t in hits)
            {
                Console.WriteLine($"  {t!.FullName}");
                MethodInfo[] methods;
                try { methods = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly); }
                catch { continue; }
                foreach (var m in methods
                    .Where(m => !m.IsSpecialName)
                    .OrderBy(m => m.Name))
                {
                    // Some methods reference Godot types we deliberately
                    // don't stub (NDriver-style UI nodes); probing their
                    // return/parameter types throws TypeLoadException.
                    // Skip those rather than aborting the whole dump.
                    try
                    {
                        var rt = m.ReturnType;
                        var rtName = typeof(System.Threading.Tasks.Task).IsAssignableFrom(rt)
                            ? $"Task" + (rt.IsGenericType ? $"<{rt.GetGenericArguments()[0].Name}>" : "")
                            : rt.Name;
                        var ps = string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name));
                        Console.WriteLine($"    {m.Name}({ps}) -> {rtName}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"    {m.Name}(?) -> ? <{ex.GetType().Name}: {ex.Message.Split('\n')[0]}>");
                    }
                }
            }
        }
        return 0;
    }
}
