using Sts2Headless;

// Skeleton entry: validates that the toolchain wires together and that
// vendor/ is populated. Does not yet load sts2.dll — that comes once
// GodotStubs has enough surface to satisfy its references.

var repoRoot = LocateRepoRoot();
var vendorDir = Path.Combine(repoRoot, "vendor");
var gameVersionFile = Path.Combine(repoRoot, "GAME_VERSION");

VendorAssemblyResolver.Install(vendorDir);

if (args.Contains("--inspect-sts2"))
{
    return InspectCommand.Run(vendorDir);
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

static string LocateRepoRoot()
{
    // Walk up from the exe location looking for the GAME_VERSION marker.
    // dotnet run drops us in src/Sts2Headless/bin/.../; published builds
    // could be anywhere. The marker is a stable repo-root anchor.
    var dir = AppContext.BaseDirectory;
    while (dir is not null)
    {
        if (File.Exists(Path.Combine(dir, "GAME_VERSION")) ||
            File.Exists(Path.Combine(dir, "justfile")))
        {
            return dir;
        }
        dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
    }
    throw new InvalidOperationException(
        $"Could not locate repo root from {AppContext.BaseDirectory}. " +
        "Expected to find a GAME_VERSION or justfile by walking up.");
}
