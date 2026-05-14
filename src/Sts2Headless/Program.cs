using Sts2Headless;
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

    Sts2Bindings bindings;
    try { bindings = Sts2Bindings.Bind(preamble.Sts2!, preamble.SyncContext); }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"sts2-headless: binding failed — {Diagnostics.Describe(Diagnostics.Unwrap(ex))}");
        return 1;
    }

    var session = new Session();
    return StdioHost.Run(Console.In, Console.Out, HostMethods.Build(repoRoot, bindings, session));
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
