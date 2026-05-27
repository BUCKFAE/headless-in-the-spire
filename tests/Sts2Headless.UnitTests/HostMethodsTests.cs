using System.Security.Cryptography;
using Sts2Headless.Utils;
using Xunit;

namespace Sts2Headless.UnitTests;

// HostMethods.Ping shapes the host/ping wire response: a version label
// from GAME_VERSION (the pin) and a SHA-256 of the live vendor/sts2.dll.
// The two fields have deliberately different sources — see Sts2Identity
// for the rationale (pin-SHA diverges from loaded-SHA on macOS).
//
// Both fields are independently nullable on the wire: missing GAME_VERSION
// nulls Version; missing vendor/sts2.dll nulls Sha256. Tests cover both
// presence axes plus version-string parsing edge cases.
public class HostMethodsTests
{
    [Fact]
    public void Ping_ReadsVersionFromGameVersionAndShaFromVendorDll()
    {
        // Both sources present: version comes from the pin (label),
        // SHA comes from a fresh hash of the on-disk vendor bytes.
        var fakeBytes = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        using var temp = new TempRepoRoot(
            gameVersionContents: """
                VERSION  0.1.2.3
                SHA256   pinned-sha-which-should-be-ignored
                """,
            vendorSts2Bytes: fakeBytes);

        var result = HostMethods.Ping(temp.Path);

        Assert.True(result.Ok);
        Assert.Equal("0.1.2.3", result.GameVersion);
        // SHA must reflect the live bytes, not the pin's recorded SHA.
        Assert.Equal(ExpectedSha256(fakeBytes), result.GameSha256);
    }

    [Fact]
    public void Ping_HandlesMissingGameVersionAndVendorDll()
    {
        using var temp = new TempRepoRoot(gameVersionContents: null, vendorSts2Bytes: null);

        var result = HostMethods.Ping(temp.Path);

        Assert.True(result.Ok);
        Assert.Null(result.GameVersion);
        Assert.Null(result.GameSha256);
    }

    [Fact]
    public void Ping_NullsShaWhenVendorDllAbsentButPinPresent()
    {
        // Half-populated tempdir: pin exists but no vendor/sts2.dll. Version
        // still parses; Sha256 is null because there's no DLL to hash.
        using var temp = new TempRepoRoot(
            gameVersionContents: """
                VERSION  9.9.9
                SHA256   irrelevant
                """,
            vendorSts2Bytes: null);

        var result = HostMethods.Ping(temp.Path);

        Assert.Equal("9.9.9", result.GameVersion);
        Assert.Null(result.GameSha256);
    }

    [Fact]
    public void Ping_IgnoresLinesWithoutKeyValueShape()
    {
        // Blank lines and comment-like junk should not derail the parser.
        using var temp = new TempRepoRoot(
            gameVersionContents: """

                # this is a comment-like line we ignore

                VERSION  1.0.0
                random noise without a recognised key
                SHA256   deadbeef
                """,
            vendorSts2Bytes: null);

        var result = HostMethods.Ping(temp.Path);
        Assert.Equal("1.0.0", result.GameVersion);
    }

    [Fact]
    public void Ping_PreservesSpacesInVersionString()
    {
        // The pin's VERSION token is whatever text comes after "VERSION", up
        // to end-of-line. The placeholder before a real bump literally
        // contains <fill-in-from-game-credits-screen> — single tokens with
        // angle brackets — but a real version with spaces should also survive.
        using var temp = new TempRepoRoot(
            gameVersionContents: "VERSION  Some Build 0.1\nSHA256  ff",
            vendorSts2Bytes: null);

        var result = HostMethods.Ping(temp.Path);
        Assert.Equal("Some Build 0.1", result.GameVersion);
    }

    private static string ExpectedSha256(byte[] bytes)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
    }

    // RAII helper: a tempdir that looks enough like a repo root for Ping —
    // optional GAME_VERSION at the root, optional vendor/sts2.dll under
    // vendor/. We don't go through Paths.LocateRepoRoot here on purpose —
    // Ping is given a path, so the test owns that path explicitly.
    private sealed class TempRepoRoot : IDisposable
    {
        public string Path { get; }

        public TempRepoRoot(string? gameVersionContents, byte[]? vendorSts2Bytes)
        {
            Path = Directory.CreateTempSubdirectory("sts2-unit-ping-").FullName;
            if (gameVersionContents is not null)
            {
                File.WriteAllText(System.IO.Path.Combine(Path, "GAME_VERSION"), gameVersionContents);
            }
            if (vendorSts2Bytes is not null)
            {
                var vendor = System.IO.Path.Combine(Path, "vendor");
                Directory.CreateDirectory(vendor);
                File.WriteAllBytes(System.IO.Path.Combine(vendor, Paths.Sts2DllName), vendorSts2Bytes);
            }
        }

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); }
            catch { /* tempdir cleanup is best-effort */ }
        }
    }
}
