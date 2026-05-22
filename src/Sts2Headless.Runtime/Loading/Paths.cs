namespace Sts2Headless.Runtime.Loading;

// Repo-root discovery. Both the exe (`dotnet run` drops us into
// src/Sts2Headless/bin/.../) and the test runner (somewhere under tests/)
// need to find the repo root to locate vendor/ and GAME_VERSION. Walking up
// from AppContext.BaseDirectory looking for a stable marker file works for
// both, plus published builds that live anywhere.
public static class Paths
{
    public static string LocateRepoRoot(string? from = null)
    {
        var dir = from ?? AppContext.BaseDirectory;
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
            $"Could not locate repo root from {from ?? AppContext.BaseDirectory}. " +
            "Expected to find a GAME_VERSION or justfile by walking up.");
    }
}
