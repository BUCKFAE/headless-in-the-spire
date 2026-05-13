// Empty type shells matching what sts2.dll's metadata references. Bodies are
// intentionally minimal — these types never run; they exist so the metadata
// loader resolves every TypeRef. Members are added on demand only when a
// caller forces it (runtime MissingMethodException / TypeLoadException).
//
// The MethodName / PropertyName / SignalName pattern mirrors real
// GodotSharp's source-generated layout: every node-like class gets its own
// nested helpers that inherit from the parent's. sts2's own SG-emitted
// user types extend these, so they must exist nested in each Godot class
// that sts2 directly subclasses.
//
// Struct-vs-class is load-bearing — metadata uses ELEMENT_TYPE_VALUETYPE
// vs ELEMENT_TYPE_CLASS and a mismatch fails type resolution. Keep the
// kind matching real GodotSharp.

namespace Godot;

// ── Static helpers ──────────────────────────────────────────────────────

// from: MegaCrit.Sts2.Core.Models.ModelIdSerializationCache.Init
//   (TypeLoadException → MissingMethodException progression during
//    probe-bootstrap.)
// Real Mathf is a static class with dozens of helpers. Members added on
// demand, each with the probe failure that forced it.
public static class Mathf
{
    // from: ModelIdSerializationCache.Init — MissingMethodException
    //   "Method not found: 'Int32 Godot.Mathf.CeilToInt(Double)'."
    public static int CeilToInt(double s) => (int)Math.Ceiling(s);

    // from: MegaCrit.Sts2.Core.Map.StandardActMap.GenerateNextCoord
    //   MissingMethodException during EnterAct's map generation:
    //   "Method not found: 'Int32 Godot.Mathf.Max(Int32, Int32)'."
    public static int Max(int a, int b) => Math.Max(a, b);

    // from: MegaCrit.Sts2.Core.Map.StandardActMap.GenerateNextCoord
    //   MissingMethodException during EnterAct's map generation:
    //   "Method not found: 'Int32 Godot.Mathf.Min(Int32, Int32)'."
    public static int Min(int a, int b) => Math.Min(a, b);
}

// from: every Godot.OS reference in sts2.dll (27 members), enumerated via
//   `just list-members Godot.OS`. Defaults are the smallest safe values
//   (empty string/array, false, 0, Error.Ok, empty Dictionary). If any
//   headless code path needs richer behaviour, override that specific
//   member; do not add members that --list-members didn't surface.
public static class OS
{
    // ── bool returns: default false ──
    public static bool HasFeature(string _) => false;
    public static bool IsDebugBuild() => false;
    public static bool IsInLowProcessorUsageMode() => false;
    public static bool IsSandboxed() => false;
    public static bool IsStdOutVerbose() => false;
    public static bool IsUserfsPersistent() => false;

    // ── int / uint returns ──
    public static int GetProcessorCount() => 0;
    public static ulong GetStaticMemoryPeakUsage() => 0;
    public static ulong GetStaticMemoryUsage() => 0;

    // ── string returns: empty string ──
    public static string GetDataDir() => string.Empty;
    public static string GetDistributionName() => string.Empty;
    public static string GetEnvironment(string _) => string.Empty;
    public static string GetExecutablePath() => string.Empty;
    public static string GetLocale() => string.Empty;
    public static string GetLocaleLanguage() => string.Empty;
    public static string GetModelName() => string.Empty;
    public static string GetName() => string.Empty;
    public static string GetProcessorName() => string.Empty;
    public static string GetUserDataDir() => string.Empty;
    public static string GetVersion() => string.Empty;

    // ── string[] returns ──
    public static string[] GetCmdlineArgs() => Array.Empty<string>();
    public static string[] GetCmdlineUserArgs() => Array.Empty<string>();
    public static string[] GetGrantedPermissions() => Array.Empty<string>();

    // ── other ──
    public static Dictionary GetMemoryInfo() => new();
    public static Error ShellOpen(string _) => Error.Ok;
    public static Error ShellShowInFileManager(string _, bool __) => Error.Ok;
    public static void Crash(string _) { }
}

