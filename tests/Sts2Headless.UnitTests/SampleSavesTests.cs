using Sts2Headless.Runtime;
using Xunit;

namespace Sts2Headless.UnitTests;

// Pins the sample-saves fixture locator. If the fixture isn't present
// (CI without vendor/, fresh clone before `just pull-game-libs`), every
// assertion degrades to a skip — the locator is supposed to return null
// gracefully, not throw. If the fixture IS present, we assert a stable
// shape so a rename or accidental wipe of vendor/sample-saves/ surfaces
// here rather than in a downstream parser test.
public class SampleSavesTests
{
    [Fact]
    public void Root_Returns_Null_Or_Existing_Directory()
    {
        var root = SampleSaves.RootOrNull();
        if (root is null) return;
        Assert.True(Directory.Exists(root), $"locator returned non-existent path {root}");
    }

    [Fact]
    public void Profile1_Directories_Are_Discoverable_When_Fixture_Present()
    {
        var root = SampleSaves.RootOrNull();
        if (root is null) return;
        var profiles = SampleSaves.Profile1Directories().ToList();
        Assert.NotEmpty(profiles);
        foreach (var p in profiles)
        {
            Assert.True(Directory.Exists(p), $"profile dir doesn't exist: {p}");
            Assert.EndsWith($"{Path.DirectorySeparatorChar}profile1", p);
        }
    }

    [Fact]
    public void Run_History_Files_Are_Json_When_Fixture_Present()
    {
        if (SampleSaves.RootOrNull() is null) return;
        var runs = SampleSaves.RunHistoryFiles().ToList();
        Assert.NotEmpty(runs);
        // Lightweight: first byte of each is '{' (a .run is a JSON object).
        // Anything more involved goes in the dedicated RunHistory parser
        // test once the typed mirror lands (#8).
        foreach (var run in runs)
        {
            using var fs = File.OpenRead(run);
            var first = fs.ReadByte();
            Assert.Equal('{', first);
        }
    }

    [Fact]
    public void Combat_Replay_Files_Have_Mcr_Header_When_Fixture_Present()
    {
        if (SampleSaves.RootOrNull() is null) return;
        var mcrs = SampleSaves.CombatReplayFiles().ToList();
        if (mcrs.Count == 0) return;  // game writes latest.mcr only on combat-end; absence is fine
        foreach (var mcr in mcrs)
        {
            // First 4 bytes are the length prefix for the version string.
            // The game writes WriteString(version) first; version is ASCII
            // (e.g. "v0.103.2"), so we expect a small u32 length followed
            // by 'v'. This is enough to catch a corrupted / placeholder
            // file without depending on the full PacketReader.
            using var fs = File.OpenRead(mcr);
            var len = new byte[4];
            Assert.Equal(4, fs.Read(len, 0, 4));
            var lengthOfVersion = BitConverter.ToInt32(len, 0);
            Assert.InRange(lengthOfVersion, 1, 64);
            var firstChar = fs.ReadByte();
            Assert.Equal('v', firstChar);
        }
    }
}
