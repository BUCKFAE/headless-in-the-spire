using System.Reflection;
using System.Runtime.Loader;
using Xunit;

namespace Sts2Headless.UnitTests;

// Diagnostic: enumerate sts2's CardModel system to find the
// "add a card to a deck" path. Also lists known Ironclad cards by id so
// debug/add_card can validate inputs.
public class CardModelProbeTests
{
    [Fact]
    [Trait("Category", "Diagnostic")]
    public void DumpCardSystem()
    {
        var vendor = ResolveRepoPath("vendor/sts2.dll");
        if (!File.Exists(vendor)) return;
        var sts2 = AssemblyLoadContext.Default.LoadFromAssemblyPath(vendor);
        Type?[] types;
        try { types = sts2.GetTypes(); } catch (ReflectionTypeLoadException ex) { types = ex.Types; }

        var lines = new List<string>();
        // CardPile: how cards live in piles (deck/hand/draw/discard)
        var cardPile = sts2.GetType("MegaCrit.Sts2.Core.Entities.Cards.CardPile");
        if (cardPile is not null)
        {
            lines.Add($"=== {cardPile.FullName} ===");
            foreach (var m in cardPile.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(m => !m.IsSpecialName))
                lines.Add($"  meth  {m.ReturnType.Name} {m.Name}({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name))})");
            foreach (var p in cardPile.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                lines.Add($"  prop  {p.PropertyType.Name} {p.Name}");
            lines.Add("");
        }

        // Known CardCmd entry points
        foreach (var ns in new[] { "MegaCrit.Sts2.Core.Commands.CardCmd", "MegaCrit.Sts2.Core.GameActions.AddCardToDeckGameAction" })
        {
            var t = sts2.GetType(ns);
            if (t is null)
            {
                lines.Add($"(no type {ns})");
                continue;
            }
            lines.Add($"=== {t.FullName} ===");
            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(m => !m.IsSpecialName).Take(20))
                lines.Add($"  meth  {m.ReturnType.Name} {m.Name}({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name))})");
            lines.Add("");
        }

        // CardModel: stable id structure + a way to create instances
        var cardModel = types.FirstOrDefault(t => t?.FullName == "MegaCrit.Sts2.Core.Models.CardModel");
        if (cardModel is not null)
        {
            lines.Add($"=== {cardModel.FullName} ===");
            foreach (var m in cardModel.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(m => !m.IsSpecialName).Take(15))
                lines.Add($"  meth  {m.ReturnType.Name} {m.Name}({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name))})");
            foreach (var p in cardModel.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Take(15))
                lines.Add($"  prop  {p.PropertyType.Name} {p.Name}");
            lines.Add("");
        }

        // List a few Ironclad card type names so the test fixture has
        // valid IDs. FullName is safe (precomputed); Namespace touches a
        // declaring-type resolution chain that pulls Steamworks.NET.
        var ironcladCards = new List<string>();
        foreach (var t in types)
        {
            if (t is null) continue;
            string? full;
            try { full = t.FullName; } catch { continue; }
            if (full is null) continue;
            if (full.StartsWith("MegaCrit.Sts2.Core.Models.Cards.Ironclad", StringComparison.Ordinal))
                ironcladCards.Add(t.Name);
        }
        ironcladCards.Sort();
        lines.Add($"=== Ironclad card types ({ironcladCards.Count}) ===");
        foreach (var n in ironcladCards) lines.Add($"  {n}");

        File.WriteAllLines("/tmp/card-system.txt", lines);
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
