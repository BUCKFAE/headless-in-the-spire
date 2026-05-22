namespace Sts2Headless.Utils;

// The parsed contents of the checked-in GAME_VERSION pin file (AD-3): the
// game's version string and the SHA-256 of the pinned sts2.dll. This is the
// single parser — host ping, replay headers, and schema export all read the
// pin through here instead of re-implementing the line scan.
public sealed record GameVersionPin(string Version, string Sha256)
{
    public const string FileName = "GAME_VERSION";

    // Parse GAME_VERSION from the given repo root. Returns null when the file
    // is absent so each caller can choose its own fallback (host ping reports
    // nulls; replay headers substitute "UNKNOWN"). The format is whitespace-
    // separated `KEY value` lines; unknown keys are ignored.
    public static GameVersionPin? Read(string repoRoot)
    {
        var path = Path.Combine(repoRoot, FileName);
        if (!File.Exists(path)) return null;

        string version = "", sha = "";
        foreach (var line in File.ReadAllLines(path))
        {
            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 2) continue;
            if (parts[0] == "VERSION") version = string.Join(' ', parts.Skip(1));
            else if (parts[0] == "SHA256") sha = parts[1];
        }
        return new GameVersionPin(version, sha);
    }
}
