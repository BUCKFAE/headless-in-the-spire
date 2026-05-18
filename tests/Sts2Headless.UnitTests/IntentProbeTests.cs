using System.Reflection;
using System.Runtime.Loader;
using Xunit;

namespace Sts2Headless.UnitTests;

// One-shot diagnostic that loads sts2.dll and dumps every Intent-related
// type's members to /tmp/intent-types.txt. Used to design the intent
// damage/hits/block binding gap. Marked diagnostic so it stays out of
// the normal `just test` run.
public class IntentProbeTests
{
    [Fact]
    [Trait("Category", "Diagnostic")]
    public void DumpIntentTypes()
    {
        var vendor = ResolveRepoPath("vendor/sts2.dll");
        if (!File.Exists(vendor)) return; // no vendor → skip silently
        var sts2 = AssemblyLoadContext.Default.LoadFromAssemblyPath(vendor);
        Type?[] types;
        try { types = sts2.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { types = ex.Types; }

        var lines = new List<string>();
        foreach (var t in types.Where(t => t is not null && t.Name.Contains("Intent", StringComparison.Ordinal)).Cast<Type>())
        {
            lines.Add($"=== {t.FullName} (kind={(t.IsAbstract ? "abstract " : "")}{(t.IsInterface ? "interface" : (t.IsValueType ? "struct" : "class"))})");
            if (t.BaseType is not null && t.BaseType != typeof(object))
                lines.Add($"    base: {t.BaseType.FullName}");
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                lines.Add($"    field {f.FieldType.Name} {f.Name}");
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                lines.Add($"    prop  {p.PropertyType.Name} {p.Name}");
            lines.Add("");
        }
        File.WriteAllLines("/tmp/intent-types.txt", lines);
    }

    private static string ResolveRepoPath(string relative)
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8; i++)
        {
            if (File.Exists(Path.Combine(dir, "Sts2Headless.slnx"))) return Path.Combine(dir, relative);
            var p = Directory.GetParent(dir);
            if (p is null) break;
            dir = p.FullName;
        }
        return Path.Combine(AppContext.BaseDirectory, relative);
    }
}
