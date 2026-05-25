namespace Sts2Headless.Utils;

// Repo- and vendor-relative path resolution. Every tool that needs to find
// the checked-in repo root, the gitignored vendor/ directory, or the pinned
// sts2.dll goes through here so the "where are the files" knowledge lives in
// exactly one place.
public static class Paths
{
    // The pinned game assembly's filename. Never check this DLL in (it's
    // proprietary) — it lives under vendor/, populated by `just setup::setup`.
    public const string Sts2DllName = "sts2.dll";

    // The standard operator-facing message when vendor/sts2.dll is absent.
    // Shared so every command prints the same actionable hint.
    public const string Sts2DllMissingMessage = "vendor/sts2.dll missing — run `just setup::setup`.";

    // Walk up from `from` (defaulting to the running assembly's directory)
    // until a directory containing GAME_VERSION or justfile is found — that's
    // the repo root. Throws if neither marker is found on the way up.
    public static string LocateRepoRoot(string? from = null)
    {
        var dir = from ?? AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, GameVersionPin.FileName)) ||
                File.Exists(Path.Combine(dir, "justfile")))
            {
                return dir;
            }
            dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
        }
        throw new InvalidOperationException(
            $"Could not locate repo root from {from ?? AppContext.BaseDirectory}. " +
            "Expected to find a GAME_VERSION or justfile by walking up.");
    }

    // The gitignored vendor/ directory under the repo root — the curated set
    // of game DLLs (see VendorAssemblyResolver / AD-3).
    public static string VendorDir(string repoRoot) => Path.Combine(repoRoot, "vendor");

    // The pinned sts2.dll inside a vendor directory.
    public static string Sts2DllPath(string vendorDir) => Path.Combine(vendorDir, Sts2DllName);
}
