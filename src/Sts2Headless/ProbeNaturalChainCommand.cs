using System.Text;
using Sts2Headless.Protocol.Methods;
using Sts2Headless.Runtime;

namespace Sts2Headless;

// Phase-1 cataloging probe: drive the engine's natural enemy-turn chain with
// Player.NetId = 1uL (the contract sts2-cli proves works end-to-end) and dump
// every MissingMethodException / NRE that surfaces. The output is a markdown
// checklist that drives Phase 2's stub additions.
//
// What "natural" means here:
//   - PlayerCmd.EndTurn called once.
//   - Pump the InlineSynchronizationContext + DrainActionExecutor in a loop.
//   - NO ForceSwitchToEnemySide fallback — every gap surfaces as an exception.
//   - NO try/catch around reward paths.
//
// Production wire dispatch keeps the safety nets. This probe is run on demand
// (`just probe-natural-chain`) and writes to documentation/research/.
internal static class ProbeNaturalChainCommand
{
    private const string OutputRelative = "documentation/research/natural-chain-gaps.md";

    public static int Run(string vendorDir, string repoRoot)
    {
        Console.WriteLine("probe-natural-chain:");

        var preamble = RuntimeBootstrap.Run(vendorDir);
        if (preamble.SetupError is not null)
        {
            Console.Error.WriteLine($"  bootstrap setup failed: {preamble.SetupError}");
            return 1;
        }

        foreach (var p in preamble.Patches)
            if (!p.Patched) Console.Error.WriteLine($"  WARN: patch '{p.Target}' did not apply ({p.Detail})");
        foreach (var s in BootstrapSequence.Apply(preamble.Sts2!))
            if (!s.Ok) Console.Error.WriteLine($"  WARN: bootstrap step '{s.Label}' did not succeed ({s.Detail})");

        Sts2Bindings bindings;
        try { bindings = Sts2Bindings.Bind(preamble.Sts2!, preamble.SyncContext); }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  bind failed: {Diagnostics.Describe(Diagnostics.Unwrap(ex))}");
            return 1;
        }

        // Use a fixed seed so the probe is reproducible run-over-run. The
        // catalog should change only when the engine version, our stubs, or
        // the natural chain itself changes — not from RNG noise.
        //
        // Pass playerNetIdOverride: 1uL so Player.NetId matches NetSingle-
        // playerGameService.NetId from the start. Aligning post-hoc (after
        // RunManager.SetUpTest has already populated ActionQueueSet keyed
        // by the seed-derived NetId) would leave ActionQueueSet stale — the
        // first probe iteration hit exactly that gap.
        var seed = 42uL;
        Console.WriteLine($"  starting run (seed={seed}, NetId=1)...");

