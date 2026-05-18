using System.Reflection;
using System.Runtime.Loader;
using Xunit;

namespace Sts2Headless.UnitTests;

// Diagnostic: list the Player class members, looking for a potion bag.
public class PlayerPotionsProbeTests
{
    [Fact]
    [Trait("Category", "Diagnostic")]
    public void DumpPlayerMembers()
    {
        var vendor = ResolveRepoPath("vendor/sts2.dll");
        if (!File.Exists(vendor)) return;
        var sts2 = AssemblyLoadContext.Default.LoadFromAssemblyPath(vendor);
        Type?[] types;
        try { types = sts2.GetTypes(); } catch (ReflectionTypeLoadException ex) { types = ex.Types; }
        var player = types.FirstOrDefault(t => t?.Name == "Player" && t.FullName != null && !t.FullName.Contains("Net", StringComparison.Ordinal) && !t.FullName.Contains("Serializable", StringComparison.Ordinal));
        if (player is null)
        {
            var candidates = types.Where(t => t?.Name == "Player").Select(t => t!.FullName!).ToList();
            File.WriteAllText("/tmp/player-members.txt", $"no Player; candidates: {string.Join(", ", candidates)}\n");
            return;
        }
        var lines = new List<string> { $"=== {player.FullName} ===" };
        foreach (var p in player.GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(p => p.Name))
            lines.Add($"  prop  {p.PropertyType.FullName ?? p.PropertyType.Name} {p.Name}");
        foreach (var f in player.GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(f => f.Name))
            lines.Add($"  field {f.FieldType.FullName ?? f.FieldType.Name} {f.Name}");

        var potionSlots = player.GetProperty("PotionSlots", BindingFlags.Public | BindingFlags.Instance);
        if (potionSlots is not null)
        {
            lines.Add("");
            lines.Add($"PotionSlots type: {potionSlots.PropertyType.FullName}");
            var elem = potionSlots.PropertyType.IsGenericType ? potionSlots.PropertyType.GetGenericArguments()[0] : null;
            if (elem is not null)
            {
                lines.Add($"PotionSlot element: {elem.FullName} (kind={(elem.IsValueType ? "struct" : "class")})");
                foreach (var p in elem.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    lines.Add($"  prop  {p.PropertyType.FullName ?? p.PropertyType.Name} {p.Name}");
                foreach (var f in elem.GetFields(BindingFlags.Public | BindingFlags.Instance))
                    lines.Add($"  field {f.FieldType.FullName ?? f.FieldType.Name} {f.Name}");
            }
        }

        // Also: probe Creature/Enemy targetability — UsePotionAction needs a Creature.
        var combat = sts2.GetType("MegaCrit.Sts2.Core.Combat.CombatState");
        if (combat is not null)
        {
            lines.Add("");
            lines.Add($"=== CombatState members ===");
            foreach (var p in combat.GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(p => p.Name))
                lines.Add($"  prop  {p.PropertyType.Name} {p.Name}");
        }
        File.WriteAllLines("/tmp/player-members.txt", lines);
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