// from: Godot.OS.GetMemoryInfo() return type (and likely future references).
// Real Godot.Dictionary is a Variant-keyed dict exposed to GDScript; for
// our purposes an empty class is enough — the result is never read in
// headless paths we've reached so far.
public class Dictionary { }

// from: every Godot.GD reference in sts2.dll (8 members), enumerated via
//   `just list-members Godot.GD`. Load<T> returns default (null for ref
//   types); rand helpers return 0. Print* go to stdout, PushError/PrintErr/
//   PushWarning go to stderr with a [godot] prefix — real Godot surfaces
//   these in its log/editor, and silently dropping them masks engine-side
//   complaints that we'd otherwise have to reverse-engineer from symptoms.
public static class GD
{
    public static T Load<T>(string _) => default!;
    public static double RandRange(double _, double __) => 0;
    public static float Randf() => 0;
    // Everything routes to stderr: stdout is the AD-2 NDJSON wire and must
    // never carry log noise. Real Godot displays Print* in the editor/console,
    // which is logically stderr for a headless host. Push*/PrintErr also dump
    // the MegaCrit frames of the current stack so silently-swallowed engine
    // exceptions are recoverable without re-running under a debugger.
    public static void Print(string s) => Console.Error.WriteLine($"[godot] {s}");
    public static void PrintRich(string s) => Console.Error.WriteLine($"[godot] {s}");
    public static void PrintErr(string s) { Console.Error.WriteLine($"[godot:err] {s}"); WriteMegaCritFrames("[godot:err]"); }
    public static void PushError(string s) { Console.Error.WriteLine($"[godot:push-error] {s}"); WriteMegaCritFrames("[godot:push-error]"); }
    public static void PushWarning(string s) => Console.Error.WriteLine($"[godot:push-warning] {s}");

    private static void WriteMegaCritFrames(string prefix)
    {
        foreach (var line in Environment.StackTrace.Split('\n'))
        {
            if (line.Contains("MegaCrit.")) Console.Error.WriteLine($"{prefix}   {line.TrimStart()}");
        }
    }
}

// ── Value types ─────────────────────────────────────────────────────────

public readonly struct Vector2
{
    // from: MegaCrit.Sts2.Core.Nodes.NGame..cctor (and likely siblings)
    //   MissingMethodException during EnterAct: "Method not found:
    //   'Void Godot.Vector2..ctor(Single, Single)'." — static fields on
    //   node classes initialise Vector2 size/position constants. Stored
    //   but never read; no-op body is fine.
    public Vector2(float _, float __) { }

    // from: Neow path during EnterAct (PROBE_NEOW=1) — caught by GD.PushError
    //   "Method not found: 'Godot.Vector2 Godot.Vector2.get_Zero()'."
    //   NEventRoom.Create reads this for initial node positioning. Default
    //   struct value is the correct zero.
    public static Vector2 Zero => default;
}
public readonly struct Vector2I { }
public readonly struct Vector3 { }
public readonly struct Rect2 { }
public readonly struct Color
{
    // from: 19 ModelDb subtypes (Defect, BouncingFlask, DeprecatedCharacter, …)
    //   MissingMethodException during Inject: "Method not found:
    //   'Void Godot.Color..ctor(System.String)'." — model cctors build
    //   colors from hex strings. Body is a no-op; nothing on this stub is
    //   actually read.
    public Color(string _) { }
}
public readonly struct Variant { }
public readonly struct Callable { }

// ── Enums ───────────────────────────────────────────────────────────────
// Real Godot enums have many values; we declare a placeholder until a
// caller actually compares against one.

public enum Error { Ok = 0 }
public enum Key { None = 0 }

// ── Object hierarchy ────────────────────────────────────────────────────
// Every class redeclares MethodName/PropertyName/SignalName via `new class
// : Parent.X`. That mirrors how real GodotSharp's SG emits them, and is
// what lets sts2's user types extend them by short name.

public class GodotObject
{
    public class MethodName { }
    public class PropertyName { }
    public class SignalName { }
}

