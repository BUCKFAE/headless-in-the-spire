using System.Collections;
using System.Reflection;

namespace Sts2Headless.Runtime;

// Reflection-only port of sts2-cli's EnsureModelDbInitialized() bootstrap
// sequence (external-tools/sts2-cli/src/Sts2Headless/RunSimulator.cs, around
// line 2798). Runs AFTER the sync-context install and Harmony hang patches.
//
// AD-4: no `using MegaCrit.Sts2.…` directives. Each step resolves its target
// reflectively and reports a StepOutcome so the probe command can surface
// what worked and what didn't.
//
// What we deliberately skip in this iteration:
//   - LocManager initialization (sts2-cli builds it with GetUninitializedObject
//     + JSON loading from `localization_eng/`). We'll add it if the ModelDb
//     loop or Player creation actually needs it; until then it's premature.
public static class BootstrapSequence
{
    public sealed record StepOutcome(string Label, bool Ok, string? Detail);

    public static IReadOnlyList<StepOutcome> Apply(Assembly sts2)
    {
        return
        [
            SetTestMode(sts2),
            WarmPlatformUtil(sts2),
            InitSaveProfileId(sts2),
            InitSaveProgressData(sts2),
            InjectModelSubtypes(sts2),
            InitModelIdSerializationCache(sts2),
            CreateIroncladSmoke(sts2),
        ];
    }

    private static StepOutcome SetTestMode(Assembly sts2)
    {
        const string label = "TestMode.IsOn = true";
        var lookup = Sts2Reflection.FindType(sts2, "MegaCrit.Sts2.Core.TestSupport.TestMode");
        if (!lookup.Found) return new(label, false, lookup.Source);

        var prop = lookup.Type!.GetProperty("IsOn", BindingFlags.Public | BindingFlags.Static);
        if (prop is null || !prop.CanWrite) return new(label, false, "IsOn (public static set) not found");

        try
        {
            prop.SetValue(null, true);
            return new(label, true, lookup.Source);
        }
        catch (Exception ex) { return new(label, false, Describe(ex)); }
    }

    private static StepOutcome WarmPlatformUtil(Assembly sts2)
    {
        const string label = "PlatformUtil.PrimaryPlatform (warm)";
        var lookup = Sts2Reflection.FindType(sts2, "MegaCrit.Sts2.Core.Platform.PlatformUtil");
        if (!lookup.Found) return new(label, false, lookup.Source);

        var prop = lookup.Type!.GetProperty("PrimaryPlatform", BindingFlags.Public | BindingFlags.Static);
        if (prop is null) return new(label, false, "PrimaryPlatform (public static) not found");

        // sts2-cli catches and warns on this — Steam/platform services may
        // legitimately be unavailable in a headless context. Treat it as a
        // soft pass with a warn-tagged detail rather than a hard failure.
        try
        {
            var value = prop.GetValue(null);
            return new(label, true, value?.GetType().FullName ?? "<null>");
        }
        catch (Exception ex)
        {
            return new(label, true, "warn: " + Describe(Unwrap(ex)));
        }
    }

    private static StepOutcome InitSaveProfileId(Assembly sts2)
        => CallSaveManager(sts2, "InitProfileId(0)", "InitProfileId", new object?[] { 0 });

    private static StepOutcome InitSaveProgressData(Assembly sts2)
        => CallSaveManager(sts2, "InitProgressData()", "InitProgressData", Array.Empty<object?>());

