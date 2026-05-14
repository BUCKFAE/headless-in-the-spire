using System.Reflection;
using HarmonyLib;

namespace Sts2Headless.Runtime;

// Localization patches. Without a real LocManager (the JSON-backed table
// loader that sts2-cli ports out of `localization_eng/`), sts2.dll's
// LocString / LocTable methods throw NullReferenceException as soon as any
// model asks "does this key exist". Event option generation hits this path
// during room entry (Neow's CurseOptions → EventModel.GetOptionTitle →
// LocString.Exists → boom), so without these patches no event ever
// populates its CurrentOptions list and the event-choice wire method has
// nothing to act on.
//
// Modelled after sts2-cli's InitLocManager → LocPatches block (see
// external-tools/sts2-cli/src/Sts2Headless/RunSimulator.cs around line 3287).
// We deliberately stop short of standing up a full LocManager: the wire
// surface returns raw text keys, and clients can translate on their side
// (this also keeps the host language-agnostic).
//
// AD-4: types are discovered by FQN through Sts2Reflection; if MegaCrit
// renames a namespace the patch reports a soft failure rather than crashing
// the host bootstrap.
public static class LocPatches
{
    public sealed record PatchOutcome(string Target, bool Patched, string? Detail);

    private const string HarmonyId = "headless-in-the-spire.loc-patches";

    public static IReadOnlyList<PatchOutcome> Apply(Assembly sts2)
    {
        var harmony = new Harmony(HarmonyId);
        var outcomes = new List<PatchOutcome>
        {
            PatchStaticReturnTrue(harmony, sts2,
                "MegaCrit.Sts2.Core.Localization.LocString", "Exists"),
            PatchStaticReturnTrue(harmony, sts2,
                "MegaCrit.Sts2.Core.Localization.LocString", "GetIfExists",
                returnAs: ReturnShape.Null),
            PatchInstanceReturnTrue(harmony, sts2,
                "MegaCrit.Sts2.Core.Localization.LocTable", "HasEntry"),
            PatchInstanceReturnTrue(harmony, sts2,
                "MegaCrit.Sts2.Core.Localization.LocTable", "IsLocalKey"),
            // The text getters throw NRE when LocManager isn't initialised —
            // they walk loc tables to format a key. Fall back to the entry
            // key itself so callers get *some* string, while the wire layer
            // still surfaces the canonical key (text_key) for translation.
            PatchLocStringTextReturnKey(harmony, sts2, "GetFormattedText"),
            PatchLocStringTextReturnKey(harmony, sts2, "GetRawText"),
            // EventOption.AddLocVars(EventModel) stuffs character- and event-
            // specific loc vars into an EventOption's LocString during ctor.
            // It walks player/character/event properties — several are
            // Texture2D / StringName-typed and rely on ResourceLoader.Load
            // ("res://..."), which is unwired in headless mode. Accessing
            // them throws NRE; the EventOption ctor never completes; the
            // event's _currentOptions list stays empty.
            //
            // Neow sidesteps this because its option text-keys don't
            // reference the null vars. In-run events (SunkenStatue and
            // presumably any other event whose options carry
            // $character.icon / $character.color / equivalent) hit it.
            //
            // Skip the body: our wire surfaces TextKey/IsLocked, both set
            // before AddLocVars runs, so the missing substitution doesn't
            // affect any callers.
            PatchSkipVoidInstance(harmony, sts2,
                "MegaCrit.Sts2.Core.Events.EventOption", "AddLocVars"),
            // EventSynchronizer.BeginEvent calls EventOption.ToString() for
            // debug logging; ToString() walks the same loc-var chain that
            // AddLocVars would have populated, and NREs when those are
            // missing (since we skip AddLocVars). Returning TextKey is both
            // safe and useful — it's what we'd want in a log anyway.
            PatchEventOptionToString(harmony, sts2),
        };

        return outcomes;
    }