        RunHandle handle;
        try { handle = bindings.StartIroncladRun(seed, playerNetIdOverride: 1uL); }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  StartIroncladRun threw: {Diagnostics.Describe(Diagnostics.Unwrap(ex))}");
            return 1;
        }

        // Navigate to the first reachable monster room.
        Console.WriteLine("  navigating to first combat...");
        var snap = bindings.ReadSnapshot(handle);
        if (snap.CurrentRoomType != RoomType.MapRoom)
        {
            Console.Error.WriteLine($"  expected MapRoom after StartIroncladRun, got {snap.CurrentRoomType}");
            return 1;
        }
        var monster = snap.AvailableMapNodes.FirstOrDefault(n => n.Type == MapNodeType.Monster && n.Row > 0);
        if (monster is null)
        {
            Console.Error.WriteLine("  no Monster node available on the first map row");
            return 1;
        }
        try { bindings.EnterMapCoord(handle, monster.Col, monster.Row); }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  EnterMapCoord threw: {Diagnostics.Describe(Diagnostics.Unwrap(ex))}");
            return 1;
        }
        snap = bindings.ReadSnapshot(handle);
        if (snap.CurrentRoomType != RoomType.CombatRoom)
        {
            Console.Error.WriteLine($"  expected CombatRoom after EnterMapCoord, got {snap.CurrentRoomType}");
            return 1;
        }
        Console.WriteLine($"  entered combat: hp={snap.CurrentHp}/{snap.MaxHp}, enemies={snap.CombatState?.Enemies.Count ?? 0}");

        Console.WriteLine("  driving natural EndTurn chain (no manual side-switch fallback)...");
        Sts2Bindings.CatalogResult catalog;
        try { catalog = bindings.EndTurnAndCatalog(handle); }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  EndTurnAndCatalog threw outside the catch path: {Diagnostics.Describe(Diagnostics.Unwrap(ex))}");
            return 1;
        }

        Console.WriteLine($"  → terminal: {catalog.TerminalState} after {catalog.Iterations} iterations");
        Console.WriteLine($"  → gaps: {catalog.Gaps.Count} caught, {catalog.UniqueGaps.Count} unique");
        foreach (var g in catalog.UniqueGaps)
            Console.WriteLine($"    [{g.Phase}] {ShortName(g.ExceptionType)}: {g.Message}");

        var outputPath = Path.Combine(repoRoot, OutputRelative);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, FormatCatalog(catalog, seed));
        Console.WriteLine($"  wrote {OutputRelative}");

        // Exit code: 0 if no gaps (Phase 2 complete!), otherwise 2 so callers
        // can distinguish "ran successfully but found gaps" from "command
        // failed entirely" (1).
        return catalog.UniqueGaps.Count == 0 ? 0 : 2;
    }

    // Walk captured stderr and extract each engine-logged exception block.
    //
    // sts2's logger writes "[godot:err] [ERROR] <type>: <message>" then dumps
    // the exception's StackTrace directly via Console.Error.Write — the stack
    // frames appear as bare "   at <frame>" lines without any prefix. After
    // that, GD.PrintErr's WriteMegaCritFrames adds prefixed continuation
    // lines for the surrounding call stack.
    //
    // Strategy: start a block on `[ERROR]`, include subsequent lines as long
    // as they look like part of the dump (`   at ...`, prefixed lines, empty
    // lines), and end the block on a clearly-unrelated line.
    private static List<string> ExtractEngineErrors(string stderr)
    {
        var blocks = new List<string>();
        if (string.IsNullOrWhiteSpace(stderr)) return blocks;

        var lines = stderr.Split('\n').Select(s => s.TrimEnd('\r')).ToArray();
        var current = new StringBuilder();
        var inBlock = false;
        var blankRunInBlock = 0;

        foreach (var line in lines)
        {
            var startsBlock = line.Contains("[godot:err] [ERROR]") || line.Contains("[godot:push-error]");
            var isStackFrame = line.StartsWith("   at ") || line.StartsWith("\tat ");
            var isPrefixed = line.StartsWith("[godot:err]") || line.StartsWith("[godot:push-error]");
            var isBlank = line.Length == 0;

            if (startsBlock)
            {
                if (inBlock && current.Length > 0)
                {
                    blocks.Add(current.ToString().TrimEnd());
                    current.Clear();
                }
                inBlock = true;
                blankRunInBlock = 0;
                current.AppendLine(line);
            }
            else if (inBlock)
            {
                if (isStackFrame || isPrefixed)
                {
                    current.AppendLine(line);
                    blankRunInBlock = 0;
                }
                else if (isBlank)
                {
                    // Allow up to one blank line inside a block (the engine
                    // logger sometimes inserts one between StackTrace and
                    // the WriteMegaCritFrames continuation). Two consecutive
                    // blanks ends the block.
                    blankRunInBlock++;
                    if (blankRunInBlock >= 2)
                    {
                        blocks.Add(current.ToString().TrimEnd());
                        current.Clear();
                        inBlock = false;
                    }
                    else
                    {
                        current.AppendLine(line);
                    }
                }
                else
                {
                    blocks.Add(current.ToString().TrimEnd());
                    current.Clear();
                    inBlock = false;
                }
            }
        }
        if (inBlock && current.Length > 0)
            blocks.Add(current.ToString().TrimEnd());

        return blocks
            .GroupBy(b => b.Split('\n')[0])
            .Select(g => g.First())
            .ToList();
    }

    private static string ShortName(string fullyQualified)
    {
        var idx = fullyQualified.LastIndexOf('.');
        return idx < 0 ? fullyQualified : fullyQualified[(idx + 1)..];
    }

    private static string FormatCatalog(Sts2Bindings.CatalogResult c, ulong seed)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# natural-chain gaps");
        sb.AppendLine();
        sb.AppendLine($"Generated by `just probe-natural-chain` (seed={seed}). Walks `PlayerCmd.EndTurn` with Player.NetId = LocalContext.NetId = 1uL and no manual side-switch fallback. Each gap below is a `MissingMethodException` / `NullReferenceException` / etc. that the natural chain raised — the Phase-2 punch list.");
        sb.AppendLine();
        sb.AppendLine("Re-run after every stub addition; the goal is **0 unique gaps** with TerminalState = `next-player-turn`.");
        sb.AppendLine();
        sb.AppendLine("## summary");
        sb.AppendLine();
        sb.AppendLine($"- TerminalState: `{c.TerminalState}`");
        sb.AppendLine($"- Converged: `{c.Converged}`");
        sb.AppendLine($"- Pump iterations: `{c.Iterations}`");
        sb.AppendLine($"- Total exceptions caught (synchronous): `{c.Gaps.Count}`");
        sb.AppendLine($"- Unique synchronous gaps: `{c.UniqueGaps.Count}`");
        sb.AppendLine();

        // Engine-side log entries: sts2 wraps a lot of fire-and-forget work in
        // TaskHelper.LogTaskExceptions, which catches and routes to Logger.Error
        // → GD.PrintErr. Those exceptions never bubble through our reflection
        // invoke, so the only way to see them is to tee stderr. The engine-
        // logged gaps are typically the *first* and most important ones to fix.
        var engineErrors = ExtractEngineErrors(c.CapturedStderr);
        if (engineErrors.Count > 0)
        {
            sb.AppendLine("## engine-logged gaps (caught & swallowed by sts2)");
            sb.AppendLine();
            sb.AppendLine("These are exceptions the engine catches inside `TaskHelper.LogTaskExceptions` (or similar) and writes to its logger. They never propagate through our reflection invoke, but they DO indicate the chain is broken — fix them in the same way as synchronous gaps.");
            sb.AppendLine();
            for (var k = 0; k < engineErrors.Count; k++)
            {
                sb.AppendLine($"### E{k + 1}.");
                sb.AppendLine();
                sb.AppendLine("```");
                sb.AppendLine(engineErrors[k]);
                sb.AppendLine("```");
                sb.AppendLine();
            }
        }

        if (c.UniqueGaps.Count == 0 && engineErrors.Count == 0)
        {
            sb.AppendLine("## ✓ no gaps — natural chain runs end-to-end");
            sb.AppendLine();
            sb.AppendLine("Phase 2 is complete for this scenario. Extend the probe to cover reward paths and other combat scenarios before declaring it done.");
            return sb.ToString();
        }

        if (c.UniqueGaps.Count == 0)
        {
            sb.AppendLine("## (no synchronous exceptions surfaced)");
            sb.AppendLine();
            sb.AppendLine("All gaps were swallowed by sts2's internal exception handlers (see above). Fix those first — synchronous exceptions will likely follow.");
            sb.AppendLine();
            return sb.ToString();
        }

        sb.AppendLine("## unique gaps");
        sb.AppendLine();
        var i = 1;
        foreach (var g in c.UniqueGaps)
        {
            sb.AppendLine($"### {i}. `{ShortName(g.ExceptionType)}` (caught during `{g.Phase}`)");
            sb.AppendLine();
            sb.AppendLine($"**Message:** `{g.Message}`");
            sb.AppendLine();
            sb.AppendLine("**Stack (top frames):**");
            sb.AppendLine();
            sb.AppendLine("```");
            foreach (var f in g.StackFrames) sb.AppendLine(f);
            sb.AppendLine("```");
            sb.AppendLine();
            i++;
        }

        sb.AppendLine("## raw timeline");
        sb.AppendLine();
        sb.AppendLine("Every exception caught, in order. Useful for spotting cascades (one gap triggering another).");
        sb.AppendLine();
        sb.AppendLine("| iter | phase | exception | message |");
        sb.AppendLine("|---:|---|---|---|");
        foreach (var g in c.Gaps)
        {
            var msg = g.Message.Replace("|", "\\|").Replace("\n", " ");
            if (msg.Length > 120) msg = msg[..117] + "...";
            sb.AppendLine($"| {g.Iteration} | {g.Phase} | `{ShortName(g.ExceptionType)}` | {msg} |");
        }

        return sb.ToString();
    }
}
