using System.Reflection;
using System.Runtime.Loader;

namespace Sts2Headless.Runtime;

// The "make sts2.dll safe to call into" preamble: vendor lookup + load,
// inline SynchronizationContext install, and Harmony hang patches.
//
// Both --probe-init and --probe-bootstrap (and eventually the real stdio
// host) start with exactly this sequence. Centralising it keeps the
// invocation order in one place — if a future game version needs (say) a
// new patch or a different load order, only this file changes.
public static class RuntimeBootstrap
{
    public sealed record Result(
        Assembly? Sts2,
        string? SetupError,
        bool SyncContextInstalled,
        bool TestModeEnabled,
        IReadOnlyList<HangPatches.PatchOutcome> Patches,
        IReadOnlyList<LocPatches.PatchOutcome> LocPatches,
        InlineSynchronizationContext? SyncContext,
        CardSelectorInstaller.InstallOutcome CardSelector);

    public static Result Run(string vendorDir)
    {
        if (!File.Exists(Path.Combine(vendorDir, "sts2.dll")))
        {
            return new Result(null, "vendor/sts2.dll missing — run `just setup`.", false, false,
                Array.Empty<HangPatches.PatchOutcome>(),
                Array.Empty<LocPatches.PatchOutcome>(),
                SyncContext: null,
                CardSelector: new CardSelectorInstaller.InstallOutcome(false, "sts2.dll missing", null));
        }

        var sts2Path = Path.Combine(vendorDir, "sts2.dll");
        var sts2 = AssemblyLoadContext.Default.LoadFromAssemblyPath(sts2Path);

        var syncCtx = new InlineSynchronizationContext();
        SynchronizationContext.SetSynchronizationContext(syncCtx);

        // sts2.dll branches a lot of behavior on TestMode.IsOn: under it, the
        // engine skips animations, auto-resolves screens that would otherwise
        // wait on UI input, and (critically) drives combat-start synchronously
        // from CombatRoom.EnterInternal. Without this flag set, EnterMapCoord
        // lands the player in a CombatRoom whose CombatManager is set up but
        // never actually starts the player turn — hand stays empty, energy
        // stays at 0, IsInProgress stays false. Mirrors sts2-cli's
        // EnsureModelDbInitialized → `TestMode.IsOn = true`.
        var testModeOn = SetTestModeOn(sts2);

        var patches = HangPatches.Apply(sts2);
        var locPatches = LocPatches.Apply(sts2);
        // The selector is the supported sts2 hook for card-pick prompts; with
        // one installed, CardSelectCmd.From* factories route the choice
        // through us instead of trying to load a Godot scene. Without it,
        // Headbutt / Armaments / Burning Pact / event "pick a card" all
        // NRE inside their OnPlay because the factory result they await is
        // null. Failure here is non-fatal at bootstrap — the wire surface
        // still works for non-selecting cards — but it is loud on probe-init
        // so an operator sees the regression before they reach a Headbutt.
        var cardSelector = CardSelectorInstaller.Install(sts2);
        return new Result(sts2, null, true, testModeOn, patches, locPatches, syncCtx, cardSelector);
    }

    private static bool SetTestModeOn(Assembly sts2)
    {
        var lookup = Sts2Reflection.FindType(sts2, "MegaCrit.Sts2.Core.TestSupport.TestMode");
        if (!lookup.Found) return false;
        var setter = lookup.Type!.GetProperty("IsOn", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            ?.GetSetMethod();
        if (setter is null) return false;
        setter.Invoke(null, new object?[] { true });
        return true;
    }
}
