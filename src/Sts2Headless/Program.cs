using Sts2Headless;
using Sts2Headless.Replay;
using Sts2Headless.Runtime;

// Skeleton entry: validates that the toolchain wires together and that
// vendor/ is populated. Does not yet load sts2.dll — that comes once
// GodotStubs has enough surface to satisfy its references.

var repoRoot = Paths.LocateRepoRoot();
var vendorDir = Path.Combine(repoRoot, "vendor");
var gameVersionFile = Path.Combine(repoRoot, "GAME_VERSION");

VendorAssemblyResolver.Install(vendorDir);

if (args.Contains("--inspect-sts2"))
{
    return InspectCommand.Run(vendorDir);
}

if (args.Contains("--probe-init"))
{
    return ProbeInitCommand.Run(vendorDir);
}

if (args.Contains("--probe-bootstrap"))
{
    return ProbeBootstrapCommand.Run(vendorDir);
}

if (args.Contains("--probe-run-state"))
{
    return ProbeRunStateCommand.Run(vendorDir);
}

if (args.Contains("--probe-natural-chain"))
{
    return ProbeNaturalChainCommand.Run(vendorDir, repoRoot);
}

if (args.Contains("--probe-rewards-natural-chain"))
{
    return ProbeRewardsNaturalChainCommand.Run(vendorDir, repoRoot);
}

if (args.Contains("--probe-merchant"))
{
    return ProbeMerchantCommand.Run(vendorDir);
}

if (args.Contains("--probe-combat-stall"))
{
    return ProbeCombatStallCommand.Run(vendorDir, args);
}

if (args.Contains("--probe-types"))
{
    return ProbeTypesCommand.Run(vendorDir, args);
}

if (args.Contains("--probe-callers"))
{
    return ProbeCallersCommand.Run(vendorDir, args);
}

if (args.Contains("--probe-creatures"))
{
    return ProbeCreaturesCommand.Run(vendorDir, args);
}

if (args.Contains("--probe-method-body"))
{
    return ProbeMethodBodyCommand.Run(vendorDir, args);
}

if (args.Contains("--probe-encounter"))
{
    return ProbeEncounterCommand.Run(vendorDir, args);
}

if (args.Contains("--generate-content-ids") || args.Contains("--generate-card-ids"))
{
    // `--generate-card-ids` retained as an alias so older shell history
    // and justfile checkouts keep working through the rename. The new
    // command always emits every kind's manifest, not just CardId.
    return GenerateContentIdsCommand.Run(vendorDir, repoRoot);
}

if (args.Contains("--probe-modeldb"))
{
    return ProbeModelDbCommand.Run(vendorDir, repoRoot);
}

if (args.Contains("--probe-listener-dispatch"))
{
    return ProbeListenerDispatchCommand.Run(vendorDir, repoRoot);
}

// `--rebuild-replay-index <root>?` — walks <root>/<version>/<run-id>/manifest.json
// and rewrites <root>/runs.json. Useful after manually copying recordings
// in/out of vendor/replays. With no root argument, defaults to
// <repoRoot>/vendor/replays. Doesn't load sts2.dll.
{
    var rebuildIdx = Array.IndexOf(args, "--rebuild-replay-index");
    if (rebuildIdx >= 0)
    {
        var rootArg = rebuildIdx + 1 < args.Length && !args[rebuildIdx + 1].StartsWith('-')
            ? args[rebuildIdx + 1]
            : Path.Combine(repoRoot, ReplayLayout.DefaultRootRelative);
        var n = ReplayIndex.Rebuild(rootArg);
        Console.WriteLine($"rebuilt {ReplayLayout.RunsIndexPath(rootArg)} with {n} run(s)");
        return 0;
    }
}


if (args.Contains("--stdio"))
{
    var preamble = RuntimeBootstrap.Run(vendorDir);
    if (preamble.SetupError is not null)
    {
        Console.Error.WriteLine($"sts2-headless: bootstrap setup failed — {preamble.SetupError}");
        return 1;
    }

    // Step failures are surfaced as stderr warnings rather than fatals — the
    // host keeps serving requests that may not depend on the failing step.
    foreach (var step in BootstrapSequence.Apply(preamble.Sts2!))
    {
        if (!step.Ok) Console.Error.WriteLine($"sts2-headless: bootstrap step '{step.Label}' did not succeed — {step.Detail}");
    }

    if (!preamble.CardSelector.Installed)
    {
        // Loud non-fatal warning so an operator notices a regression in the
        // selector install without the host refusing to serve simpler cards.
        Console.Error.WriteLine($"sts2-headless: ICardSelector did not install — {preamble.CardSelector.Detail}. Cards that prompt for card selection (Headbutt, Armaments, Burning Pact, event card-picks) will crash.");
    }

    Sts2Bindings bindings;
    try { bindings = Sts2Bindings.Bind(preamble.Sts2!, preamble.SyncContext, preamble.CardSelector.Selector); }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"sts2-headless: binding failed — {Diagnostics.Describe(Diagnostics.Unwrap(ex))}");
        return 1;
    }

    // AD-7: debug/* methods are opt-in via --enable-debug. Without it, any
    // debug/* call is rejected with WireErrorCode.DebugMethodDisabled. When
    // it IS set, we log a loud stderr banner so the capability is visible
    // in any log capture and never accidentally invisible to an operator.
    var debugEnabled = args.Contains("--enable-debug");
    if (debugEnabled)
    {
        Console.Error.WriteLine("sts2-headless: debug methods ENABLED via --enable-debug (development/test only — never use in production).");
    }

    var session = new Session();
    var exitCode = StdioHost.Run(Console.In, Console.Out, HostMethods.Build(repoRoot, bindings, session, debugEnabled));
    // AD-8: graceful host shutdown finalises any in-flight recorder so the
    // last run's combats and manifest land on disk. The Harmony prefix on
    // RunManager.CleanUp also flushes — but CleanUp only fires when the
    // engine itself tears down (next run/new, OnEnded). A clean stdin
    // close exits the loop without either, so without this step the last
    // recorder's _replay would be lost.
    session.Recorder?.FinalizeRun();
    return exitCode;
}

// --list-members <FQN>: dump every member of <FQN> that sts2.dll references.
// Used to grow GodotStubs accurately without speculation.
var listIdx = Array.IndexOf(args, "--list-members");
if (listIdx >= 0)
{
    if (listIdx + 1 >= args.Length)
    {
        Console.Error.WriteLine("--list-members needs a fully-qualified type name (e.g. Godot.OS).");
        return 1;
    }
    return ListMembersCommand.Run(vendorDir, args[listIdx + 1]);
}

Console.WriteLine("sts2-headless");
Console.WriteLine($"  repo:    {repoRoot}");
Console.WriteLine($"  vendor:  {vendorDir}");

if (Directory.Exists(vendorDir))
{
    var dlls = Directory.GetFiles(vendorDir, "*.dll").OrderBy(p => p).ToArray();
    Console.WriteLine($"  dlls:    {dlls.Length}");
    foreach (var dll in dlls)
    {
        Console.WriteLine($"    - {Path.GetFileName(dll)}");
    }
}
else
{
    Console.Error.WriteLine($"  vendor/ missing — run `just setup`.");
    return 1;
}

if (File.Exists(gameVersionFile))
{
    Console.WriteLine("  pin:");
    foreach (var line in File.ReadAllLines(gameVersionFile))
    {
        Console.WriteLine($"    {line}");
    }
}
else
{
    Console.WriteLine("  pin:     (GAME_VERSION not present)");
}

return 0;
