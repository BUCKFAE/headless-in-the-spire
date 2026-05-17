using System.Reflection;

namespace Sts2Headless.Replay;

// Reflective serialiser: takes a live `MegaCrit.Sts2.Core.Multiplayer.Replay.CombatReplay`
// instance, runs `Anonymized()` on it, then walks the engine's own
// `PacketWriter.Write<CombatReplay>(replay)` to produce the exact byte
// shape the retail `.mcr` files use.
//
// This deliberately bypasses `CombatReplayWriter.WriteReplay`. That method
// writes through `Godot.FileAccess` via `Sts2.Core.Saves.FileAccessStream`,
// and our `GodotStubs.FileAccess` is "empty filesystem" no-ops by design
// (Resources.cs: StoreBuffer returns true without writing). Reaching for
// our own `System.IO.File.WriteAllBytes` keeps the GodotStubs surface
// untouched per CLAUDE.md ("GodotStubs grows on demand") while still
// producing real bytes on disk.
//
// AD-4 stands: we never name `CombatReplay` or `PacketWriter` at compile
// time. The serialise call site is `replay.Serialize(packetWriter)`,
// which is a public instance method we resolve reflectively from the
// loaded assembly.
public sealed class CombatReplayBytes
{
    private readonly Type _packetWriterType;
    private readonly ConstructorInfo _packetWriterCtor;
    private readonly MethodInfo _packetWriterReset;
    private readonly PropertyInfo _packetWriterBuffer;
    private readonly PropertyInfo _packetWriterBytePosition;
    private readonly MethodInfo _combatReplayAnonymized;
    private readonly MethodInfo _combatReplaySerialize;
    private readonly FieldInfo _combatReplayEvents;
    private readonly FieldInfo _combatReplayChecksumData;

    public CombatReplayBytes(Assembly sts2)
    {
        _packetWriterType = sts2.GetType("MegaCrit.Sts2.Core.Multiplayer.Serialization.PacketWriter")
            ?? throw new InvalidOperationException("PacketWriter not found in sts2 assembly");
        _packetWriterCtor = _packetWriterType.GetConstructor(Type.EmptyTypes)
            ?? throw new InvalidOperationException("PacketWriter has no public parameterless constructor");
        _packetWriterReset = RequireMethod(_packetWriterType, "Reset");
        _packetWriterBuffer = RequireProperty(_packetWriterType, "Buffer");
        _packetWriterBytePosition = RequireProperty(_packetWriterType, "BytePosition");

        var combatReplayType = sts2.GetType("MegaCrit.Sts2.Core.Multiplayer.Replay.CombatReplay")
            ?? throw new InvalidOperationException("CombatReplay not found in sts2 assembly");
        _combatReplayAnonymized = RequireMethod(combatReplayType, "Anonymized");
        _combatReplaySerialize = combatReplayType.GetMethod("Serialize", BindingFlags.Public | BindingFlags.Instance, [_packetWriterType])
            ?? throw new InvalidOperationException("CombatReplay.Serialize(PacketWriter) not found");
        _combatReplayEvents = combatReplayType.GetField("events")
            ?? throw new InvalidOperationException("CombatReplay.events field not found");
        _combatReplayChecksumData = combatReplayType.GetField("checksumData")
            ?? throw new InvalidOperationException("CombatReplay.checksumData field not found");
    }

    // Serialise (anonymised) and persist. Returns counts so the caller can
    // populate ReplayCombatEntry without re-parsing the bytes we just wrote.
    public WriteResult Write(object combatReplay, string filePath)
    {
        var anonymised = _combatReplayAnonymized.Invoke(combatReplay, null)
            ?? throw new InvalidOperationException("CombatReplay.Anonymized returned null");

        var writer = _packetWriterCtor.Invoke(null);
        _packetWriterReset.Invoke(writer, null);
        _combatReplaySerialize.Invoke(anonymised, [writer]);

        var buffer = (byte[])_packetWriterBuffer.GetValue(writer)!;
        var bytePosition = (int)_packetWriterBytePosition.GetValue(writer)!;

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllBytes(filePath, buffer.AsSpan(0, bytePosition).ToArray());

        var eventCount = CountList(_combatReplayEvents.GetValue(combatReplay));
        var checksumCount = CountList(_combatReplayChecksumData.GetValue(combatReplay));
        return new WriteResult(bytePosition, eventCount, checksumCount);
    }

    public int EventCount(object combatReplay) => CountList(_combatReplayEvents.GetValue(combatReplay));
    public int ChecksumCount(object combatReplay) => CountList(_combatReplayChecksumData.GetValue(combatReplay));

    public readonly record struct WriteResult(int Bytes, int Events, int Checksums);

    private static int CountList(object? list)
    {
        if (list is null) return 0;
        var countProp = list.GetType().GetProperty("Count");
        return countProp?.GetValue(list) is int n ? n : 0;
    }

    private static MethodInfo RequireMethod(Type type, string name)
        => type.GetMethod(name, BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes)
           ?? throw new InvalidOperationException($"{type.FullName}.{name}() not found");

    private static PropertyInfo RequireProperty(Type type, string name)
        => type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
           ?? throw new InvalidOperationException($"{type.FullName}.{name} property not found");
}
