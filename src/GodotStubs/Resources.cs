// Resource branch of the type tree (Resource → PackedScene, plus the
// standalone Resource-rooted types sts2 references). See Nodes.cs for the
// general approach and the MethodName/PropertyName/SignalName pattern.

namespace Godot;

public partial class Resource : GodotObject
{
    public new class MethodName : GodotObject.MethodName { }
    public new class PropertyName : GodotObject.PropertyName { }
    public new class SignalName : GodotObject.SignalName { }
}

// from: MegaCrit.Sts2.Core.Nodes.Rooms.NEventRoom.Create
//   TypeLoadException during EnterAct's Neow room entry: "Could not load
//   type 'Godot.PackedScene'." Real type is sealed and instantiates Godot
//   scene trees; here we only need a placeholder type to satisfy field
//   types and method signatures. No instances are ever exercised.
public partial class PackedScene : Resource
{
    public new class MethodName : Resource.MethodName { }
    public new class PropertyName : Resource.PropertyName { }
    public new class SignalName : Resource.SignalName { }

    // from: MegaCrit.Sts2.Core.Nodes.Rooms.NEventRoom.Create
    //   MissingMethodException: "Method not found: '!!0
    //   Godot.PackedScene.Instantiate(GenEditState)'." Real method
    //   instantiates a scene; returning null short-circuits NEventRoom
    //   creation in a way that zeroes Player.Creature.CurrentHp (probed
    //   directly — IsGameOver flips to true after EnterAct). Match sts2-cli
    //   and return `new T()` so downstream code has a non-null node to
    //   read back.
    public enum GenEditState { Disabled = 0, Instance = 1, Main = 2, MainInherited = 3 }

    public T Instantiate<T>(GenEditState _ = GenEditState.Disabled) where T : Node, new() => new T();
}

public partial class ResourceFormatLoader : GodotObject
{
    public new class MethodName : GodotObject.MethodName { }
    public new class PropertyName : GodotObject.PropertyName { }
    public new class SignalName : GodotObject.SignalName { }
}

public partial class ENetConnection : GodotObject
{
    public new class MethodName : GodotObject.MethodName { }
    public new class PropertyName : GodotObject.PropertyName { }
    public new class SignalName : GodotObject.SignalName { }

    public enum EventType { None = 0 }
}

// from: SaveManager.InitProfileId — `just runner::probe::list-members Godot.FileAccess`
//   (17 members). "Empty filesystem" semantics: FileExists/IsOpen false,
//   sizes 0, reads return empty buffers/strings, writes no-op return true.
//   Open returns a fresh instance (rather than null) so write paths don't
//   NPE; the resulting "file" is discarded since nothing reads back.
public partial class FileAccess : GodotObject
{
    public new class MethodName : GodotObject.MethodName { }
    public new class PropertyName : GodotObject.PropertyName { }
    public new class SignalName : GodotObject.SignalName { }

    public enum ModeFlags { Read = 1, Write = 2, ReadWrite = 3, WriteRead = 7 }

    public bool IsOpen() => false;
    public bool StoreBuffer(byte[] _) => true;
    public bool StoreLine(string _) => true;
    public byte[] GetBuffer(long _) => Array.Empty<byte>();
    public Error GetError() => Error.Ok;
    public string GetAsText(bool _) => string.Empty;
    public string GetLine() => string.Empty;
    public ulong GetLength() => 0;
    public ulong GetPosition() => 0;
    public void Close() { }
    public void Flush() { }
    public void Seek(ulong _) { }

    public static bool FileExists(string _) => false;
    public static Error GetOpenError() => Error.Ok;
    public static FileAccess Open(string _, ModeFlags __) => new();
    public static long GetSize(string _) => 0;
    public static ulong GetModifiedTime(string _) => 0;
}

// from: SaveManager.InitProfileId(0) — `just runner::probe::list-members Godot.DirAccess`
//   (15 members). Stubbed as an "empty filesystem": bool false, empty
//   strings/arrays, Error.Ok on writes (caller proceeds as if mkdir
//   succeeded), and Open returns a fresh instance so callers don't NPE
//   on the result. If any path actually reads contents back, override
//   that specific member.
public partial class DirAccess : GodotObject
{
    public new class MethodName : GodotObject.MethodName { }
    public new class PropertyName : GodotObject.PropertyName { }
    public new class SignalName : GodotObject.SignalName { }

    public bool CurrentIsDir() => false;
    public Error ListDirBegin() => Error.Ok;
    public Error Remove(string _) => Error.Ok;
    public string GetNext() => string.Empty;
    public string[] GetDirectories() => Array.Empty<string>();
    public string[] GetFiles() => Array.Empty<string>();
    public bool IncludeHidden { set { } }

    public static bool DirExistsAbsolute(string _) => false;
    public static DirAccess Open(string _) => new();
    public static Error MakeDirAbsolute(string _) => Error.Ok;
    public static Error MakeDirRecursiveAbsolute(string _) => Error.Ok;
    public static Error RemoveAbsolute(string _) => Error.Ok;
    public static Error RenameAbsolute(string _, string __) => Error.Ok;
    public static string[] GetDirectoriesAt(string _) => Array.Empty<string>();
    public static string[] GetFilesAt(string _) => Array.Empty<string>();
}

public partial class RichTextEffect : Resource
{
    public new class MethodName : Resource.MethodName { }
    public new class PropertyName : Resource.PropertyName { }
    public new class SignalName : Resource.SignalName { }
}

// from: MegaCrit.Sts2.Core.HoverTips.HoverTip..ctor — TypeLoadException
//   when Neow event option generation walks CursedPearl.ExtraHoverTips,
//   which constructs HoverTips that hold a Texture2D field. We only need
//   the three members `just runner::probe::list-members Godot.Texture2D` surfaces; no
//   real image data is read.
public partial class Texture2D : Resource
{
    public new class MethodName : Resource.MethodName { }
    public new class PropertyName : Resource.PropertyName { }
    public new class SignalName : Resource.SignalName { }

    public Image GetImage() => new();
    public int GetWidth() => 0;
    public Vector2 GetSize() => Vector2.Zero;
}

// from: MegaCrit.Sts2.Core.HoverTips.HoverTip..ctor (via Texture2D.GetImage).
//   Image exists only as the return type of Texture2D.GetImage(); no
//   pixels are inspected and no members are referenced beyond the
//   constructor.
public partial class Image : Resource
{
    public new class MethodName : Resource.MethodName { }
    public new class PropertyName : Resource.PropertyName { }
    public new class SignalName : Resource.SignalName { }
}
