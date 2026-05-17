namespace Sts2Headless.Runtime;

// Locator for the fixture save tree under vendor/sample-saves/. Tests glob
// `steam/*/profile1/` rather than hard-coding the placeholder directory
// name — see vendor/sample-saves/README.md. Centralised here so a future
// rename (e.g. multi-profile fixtures, secondary platform mirror) only
// touches one file. Lives in Runtime next to Paths because Paths is the
// repo-root locator this builds on, and Runtime is the lowest-common
// project for cross-test sharing without pulling in Replay's Harmony
// dependency.
public static class SampleSaves
{
    public const string RelativeRoot = "vendor/sample-saves/SlayTheSpire2";

    // Returns absolute path to vendor/sample-saves/SlayTheSpire2 if it
    // exists, or null if the fixture isn't present (tests should skip).
    public static string? RootOrNull(string? repoRoot = null)
    {
        var root = repoRoot ?? Paths.LocateRepoRoot();
        var p = Path.Combine(root, RelativeRoot);
        return Directory.Exists(p) ? p : null;
    }

    // Enumerates profile1 directories under any steam id (the placeholder
    // "test-steam-id" today, but globbing keeps a real-user copy working
    // without further redaction). Empty if no fixture present.
    public static IEnumerable<string> Profile1Directories(string? repoRoot = null)
    {
        var root = RootOrNull(repoRoot);
        if (root is null) yield break;
        var steam = Path.Combine(root, "steam");
        if (!Directory.Exists(steam)) yield break;
        foreach (var idDir in Directory.EnumerateDirectories(steam))
        {
            var profile = Path.Combine(idDir, "profile1");
            if (Directory.Exists(profile)) yield return profile;
        }
    }

    // Convenience: all .run files across all profiles in the fixture.
    public static IEnumerable<string> RunHistoryFiles(string? repoRoot = null)
    {
        foreach (var profile in Profile1Directories(repoRoot))
        {
            var history = Path.Combine(profile, "saves", "history");
            if (!Directory.Exists(history)) continue;
            foreach (var f in Directory.EnumerateFiles(history, "*.run", SearchOption.TopDirectoryOnly))
                yield return f;
        }
    }

    // Convenience: all .mcr files (combat replays) across all profiles.
    // Game writes replays/latest.mcr (single file, rotated on each new
    // recording); future fixtures may carry an archive, so SearchOption is
    // recursive.
    public static IEnumerable<string> CombatReplayFiles(string? repoRoot = null)
    {
        foreach (var profile in Profile1Directories(repoRoot))
        {
            var replays = Path.Combine(profile, "replays");
            if (!Directory.Exists(replays)) continue;
            foreach (var f in Directory.EnumerateFiles(replays, "*.mcr", SearchOption.AllDirectories))
                yield return f;
        }
    }
}
