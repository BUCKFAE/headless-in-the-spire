using Sts2Headless.Runtime;

namespace Sts2Headless.Commands;

// Phase-2 diagnostic: --probe-init + the game-state bootstrap chain.
//
// Walks the same sequence as sts2-cli's EnsureModelDbInitialized():
// TestMode → PlatformUtil warm → SaveManager → ModelDb.Inject loop →
// ModelIdSerializationCache, then attempts a smoke Player.CreateForNewRun.
// Failures are non-fatal at the command level — the whole point is to
// surface which step is the first to misbehave so the next iteration can
// target it.
internal static class ProbeBootstrapCommand
{
    public static int Run(string vendorDir)
    {
        Console.WriteLine("probe-bootstrap:");

        var preamble = RuntimeBootstrap.Run(vendorDir);
        if (preamble.SetupError is not null)
        {
            Console.Error.WriteLine($"  {preamble.SetupError}");
            return 1;
        }

        var sts2Name = preamble.Sts2!.GetName();
        Console.WriteLine($"  load sts2:                          ok ({sts2Name.Name} {sts2Name.Version})");
        Console.WriteLine($"  install InlineSynchronizationCtx:   {(preamble.SyncContextInstalled ? "ok" : "MISS")}");
        Console.WriteLine($"  enable TestMode.IsOn:               {(preamble.TestModeEnabled ? "ok" : "MISS")}");

        Console.WriteLine("  harmony patches:");
        var patchesOk = true;
        foreach (var o in preamble.Patches)
        {
            var status = o.Patched ? "ok" : "MISS";
            if (!o.Patched) patchesOk = false;
            var detail = o.Detail is null ? "" : $"  ({o.Detail})";
            Console.WriteLine($"    [{status,-4}] {o.Target}{detail}");
        }
        Console.WriteLine("  loc patches:");
        foreach (var o in preamble.LocPatches)
        {
            var status = o.Patched ? "ok" : "MISS";
            if (!o.Patched) patchesOk = false;
            var detail = o.Detail is null ? "" : $"  ({o.Detail})";
            Console.WriteLine($"    [{status,-4}] {o.Target}{detail}");
        }

        Console.WriteLine("  bootstrap:");
        var steps = BootstrapSequence.Apply(preamble.Sts2!);
        var stepsOk = true;
        foreach (var s in steps)
        {
            var status = s.Ok ? "ok" : "FAIL";
            if (!s.Ok) stepsOk = false;
            var detail = s.Detail is null ? "" : $"  ({s.Detail})";
            Console.WriteLine($"    [{status,-4}] {s.Label}{detail}");
        }

        Console.WriteLine();
        if (patchesOk && stepsOk)
        {
            Console.WriteLine("✅ bootstrap chain green.");
            return 0;
        }
        Console.WriteLine("⚠ one or more bootstrap steps failed — see above.");
        return 2;
    }
}
