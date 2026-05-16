using System.Collections;
using System.Reflection;
using HarmonyLib;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Runtime;

// Coverage instrumentation core: Harmony-postfix every override of an
// AbstractModel listener-hook (After*/Before*/On*) declared on a model
// subtype. The postfix records (kind, id, hookName) into TriggerLog,
// which the host drains on each run/state.
//
// Why this shape — see RelicHookPatches.cs's historical comment, which
// this file generalises. The same pattern applies to every kind that
// derives from AbstractModel: relics, cards, monsters, potions, powers,
// (and afflictions/orbs/enchantments/etc. if we ever want them).
//
// Per-kind front doors (RelicHookPatches.Apply, CardHookPatches.Apply,
// …) are thin wrappers around ApplyForBase below — they just supply
// the model base type and the TriggerKind enum value. The instance map
// (_methodToTrigger) is shared across kinds: each MethodBase belongs to
// exactly one (relic|card|monster|potion|power) class, so the same dict
// disambiguates without collision.
//
// Patch budget on the pinned game version (approximate):
//   relics:    ~290 overrides
//   powers:    ~?   overrides (POWER_COUNT * avg ~3 each)
//   monsters:  ~?   overrides
//   cards:     ~?   overrides
//   potions:   ~?   overrides
// Measured via the bootstrap-step Detail line at startup; if the total
// adds more than a few seconds to host startup, scale back by passing
// `applyTo: ModelKindMask.PowerOnly` or similar (future extension).
public static class ModelHookPatcher
{
    public sealed record PatchOutcome(string Target, bool Patched, string? Detail);

    private const string HarmonyId = "headless-in-the-spire.model-hook-patches";

    // Single shared lookup: MethodBase → (TriggerKind, wireId). All kinds
    // share this map because (a) a MethodBase is per-Type so no two kinds
    // can collide on the same key, and (b) the postfix is a single
    // static method that dispatches via this map.
    private static readonly Dictionary<MethodBase, (TriggerKind Kind, string Id)> _methodToTrigger = new();

    // Resolved once per bootstrap, shared across all kinds: which
    // AbstractModel virtual names are eligible hooks (After*/Before*/On*).
    private static HashSet<string>? _cachedHookNames;

    // Resolved once per bootstrap: Type → canonical Id.Entry from
    // ModelDb._contentById. Shared because every model kind goes through
    // the same canonical registry.
    private static Dictionary<Type, string>? _cachedTypeToId;

    // Single Harmony instance, lazily initialised on the first
    // ApplyForBase call. Sharing the id keeps cleanup uniform if a
    // future need to UnpatchAll arises.
    private static Harmony? _harmony;