// from: MegaCrit.Sts2.Core.Nodes.Rooms.NEventRoom.Create
//   TypeLoadException during EnterAct's Neow room entry: "Could not load
//   type 'Godot.PackedScene'." Real type is sealed and instantiates Godot
//   scene trees; here we only need a placeholder type to satisfy field
//   types and method signatures. No instances are ever exercised.
public class PackedScene : Resource
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

public class Resource : GodotObject
{
    public new class MethodName : GodotObject.MethodName { }
    public new class PropertyName : GodotObject.PropertyName { }
    public new class SignalName : GodotObject.SignalName { }
}

public class ResourceFormatLoader : GodotObject
{
    public new class MethodName : GodotObject.MethodName { }
    public new class PropertyName : GodotObject.PropertyName { }
    public new class SignalName : GodotObject.SignalName { }
}

public class ENetConnection : GodotObject
{
    public new class MethodName : GodotObject.MethodName { }
    public new class PropertyName : GodotObject.PropertyName { }
    public new class SignalName : GodotObject.SignalName { }

    public enum EventType { None = 0 }
}

// from: SaveManager.InitProfileId — `just list-members Godot.FileAccess`
//   (17 members). "Empty filesystem" semantics: FileExists/IsOpen false,
//   sizes 0, reads return empty buffers/strings, writes no-op return true.
//   Open returns a fresh instance (rather than null) so write paths don't
//   NPE; the resulting "file" is discarded since nothing reads back.
public class FileAccess : GodotObject
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

// from: SaveManager.InitProfileId(0) — `just list-members Godot.DirAccess`
//   (15 members). Stubbed as an "empty filesystem": bool false, empty
//   strings/arrays, Error.Ok on writes (caller proceeds as if mkdir
//   succeeded), and Open returns a fresh instance so callers don't NPE
//   on the result. If any path actually reads contents back, override
//   that specific member.
public class DirAccess : GodotObject
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

public class RichTextEffect : Resource
{
    public new class MethodName : Resource.MethodName { }
    public new class PropertyName : Resource.PropertyName { }
    public new class SignalName : Resource.SignalName { }
}

public class Node : GodotObject
{
    public new class MethodName : GodotObject.MethodName { }
    public new class PropertyName : GodotObject.PropertyName { }
    public new class SignalName : GodotObject.SignalName { }
}

public class CanvasItem : Node
{
    public new class MethodName : Node.MethodName { }
    public new class PropertyName : Node.PropertyName { }
    public new class SignalName : Node.SignalName { }
}

// ── Node2D and descendants ──────────────────────────────────────────────

public class Node2D : CanvasItem
{
    public new class MethodName : CanvasItem.MethodName { }
    public new class PropertyName : CanvasItem.PropertyName { }
    public new class SignalName : CanvasItem.SignalName { }

    // from: Neow path during EnterAct (PROBE_NEOW=1) — caught by GD.PushError
    //   "Method not found: 'Void Godot.Node2D.set_Position(Godot.Vector2)'."
    //   NEventRoom places child nodes at a Position. Auto-property is
    //   enough; nothing reads it back in headless paths.
    public Vector2 Position { get; set; }
}

public class Sprite2D : Node2D
{
    public new class MethodName : Node2D.MethodName { }
    public new class PropertyName : Node2D.PropertyName { }
    public new class SignalName : Node2D.SignalName { }
}

public class GpuParticles2D : Node2D
{
    public new class MethodName : Node2D.MethodName { }
    public new class PropertyName : Node2D.PropertyName { }
    public new class SignalName : Node2D.SignalName { }
}

public class CpuParticles2D : Node2D
{
    public new class MethodName : Node2D.MethodName { }
    public new class PropertyName : Node2D.PropertyName { }
    public new class SignalName : Node2D.SignalName { }
}

public class Line2D : Node2D
{
    public new class MethodName : Node2D.MethodName { }
    public new class PropertyName : Node2D.PropertyName { }
    public new class SignalName : Node2D.SignalName { }
}

