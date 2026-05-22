using System.Reflection;
using Sts2Headless.IntegrationTests.Coverage;
using Sts2Headless.Protocol.Methods;
using Sts2Headless.Replay;
using Sts2Headless.Runtime;
using Xunit;
using Sts2Headless.Runtime.Loading;

namespace Sts2Headless.IntegrationTests;

// Proves the AD-8 `.run` writer wired in task #13. Path:
//   1. Bootstrap sts2 in-process (so JsonSerializationUtility is ready).
//   2. Install ReplayHook + Bind a recorder rooted at a tmp dir.
//   3. Take a sample .run file from vendor/sample-saves, parse its JSON
//      into the game's own `RunHistory` model via JsonSerializationUtility.
//   4. Call recorder.OnSaveHistory(runHistory) directly (simulates what
//      the Harmony prefix on RunHistorySaveManager.SaveHistory does).
//   5. Assert <RunDirectory>/run.json appears, contains valid JSON, and
//      parses cleanly via our typed mirror RunHistoryDocument.
//   6. Cross-check a few key fields between source and emitted: seed,
//      schema_version, build_id. These must survive verbatim.
//
// Doesn't drive a full run because the engine only calls SaveHistory
// on RunManager.OnEnded — needs a victory or death path. That's #10's
// orchestration scope. This isolates the wiring so a hook regression
// surfaces here independently.
[Collection(InProcessSts2Collection.Name)]
public class RunJsonEmissionTests
{
    [Fact]
    public void OnSaveHistory_Writes_Engine_Serialised_Run_Json_Into_RunDirectory()
    {
        var repoRoot = Paths.LocateRepoRoot();
        var vendorDir = Path.Combine(repoRoot, "vendor");
        Assert.True(Directory.Exists(vendorDir), $"vendor/ missing at {vendorDir} — run `just setup`.");

        var samplePath = SampleSaves.RunHistoryFiles().FirstOrDefault();
        if (samplePath is null) return;  // fixture absent — skip

        VendorAssemblyResolver.Install(vendorDir);
        var preamble = RuntimeBootstrap.Run(vendorDir);
        Assert.Null(preamble.SetupError);
        Assert.NotNull(preamble.Sts2);
        var steps = BootstrapSequence.Apply(preamble.Sts2!);
        Assert.All(steps, s => Assert.True(s.Ok, $"bootstrap step failed: {s.Label}"));

        var sts2 = preamble.Sts2!;
        var runHistory = LoadRunHistoryFromDisk(sts2, samplePath);

        using var tempReplays = new TempDir();
        var (gameVersion, sha) = ReplayHeaderFactory.ReadGameVersionPin(repoRoot);
        var header = ReplayHeaderFactory.Create(
            sts2: sts2,
            gameVersion: gameVersion,
            sts2DllSha256: sha,
            seed: "42",
            character: Character.Ironclad,
            ascension: 0,
            modifiers: Array.Empty<string>(),
            startTime: DateTimeOffset.UtcNow);
        var recorder = new ReplayRecorder(sts2, tempReplays.Path, header);

        // Direct invocation — simulates the Harmony prefix's callback.
        recorder.OnSaveHistory(runHistory);

        var runJsonPath = Path.Combine(recorder.RunDirectory, ReplayLayout.RunHistoryFileName);
        Assert.True(File.Exists(runJsonPath), $"run.json missing at {runJsonPath}");

        // Parses cleanly via our typed mirror.
        var doc = RunHistoryDocument.ParseFile(runJsonPath);
        Assert.Equal(9, doc.SchemaVersion);
        Assert.False(string.IsNullOrEmpty(doc.BuildId));
        Assert.False(string.IsNullOrEmpty(doc.Seed));

        // Cross-check against the source .run file. The seed / schema /
        // build_id must survive verbatim because the engine's serializer
        // produces the bytes both at original-write time and at our
        // OnSaveHistory time — same `JsonSerializationUtility.ToJson<T>`
        // call. Other fields (counters, etc.) we don't enumerate here:
        // it's the schema-equivalence test, not deep value comparison.
        var sourceDoc = RunHistoryDocument.ParseFile(samplePath);
        Assert.Equal(sourceDoc.Seed, doc.Seed);
        Assert.Equal(sourceDoc.SchemaVersion, doc.SchemaVersion);
        Assert.Equal(sourceDoc.BuildId, doc.BuildId);
        Assert.Equal(sourceDoc.GameMode, doc.GameMode);
        Assert.Equal(sourceDoc.PlatformType, doc.PlatformType);
        Assert.Equal(sourceDoc.Players.Count, doc.Players.Count);
        Assert.Equal(sourceDoc.MapPointHistory.Count, doc.MapPointHistory.Count);
    }

    // Reads a .run JSON from disk and turns it into the game's
    // engine-level RunHistory model. Bypasses `JsonSerializationUtility.FromJson`
    // — that returns a `ReadSaveResult<T>` whose accessor field name we'd
    // have to keep in sync with the game's version. Going one layer
    // deeper to `JsonSerializer.Deserialize(json, GetTypeInfo<T>())` is
    // stable: same call the engine's own FromJson uses internally.
    private static object LoadRunHistoryFromDisk(Assembly sts2, string path)
    {
        var json = File.ReadAllText(path);
        var jsonUtilType = sts2.GetType("MegaCrit.Sts2.Core.Saves.JsonSerializationUtility")
            ?? throw new InvalidOperationException("JsonSerializationUtility not found");
        var runHistoryType = sts2.GetType("MegaCrit.Sts2.Core.Runs.RunHistory")
            ?? throw new InvalidOperationException("RunHistory not found");

        // GetTypeInfo<T>() — parameterless generic.
        var getTypeInfoGeneric = jsonUtilType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m => m is { Name: "GetTypeInfo", IsGenericMethodDefinition: true } && m.GetParameters().Length == 0);
        var getTypeInfo = getTypeInfoGeneric.MakeGenericMethod(runHistoryType);
        var typeInfo = getTypeInfo.Invoke(null, null)
            ?? throw new InvalidOperationException("GetTypeInfo<RunHistory> returned null");

        // System.Text.Json.JsonSerializer.Deserialize<T>(string, JsonTypeInfo<T>).
        // Find the overload that takes (string, JsonTypeInfo<T>) — there are
        // many, so filter on parameter count + types.
        var serializerType = typeof(System.Text.Json.JsonSerializer);
        var deserialize = serializerType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m =>
            {
                if (m.Name != "Deserialize" || !m.IsGenericMethodDefinition) return false;
                var ps = m.GetParameters();
                return ps.Length == 2
                    && ps[0].ParameterType == typeof(string)
                    && ps[1].ParameterType.IsGenericType
                    && ps[1].ParameterType.GetGenericTypeDefinition() == typeof(System.Text.Json.Serialization.Metadata.JsonTypeInfo<>);
            });
        var deserializeForRunHistory = deserialize.MakeGenericMethod(runHistoryType);
        return deserializeForRunHistory.Invoke(null, [json, typeInfo])
            ?? throw new InvalidDataException(".run JSON deserialised to null");
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sts2-replay-test-" + Guid.NewGuid().ToString("N"));
        public TempDir() => Directory.CreateDirectory(Path);
        public void Dispose() { try { Directory.Delete(Path, recursive: true); } catch { /* best-effort */ } }
    }
}
