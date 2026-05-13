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
        IReadOnlyList<HangPatches.PatchOutcome> Patches,
        IReadOnlyList<LocPatches.PatchOutcome> LocPatches);

    public static Result Run(string vendorDir)
    {
        var sts2Path = Path.Combine(vendorDir, "sts2.dll");
        if (!File.Exists(sts2Path))
        {
            return new Result(null, "vendor/sts2.dll missing — run `just setup`.", false,
                Array.Empty<HangPatches.PatchOutcome>(),
                Array.Empty<LocPatches.PatchOutcome>());
        }

        var sts2 = AssemblyLoadContext.Default.LoadFromAssemblyPath(sts2Path);

        var syncCtx = new InlineSynchronizationContext();
        SynchronizationContext.SetSynchronizationContext(syncCtx);

        var patches = HangPatches.Apply(sts2);
        var locPatches = LocPatches.Apply(sts2);
        return new Result(sts2, null, true, patches, locPatches);
    }
}
