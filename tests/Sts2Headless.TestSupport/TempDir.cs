namespace Sts2Headless.TestSupport;

// A uniquely-named temporary directory that deletes itself (best-effort) on
// Dispose. Replaces the copy-pasted
//   Path.Combine(Path.GetTempPath(), "sts2-<purpose>-" + Guid.NewGuid())
//   + Directory.CreateDirectory + manual cleanup
// pattern that was duplicated across the integration and end-to-end suites.
//
// Usage:
//   using var dir = new TempDir("sts2-replays");
//   var host = RecordingHost.Start(dir.Path);
public sealed class TempDir : IDisposable
{
    public string Path { get; }

    // `prefix` is purely cosmetic — it keeps temp-dir names recognisable in
    // logs (e.g. "sts2-replays", "sts2-bench"). A GUID is always appended for
    // uniqueness. The directory is created eagerly.
    public TempDir(string prefix = "sts2-test")
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    public override string ToString() => Path;

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); }
        catch { /* best-effort: a leaked temp dir is not a test failure */ }
    }
}
