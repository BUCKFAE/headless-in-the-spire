using Sts2Headless;
using Sts2Headless.Commands;
using Sts2Headless.Runtime;
using Sts2Headless.Runtime.Bindings;
using Sts2Headless.Runtime.Loading;
using Sts2Headless.Utils;

// Skeleton entry: validates that the toolchain wires together and that
// vendor/ is populated. Does not yet load sts2.dll — that comes once
// GodotStubs has enough surface to satisfy its references.

var repoRoot = Paths.LocateRepoRoot();
var vendorDir = Paths.VendorDir(repoRoot);
var gameVersionFile = Path.Combine(repoRoot, "GAME_VERSION");

VendorAssemblyResolver.Install(vendorDir);

// The product path. The stdio host owns the full bootstrap/binding lifecycle,
// so it stays here on its own branch rather than in the diagnostic command
// table (CliCommands). Checked first: pairing --stdio with a diagnostic verb is
// meaningless, and the host should always win that race.
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

if (args.Contains("--help") || args.Contains("-h"))
{
    CliCommands.WriteHelp(Console.Out);
    return 0;
}

// Diagnostic / generator commands all live in one dispatch table; adding one
// there makes it discoverable via --help with no change here.
var command = CliCommands.Match(args);
if (command is not null)
{
    return command.Invoke(new CliContext(repoRoot, vendorDir, args));
}

// No recognised verb: print repo / vendor / pin status.
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
