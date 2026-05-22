using System.Reflection;
using System.Runtime.Loader;

namespace Sts2Headless.Commands;

// One-shot diagnostic: dump the public surface of the merchant types so the
// binding code in Sts2Bindings.cs can be authored against real members
// rather than speculative names. Mirrors the existing probe-* family — runs
// against vendor/sts2.dll alone, no run start, no Harmony patches.
//
// Lives in a temporary command because the merchant slice was the first
// room to use a non-RunManager synchronizer (OneOffSynchronizer.DoMerchantCard-
// Removal), so the existing list-members tooling (which enumerates external-
// type refs) didn't cover it.
internal static class ProbeMerchantCommand
{
    public static int Run(string vendorDir)
    {
        var sts2Path = Path.Combine(vendorDir, "sts2.dll");
        if (!File.Exists(sts2Path))
        {
            Console.Error.WriteLine("vendor/sts2.dll missing — run `just setup`.");
            return 1;
        }

        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(sts2Path);
        Type?[] types;
        try { types = assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { types = ex.Types; }

        var targets = new[]
        {
            "MegaCrit.Sts2.Core.Rooms.MerchantRoom",
            "MegaCrit.Sts2.Core.Entities.Merchant.MerchantInventory",
            "MegaCrit.Sts2.Core.Entities.Merchant.MerchantEntry",
            "MegaCrit.Sts2.Core.Entities.Merchant.MerchantCardEntry",
            "MegaCrit.Sts2.Core.Entities.Merchant.MerchantRelicEntry",
            "MegaCrit.Sts2.Core.Entities.Merchant.MerchantPotionEntry",
            "MegaCrit.Sts2.Core.Entities.Merchant.MerchantCardRemovalEntry",
        };

        var creation = types.FirstOrDefault(t => t is not null && t.Name == "CardCreationResult");
        if (creation is not null)
        {
            Console.WriteLine($"=== {creation.FullName} (CardCreationResult) ===");
            DumpType(creation);
            Console.WriteLine();
        }

        foreach (var modelName in new[] { "CardModel", "RelicModel", "PotionModel" })
        {
            var m = types.FirstOrDefault(t => t is not null && t.Name == modelName);
            if (m is not null)
            {
                Console.WriteLine($"=== {m.FullName} (Id property only) ===");
                var idProp = m.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
                if (idProp is not null) Console.WriteLine($"  prop  {Pretty(idProp.PropertyType)} Id");
                Console.WriteLine();
            }
        }

        // Look up the inventory type by name even if FQN doesn't match
        // (sub-namespace shuffling).
        var inv = types.FirstOrDefault(t => t is not null && t.Name == "MerchantInventory");
        if (inv is not null)
        {
            Console.WriteLine($"=== {inv.FullName} (found by Name) ===");
            DumpType(inv);
            Console.WriteLine();
        }

        foreach (var fqn in targets)
        {
            var t = types.FirstOrDefault(x => x is not null && x.FullName == fqn);
            Console.WriteLine($"=== {fqn} ===");
            if (t is null) { Console.WriteLine("  (not found)"); continue; }
            Console.WriteLine($"  base: {t.BaseType?.FullName}");
            DumpType(t);
            Console.WriteLine();
        }

        // RunManager — find any properties matching "*Merchant*" so we know
        // whether there's a Merchant synchronizer / state slot we missed.
        var runManager = types.FirstOrDefault(x => x?.FullName == "MegaCrit.Sts2.Core.Game.RunManager");
        if (runManager is not null)
        {
            Console.WriteLine("=== RunManager Merchant-related properties ===");
            foreach (var p in runManager.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                         .Where(p => p.Name.Contains("Merchant", StringComparison.OrdinalIgnoreCase)
                                     || p.Name.Contains("OneOff", StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine($"  prop {p.PropertyType.Name} {p.Name}");
            }
            Console.WriteLine();
        }

        // OneOffSynchronizer — card-removal goes through this.
        var oneOff = types.FirstOrDefault(x => x?.FullName == "MegaCrit.Sts2.Core.Multiplayer.Game.OneOffSynchronizer");
        if (oneOff is not null)
        {
            Console.WriteLine("=== OneOffSynchronizer ===");
            DumpType(oneOff);
        }
        else
        {
            // Try harder — namespace might differ.
            var oneOffByName = types.FirstOrDefault(x => x is not null && x.Name == "OneOffSynchronizer");
            if (oneOffByName is not null)
            {
                Console.WriteLine($"=== OneOffSynchronizer ({oneOffByName.FullName}) ===");
                DumpType(oneOffByName);
            }
        }

        return 0;
    }

    private static void DumpType(Type t)
    {
        foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(f => f.Name))
            Console.WriteLine($"  field {Pretty(f.FieldType)} {f.Name}");
        foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(p => p.Name))
            Console.WriteLine($"  prop  {Pretty(p.PropertyType)} {p.Name}");
        foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                     .Where(m => !m.IsSpecialName && m.DeclaringType == t)
                     .OrderBy(m => m.Name))
        {
            var ps = string.Join(", ", m.GetParameters().Select(p => $"{Pretty(p.ParameterType)} {p.Name}"));
            Console.WriteLine($"  meth  {Pretty(m.ReturnType)} {m.Name}({ps})");
        }
    }

    private static string Pretty(Type t)
    {
        if (!t.IsGenericType) return t.Name;
        var args = string.Join(",", t.GenericTypeArguments.Select(Pretty));
        var raw = t.Name;
        var tick = raw.IndexOf('`');
        if (tick > 0) raw = raw[..tick];
        return $"{raw}<{args}>";
    }
}
