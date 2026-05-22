using System.Collections;
using System.Reflection;
using Sts2Headless.Runtime;
using Sts2Headless.Runtime.Loading;
using Sts2Headless.Runtime.Bindings;

namespace Sts2Headless.Commands;

// Phase-3 diagnostic: after bootstrap, walk sts2-cli's StartRun chain
// reflectively and report which step is first to misbehave, then dump the
// shape of the resulting RunState (Map populated? CurrentRoom populated?
// what type is it?) so we know what to bind as the first real action.
//
// Mirrors RunSimulator.StartRun in external-tools/sts2-cli (around line
// 231): RunState.CreateForTest → RunManager.SetUpTest → flip
// StartedWithNeow → GenerateRooms → Launch → FinalizeStartingRelics →
// EnterAct(0). No production bindings are added — this command is
// throwaway scaffolding to inform Pass C scoping.
internal static class ProbeRunStateCommand
{
    public static int Run(string vendorDir)
    {
        Console.WriteLine("probe-run-state:");

        var preamble = RuntimeBootstrap.Run(vendorDir);
        if (preamble.SetupError is not null)
        {
            Console.Error.WriteLine($"  {preamble.SetupError}");
            return 1;
        }
        var sts2 = preamble.Sts2!;
        Console.WriteLine($"  sts2 loaded: {sts2.GetName().Name} {sts2.GetName().Version}");

        foreach (var p in preamble.Patches)
        {
            if (!p.Patched) Console.Error.WriteLine($"  WARN: hang patch '{p.Target}' did not apply ({p.Detail})");
        }

        foreach (var s in BootstrapSequence.Apply(sts2))
        {
            if (!s.Ok) Console.Error.WriteLine($"  WARN: bootstrap step '{s.Label}' did not succeed ({s.Detail})");
        }

        Console.WriteLine();
        Console.WriteLine("run start chain:");

        var steps = new List<(string Label, bool Ok, string? Detail)>();
        object? player = null;
        object? runState = null;
        object? runManagerInstance = null;

        steps.Add(Step("CreatePlayer<Ironclad>(seed=1)", () =>
        {
            var bindings = Sts2Bindings.Bind(sts2);
            player = bindings.CreateIroncladRun(1uL);
            return $"player = {player.GetType().FullName} (hp = {HpSnapshot(player)})";
        }));
        if (player is null) return ReportAndExit(steps);

        steps.Add(Step("RunState.CreateForTest", () =>
        {
            var runStateType = Require(sts2, "MegaCrit.Sts2.Core.Runs.RunState");
            var method = runStateType
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "CreateForTest")
                ?? throw new InvalidOperationException("RunState.CreateForTest (public static) not found");

            // Build a Player[] (or IEnumerable<Player>) sized for the
            // `players` parameter. CreateForTest's signature is discovered
            // at runtime; we map known parameter names by hand.
            var playerType = player.GetType();
            var playerArray = Array.CreateInstance(playerType, 1);
            playerArray.SetValue(player, 0);

            var ps = method.GetParameters();
            var args = new object?[ps.Length];
            for (var i = 0; i < ps.Length; i++)
            {
                args[i] = ps[i].Name switch
                {
                    "players" => playerArray,
                    "ascensionLevel" => 0,
                    "seed" => "probe-seed",
                    _ => ps[i].HasDefaultValue
                        ? ps[i].DefaultValue
                        : throw new InvalidOperationException($"unexpected required RunState.CreateForTest parameter '{ps[i].Name}': {ps[i].ParameterType.Name}"),
                };
            }
            runState = method.Invoke(null, args)
                ?? throw new InvalidOperationException("RunState.CreateForTest returned null");
            var paramSig = string.Join(", ", ps.Select(p => $"{p.Name}: {p.ParameterType.Name}{(p.HasDefaultValue ? "?" : "")}"));
            return $"{runState.GetType().FullName} via ({paramSig})";
        }));
        if (runState is null) return ReportAndExit(steps);

