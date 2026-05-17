using Sts2Headless.Replay;
using Sts2Headless.Runtime;
using Xunit;

namespace Sts2Headless.UnitTests;

// Unit-level coverage for ReplayQuery — the load-side helper that
// powers the `run/history` wire method. No engine bootstrap needed:
// the test stages a `run.json` from the sample fixture into a tmp
// directory shaped like `<replay-root>/<game-version>/<run-id>/`,
// then asserts ReplayQuery's read path produces the right shape and
// raises the right error when the file is absent.
public class ReplayQueryTests
{
    [Fact]
    public void LoadAsWireJson_Returns_SnakeCase_Json_Matching_Source()
    {
        var sample = SampleSaves.RunHistoryFiles().FirstOrDefault();
        if (sample is null) return;

        using var tempDir = new TempDir();
        var runDir = tempDir.Path;
        Directory.CreateDirectory(runDir);
        var runJsonTarget = Path.Combine(runDir, ReplayLayout.RunHistoryFileName);
        File.Copy(sample, runJsonTarget);

        var node = ReplayQuery.LoadAsWireJson(runDir);

        // Snake-case fields are present at the top level. If the
        // re-encoding accidentally switched to camelCase or PascalCase,
        // these reads would return null and the test would fail.
        Assert.NotNull(node["schema_version"]);
        Assert.NotNull(node["build_id"]);
        Assert.NotNull(node["seed"]);
        Assert.NotNull(node["game_mode"]);
        Assert.NotNull(node["map_point_history"]);
        Assert.NotNull(node["players"]);
        Assert.Equal(9, node["schema_version"]!.GetValue<int>());
    }

    [Fact]
    public void LoadAsWireJson_Throws_InvalidOperationException_When_File_Missing()
    {
        using var tempDir = new TempDir();
        var ex = Assert.Throws<InvalidOperationException>(() => ReplayQuery.LoadAsWireJson(tempDir.Path));
        Assert.Contains("hasn't ended", ex.Message);
        Assert.Contains("run.json", ex.Message);
    }

    [Fact]
    public void Load_Returns_Typed_Document_Matching_Source_Fields()
    {
        var sample = SampleSaves.RunHistoryFiles().FirstOrDefault();
        if (sample is null) return;

        using var tempDir = new TempDir();
        File.Copy(sample, Path.Combine(tempDir.Path, ReplayLayout.RunHistoryFileName));

        var doc = ReplayQuery.Load(tempDir.Path);
        var direct = Sts2Headless.Protocol.Methods.RunHistoryDocument.ParseFile(sample);

        Assert.Equal(direct.SchemaVersion, doc.SchemaVersion);
        Assert.Equal(direct.Seed, doc.Seed);
        Assert.Equal(direct.BuildId, doc.BuildId);
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sts2-replay-test-" + Guid.NewGuid().ToString("N"));
        public TempDir() => Directory.CreateDirectory(Path);
        public void Dispose() { try { Directory.Delete(Path, recursive: true); } catch { /* best-effort */ } }
    }
}
