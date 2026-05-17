using System.Reflection;

namespace Sts2Headless.Replay;

// Read-side mirror of CombatReplayBytes. Takes `.mcr` bytes off disk and
// produces a live (deserialised) instance of
// `MegaCrit.Sts2.Core.Multiplayer.Replay.CombatReplay` via the engine's
// own `PacketReader.Read<T>` machinery — same binary shape the engine
// itself reads in `NMultiplayerTest.LoadReplay`.
//
// The returned object IS the engine's CombatReplay; consumers can pull
// fields like `events`, `checksumData`, `serializableRun` via reflection
// (or via the helpers on this class) without having to repeat the byte
// walk. CombatTimelineEmitter is the canonical consumer.
//
// AD-4 stands: we never reference `CombatReplay` or `PacketReader` at
// compile time. Reflection happens once per constructor; the cached
// MethodInfo / FieldInfo handles are reusable across many reads.
public sealed class CombatReplayReader
{
    private readonly Type _combatReplayType;
    private readonly Type _packetReaderType;
    private readonly ConstructorInfo _packetReaderCtor;
    private readonly MethodInfo _packetReaderReset;
    private readonly MethodInfo _packetReaderReadCombatReplay;

    public CombatReplayReader(Assembly sts2)
    {
        _combatReplayType = sts2.GetType("MegaCrit.Sts2.Core.Multiplayer.Replay.CombatReplay")
            ?? throw new InvalidOperationException("CombatReplay not found in sts2 assembly");
        _packetReaderType = sts2.GetType("MegaCrit.Sts2.Core.Multiplayer.Serialization.PacketReader")
            ?? throw new InvalidOperationException("PacketReader not found in sts2 assembly");
        _packetReaderCtor = _packetReaderType.GetConstructor(Type.EmptyTypes)
            ?? throw new InvalidOperationException("PacketReader has no public parameterless constructor");
        _packetReaderReset = _packetReaderType.GetMethod("Reset", BindingFlags.Public | BindingFlags.Instance, [typeof(byte[])])
            ?? throw new InvalidOperationException("PacketReader.Reset(byte[]) not found");

        // PacketReader exposes a generic `T Read<T>() where T : IPacketSerializable, new()`.
        // We close it over CombatReplay once so subsequent reads avoid the
        // generic-method resolution cost.
        var readGeneric = _packetReaderType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == "Read" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0)
            ?? throw new InvalidOperationException("PacketReader.Read<T>() not found");
        _packetReaderReadCombatReplay = readGeneric.MakeGenericMethod(_combatReplayType);
    }

    // Loads a `.mcr` file from disk and returns the deserialised
    // CombatReplay instance (typed `object`, since we don't reference
    // the type at compile time — AD-4).
    public object ReadFile(string path)
    {
        var bytes = File.ReadAllBytes(path);
        return Read(bytes);
    }

    public object Read(byte[] bytes)
    {
        var reader = _packetReaderCtor.Invoke(null);
        _packetReaderReset.Invoke(reader, [bytes]);
        return _packetReaderReadCombatReplay.Invoke(reader, null)
            ?? throw new InvalidDataException("PacketReader.Read<CombatReplay>() returned null");
    }

    public Type CombatReplayType => _combatReplayType;
}
