using System.Reflection;
using Sts2Headless.IntegrationTests.Coverage;
using Sts2Headless.Replay;
using Sts2Headless.Runtime;
using Xunit;
using Sts2Headless.Runtime.Loading;

namespace Sts2Headless.IntegrationTests;

// In-process round-trip: load vendor/sts2.dll, run the bootstrap
// preamble (so ModelDb + ModelIdSerializationCache are populated — the
// .mcr binary format encodes ModelIds with a bit width derived from the
// cache, so parsing without bootstrap desyncs the bit stream and crashes
// inside SerializableMapPoint.Deserialize), parse an existing .mcr from
// vendor/sample-saves via the game's own PacketReader, re-serialise via
// CombatReplayBytes, and parse the re-serialised bytes back.
//
// What this proves about #6:
//   * CombatReplayBytes produces bytes the game's own reader accepts.
//   * Our serialiser path bypasses Godot.FileAccess cleanly — the bytes
//     on disk are real, not the stub's no-op "wrote 0 bytes ok".
//   * The modelIdHash baked into the captured .mcr matches the value the
//     log captured at recording time (1357847701), giving us a stable
//     reference for the AD-3 version-pin posture.
[Collection(InProcessSts2Collection.Name)]
public class ReplayBytesRoundTripTests
{
    [Fact]
    public void RoundTrip_Existing_LatestMcr_Preserves_Event_And_Checksum_Counts()
    {
        var repoRoot = Paths.LocateRepoRoot();
        var vendorDir = Path.Combine(repoRoot, "vendor");
        Assert.True(Directory.Exists(vendorDir), $"vendor/ missing at {vendorDir} — run `just setup`.");

        var mcr = SampleSaves.CombatReplayFiles().FirstOrDefault();
        if (mcr is null) return;  // fixture absent — skip rather than fail

        VendorAssemblyResolver.Install(vendorDir);
        var preamble = RuntimeBootstrap.Run(vendorDir);
        Assert.Null(preamble.SetupError);
        Assert.NotNull(preamble.Sts2);
        // BootstrapSequence populates ModelDb + ModelIdSerializationCache —
        // both needed for ModelId encoding in CombatReplay.
        var steps = BootstrapSequence.Apply(preamble.Sts2!);
        Assert.All(steps, s => Assert.True(s.Ok, $"bootstrap step failed: {s.Label} ({s.Detail ?? "<no detail>"})"));

        var sts2 = preamble.Sts2!;
        var packetReaderType = sts2.GetType("MegaCrit.Sts2.Core.Multiplayer.Serialization.PacketReader")
            ?? throw new InvalidOperationException("PacketReader not in sts2");
        var combatReplayType = sts2.GetType("MegaCrit.Sts2.Core.Multiplayer.Replay.CombatReplay")
            ?? throw new InvalidOperationException("CombatReplay not in sts2");

        var bytes = File.ReadAllBytes(mcr);
        var reader = Activator.CreateInstance(packetReaderType)!;
        var resetMethod = packetReaderType.GetMethod("Reset", BindingFlags.Public | BindingFlags.Instance, [typeof(byte[])])
            ?? throw new InvalidOperationException("PacketReader.Reset(byte[]) not in sts2");
        resetMethod.Invoke(reader, [bytes]);
        var readGeneric = packetReaderType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .First(m => m is { Name: "Read", IsGenericMethodDefinition: true } && m.GetParameters().Length == 0);
        var readCombatReplay = readGeneric.MakeGenericMethod(combatReplayType);
        var parsed = readCombatReplay.Invoke(reader, null)
            ?? throw new InvalidDataException(".mcr parsed to null");

        var serializer = new CombatReplayBytes(sts2);
        var originalEvents = serializer.EventCount(parsed);
        var originalChecksums = serializer.ChecksumCount(parsed);
        Assert.True(originalEvents > 0, "expected the sample .mcr to carry events");

        using var tempDir = new TempDir();
        var outPath = Path.Combine(tempDir.Path, "round-trip.mcr");
        var result = serializer.Write(parsed, outPath);
        Assert.True(File.Exists(outPath));
        Assert.True(new FileInfo(outPath).Length > 0);
        Assert.Equal(originalEvents, result.Events);
        Assert.Equal(originalChecksums, result.Checksums);

        var roundBytes = File.ReadAllBytes(outPath);
        var reader2 = Activator.CreateInstance(packetReaderType)!;
        resetMethod.Invoke(reader2, [roundBytes]);
        var roundParsed = readCombatReplay.Invoke(reader2, null)
            ?? throw new InvalidDataException("round-tripped .mcr parsed to null");

        Assert.Equal(originalEvents, serializer.EventCount(roundParsed));
        Assert.Equal(originalChecksums, serializer.ChecksumCount(roundParsed));

        var versionField = combatReplayType.GetField("version")!;
        var commitField = combatReplayType.GetField("gitCommit")!;
        var modelHashField = combatReplayType.GetField("modelIdHash")!;
        Assert.Equal(versionField.GetValue(parsed), versionField.GetValue(roundParsed));
        Assert.Equal(commitField.GetValue(parsed), commitField.GetValue(roundParsed));
        Assert.Equal(modelHashField.GetValue(parsed), modelHashField.GetValue(roundParsed));

        // Cross-check the modelIdHash matches the value pinned in
        // vendor/sample-saves/README.md for this build (1357847701).
        Assert.Equal(1357847701u, (uint)modelHashField.GetValue(parsed)!);
    }

    [Fact]
    public void ReplayHook_Install_Succeeds_Without_Throwing()
    {
        var repoRoot = Paths.LocateRepoRoot();
        var vendorDir = Path.Combine(repoRoot, "vendor");
        VendorAssemblyResolver.Install(vendorDir);
        var preamble = RuntimeBootstrap.Run(vendorDir);
        Assert.NotNull(preamble.Sts2);

        // Install is idempotent; calling twice should not throw.
        ReplayHook.Install(preamble.Sts2!);
        ReplayHook.Install(preamble.Sts2!);
        // No active recorder bound yet — the static slot should be empty.
        Assert.False(ReplayHook.HasActiveRecorder);
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sts2-replay-test-" + Guid.NewGuid().ToString("N"));
        public TempDir() => Directory.CreateDirectory(Path);
        public void Dispose() { try { Directory.Delete(Path, recursive: true); } catch { /* best-effort */ } }
    }
}
