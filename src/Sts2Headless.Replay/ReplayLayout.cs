using System.Globalization;

namespace Sts2Headless.Replay;

// On-disk layout for a recorded run. Mirrors AD-3's
// snapshots/<game-version>/... posture: per-version segregation, so a
// v0.103.2 capture and a v0.103.3 capture never collide, and a cross-version
// re-execution refusal is a path lookup, not a content check.
//
// One run is one directory:
//
//     <root>/<game-version>/<run-id>/
//         manifest.json              authored by us (the only file in the
//                                    layout we own; everything else is the
//                                    game's bytes per AD-8)
//         run.json                   the game's RunHistory writer
//                                    (SaveManager.SaveRun → schema_version
//                                    matches the version pinned in
//                                    GAME_VERSION at recording time)
//         combats/
//             act1-floor3-monster.mcr
//             act1-floor7-elite.mcr
//             …                      CombatReplayWriter.WriteReplay output;
//                                    one file per combat, named so a human
//                                    can find a combat in the manifest by
//                                    eye.
//
// The combat filename pattern is `act<n>-floor<n>-<roomslug>.mcr`. It is
// purely human-readability — the manifest is the load-bearing index and is
// what tools should consume. We do not try to parse filenames back into
// coordinates.
public static class ReplayLayout
{
    public const string ManifestFileName = "manifest.json";
    public const string RunHistoryFileName = "run.json";
    public const string RunsIndexFileName = "runs.json";
    public const string CombatsDirectoryName = "combats";

    // Default repo-relative root. Under vendor/ because the bytes are
    // game-derived (proprietary derivative posture, same as vendor/sts2.dll);
    // gitignored via the existing /vendor rule.
    public const string DefaultRootRelative = "vendor/replays";

    public static string RunDirectory(string root, string gameVersion, string runId)
        => Path.Combine(root, gameVersion, runId);

    public static string ManifestPath(string runDirectory)
        => Path.Combine(runDirectory, ManifestFileName);

    public static string RunHistoryPath(string runDirectory)
        => Path.Combine(runDirectory, RunHistoryFileName);

    public static string CombatsDirectory(string runDirectory)
        => Path.Combine(runDirectory, CombatsDirectoryName);

    public static string RunsIndexPath(string root)
        => Path.Combine(root, RunsIndexFileName);

    public static string CombatFileName(int actIndex, int floor, string roomSlug)
        => string.Create(CultureInfo.InvariantCulture, $"act{actIndex + 1}-floor{floor}-{roomSlug}.mcr");

    // RunId is a sortable timestamp + short seed slice + process id. The
    // timestamp orders a listing chronologically; the seed slice
    // disambiguates simultaneous starts that happen to share a second;
    // the pid disambiguates the same-second-same-seed case that arises
    // when a HostPool of N workers all takes the same task off a queue
    // with overlapping run starts.
    public static string NewRunId(DateTimeOffset startTime, string seed)
        => NewRunId(startTime, seed, Environment.ProcessId);

    public static string NewRunId(DateTimeOffset startTime, string seed, int processId)
    {
        var ts = startTime.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var seedSlice = seed.Length <= 8 ? seed : seed[..8];
        var pid = processId.ToString(CultureInfo.InvariantCulture);
        return $"{ts}-{seedSlice}-{pid}";
    }
}
