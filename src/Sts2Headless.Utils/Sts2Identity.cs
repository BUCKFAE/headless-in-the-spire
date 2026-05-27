namespace Sts2Headless.Utils;

// Identity of the sts2.dll the host is loaded against, as recorded in
// per-run artefacts (replay manifests, eval summaries, host/ping wire
// responses). Two fields with deliberately different sources:
//
//   GameVersion   — human label sourced from the GAME_VERSION pin (AD-3).
//                   Platform-independent: the label travels with the game
//                   release, not the byte sequence.
//   Sts2DllSha256 — lowercase hex SHA-256 of the live bytes of
//                   vendor/sts2.dll, the file that was actually loaded
//                   into this process.
//
// The split matters because on macOS the pin's recorded SHA never matches
// the on-disk DLL: Godot's C# pipeline emits per-arch binaries and the pin
// is Linux-canonical. Recording the pin's SHA in a per-run artefact would
// be a small lie — it would describe bytes that did not run. The live SHA
// describes truth, and a future replayer comparing recorded-vs-loaded SHA
// gets the determinism gate it actually needs.
//
// The pin's Sha256 field is still useful — but only at setup time, where
// scripts/setup/pull-game-libs.sh uses it to catch unexpected upstream
// changes. Anywhere else, route through this helper.
public sealed record Sts2Identity(string GameVersion, string Sts2DllSha256)
{
    private static readonly Lazy<Sts2Identity> _current =
        new(() => From(Paths.LocateRepoRoot()), isThreadSafe: true);

    // Identity for the running host process. Cached for the life of the
    // process because vendor bytes don't change mid-run. Use this from
    // anywhere that records identity into an artefact.
    public static Sts2Identity Current => _current.Value;

    // Identity for an arbitrary repo root. Used by tools and tests that
    // operate on a specific path instead of the running process's install.
    // Falls back to "" for either field when its source is absent.
    public static Sts2Identity From(string repoRoot)
    {
        var pin = GameVersionPin.Read(repoRoot);
        var sts2Path = Paths.Sts2DllPath(Paths.VendorDir(repoRoot));
        return new Sts2Identity(
            GameVersion:   pin?.Version ?? "",
            Sts2DllSha256: File.Exists(sts2Path) ? FileHash.Sha256(sts2Path) : "");
    }
}