        steps.Add(Step("RunManager.Instance + NetSingleplayerGameService + SetUpTest", () =>
        {
            var runManagerType = Require(sts2, "MegaCrit.Sts2.Core.Runs.RunManager");
            var instanceProp = runManagerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
                ?? throw new InvalidOperationException("RunManager.Instance (public static) not found");
            runManagerInstance = instanceProp.GetValue(null)
                ?? throw new InvalidOperationException("RunManager.Instance returned null");

            var netType = Require(sts2, "MegaCrit.Sts2.Core.Multiplayer.NetSingleplayerGameService");
            var netService = Activator.CreateInstance(netType)
                ?? throw new InvalidOperationException("NetSingleplayerGameService default ctor returned null");

            var setUpOverloads = runManagerType
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name == "SetUpTest")
                .ToArray();
            if (setUpOverloads.Length == 0) throw new InvalidOperationException("RunManager.SetUpTest (public instance) not found");
            if (setUpOverloads.Length > 1)
            {
                var sigs = string.Join(" | ", setUpOverloads.Select(m => $"({string.Join(", ", m.GetParameters().Select(p => $"{p.Name}: {p.ParameterType.Name}{(p.HasDefaultValue ? "?" : "")}"))})"));
                throw new InvalidOperationException($"RunManager.SetUpTest has {setUpOverloads.Length} overloads: {sigs}");
            }
            var setUp = setUpOverloads[0];
            var setUpParams = setUp.GetParameters();
            var setUpArgs = new object?[setUpParams.Length];
            for (var i = 0; i < setUpParams.Length; i++)
            {
                var pname = setUpParams[i].Name;
                var ptype = setUpParams[i].ParameterType;
                if (pname == "runState" || (ptype == runState.GetType()))
                    setUpArgs[i] = runState;
                else if (pname == "netService" || pname == "netGameService" || ptype.Name.Contains("Net"))
                    setUpArgs[i] = netService;
                else if (setUpParams[i].HasDefaultValue)
                    setUpArgs[i] = setUpParams[i].DefaultValue;
                else
                    throw new InvalidOperationException($"unexpected required SetUpTest parameter '{pname}': {ptype.Name}. signature: ({string.Join(", ", setUpParams.Select(p => $"{p.Name}: {p.ParameterType.Name}{(p.HasDefaultValue ? "?" : "")}"))})");
            }
            setUp.Invoke(runManagerInstance, setUpArgs);

            // sts2-cli also assigns LocalContext.NetId = netService.NetId here.
            TrySetLocalContextNetId(sts2, netService);

            return $"instance={runManagerInstance.GetType().FullName}, net={netType.FullName}";
        }));

        // sts2-cli sets this to true so the run auto-enters the Neow event.
        // We leave it false during probing to isolate where HP gets zeroed:
        // with Neow on, EnterAct walks straight into NEventRoom.Create which
        // depends on more GodotStubs surface; flipping it off should land us
        // at the map (CurrentRoom = MapRoom) with HP intact.
        var startWithNeow = Environment.GetEnvironmentVariable("PROBE_NEOW") == "1";
        steps.Add(Step($"runState.ExtraFields.StartedWithNeow = {startWithNeow}", () =>
        {
            var extraFieldsProp = runState!.GetType().GetProperty("ExtraFields", BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException("RunState.ExtraFields not found");
            var extra = extraFieldsProp.GetValue(runState)
                ?? throw new InvalidOperationException("RunState.ExtraFields was null");

            var flag = extra.GetType().GetProperty("StartedWithNeow", BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException("ExtraFields.StartedWithNeow not found");
            flag.SetValue(extra, startWithNeow);
            return $"set on {extra.GetType().FullName}";
        }));

        steps.Add(Step("RunManager.Instance.GenerateRooms()", () =>
            $"{InvokeNoArg(runManagerInstance!, "GenerateRooms")} hp={HpSnapshot(player)}"));

        steps.Add(Step("RunManager.Instance.Launch()", () =>
            $"{InvokeNoArg(runManagerInstance!, "Launch")} hp={HpSnapshot(player)}"));

        steps.Add(Step("RunManager.Instance.FinalizeStartingRelics()", () =>
            $"{InvokeAsyncNoArg(runManagerInstance!, "FinalizeStartingRelics")} hp={HpSnapshot(player)}"));

        steps.Add(Step("RunManager.Instance.EnterAct(0, doTransition: false)", () =>
        {
            var overloads = runManagerInstance!.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name == "EnterAct")
                .ToArray();
            if (overloads.Length == 0) throw new InvalidOperationException("RunManager.EnterAct not found");
            if (overloads.Length > 1)
            {
                var sigs = string.Join(" | ", overloads.Select(m => $"({string.Join(", ", m.GetParameters().Select(p => p.Name))})"));
                throw new InvalidOperationException($"RunManager.EnterAct has {overloads.Length} overloads: {sigs}");
            }
            var method = overloads[0];
            var ps = method.GetParameters();
            var args = new object?[ps.Length];
            for (var i = 0; i < ps.Length; i++)
            {
                args[i] = ps[i].Name switch
                {
                    "act" or "actIndex" or "actNumber" or "currentActIndex" => 0,
                    "doTransition" => false,
                    _ => ps[i].HasDefaultValue
                        ? ps[i].DefaultValue
                        : throw new InvalidOperationException($"unexpected required EnterAct parameter '{ps[i].Name}': {ps[i].ParameterType.Name}. full signature: ({string.Join(", ", ps.Select(q => $"{q.Name}: {q.ParameterType.Name}{(q.HasDefaultValue ? "?" : "")}"))})"),
                };
            }
            var result = method.Invoke(runManagerInstance, args);
            AwaitIfTask(result);
            return $"({string.Join(", ", ps.Select(p => $"{p.Name}: {p.ParameterType.Name}{(p.HasDefaultValue ? "?" : "")}"))}) hp={HpSnapshot(player)}";
        }));

        PrintSteps(steps);

        Console.WriteLine();
        Console.WriteLine("post-boot state:");
        DumpState(runState, player);

        Console.WriteLine();
        Console.WriteLine("decision context:");
        DumpDecisionContext(runState, runManagerInstance, player);

        var anyFail = steps.Any(s => !s.Ok);
        return anyFail ? 2 : 0;
    }

    private static (string Label, bool Ok, string? Detail) Step(string label, Func<string?> body)
    {
        try
        {
            var detail = body();
            return (label, true, detail);
        }
        catch (Exception ex)
        {
            // Stack helps when the failure is an NPE deep in sts2 — without it
            // we can't tell which member is null and have to guess.
            return (label, false, Diagnostics.DescribeWithStack(Diagnostics.Unwrap(ex)));
        }
    }

    private static string? InvokeNoArg(object instance, string methodName)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes)
            ?? instance.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == methodName && m.GetParameters().Length == 0)
            ?? throw new InvalidOperationException($"{instance.GetType().Name}.{methodName}() (no-arg) not found");
        var result = method.Invoke(instance, null);
        AwaitIfTask(result);
        return result?.GetType() is { } rt && typeof(Task).IsAssignableFrom(rt) ? "→ Task (awaited)" : null;
    }

    private static string? InvokeAsyncNoArg(object instance, string methodName)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes)
            ?? instance.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == methodName && m.GetParameters().Length == 0)
            ?? throw new InvalidOperationException($"{instance.GetType().Name}.{methodName}() (no-arg) not found");
        var result = method.Invoke(instance, null);
        AwaitIfTask(result);
        return $"→ {method.ReturnType.Name} (awaited)";
    }

    // Sync-context inlines awaits, but we still need to surface exceptions:
    // a Task that faulted will hold the exception until something observes it.
    private static void AwaitIfTask(object? result)
    {
        if (result is Task t)
        {
            t.GetAwaiter().GetResult();
        }
    }

    private static void TrySetLocalContextNetId(Assembly sts2, object netService)
    {
        try
        {
            var ctxType = Sts2Reflection.FindType(sts2, "MegaCrit.Sts2.Core.Context.LocalContext").Type;
            if (ctxType is null) return;
            var netIdProp = netService.GetType().GetProperty("NetId", BindingFlags.Public | BindingFlags.Instance);
            var netId = netIdProp?.GetValue(netService);
            if (netId is null) return;
            var setter = ctxType.GetProperty("NetId", BindingFlags.Public | BindingFlags.Static);
            if (setter is { CanWrite: true }) setter.SetValue(null, netId);
            else ctxType.GetField("NetId", BindingFlags.Public | BindingFlags.Static)?.SetValue(null, netId);
        }
        catch
        {
            // Non-fatal — sts2-cli sets it but we don't yet know if downstream
            // steps actually depend on it. If they do, EnterAct will surface.
        }
    }

    private static Type Require(Assembly sts2, string fqn)
    {
        var lookup = Sts2Reflection.FindType(sts2, fqn);
        if (!lookup.Found) throw new InvalidOperationException($"type {fqn} not found ({lookup.Source})");
        return lookup.Type!;
    }

    private static void PrintSteps(List<(string Label, bool Ok, string? Detail)> steps)
    {
        foreach (var (label, ok, detail) in steps)
        {
            var status = ok ? "ok  " : "FAIL";
            var tail = detail is null ? "" : $"  ({detail})";
            Console.WriteLine($"  [{status}] {label}{tail}");
        }
    }

    private static int ReportAndExit(List<(string Label, bool Ok, string? Detail)> steps)
    {
        PrintSteps(steps);
        Console.WriteLine();
        Console.WriteLine("⚠ aborted before state dump — earlier step did not produce required object.");
        return 2;
    }

    // Dump the shape of the run after boot — what we'd want to read out via
    // run/state once we bind the real thing. Keep it shallow: top-level
    // RunState properties + Player.Creature/Gold + Map.StartingMapPoint.
    private static void DumpState(object? runState, object? player)
    {
        if (runState is null)
        {
            Console.WriteLine("  runState: <null>");
        }
        else
        {
            Console.WriteLine($"  runState: {runState.GetType().FullName}");
            foreach (var prop in runState.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                         .OrderBy(p => p.Name))
            {
                PrintMember(runState, prop, indent: 4);
            }
        }

        if (player is null)
        {
            Console.WriteLine("  player: <null>");
        }
        else
        {
            Console.WriteLine($"  player: {player.GetType().FullName}");
            foreach (var name in new[] { "Gold", "Creature", "Deck" })
            {
                var prop = player.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                if (prop is null) continue;
                PrintMember(player, prop, indent: 4);
            }
        }
    }

    private static void PrintMember(object owner, PropertyInfo prop, int indent)
    {
        string pad = new(' ', indent);
        object? value;
        try { value = prop.GetValue(owner); }
        catch (Exception ex)
        {
            Console.WriteLine($"{pad}{prop.Name}: <throws {Diagnostics.Describe(Diagnostics.Unwrap(ex))}>");
            return;
        }

        if (value is null)
        {
            Console.WriteLine($"{pad}{prop.Name}: <null> ({prop.PropertyType.Name})");
            return;
        }

        var desc = value switch
        {
            string s => $"\"{s}\"",
            bool or int or long or float or double or ulong => value.ToString()!,
            IEnumerable e when value is not string => $"<{value.GetType().Name}> count={Count(e)}",
            _ => $"<{value.GetType().FullName}>",
        };
        Console.WriteLine($"{pad}{prop.Name}: {desc}");
    }

    private static int Count(IEnumerable e)
    {
        var n = 0;
        foreach (var _ in e) n++;
        return n;
    }

    // Targeted second pass: the post-boot dump showed `IsGameOver: True` on
    // RunState, which is alarming. Disambiguate by reading RunManager's own
    // IsGameOver (sts2-cli treats it as authoritative), player HP, and the
    // CurrentRoom's identity — together these tell us whether we're at a
    // genuine decision point or if a stub-returned-null broke something.
    private static void DumpDecisionContext(object? runState, object? runManager, object? player)
    {
        TryPrint("  RunManager.IsGameOver", () => GetProp(runManager, "IsGameOver"));
        TryPrint("  RunState.IsGameOver", () => GetProp(runState, "IsGameOver"));
        TryPrint("  RunState.CurrentRoom.GetType()", () =>
        {
            var room = GetProp(runState, "CurrentRoom");
            return room is null ? "null" : room.GetType().FullName!;
        });
        TryPrint("  RunState.MapLocation", () =>
        {
            var ml = GetProp(runState, "MapLocation");
            if (ml is null) return "null";
            var parts = new List<string>();
            foreach (var p in ml.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (p.PropertyType.IsPrimitive || p.PropertyType == typeof(string))
                {
                    try { parts.Add($"{p.Name}={p.GetValue(ml)}"); }
                    catch { /* skip throwing props */ }
                }
            }
            return string.Join(", ", parts);
        });
        TryPrint("  Player.Creature.CurrentHp / MaxHp", () =>
        {
            var creature = GetProp(player, "Creature");
            if (creature is null) return "<no creature>";
            var cur = GetProp(creature, "CurrentHp");
            var max = GetProp(creature, "MaxHp");
            return $"{cur} / {max}";
        });
        TryPrint("  Player.Deck.Cards count", () =>
        {
            var deck = GetProp(player, "Deck");
            if (deck is null) return "<no deck>";
            var cards = GetProp(deck, "Cards");
            return cards is IEnumerable e ? Count(e).ToString() : cards?.ToString() ?? "null";
        });
    }

    private static string HpSnapshot(object? player)
    {
        if (player is null) return "<no player>";
        var creature = GetProp(player, "Creature");
        if (creature is null) return "<no creature>";
        return $"{GetProp(creature, "CurrentHp")}/{GetProp(creature, "MaxHp")}";
    }

    private static object? GetProp(object? owner, string name)
    {
        if (owner is null) return null;
        var p = owner.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        return p?.GetValue(owner);
    }

    private static void TryPrint(string label, Func<object?> body)
    {
        try
        {
            var v = body();
            Console.WriteLine($"{label}: {v ?? "<null>"}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{label}: <throws {Diagnostics.Describe(Diagnostics.Unwrap(ex))}>");
        }
    }
}
