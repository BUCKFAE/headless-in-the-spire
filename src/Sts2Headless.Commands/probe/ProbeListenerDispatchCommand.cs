using System.Reflection;
using System.Text;
using Sts2Headless.Runtime.Loading;

namespace Sts2Headless.Commands;

// One-shot probe to find the engine's relic-listener dispatch path.
//
// Why: relic side-effects are template-method overrides on AbstractModel —
// e.g. LuckyFysh.AfterCardChangedPiles(...). Patching the base virtual
// doesn't catch derived overrides (Harmony patches a specific MethodInfo);
// patching every override would mean N patches × M hook types. The
// single-Harmony-patch story only works if there's one engine call site
// per hook kind that iterates all interested listeners.
//
// This probe dumps two things:
//
//   1. AbstractModel's listener-shaped public/protected methods —
//      anything starting with After/On/Before. These are the *hooks*.
//
//   2. Every method in sts2.dll that *invokes* each of those hooks (i.e.
//      calls AbstractModel.<HookName> on an instance). The callers are
//      the dispatch sites. If there's a small handful of them, we have
//      our patch targets.
//
// Output: documentation/research/modeldb/listener-dispatch.txt (gitignored).
internal static class ProbeListenerDispatchCommand
{
    public static int Run(string vendorDir, string repoRoot)
    {
        var preamble = RuntimeBootstrap.Run(vendorDir);
        if (preamble.SetupError is not null)
        {
            Console.Error.WriteLine($"probe-listener-dispatch: bootstrap setup failed — {preamble.SetupError}");
            return 1;
        }
        // No need for BootstrapSequence here — we're only reading
        // metadata, not executing models. Skips ~1s of setup.

        var sts2 = preamble.Sts2!;
        var abstractModel = sts2.GetType("MegaCrit.Sts2.Core.Models.AbstractModel")
            ?? throw new InvalidOperationException("AbstractModel type not found in sts2.dll");

        var sb = new StringBuilder();
        sb.AppendLine("# Relic-listener dispatch probe");
        sb.AppendLine();
        sb.AppendLine("## AbstractModel — listener-shaped methods (After/On/Before*)");
        sb.AppendLine();

        var hookMethods = abstractModel
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .Where(m =>
                m.Name.StartsWith("After", StringComparison.Ordinal)
                || m.Name.StartsWith("On", StringComparison.Ordinal)
                || m.Name.StartsWith("Before", StringComparison.Ordinal))
            .OrderBy(m => m.Name, StringComparer.Ordinal)
            .ToList();

        foreach (var m in hookMethods)
        {
            var virt = m.IsVirtual ? "virtual" : "       ";
            var args = string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
            sb.AppendLine($"  {virt}  {m.ReturnType.Name} {m.Name}({args})");
        }
        sb.AppendLine();

        // For each hook, find every method in sts2 that calls it (by name).
        // We can't easily detect "call AbstractModel.Foo" without IL parsing,
        // but methods that are likely dispatchers contain "for each listener"
        // shapes — namely, methods named ForEach*, Apply*, Fire*, Invoke*,
        // Dispatch*, Notify*, etc., or simply methods that contain the hook
        // name as a substring (so e.g. "OnCardObtainedEvent" or
        // "ForEachListener_AfterCardChangedPiles").
        //
        // Cheap heuristic: list every method in sts2.dll whose *name* matches
        // each hook method's name. The hits are very likely the call sites
        // (and the hook itself, which we strip out).
        sb.AppendLine("## Engine methods named like the hooks (candidate dispatch sites)");
        sb.AppendLine();

        // Pull every method declared in sts2.dll once — iterate per hook.
        var allTypes = sts2.GetTypes();
        foreach (var hook in hookMethods)
        {
            var matches = new List<string>();
            foreach (var t in allTypes)
            {
                MethodInfo[] methods;
                try { methods = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly); }
                catch { continue; }
                foreach (var m in methods)
                {
                    if (m.DeclaringType == abstractModel) continue;
                    // Match exact name OR substring (catches "FireAfterCardChangedPiles", "DispatchAfterCardChangedPiles").
                    if (m.Name == hook.Name
                        || m.Name.Contains(hook.Name, StringComparison.Ordinal))
                    {
                        matches.Add($"{t.FullName}.{m.Name}");
                    }
                }
            }
            sb.AppendLine($"### {hook.Name}  ({matches.Count} candidate method(s))");
            foreach (var line in matches.Take(40)) sb.AppendLine($"  {line}");
            if (matches.Count > 40) sb.AppendLine($"  ... ({matches.Count - 40} more)");
            sb.AppendLine();
        }