public class PathFollow2D : Node2D
{
    public new class MethodName : Node2D.MethodName { }
    public new class PropertyName : Node2D.PropertyName { }
    public new class SignalName : Node2D.SignalName { }
}

public class BackBufferCopy : Node2D
{
    public new class MethodName : Node2D.MethodName { }
    public new class PropertyName : Node2D.PropertyName { }
    public new class SignalName : Node2D.SignalName { }
}

// from: MegaCrit.Sts2.Core.Nodes.NGame..cctor
//   TypeLoadException during EnterAct: "Could not load type 'Godot.Window'".
//   Static field on the node class is typed as Window; never accessed via
//   real Godot APIs at runtime in headless. Empty placeholder is enough.
public class Window : Node
{
    public new class MethodName : Node.MethodName { }
    public new class PropertyName : Node.PropertyName { }
    public new class SignalName : Node.SignalName { }
}

// ── Control and descendants ─────────────────────────────────────────────

public class Control : CanvasItem
{
    public new class MethodName : CanvasItem.MethodName { }
    public new class PropertyName : CanvasItem.PropertyName { }
    public new class SignalName : CanvasItem.SignalName { }
}

public class Range : Control
{
    public new class MethodName : Control.MethodName { }
    public new class PropertyName : Control.PropertyName { }
    public new class SignalName : Control.SignalName { }
}

public class Label : Control
{
    public new class MethodName : Control.MethodName { }
    public new class PropertyName : Control.PropertyName { }
    public new class SignalName : Control.SignalName { }
}

public class LineEdit : Control
{
    public new class MethodName : Control.MethodName { }
    public new class PropertyName : Control.PropertyName { }
    public new class SignalName : Control.SignalName { }
}

public class TextEdit : Control
{
    public new class MethodName : Control.MethodName { }
    public new class PropertyName : Control.PropertyName { }
    public new class SignalName : Control.SignalName { }
}

public class Panel : Control
{
    public new class MethodName : Control.MethodName { }
    public new class PropertyName : Control.PropertyName { }
    public new class SignalName : Control.SignalName { }
}

public class ColorRect : Control
{
    public new class MethodName : Control.MethodName { }
    public new class PropertyName : Control.PropertyName { }
    public new class SignalName : Control.SignalName { }
}

public class TextureRect : Control
{
    public new class MethodName : Control.MethodName { }
    public new class PropertyName : Control.PropertyName { }
    public new class SignalName : Control.SignalName { }
}

public class RichTextLabel : Control
{
    public new class MethodName : Control.MethodName { }
    public new class PropertyName : Control.PropertyName { }
    public new class SignalName : Control.SignalName { }
}

public class Container : Control
{
    public new class MethodName : Control.MethodName { }
    public new class PropertyName : Control.PropertyName { }
    public new class SignalName : Control.SignalName { }
}

public class AspectRatioContainer : Container
{
    public new class MethodName : Container.MethodName { }
    public new class PropertyName : Container.PropertyName { }
    public new class SignalName : Container.SignalName { }
}

public class MarginContainer : Container
{
    public new class MethodName : Container.MethodName { }
    public new class PropertyName : Container.PropertyName { }
    public new class SignalName : Container.SignalName { }
}

public class FlowContainer : Container
{
    public new class MethodName : Container.MethodName { }
    public new class PropertyName : Container.PropertyName { }
    public new class SignalName : Container.SignalName { }
}

public class BoxContainer : Container
{
    public new class MethodName : Container.MethodName { }
    public new class PropertyName : Container.PropertyName { }
    public new class SignalName : Container.SignalName { }
}

public class VBoxContainer : BoxContainer
{
    public new class MethodName : BoxContainer.MethodName { }
    public new class PropertyName : BoxContainer.PropertyName { }
    public new class SignalName : BoxContainer.SignalName { }
}

public class HBoxContainer : BoxContainer
{
    public new class MethodName : BoxContainer.MethodName { }
    public new class PropertyName : BoxContainer.PropertyName { }
    public new class SignalName : BoxContainer.SignalName { }
}
