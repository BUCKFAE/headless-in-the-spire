using System.Reflection;
using HarmonyLib;
using Sts2Headless.Runtime.Loading;

namespace Sts2Headless.Commands;

// `just runner::probe::method-body <Type.FullName> <MethodName>` — dump a single
// method's full CIL using HarmonyLib's PatchProcessor, with operands
// resolved to readable shapes (method FQN+generic-args, field owner+name,
// strings, type names). Async state machines are auto-followed: if the
// method has an [AsyncStateMachine] attribute, the dump points at the
// state machine's MoveNext (where the actual body lives).
//
// Built for the Doormaker investigation: SwapPhasePower<T>'s body is
// what we currently skip via Harmony prefix, and we need to know what
// it ACTUALLY does (AddPower? RemovePower? SetHp? pure UI?) before
// deciding whether to transpile vs replace vs leave-alone. Reusable
// for the next "what does this engine method actually do" investigation.
internal static class ProbeMethodBodyCommand
{
    public static int Run(string vendorDir, string[] args)
    {
        var idx = Array.IndexOf(args, "--probe-method-body");
        var typeFqn = idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : "";
        var methodName = idx >= 0 && idx + 2 < args.Length ? args[idx + 2] : "";
        if (string.IsNullOrWhiteSpace(typeFqn) || string.IsNullOrWhiteSpace(methodName))
        {
            Console.Error.WriteLine("usage: --probe-method-body <Type.FullName> <MethodName>");
            return 1;
        }

        // Optional `--generic <Type.FullName>` to pin a single closed
        // instantiation of an open generic method. Without it we dump
        // every overload that matches the method name.
        var genIdx = Array.IndexOf(args, "--generic");
        var genericArgFqn = genIdx >= 0 && genIdx + 1 < args.Length ? args[genIdx + 1] : null;

        var preamble = RuntimeBootstrap.Run(vendorDir);
        if (preamble.SetupError is not null)
        {
            Console.Error.WriteLine($"  bootstrap setup failed: {preamble.SetupError}");
            return 1;
        }

        var sts2 = preamble.Sts2!;
        var declaringType = sts2.GetType(typeFqn);
        if (declaringType is null)
        {
            Console.Error.WriteLine($"  type not found: {typeFqn}");
            return 1;
        }

        var overloads = declaringType
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(m => m.Name == methodName)
            .ToList();
        if (overloads.Count == 0)
        {
            Console.Error.WriteLine($"  method not found: {typeFqn}.{methodName}");
            return 1;
        }

        foreach (var m in overloads)
        {
            DumpOne(m, sts2, genericArgFqn);
        }
        return 0;
    }

    private static void DumpOne(MethodInfo method, Assembly sts2, string? genericArgFqn)
    {
        // For an async method the visible body is just the state-machine
        // launcher (Builder.Start + GetStateMachine). The actual await /
        // gameplay sequence lives in the SM's MoveNext. Hop there
        // automatically so the dump is useful by default.
        var asyncAttr = method.GetCustomAttributesData()
            .FirstOrDefault(a => a.AttributeType.Name == "AsyncStateMachineAttribute");
        MethodBase target = method;
        string headerSuffix = "";
        if (asyncAttr is not null && asyncAttr.ConstructorArguments.Count == 1
            && asyncAttr.ConstructorArguments[0].Value is Type smType)
        {
            // If this is an open-generic state machine and the caller
            // pinned a generic arg, close it before resolving MoveNext.
            if (smType.IsGenericTypeDefinition && genericArgFqn is not null)
            {
                var ga = sts2.GetType(genericArgFqn);
                if (ga is null)
                {
                    Console.WriteLine($"  generic arg type not found: {genericArgFqn}");
                    return;
                }
                smType = smType.MakeGenericType(ga);
            }
            var moveNext = smType.GetMethod("MoveNext", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (moveNext is not null)
            {
                target = moveNext;
                headerSuffix = $"  [following async state machine → {smType.Name}.MoveNext]";
            }
        }
        else if (method.IsGenericMethodDefinition && genericArgFqn is not null)
        {
            var ga = sts2.GetType(genericArgFqn);
            if (ga is null) { Console.WriteLine($"  generic arg type not found: {genericArgFqn}"); return; }
            target = method.MakeGenericMethod(ga);
        }

        Console.WriteLine($"=== {method.DeclaringType!.FullName}.{method.Name} ==={headerSuffix}");
        List<CodeInstruction> instructions;
        try { instructions = PatchProcessor.GetCurrentInstructions(target); }
        catch (Exception ex)
        {
            Console.WriteLine($"  GetCurrentInstructions threw: {ex.GetType().Name}: {ex.Message}");
            return;
        }

        var lineNo = 0;
        foreach (var ins in instructions)
        {
            var operand = FormatOperand(ins);
            Console.WriteLine($"  {lineNo++,4}: {ins.opcode.Name,-13} {operand}");
        }
    }

    private static string FormatOperand(CodeInstruction ins)
    {
        switch (ins.operand)
        {
            case null: return "";
            case MethodBase mb:
                var owner = mb.DeclaringType?.FullName ?? "?";
                var generics = mb is MethodInfo mi && mi.IsGenericMethod
                    ? "<" + string.Join(",", mi.GetGenericArguments().Select(a => a.Name)) + ">"
                    : "";
                var classification = ClassifyCall(mb);
                return $"{owner}.{mb.Name}{generics}{classification}";
            case FieldInfo fi:
                return $"{fi.DeclaringType?.FullName ?? "?"}.{fi.Name}  [{fi.FieldType.Name}]";
            case Type t:
                return t.FullName ?? t.Name;
            case string s:
                return $"\"{s}\"";
            case sbyte b: return b.ToString();
            case byte b: return b.ToString();
            case short s: return s.ToString();
            case int i: return i.ToString();
            case long l: return l.ToString();
            case float f: return f.ToString();
            case double d: return d.ToString();
            default: return $"<{ins.operand.GetType().Name}> {ins.operand}";
        }
    }

    // Tag each call site as "gameplay" (state-mutating; we MUST keep
    // these), "ui" (animation/yield; we WANT to strip these), or
    // "neutral" (helpers / framework — keep unless they pull in UI).
    // Heuristic-based; the dump operator should still eyeball each.
    private static string ClassifyCall(MethodBase mb)
    {
        var owner = mb.DeclaringType?.FullName ?? "";
        var name = mb.Name;
        if (owner.StartsWith("Godot.", StringComparison.Ordinal)) return "   [UI: godot]";
        if (owner.Contains(".Cmds.Cmd", StringComparison.Ordinal) && name.StartsWith("Wait", StringComparison.Ordinal)) return "   [UI: cmd-wait]";
        if (name.StartsWith("WaitUntil", StringComparison.Ordinal) || name == "WaitForFrame" || name == "WaitOneFrame") return "   [UI: wait]";
        if (owner.EndsWith(".TaskHelper", StringComparison.Ordinal)) return "   [UI: taskhelper]";
        if (name.Contains("AddPower", StringComparison.Ordinal)
            || name.Contains("RemovePower", StringComparison.Ordinal)
            || name.Contains("SetHp", StringComparison.Ordinal)
            || name.Contains("ChangeHp", StringComparison.Ordinal)
            || name.Contains("Damage", StringComparison.Ordinal)
            || name.Contains("Heal", StringComparison.Ordinal)) return "   [GAMEPLAY]";
        return "";
    }
}
