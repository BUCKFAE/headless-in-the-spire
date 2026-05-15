using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using Xunit;

namespace Sts2Headless.UnitTests;

// One-shot diagnostic: dump every Monster type's name + its move-handler
// methods, plus the ESCAPE_ARTIST power's methods. Used to identify which
// method body to no-op when a particular monster turn hangs in headless
// (same investigation shape as the VANTOM/DismemberMove patch).
//
// Marked diagnostic; not part of the default test run.
public class MonsterHangDiscoveryTests
{
    [Fact]
    [Trait("category", "diagnostic")]
    public void DumpMonsterAndPowerTypes()
    {
        var vendor = ResolveRepoPath("vendor/sts2.dll");
        if (!File.Exists(vendor)) return;
        var sts2 = AssemblyLoadContext.Default.LoadFromAssemblyPath(vendor);

        Type?[] types;
        try { types = sts2.GetTypes(); } catch (ReflectionTypeLoadException ex) { types = ex.Types; }

        var md = new StringBuilder();
        md.AppendLine("# Monster + Power type discovery");
        md.AppendLine();

        // ── 1. Every type with "Hopper" in its name ──
        md.AppendLine("## Types matching *Hopper* / *Thiev* / *Escape*");
        md.AppendLine();
        foreach (var t in types.OrderBy(x => x?.FullName ?? "", StringComparer.Ordinal))
        {
            if (t is null) continue;
            string? fn = null;
            try { fn = t.FullName; } catch { continue; }
            if (fn is null) continue;
            if (fn.Contains("Hopper", StringComparison.OrdinalIgnoreCase)
                || fn.Contains("Thiev", StringComparison.OrdinalIgnoreCase)
                || fn.Contains("Escape", StringComparison.OrdinalIgnoreCase))
            {
                md.AppendLine($"  - {fn}");
            }
        }
        md.AppendLine();

        // ── 2. Every Monster type in the .Monsters namespace ──
        md.AppendLine("## Monster types (MegaCrit.Sts2.Core.Models.Monsters.*)");
        md.AppendLine();
        var monsters = new List<Type>();
        foreach (var t in types)
        {
            if (t is null) continue;
            string? ns = null;
            try { ns = t.Namespace; } catch { continue; }
            if (ns == "MegaCrit.Sts2.Core.Models.Monsters")
                monsters.Add(t);
        }
        monsters.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
        foreach (var t in monsters)
        {
            md.AppendLine($"  - {t.Name}");
        }
        md.AppendLine();

        // ── 3. Methods on every monster whose name matches a target list ──
        var monsterTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ThievingHopper", "BowlbugRock", "EyeWithTeeth", "Fogmog", "SlitheringStrangler" };
        foreach (var t in monsters.Where(m => monsterTargets.Contains(m.Name)
                                              || m.Name.Contains("Hopper", StringComparison.OrdinalIgnoreCase)
                                              || m.Name.Contains("Bowlbug", StringComparison.OrdinalIgnoreCase)))
        {
            md.AppendLine($"## {t.FullName} methods");
            md.AppendLine();
            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName))
            {
                var ps = string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name));
                md.AppendLine($"  - {m.ReturnType.Name} {m.Name}({ps})");
            }
            md.AppendLine();
        }

        // ── 4. ESCAPE_ARTIST_POWER lookup ──
        md.AppendLine("## Power types matching *EscapeArtist* / *Slippery*");
        md.AppendLine();
        foreach (var t in types.OrderBy(x => x?.FullName ?? "", StringComparer.Ordinal))
        {
            if (t is null) continue;
            string? fn = null;
            try { fn = t.FullName; } catch { continue; }
            if (fn is null) continue;
            if ((fn.Contains("EscapeArtist", StringComparison.OrdinalIgnoreCase)
                 || fn.Contains("Slippery", StringComparison.OrdinalIgnoreCase)
                 || fn.Contains("Imbalanced", StringComparison.OrdinalIgnoreCase)
                 || fn.Contains("Illusion", StringComparison.OrdinalIgnoreCase)
                 || fn.Contains("Minion", StringComparison.OrdinalIgnoreCase)
                 || fn.Contains("Constrict", StringComparison.OrdinalIgnoreCase))
                && fn.Contains("Power", StringComparison.OrdinalIgnoreCase))
            {
                md.AppendLine($"### {fn}");
                md.AppendLine();
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Where(m => !m.IsSpecialName))
                {
                    var ps = string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name));
                    md.AppendLine($"  - {m.ReturnType.Name} {m.Name}({ps})");
                }
                md.AppendLine();
            }
        }

        File.WriteAllText("/tmp/monster-hang-discovery.md", md.ToString());
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
