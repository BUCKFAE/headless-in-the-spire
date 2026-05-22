using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Sts2Headless.Runtime.Patches;

namespace Sts2Headless.Runtime.Hooks;

// "Does this monster patch silently strip gameplay logic?" auditor.
//
// Doormaker's failure mode: patching every move body with a "skip the
// body, return Task.CompletedTask" prefix didn't just neutralise the UI
// helpers — it also threw away the move body's CreatureCmd.SetMaxAndCurrentHp,
// PowerCmd.Apply/Remove, DamageCmd.Attack calls. The boss never reached
// its real MaxHp, never gained HungerPower, never attacked. Combat
// "stalled" at sentinel HP. Symptom was loud only because the engine
// uses 999999999 as the pre-init MaxHp; a normal-HP boss with the same
// patch would silently fight without ever damaging the player.
//
// This auditor walks every (TypeFqn, MethodName) pair on the
// HangPatches monster-patch registry, reads each method's IL (following
// [AsyncStateMachine] to MoveNext when present, like
// ProbeMethodBodyCommand does), and flags any call/callvirt site whose
// target FQN matches a known gameplay mutator. The set of flagged
// methods is the candidate list for the Doormaker-shape fix: patch the
// leaf UI helpers, unpatch the move body.
//
// Pairs with MonsterPatchAuditTests, which freezes the current flagged
// set so future patch additions/removals surface as test failures.
public static class MonsterPatchAuditor
{
    public sealed record AuditEntry(
        string TypeFqn,
        string MethodName,
        bool MethodFound,
        IReadOnlyList<string> GameplayCalls);

    // Method FQNs (Owner.MethodName) whose presence in a patched body
    // means we're stripping real gameplay logic, not just UI. Conservative
    // by design: every entry was sighted in the Doormaker investigation
    // as a "must run for combat to make progress" command. Grow this set
    // when a new shape surfaces (Block? Heal? CardCmd.*?) — the auditor
    // happily detects the new shape, and the test will fail until the
    // expected list is updated.
    //
    // Match is plain string equality on "DeclaringType.FullName.MethodName".
    // Generic method instantiations resolve via PatchProcessor's IL reader
    // which gives us the underlying method definition's name (e.g. Apply
    // for PowerCmd.Apply<T>), so a single string here covers every closed
    // form.
    public static readonly IReadOnlySet<string> GameplayMutators = new HashSet<string>(StringComparer.Ordinal)
    {
        "MegaCrit.Sts2.Core.Commands.CreatureCmd.SetMaxAndCurrentHp",
        "MegaCrit.Sts2.Core.Commands.CreatureCmd.Damage",
        "MegaCrit.Sts2.Core.Commands.CreatureCmd.Kill",
        "MegaCrit.Sts2.Core.Commands.CreatureCmd.Heal",
        "MegaCrit.Sts2.Core.Commands.DamageCmd.Attack",
        "MegaCrit.Sts2.Core.Commands.PowerCmd.Apply",
        "MegaCrit.Sts2.Core.Commands.PowerCmd.Remove",
        "MegaCrit.Sts2.Core.Commands.BlockCmd.Apply",
        "MegaCrit.Sts2.Core.Commands.HealCmd.Apply",
    };

    public static IReadOnlyList<AuditEntry> Audit(Assembly sts2)
    {
        var entries = HangPatches.EnumerateMonsterPatchEntries();
        var results = new List<AuditEntry>(entries.Count * 6);
        foreach (var entry in entries)
        {
            var type = sts2.GetType(entry.TypeFqn);
            if (type is null)
            {
                results.Add(new AuditEntry(entry.TypeFqn, "<type-missing>", MethodFound: false, GameplayCalls: []));
                continue;
            }
            foreach (var name in entry.MethodNames.OrderBy(n => n, StringComparer.Ordinal))
            {
                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Where(m => m.Name == name && !m.IsSpecialName)
                    .ToArray();
                if (methods.Length == 0)
                {
                    results.Add(new AuditEntry(entry.TypeFqn, name, MethodFound: false, GameplayCalls: []));
                    continue;
                }
                foreach (var m in methods)
                {
                    var calls = CollectGameplayCalls(m);
                    results.Add(new AuditEntry(entry.TypeFqn, name, MethodFound: true, GameplayCalls: calls));
                }
            }
        }
        return results;
    }

    private static IReadOnlyList<string> CollectGameplayCalls(MethodInfo method)
    {
        var body = FollowToBody(method);
        if (body is null) return [];
        List<CodeInstruction> instructions;
        try { instructions = PatchProcessor.GetCurrentInstructions(body); }
        catch { return []; }

        var hits = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var ins in instructions)
        {
            if (ins.opcode != OpCodes.Call && ins.opcode != OpCodes.Callvirt) continue;
            if (ins.operand is not MethodBase mb) continue;
            var owner = mb.DeclaringType?.FullName;
            if (owner is null) continue;
            var key = $"{owner}.{mb.Name}";
            if (GameplayMutators.Contains(key)) hits.Add(key);
        }
        return hits.ToList();
    }

    // Async methods compile to a state-machine launcher whose body is just
    // Builder.Start; the actual await/gameplay sequence lives in the
    // generated `<Name>d__NN.MoveNext`. PatchProcessor.GetCurrentInstructions
    // on the launcher returns IL with no real call sites, so we follow
    // [AsyncStateMachine] to MoveNext. Open-generic state machines (which
    // we can't reflect IL through without closing) return null — those are
    // already handled separately by HangPatches.FindClosedGenericCallers.
    private static MethodBase? FollowToBody(MethodInfo method)
    {
        var asyncAttr = method.GetCustomAttributesData()
            .FirstOrDefault(a => a.AttributeType.Name == "AsyncStateMachineAttribute");
        if (asyncAttr is null) return method;
        if (asyncAttr.ConstructorArguments.Count != 1) return method;
        if (asyncAttr.ConstructorArguments[0].Value is not Type smType) return method;
        if (smType.IsGenericTypeDefinition) return null;
        return smType.GetMethod("MoveNext", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
    }
}