    private static StepOutcome CallSaveManager(Assembly sts2, string label, string method, object?[] args)
    {
        var lookup = Sts2Reflection.FindType(sts2, "MegaCrit.Sts2.Core.Saves.SaveManager");
        if (!lookup.Found) return new(label, false, lookup.Source);

        var instanceProp = lookup.Type!.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        if (instanceProp is null) return new(label, false, "SaveManager.Instance not found");

        try
        {
            var instance = instanceProp.GetValue(null);
            if (instance is null) return new(label, false, "SaveManager.Instance returned null");

            var methodInfo = instance.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == method && m.GetParameters().Length == args.Length);
            if (methodInfo is null) return new(label, false, $"{method} (arity {args.Length}) not found");

            methodInfo.Invoke(instance, args);
            return new(label, true, null);
        }
        catch (Exception ex) { return new(label, false, Describe(Unwrap(ex))); }
    }

    private static StepOutcome InjectModelSubtypes(Assembly sts2)
    {
        const string label = "ModelDb.Inject loop over AbstractModelSubtypes.All";

        var subtypesLookup = Sts2Reflection.FindType(sts2, "MegaCrit.Sts2.Core.Models.AbstractModelSubtypes");
        if (!subtypesLookup.Found) return new(label, false, subtypesLookup.Source);
        var modelDbLookup = Sts2Reflection.FindType(sts2, "MegaCrit.Sts2.Core.Models.ModelDb");
        if (!modelDbLookup.Found) return new(label, false, modelDbLookup.Source);

        var allProp = subtypesLookup.Type!.GetProperty("All", BindingFlags.Public | BindingFlags.Static);
        if (allProp is null) return new(label, false, "AbstractModelSubtypes.All not found");

        // ModelDb.Inject has at least one static overload taking a Type. Pick
        // the one with exactly one parameter typed as Type.
        var inject = modelDbLookup.Type!.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "Inject"
                              && m.GetParameters().Length == 1
                              && m.GetParameters()[0].ParameterType == typeof(Type));
        if (inject is null) return new(label, false, "ModelDb.Inject(Type) not found");

        IList list;
        try
        {
            var raw = allProp.GetValue(null);
            if (raw is null) return new(label, false, "AbstractModelSubtypes.All returned null");
            if (raw is not IList ilist) return new(label, false, $"All returned non-IList ({raw.GetType().Name})");
            list = ilist;
        }
        catch (Exception ex) { return new(label, false, "reading All: " + Describe(Unwrap(ex))); }

        int registered = 0, failed = 0;
        var firstFailures = new List<string>(capacity: 5);
        foreach (var entry in list)
        {
            if (entry is not Type t) { failed++; continue; }
            try
            {
                inject.Invoke(null, new object?[] { t });
                registered++;
            }
            catch (Exception ex)
            {
                failed++;
                if (firstFailures.Count < 3)
                {
                    firstFailures.Add($"{t.Name}: {Describe(Unwrap(ex))}");
                }
            }
        }

        var detail = $"{registered} registered, {failed} failed of {list.Count}";
        if (firstFailures.Count > 0) detail += $" [first: {string.Join(" | ", firstFailures)}]";
        return new(label, failed == 0, detail);
    }

    private static StepOutcome InitModelIdSerializationCache(Assembly sts2)
    {
        const string label = "ModelIdSerializationCache.Init()";
        var lookup = Sts2Reflection.FindType(sts2, "MegaCrit.Sts2.Core.Models.ModelIdSerializationCache");
        if (!lookup.Found) return new(label, false, lookup.Source);

        var init = lookup.Type!.GetMethod("Init", BindingFlags.Public | BindingFlags.Static, Type.EmptyTypes);
        if (init is null) return new(label, false, "Init() not found");

        try
        {
            init.Invoke(null, null);
            return new(label, true, null);
        }
        catch (Exception ex) { return new(label, false, Describe(Unwrap(ex))); }
    }

    private static StepOutcome CreateIroncladSmoke(Assembly sts2)
    {
        const string label = "Player.CreateForNewRun<Ironclad>(UnlockState.all, 1uL)";

        var playerLookup = Sts2Reflection.FindType(sts2, "MegaCrit.Sts2.Core.Entities.Players.Player");
        if (!playerLookup.Found) return new(label, false, playerLookup.Source);
        var ironcladLookup = Sts2Reflection.FindType(sts2, "MegaCrit.Sts2.Core.Models.Characters.Ironclad");
        if (!ironcladLookup.Found) return new(label, false, ironcladLookup.Source);
        var unlockLookup = Sts2Reflection.FindType(sts2, "MegaCrit.Sts2.Core.Unlocks.UnlockState");
        if (!unlockLookup.Found) return new(label, false, unlockLookup.Source);

        // UnlockState.all is `static readonly` in sts2-cli's usage — try field
        // first, then property, since either is possible.
        object? unlockAll;
        var allField = unlockLookup.Type!.GetField("all", BindingFlags.Public | BindingFlags.Static);
        if (allField is not null)
        {
            unlockAll = allField.GetValue(null);
        }
        else
        {
            var allProp = unlockLookup.Type.GetProperty("all", BindingFlags.Public | BindingFlags.Static);
            if (allProp is null) return new(label, false, "UnlockState.all (field/property) not found");
            unlockAll = allProp.GetValue(null);
        }
        if (unlockAll is null) return new(label, false, "UnlockState.all returned null");

        // Find the generic CreateForNewRun definition. Signature in sts2-cli:
        //   static Player CreateForNewRun<T>(UnlockState, ulong)
        // We accept any 2-parameter static method with one generic arg.
        var createDef = playerLookup.Type!.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "CreateForNewRun"
                              && m.IsGenericMethodDefinition
                              && m.GetGenericArguments().Length == 1
                              && m.GetParameters().Length == 2);
        if (createDef is null) return new(label, false, "generic CreateForNewRun<T>(?, ?) not found");

        try
        {
            var generic = createDef.MakeGenericMethod(ironcladLookup.Type!);
            var player = generic.Invoke(null, new object?[] { unlockAll, 1uL });
            return new(label, true, player?.GetType().FullName ?? "<null returned>");
        }
        catch (Exception ex) { return new(label, false, Describe(Unwrap(ex))); }
    }

    private static Exception Unwrap(Exception ex) => Diagnostics.Unwrap(ex);
    private static string Describe(Exception ex) => Diagnostics.Describe(ex);
}