    public static PatchOutcome ApplyForBase(Assembly sts2, string baseTypeFullName, TriggerKind kind)
    {
        var label = $"ModelHookPatcher.{kind}";
        var abstractModel = sts2.GetType("MegaCrit.Sts2.Core.Models.AbstractModel");
        if (abstractModel is null)
            return new PatchOutcome(label, false, "AbstractModel type not found");
        var baseModel = sts2.GetType(baseTypeFullName);
        if (baseModel is null)
            return new PatchOutcome(label, false, $"{baseTypeFullName} not found");

        var hookNames = ResolveHookNames(abstractModel);
        if (hookNames.Count == 0)
            return new PatchOutcome(label, false, "no AbstractModel hook methods discovered");

        var typeToId = ResolveCanonicalIds(sts2);

        Harmony harmony;
        try { harmony = _harmony ??= new Harmony(HarmonyId); }
        catch (Exception ex)
        {
            return new PatchOutcome(label, false, $"Harmony init failed: {ex.GetType().Name}: {ex.Message}");
        }

        var postfix = typeof(ModelHookPatcher).GetMethod(nameof(SharedPostfix), BindingFlags.NonPublic | BindingFlags.Static);
        if (postfix is null)
            return new PatchOutcome(label, false, "SharedPostfix not found (rename?)");

        Type[] allTypes;
        try { allTypes = sts2.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { allTypes = ex.Types.Where(t => t is not null).Cast<Type>().ToArray(); }

        var patched = 0;
        var skippedNoId = 0;
        var failed = 0;
        var firstFailures = new List<string>(capacity: 5);

        foreach (var t in allTypes)
        {
            if (t.IsAbstract) continue;
            if (!baseModel.IsAssignableFrom(t)) continue;
            typeToId.TryGetValue(t, out var id);

            MethodInfo[] declared;
            try { declared = t.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); }
            catch { continue; }
            foreach (var m in declared)
            {
                if (!m.IsVirtual) continue;
                if (m.GetBaseDefinition() == m) continue;
                if (!hookNames.Contains(m.Name)) continue;
                if (id is null)
                {
                    skippedNoId++;
                    continue;
                }

                try
                {
                    harmony.Patch(m, postfix: new HarmonyMethod(postfix));
                    _methodToTrigger[m] = (kind, id);
                    patched++;
                }
                catch (Exception ex)
                {
                    failed++;
                    if (firstFailures.Count < 3)
                        firstFailures.Add($"{t.Name}.{m.Name}: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        var detail = $"{patched} patched, {skippedNoId} skipped (no canonical id), {failed} failed";
        if (firstFailures.Count > 0) detail += $" [first: {string.Join(" | ", firstFailures)}]";
        return new PatchOutcome(label, patched > 0, detail);
    }

    private static HashSet<string> ResolveHookNames(Type abstractModel)
    {
        if (_cachedHookNames is not null) return _cachedHookNames;
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var m in abstractModel.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (m.IsSpecialName) continue;
            if (!m.IsVirtual) continue;
            if (m.Name.StartsWith("After", StringComparison.Ordinal)
                || m.Name.StartsWith("On", StringComparison.Ordinal)
                || m.Name.StartsWith("Before", StringComparison.Ordinal))
            {
                names.Add(m.Name);
            }
        }
        _cachedHookNames = names;
        return names;
    }

    // Type → canonical Id.Entry via ModelDb._contentById. Cached because
    // every kind's Apply call needs the same map; resolving once at first
    // use saves ~5-10ms per subsequent call.
    private static Dictionary<Type, string> ResolveCanonicalIds(Assembly sts2)
    {
        if (_cachedTypeToId is not null) return _cachedTypeToId;
        var typeToId = new Dictionary<Type, string>();
        var modelDbType = sts2.GetType("MegaCrit.Sts2.Core.Models.ModelDb");
        if (modelDbType is null) { _cachedTypeToId = typeToId; return typeToId; }
        var contentByIdField = modelDbType.GetField("_contentById", BindingFlags.NonPublic | BindingFlags.Static);
        if (contentByIdField?.GetValue(null) is not IDictionary contentById) { _cachedTypeToId = typeToId; return typeToId; }
        foreach (DictionaryEntry kv in contentById)
        {
            var instance = kv.Value;
            if (instance is null) continue;
            var idProp = instance.GetType().GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
            if (idProp?.GetValue(instance) is not object idObj) continue;
            var entryProp = idObj.GetType().GetProperty("Entry", BindingFlags.Public | BindingFlags.Instance);
            if (entryProp?.GetValue(idObj) is not string entry || string.IsNullOrEmpty(entry)) continue;
            typeToId.TryAdd(instance.GetType(), entry);
        }
        _cachedTypeToId = typeToId;
        return typeToId;
    }

    // Harmony postfix. Runs after every patched override invocation.
    // MUST NOT THROW — an unhandled exception would propagate into sts2's
    // call stack and break combat. Try/catch swallows any anomaly.
    private static void SharedPostfix(MethodBase __originalMethod)
    {
        try
        {
            if (_methodToTrigger.TryGetValue(__originalMethod, out var entry))
            {
                TriggerLog.Record(entry.Kind, entry.Id, __originalMethod.Name);
            }
        }
        catch
        {
            // Swallow — patch must not destabilise the engine.
        }
    }
}
