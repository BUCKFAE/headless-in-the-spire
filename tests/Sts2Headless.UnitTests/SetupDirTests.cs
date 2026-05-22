using Sts2Headless.TestSupport;
using Sts2Headless.Utils;
using Xunit;

namespace Sts2Headless.UnitTests;

// SetupDir.CleanSetupDir mirrors Python's clean_setup_dir and has no C#
// production caller yet, so these tests are its only safety net — they pin
// the create / wipe / preserve contract so a future consumer can rely on it.
public class SetupDirTests
{
    [Fact]
    public void CleanSetupDir_CreatesDirectory_WhenMissing()
    {
        using var scratch = new TempDir("sts2-utils-test");
        var target = Path.Combine(scratch.Path, "fresh");

        SetupDir.CleanSetupDir(target);

        Assert.True(Directory.Exists(target));
    }

    [Fact]
    public void CleanSetupDir_WipesExistingContents_ByDefault()
    {
        using var scratch = new TempDir("sts2-utils-test");
        var target = Path.Combine(scratch.Path, "data");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "stale.txt"), "x");

        SetupDir.CleanSetupDir(target);

        Assert.True(Directory.Exists(target));
        Assert.Empty(Directory.GetFileSystemEntries(target));
    }

    [Fact]
    public void CleanSetupDir_PreservesContents_WhenDeleteContentFalse()
    {
        using var scratch = new TempDir("sts2-utils-test");
        var target = Path.Combine(scratch.Path, "data");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "keep.txt"), "x");

        SetupDir.CleanSetupDir(target, deleteContent: false);

        Assert.True(File.Exists(Path.Combine(target, "keep.txt")));
    }
}