        // Look for likely event-bus-ish types: anything in MegaCrit.Sts2.Core
        // whose name contains "Event", "Listener", "Bus", "Notify",
        // "Dispatch", "Fire", "Hook", "Pipeline". These are the
        // archaeological dig sites if no obvious caller pattern emerges
        // above.
        sb.AppendLine("## Engine types named like an event bus / listener system");
        sb.AppendLine();
        var typeNeedles = new[] { "Listener", "EventBus", "Notify", "Dispatch", "Fire", "Pipeline", "Hook" };
        foreach (var t in allTypes)
        {
            if (t.FullName is not { } fn) continue;
            if (!fn.StartsWith("MegaCrit.Sts2.", StringComparison.Ordinal)) continue;
            if (!typeNeedles.Any(n => t.Name.Contains(n, StringComparison.Ordinal))) continue;
            sb.AppendLine($"  {fn}");
        }
        sb.AppendLine();

        // For the chosen Harmony strategy ("patch each relic-model override"),
        // count how many override methods we'd actually patch — sanity check
        // the scope before committing to the approach.
        var relicModelType = sts2.GetType("MegaCrit.Sts2.Core.Models.RelicModel");
        sb.AppendLine("## RelicModel override surface (Harmony patch budget)");
        sb.AppendLine();
        if (relicModelType is null)
        {
            sb.AppendLine("  RelicModel type not found in sts2.dll");
        }
        else
        {
            var hookNames = new HashSet<string>(hookMethods.Select(m => m.Name), StringComparer.Ordinal);
            var relicTypes = allTypes.Where(t => !t.IsAbstract && relicModelType.IsAssignableFrom(t)).ToList();
            var totalOverrides = 0;
            var distinctHooksOverridden = new HashSet<string>(StringComparer.Ordinal);
            foreach (var rt in relicTypes)
            {
                MethodInfo[] methods;
                try { methods = rt.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); }
                catch { continue; }
                foreach (var m in methods)
                {
                    if (!m.IsVirtual) continue;
                    if (m.GetBaseDefinition() == m) continue;  // not actually an override
                    if (!hookNames.Contains(m.Name)) continue;
                    totalOverrides++;
                    distinctHooksOverridden.Add(m.Name);
                }
            }
            sb.AppendLine($"  concrete relic types:    {relicTypes.Count}");
            sb.AppendLine($"  total hook overrides:    {totalOverrides}");
            sb.AppendLine($"  distinct hooks used:     {distinctHooksOverridden.Count}");
            sb.AppendLine();
            sb.AppendLine("  hooks used by ≥1 relic (= patch site candidates):");
            foreach (var h in distinctHooksOverridden.OrderBy(s => s, StringComparer.Ordinal))
                sb.AppendLine($"    - {h}");
            sb.AppendLine();
        }

        // PotionReward surface dump — the wire's reward-potion-id leak
        // shows up as "POTION.X (hash)" which doesn't match ModelId.ToString
        // (ModelId has Category + Entry only, no hash). So PotionReward.PotionId
        // must return something other than a plain ModelId.
        var potionRewardType = sts2.GetType("MegaCrit.Sts2.Core.Rewards.PotionReward");
        sb.AppendLine("## PotionReward surface");
        if (potionRewardType is null) sb.AppendLine("  PotionReward type not found");
        else
        {
            foreach (var p in potionRewardType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                sb.AppendLine($"  prop  {p.PropertyType.Name} {p.Name}");
            foreach (var f in potionRewardType.GetFields(BindingFlags.Public | BindingFlags.Instance))
                sb.AppendLine($"  field {f.FieldType.Name} {f.Name}");
        }
        sb.AppendLine();

        // ModelId surface dump — verify Entry's shape (property vs field).
        // Reward potion/relic ids surface as ModelId; ReadEntryId in
        // Sts2Bindings was leaking "POTION.X (hash)" because it didn't
        // find Entry. This dump pins which lookup actually works.
        var modelIdType = sts2.GetType("MegaCrit.Sts2.Core.Models.ModelId");
        sb.AppendLine("## ModelId surface");
        if (modelIdType is null)
        {
            sb.AppendLine("  ModelId type not found");
        }
        else
        {
            sb.AppendLine($"  IsValueType={modelIdType.IsValueType}");
            sb.AppendLine("  Properties (Public Instance):");
            foreach (var p in modelIdType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                sb.AppendLine($"    prop  {p.PropertyType.Name} {p.Name}");
            sb.AppendLine("  Fields (Public Instance):");
            foreach (var f in modelIdType.GetFields(BindingFlags.Public | BindingFlags.Instance))
                sb.AppendLine($"    field {f.FieldType.Name} {f.Name}");
            sb.AppendLine("  Fields (NonPublic Instance):");
            foreach (var f in modelIdType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
                sb.AppendLine($"    field {f.FieldType.Name} {f.Name}");
        }
        sb.AppendLine();

        var outDir = Path.Combine(repoRoot, "documentation", "research", "modeldb");
        Directory.CreateDirectory(outDir);
        var outPath = Path.Combine(outDir, "listener-dispatch.txt");
        File.WriteAllText(outPath, sb.ToString());

        Console.WriteLine($"wrote {Path.GetRelativePath(repoRoot, outPath)}");
        Console.WriteLine();
        Console.WriteLine("Top-level summary:");
        Console.WriteLine($"  hooks discovered:   {hookMethods.Count}");
        return 0;
    }
}