    private static PatchOutcome PatchEventOptionToString(Harmony harmony, Assembly sts2)
    {
        const string label = "MegaCrit.Sts2.Core.Events.EventOption.ToString (return TextKey)";
        var lookup = Sts2Reflection.FindType(sts2, "MegaCrit.Sts2.Core.Events.EventOption");
        if (!lookup.Found) return new PatchOutcome(label, false, lookup.Source);

        var method = lookup.Type!.GetMethod("ToString", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
        if (method is null || method.DeclaringType != lookup.Type)
        {
            return new PatchOutcome(label, false, "EventOption does not override ToString()");
        }

        var prefix = typeof(LocPatches).GetMethod(nameof(EventOptionToStringPrefix), BindingFlags.Static | BindingFlags.NonPublic);
        harmony.Patch(method, prefix: new HarmonyMethod(prefix));
        return new PatchOutcome(label, true, null);
    }

    private static bool EventOptionToStringPrefix(object __instance, ref string __result)
    {
        var key = __instance?.GetType().GetProperty("TextKey", BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(__instance) as string;
        __result = key ?? "<EventOption>";
        return false;
    }

    // LocString instance getters that should return the LocEntryKey rather
    // than crash. The patched method's signature is `string M()` on
    // LocString; we read LocEntryKey off __instance via reflection so we
    // don't need a compile-time reference to LocString (AD-4).
    private static PatchOutcome PatchLocStringTextReturnKey(Harmony harmony, Assembly sts2, string methodName)
    {
        var label = $"MegaCrit.Sts2.Core.Localization.LocString.{methodName} (instance, return key)";
        var lookup = Sts2Reflection.FindType(sts2, "MegaCrit.Sts2.Core.Localization.LocString");
        if (!lookup.Found) return new PatchOutcome(label, false, lookup.Source);

        var methods = lookup.Type!.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.Name == methodName && m.ReturnType == typeof(string) && m.GetParameters().Length == 0)
            .ToArray();
        if (methods.Length == 0)
        {
            return new PatchOutcome(label, false, $"no zero-arg string {methodName}");
        }

        var prefix = typeof(LocPatches).GetMethod(nameof(LocStringReturnKeyPrefix), BindingFlags.Static | BindingFlags.NonPublic);
        foreach (var m in methods)
        {
            harmony.Patch(m, prefix: new HarmonyMethod(prefix));
        }
        return new PatchOutcome(label, true, $"{methods.Length} method(s)");
    }

    private enum ReturnShape { True, Null }

    // Patch a static method to skip its body and return either `true` (for
    // bool predicates like Exists) or `null` (for "give me the value if it
    // exists" probes — null reads as "not present" downstream and avoids
    // surfacing a fake translation through the wire).
    private static PatchOutcome PatchStaticReturnTrue(
        Harmony harmony, Assembly sts2, string typeFqn, string methodName,
        ReturnShape returnAs = ReturnShape.True)
    {
        var label = $"{typeFqn}.{methodName} (static)";
        var lookup = Sts2Reflection.FindType(sts2, typeFqn);
        if (!lookup.Found) return new PatchOutcome(label, false, lookup.Source);

        var methods = lookup.Type!.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Where(m => m.Name == methodName)
            .ToArray();
        if (methods.Length == 0)
        {
            return new PatchOutcome(label, false, $"no static {methodName} on {lookup.Type.FullName}");
        }

        var prefixName = returnAs == ReturnShape.True
            ? nameof(ReturnTruePrefix)
            : nameof(ReturnNullObjectPrefix);
        var prefix = typeof(LocPatches).GetMethod(prefixName, BindingFlags.Static | BindingFlags.NonPublic);
        foreach (var m in methods)
        {
            harmony.Patch(m, prefix: new HarmonyMethod(prefix));
        }
        return new PatchOutcome(label, true, $"{methods.Length} overload(s)");
    }

    private static PatchOutcome PatchInstanceReturnTrue(
        Harmony harmony, Assembly sts2, string typeFqn, string methodName)
    {
        var label = $"{typeFqn}.{methodName} (instance)";
        var lookup = Sts2Reflection.FindType(sts2, typeFqn);
        if (!lookup.Found) return new PatchOutcome(label, false, lookup.Source);

        var methods = lookup.Type!.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.Name == methodName && m.ReturnType == typeof(bool))
            .ToArray();
        if (methods.Length == 0)
        {
            return new PatchOutcome(label, false, $"no instance bool {methodName} on {lookup.Type.FullName}");
        }

        var prefix = typeof(LocPatches).GetMethod(nameof(ReturnTruePrefix), BindingFlags.Static | BindingFlags.NonPublic);
        foreach (var m in methods)
        {
            harmony.Patch(m, prefix: new HarmonyMethod(prefix));
        }
        return new PatchOutcome(label, true, $"{methods.Length} overload(s)");
    }

    // Patch a void-returning instance method to skip its body entirely. Used
    // for "fill in extra loc vars" calls where the side effects depend on
    // game state we don't have (e.g. CharacterModel asset properties).
    private static PatchOutcome PatchSkipVoidInstance(
        Harmony harmony, Assembly sts2, string typeFqn, string methodName)
    {
        var label = $"{typeFqn}.{methodName} (instance, skip)";
        var lookup = Sts2Reflection.FindType(sts2, typeFqn);
        if (!lookup.Found) return new PatchOutcome(label, false, lookup.Source);

        var methods = lookup.Type!.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.Name == methodName && m.ReturnType == typeof(void))
            .ToArray();
        if (methods.Length == 0)
        {
            return new PatchOutcome(label, false, $"no instance void {methodName} on {lookup.Type.FullName}");
        }

        var prefix = typeof(LocPatches).GetMethod(nameof(SkipBodyPrefix), BindingFlags.Static | BindingFlags.NonPublic);
        foreach (var m in methods)
        {
            harmony.Patch(m, prefix: new HarmonyMethod(prefix));
        }
        return new PatchOutcome(label, true, $"{methods.Length} overload(s)");
    }

    private static bool SkipBodyPrefix() => false;

    private static bool ReturnTruePrefix(ref bool __result)
    {
        __result = true;
        return false;
    }

    private static bool ReturnNullObjectPrefix(ref object? __result)
    {
        __result = null;
        return false;
    }

    private static bool LocStringReturnKeyPrefix(object __instance, ref string __result)
    {
        // LocEntryKey is a public string property on LocString. Reflective
        // read so this class doesn't take a compile-time dependency on the
        // game type (AD-4).
        var key = __instance?.GetType().GetProperty("LocEntryKey", BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(__instance) as string;
        __result = key ?? string.Empty;
        return false;
    }
}
