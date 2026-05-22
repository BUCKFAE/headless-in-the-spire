using Sts2Headless.Runtime;

namespace Sts2Headless.Commands;

// Phase-1 diagnostic: load sts2.dll, install the inline sync context, and
// attach the three Harmony hang-patches. Does NOT invoke any sts2 code paths
// beyond what Harmony itself needs to reflect on. See --probe-bootstrap for
// the next phase, which actually drives the game-state init chain.
internal static class ProbeInitCommand
{
    public static int Run(string vendorDir)
    {
        Console.WriteLine("probe-init:");

        var result = RuntimeBootstrap.Run(vendorDir);
        if (result.SetupError is not null)
        {
            Console.Error.WriteLine($"  {result.SetupError}");
            return 1;
        }

        var sts2Name = result.Sts2!.GetName();
        Console.WriteLine($"  load sts2:                          ok ({sts2Name.Name} {sts2Name.Version})");
        Console.WriteLine($"  install InlineSynchronizationCtx:   {(result.SyncContextInstalled ? "ok" : "MISS")}");
        Console.WriteLine($"  enable TestMode.IsOn:               {(result.TestModeEnabled ? "ok" : "MISS")}");

        Console.WriteLine("  harmony patches:");
        var allOk = true;
        foreach (var o in result.Patches)
        {
            var status = o.Patched ? "ok" : "MISS";
            if (!o.Patched) allOk = false;
            var detail = o.Detail is null ? "" : $"  ({o.Detail})";
            Console.WriteLine($"    [{status,-4}] {o.Target}{detail}");
        }

        var selectorStatus = result.CardSelector.Installed ? "ok" : "MISS";
        if (!result.CardSelector.Installed) allOk = false;
        var selectorDetail = result.CardSelector.Detail is null ? "" : $"  ({result.CardSelector.Detail})";
        Console.WriteLine($"  install ICardSelector:              {selectorStatus}{selectorDetail}");

        Console.WriteLine();
        Console.WriteLine(allOk ? "✅ runtime patches attached." : "⚠ one or more patches missed — see above.");
        return allOk ? 0 : 2;
    }
}
