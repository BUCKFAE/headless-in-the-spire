using Xunit;

namespace Sts2Headless.UnitTests;

// HostMethods.Ping reads GAME_VERSION from disk and shapes the response.
// It deliberately doesn't touch Sts2Bindings, so it's safe to exercise
// without a game install — drop a fake GAME_VERSION into a tempdir and
// assert the parse. Returns a HostPingResult DTO; we assert on its fields
// rather than JSON shape so a rename in the wire schema is a compile error.
public class HostMethodsTests
{
    [Fact]
    public void Ping_ReadsVersionAndShaFromGameVersionFile()
    {
        using var temp = new TempRepoRoot(
            """
            VERSION  0.1.2.3
            SHA256   abcdef0123456789
            """);

        var result = HostMethods.Ping(temp.Path);

        Assert.True(result.Ok);
        Assert.Equal("0.1.2.3", result.GameVersion);
        Assert.Equal("abcdef0123456789", result.GameSha256);
    }

    [Fact]
    public void Ping_HandlesMissingGameVersionFile()
    {
        using var temp = new TempRepoRoot(null);

        var result = HostMethods.Ping(temp.Path);

        Assert.True(result.Ok);
        Assert.Null(result.GameVersion);
        Assert.Null(result.GameSha256);
    }

    [Fact]
    public void Ping_IgnoresLinesWithoutKeyValueShape()
    {
        // Blank lines and comment-like junk should not derail the parser.
        using var temp = new TempRepoRoot(
            """

            # this is a comment-like line we ignore

            VERSION  1.0.0
            random noise without a recognised key
            SHA256   deadbeef
            """);

        var result = HostMethods.Ping(temp.Path);
        Assert.Equal("1.0.0", result.GameVersion);
        Assert.Equal("deadbeef", result.GameSha256);
    }

    [Fact]
    public void Ping_PreservesSpacesInVersionString()
    {
        // The pin's VERSION token is whatever text comes after "VERSION", up
        // to end-of-line. The placeholder before a real bump literally
        // contains <fill-in-from-game-credits-screen> — single tokens with
        // angle brackets — but a real version with spaces should also survive.
        using var temp = new TempRepoRoot("VERSION  Some Build 0.1\nSHA256  ff");

        var result = HostMethods.Ping(temp.Path);
        Assert.Equal("Some Build 0.1", result.GameVersion);
    }

    // RAII helper: a tempdir that looks enough like a repo root that Ping's
    // ReadGameVersion logic finds (or doesn't find) a GAME_VERSION file. We
    // don't go through Paths.LocateRepoRoot here on purpose — Ping is given
    // a path, so the test owns that path explicitly.
    private sealed class TempRepoRoot : IDisposable
    {
        public string Path { get; }

        public TempRepoRoot(string? gameVersionContents)
        {
            Path = Directory.CreateTempSubdirectory("sts2-unit-ping-").FullName;
            if (gameVersionContents is not null)
            {
                File.WriteAllText(System.IO.Path.Combine(Path, "GAME_VERSION"), gameVersionContents);
            }
        }

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); }
            catch { /* tempdir cleanup is best-effort */ }
        }
    }
}
