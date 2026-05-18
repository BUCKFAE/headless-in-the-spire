using System.Reflection;
using System.Runtime.Loader;
using Xunit;

namespace Sts2Headless.UnitTests;

// Diagnostic: enumerate every potion-related type in sts2.dll and dump
// its members to /tmp/potion-types.txt. Used to design the run/use_potion
// binding without checking in a brittle FQN list.
public class PotionProbeTests
{
    [Fact]
    [Trait("Category", "Diagnostic")]
    public void DumpPotionTypes()
    {
        var vendor = ResolveRepoPath("vendor/sts2.dll");
        if (!File.Exists(vendor)) return;
        var sts2 = AssemblyLoadContext.Default.LoadFromAssemblyPath(vendor);
        Type?[] types;
        try { types = sts2.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { types = ex.Types; }

        var lines = new List<string>();
        var hits = types
            .Where(t => t is not null && (
                t.Name.Contains("Potion", StringComparison.Ordinal)
                || t.Name.Contains("PlayerInventory", StringComparison.Ordinal)
                || t.Name.Contains("ItemBag", StringComparison.Ordinal)))
            .Cast<Type>()
            .Where(t => t.Namespace?.Contains("Node", StringComparison.Ordinal) != true)
            .OrderBy(t => t.FullName);
        foreach (var t in hits)
        {
            lines.Add($"=== {t.FullName} (kind={(t.IsAbstract ? "abstract " : "")}{(t.IsInterface ? "interface" : (t.IsValueType ? "struct" : "class"))})");
            if (t.BaseType is not null && t.BaseType != typeof(object))
                lines.Add($"    base: {t.BaseType.FullName}");
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                lines.Add($"    field {f.FieldType.Name} {f.Name}");
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                lines.Add($"    prop  {p.PropertyType.Name} {p.Name}");
            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(m => !m.IsSpecialName).Take(15))
                lines.Add($"    meth  {m.ReturnType.Name} {m.Name}({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name))})");
            lines.Add("");
        }
        File.WriteAllLines("/tmp/potion-types.txt", lines);
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
