// Extra Godot types pulled in by sts2's combat-room load path.
//
// from: MegaCrit.Sts2.Core.Combat.CombatManager.AfterCombatRoomLoaded —
//   entering a CombatRoom queues animation/audio/control playback through
//   tween chains. sts2's metadata references the whole bag, so type-load
//   for the combat path fails until every referenced type exists somewhere
//   in this assembly. None of these are *called* in headless mode (the
//   tween chain is no-op'd and the action queue drains synchronously);
//   they just need to load.
//
// Members are added only when sts2.dll forces it (the same rule used in
// Nodes.cs / Resources.cs / Controls.cs). Inheritance follows Godot 4.x
// upstream so casts and `is Foo` checks resolve correctly.

namespace Godot;

// ── Control-tree containers (siblings of HBox/VBoxContainer) ─────────────
public partial class GridContainer : Container
{
    public new class MethodName : Container.MethodName { }
    public new class PropertyName : Container.PropertyName { }
    public new class SignalName : Container.SignalName { }
}

public partial class VFlowContainer : FlowContainer
{
    public new class MethodName : FlowContainer.MethodName { }
    public new class PropertyName : FlowContainer.PropertyName { }
    public new class SignalName : FlowContainer.SignalName { }
}

public partial class NinePatchRect : Control
{
    public new class MethodName : Control.MethodName { }
    public new class PropertyName : Control.PropertyName { }
    public new class SignalName : Control.SignalName { }
}

public partial class BaseButton : Control
{
    public new class MethodName : Control.MethodName { }
    public new class PropertyName : Control.PropertyName { }
    public new class SignalName : Control.SignalName
    {
        public static readonly StringName ButtonUp = new("button_up");
    }
}

public partial class Button : BaseButton
{
    public new class MethodName : BaseButton.MethodName { }
    public new class PropertyName : BaseButton.PropertyName { }
    public new class SignalName : BaseButton.SignalName { }
}

public partial class FileDialog : Window
{
    public new class MethodName : Window.MethodName { }
    public new class PropertyName : Window.PropertyName { }
    public new class SignalName : Window.SignalName
    {
        public static readonly StringName FileSelected = new("file_selected");
    }
}

public partial class SubViewportContainer : Container
{
    public new class MethodName : Container.MethodName { }
    public new class PropertyName : Container.PropertyName { }
    public new class SignalName : Container.SignalName { }
}

// ── Node-tree (non-Control) ──────────────────────────────────────────────
public partial class Viewport : Node
{
    public new class MethodName : Node.MethodName { }
    public new class PropertyName : Node.PropertyName { }
    public new class SignalName : Node.SignalName
    {
        public static readonly StringName GuiFocusChanged = new("gui_focus_changed");
        public static readonly StringName SizeChanged = new("size_changed");
    }

    public Rect2 GetVisibleRect() => default;
}

public partial class SubViewport : Viewport
{
    public new class MethodName : Viewport.MethodName { }
    public new class PropertyName : Viewport.PropertyName { }
    public new class SignalName : Viewport.SignalName { }
}

public partial class Timer : Node
{
    public new class MethodName : Node.MethodName { }
    public new class PropertyName : Node.PropertyName { }
    public new class SignalName : Node.SignalName
    {
        public static readonly StringName Timeout = new("timeout");
    }
}

public partial class SceneTreeTimer : GodotObject
{
    public new class MethodName : GodotObject.MethodName { }
    public new class PropertyName : GodotObject.PropertyName { }
    public new class SignalName : GodotObject.SignalName
    {
        public static readonly StringName Timeout = new("timeout");
    }
}

public partial class SceneTree : MainLoop
{
    public new class MethodName : MainLoop.MethodName { }
    public new class PropertyName : MainLoop.PropertyName { }
    public new class SignalName : MainLoop.SignalName
    {
        public static readonly StringName ProcessFrame = new("process_frame");
    }

    // from: NDamageNumVfx.Create + AutoSlay/CardPileCmd cast SceneTree.Root
    //   to Node and walk into .GetViewport(). A null Root NREs every caller;
    //   a singleton Window satisfies the chain (nothing reads back).
    private static readonly Window _root = new();
    public Window? Root => _root;
    public SceneTreeTimer CreateTimer(double _) => new();
}

public partial class AnimationMixer : Node
{
    public new class MethodName : Node.MethodName { }
    public new class PropertyName : Node.PropertyName { }
    public new class SignalName : Node.SignalName
    {
        public static readonly StringName AnimationFinished = new("animation_finished");
    }
}

public partial class AnimationPlayer : AnimationMixer
{
    public new class MethodName : AnimationMixer.MethodName { }
    public new class PropertyName : AnimationMixer.PropertyName { }
    public new class SignalName : AnimationMixer.SignalName { }
}

public partial class AudioStreamPlayer : Node
{
    public new class MethodName : Node.MethodName { }
    public new class PropertyName : Node.PropertyName { }
    public new class SignalName : Node.SignalName
    {
        public static readonly StringName Finished = new("finished");
    }
}

public partial class WorldEnvironment : Node
{
    public new class MethodName : Node.MethodName { }
    public new class PropertyName : Node.PropertyName { }
    public new class SignalName : Node.SignalName { }
}

public partial class CanvasGroup : Node2D
{
    public new class MethodName : Node2D.MethodName { }
    public new class PropertyName : Node2D.PropertyName { }
    public new class SignalName : Node2D.SignalName { }
}

public partial class Marker2D : Node2D
{
    public new class MethodName : Node2D.MethodName { }
    public new class PropertyName : Node2D.PropertyName { }
    public new class SignalName : Node2D.SignalName { }
}

public partial class Path2D : Node2D
{
    public new class MethodName : Node2D.MethodName { }
    public new class PropertyName : Node2D.PropertyName { }
    public new class SignalName : Node2D.SignalName { }
}

// ── Resource-tree ────────────────────────────────────────────────────────
public partial class Material : Resource
{
    public new class MethodName : Resource.MethodName { }
    public new class PropertyName : Resource.PropertyName { }
    public new class SignalName : Resource.SignalName { }
}

public partial class CanvasItemMaterial : Material
{
    public new class MethodName : Material.MethodName { }
    public new class PropertyName : Material.PropertyName { }
    public new class SignalName : Material.SignalName { }
}

public partial class ShaderMaterial : Material
{
    public new class MethodName : Material.MethodName { }
    public new class PropertyName : Material.PropertyName { }
    public new class SignalName : Material.SignalName { }
}

public partial class ParticleProcessMaterial : Material
{
    public new class MethodName : Material.MethodName { }
    public new class PropertyName : Material.PropertyName { }
    public new class SignalName : Material.SignalName { }

    public Vector3 EmissionBoxExtents { get; set; }
}

public partial class StyleBox : Resource
{
    public new class MethodName : Resource.MethodName { }
    public new class PropertyName : Resource.PropertyName { }
    public new class SignalName : Resource.SignalName { }
}

public partial class StyleBoxEmpty : StyleBox
{
    public new class MethodName : StyleBox.MethodName { }
    public new class PropertyName : StyleBox.PropertyName { }
    public new class SignalName : StyleBox.SignalName { }
}

public partial class Font : Resource
{
    public new class MethodName : Resource.MethodName { }
    public new class PropertyName : Resource.PropertyName { }
    public new class SignalName : Resource.SignalName { }
}

public partial class TextParagraph : RefCounted
{
    public new class MethodName : RefCounted.MethodName { }
    public new class PropertyName : RefCounted.PropertyName { }
    public new class SignalName : RefCounted.SignalName { }
}

public partial class RefCounted : GodotObject
{
    public new class MethodName : GodotObject.MethodName { }
    public new class PropertyName : GodotObject.PropertyName { }
    public new class SignalName : GodotObject.SignalName { }
}

public partial class CompressedTexture2D : Texture2D
{
    public new class MethodName : Texture2D.MethodName { }
    public new class PropertyName : Texture2D.PropertyName { }
    public new class SignalName : Texture2D.SignalName { }
}

public partial class AtlasTexture : Texture2D
{
    public new class MethodName : Texture2D.MethodName { }
    public new class PropertyName : Texture2D.PropertyName { }
    public new class SignalName : Texture2D.SignalName { }
}

public partial class ImageTexture : Texture2D
{
    public new class MethodName : Texture2D.MethodName { }
    public new class PropertyName : Texture2D.PropertyName { }
    public new class SignalName : Texture2D.SignalName { }
}

public partial class ViewportTexture : Texture2D
{
    public new class MethodName : Texture2D.MethodName { }
    public new class PropertyName : Texture2D.PropertyName { }
    public new class SignalName : Texture2D.SignalName { }
}

public partial class Curve : Resource
{
    public new class MethodName : Resource.MethodName { }
    public new class PropertyName : Resource.PropertyName { }
    public new class SignalName : Resource.SignalName { }
}

public partial class Curve2D : Resource
{
    public new class MethodName : Resource.MethodName { }
    public new class PropertyName : Resource.PropertyName { }
    public new class SignalName : Resource.SignalName { }
}

public partial class Gradient : Resource
{
    public new class MethodName : Resource.MethodName { }
    public new class PropertyName : Resource.PropertyName { }
    public new class SignalName : Resource.SignalName { }
}

public partial class GradientTexture2D : Texture2D
{
    public new class MethodName : Texture2D.MethodName { }
    public new class PropertyName : Texture2D.PropertyName { }
    public new class SignalName : Texture2D.SignalName { }
}

public partial class Noise : Resource
{
    public new class MethodName : Resource.MethodName { }
    public new class PropertyName : Resource.PropertyName { }
    public new class SignalName : Resource.SignalName { }
}

public partial class FastNoiseLite : Noise
{
    public new class MethodName : Noise.MethodName { }
    public new class PropertyName : Noise.PropertyName { }
    public new class SignalName : Noise.SignalName { }
}

public partial class NoiseTexture2D : Texture2D
{
    public new class MethodName : Texture2D.MethodName { }
    public new class PropertyName : Texture2D.PropertyName { }
    public new class SignalName : Texture2D.SignalName { }
}

public partial class AudioStream : Resource
{
    public new class MethodName : Resource.MethodName { }
    public new class PropertyName : Resource.PropertyName { }
    public new class SignalName : Resource.SignalName { }
}

public partial class CharFXTransform : RefCounted
{
    public new class MethodName : RefCounted.MethodName { }
    public new class PropertyName : RefCounted.PropertyName { }
    public new class SignalName : RefCounted.SignalName { }
}

// ── InputEvent hierarchy ─────────────────────────────────────────────────
public partial class InputEvent : Resource
{
    public new class MethodName : Resource.MethodName { }
    public new class PropertyName : Resource.PropertyName { }
    public new class SignalName : Resource.SignalName { }
}

public partial class InputEventAction : InputEvent
{
    public new class MethodName : InputEvent.MethodName { }
    public new class PropertyName : InputEvent.PropertyName { }
    public new class SignalName : InputEvent.SignalName { }
}

public partial class InputEventWithModifiers : InputEvent
{
    public new class MethodName : InputEvent.MethodName { }
    public new class PropertyName : InputEvent.PropertyName { }
    public new class SignalName : InputEvent.SignalName { }
}

public partial class InputEventKey : InputEventWithModifiers
{
    public new class MethodName : InputEventWithModifiers.MethodName { }
    public new class PropertyName : InputEventWithModifiers.PropertyName { }
    public new class SignalName : InputEventWithModifiers.SignalName { }
}

public partial class InputEventMouse : InputEventWithModifiers
{
    public new class MethodName : InputEventWithModifiers.MethodName { }
    public new class PropertyName : InputEventWithModifiers.PropertyName { }
    public new class SignalName : InputEventWithModifiers.SignalName { }
}

public partial class InputEventMouseButton : InputEventMouse
{
    public new class MethodName : InputEventMouse.MethodName { }
    public new class PropertyName : InputEventMouse.PropertyName { }
    public new class SignalName : InputEventMouse.SignalName { }
}

public partial class InputEventMouseMotion : InputEventMouse
{
    public new class MethodName : InputEventMouse.MethodName { }
    public new class PropertyName : InputEventMouse.PropertyName { }
    public new class SignalName : InputEventMouse.SignalName { }
}

public partial class InputEventJoypadMotion : InputEvent
{
    public new class MethodName : InputEvent.MethodName { }
    public new class PropertyName : InputEvent.PropertyName { }
    public new class SignalName : InputEvent.SignalName { }
}

public partial class InputEventPanGesture : InputEventWithModifiers
{
    public new class MethodName : InputEventWithModifiers.MethodName { }
    public new class PropertyName : InputEventWithModifiers.PropertyName { }
    public new class SignalName : InputEventWithModifiers.SignalName { }
}

// ── Network ──────────────────────────────────────────────────────────────
public partial class PacketPeer : RefCounted
{
    public new class MethodName : RefCounted.MethodName { }
    public new class PropertyName : RefCounted.PropertyName { }
    public new class SignalName : RefCounted.SignalName { }
}

public partial class ENetPacketPeer : PacketPeer
{
    public new class MethodName : PacketPeer.MethodName { }
    public new class PropertyName : PacketPeer.PropertyName { }
    public new class SignalName : PacketPeer.SignalName { }
}

// ── Static singletons / servers (no instances created in headless) ──────
public static partial class Input { }
public static partial class AudioServer { public class AudioServerInstance { } }
public partial class AudioServerInstance { }
public static partial class ClassDB { }
// from: enemy moves (e.g. SHRINKER_BEETLE.SHRINKER_MOVE) read named color
//   constants when staging VFX tints. MissingMethodException surfaces inside
//   the move's async chain and gets swallowed by TaskHelper.LogTaskExceptions,
//   leaving the enemy turn half-transitioned (the combat-stall pattern).
//   The set below is the closed surface — `just list-members Godot.Colors`
//   confirms sts2.dll references exactly these 14 getters and no others.
//   `GodotStubsCoverageTests.Color_And_Colors_References_From_Sts2_Resolve`
//   pins the surface so a game-version bump that adds a new named color
//   surfaces as a red unit test, not as a runtime MissingMethodException.
//   Body always returns `default` — color data is never read in headless paths.
public static partial class Colors
{
    public static Color Green => default;
    public static Color White => default;
    public static Color Red => default;
    public static Color Blue => default;
    public static Color Black => default;
    public static Color Cyan => default;
    public static Color DarkGray => default;
    public static Color DarkRed => default;
    public static Color DimGray => default;
    public static Color Gold => default;
    public static Color Gray => default;
    public static Color Magenta => default;
    public static Color Purple => default;
    public static Color Transparent => default;
}
public static partial class DisplayServer { }
public static partial class Geometry2D { }
public static partial class Performance { }
// from: MegaCrit.Sts2.Core.Debug.ReleaseInfoManager.LoadConfig
//   MissingMethodException: "Method not found: 'System.String
//   Godot.ProjectSettings.GlobalizePath(System.String)'."
//   Maps a Godot `res://` / `user://` virtual path to a filesystem path.
//   In our headless context virtual paths don't resolve to anything real
//   (FileAccess.FileExists is false for everything), so the identity
//   function is enough — the caller iterates a candidate list and gives
//   up. ReleaseInfo stays null, which ReplayHeaderFactory degrades to
//   "UNKNOWN" git commit.
public static partial class ProjectSettings
{
    public static string GlobalizePath(string path) => path;
}
public static partial class RenderingDevice { }
public static partial class RenderingServer { }

public static partial class ResourceLoader
{
    public enum CacheMode { Ignore = 0, Reuse = 1, Replace = 2, IgnoreDeep = 3, ReplaceDeep = 4 }
    public static T? Load<T>(string _, string __ = "", CacheMode ___ = CacheMode.Reuse) where T : class => null;
    public static Resource? Load(string _, string __ = "", CacheMode ___ = CacheMode.Reuse) => null;
    public static bool Exists(string _, string __ = "") => false;
}
public static partial class StringExtensions { }
public static partial class TextServer { }
public static partial class TextServerManager { public class TextServerManagerInstance { } }
public partial class TextServerManagerInstance { }
public static partial class Time
{
    private static readonly System.Diagnostics.Stopwatch _sw = System.Diagnostics.Stopwatch.StartNew();
    public static ulong GetTicksMsec() => (ulong)_sw.ElapsedMilliseconds;
    public static ulong GetTicksUsec() => (ulong)(_sw.ElapsedTicks * 1_000_000L / System.Diagnostics.Stopwatch.Frequency);
}
public static partial class TranslationServer { }

// ── Enums (header-only; values backfilled when sts2 reads them back) ────
public enum HorizontalAlignment { Left = 0, Center = 1, Right = 2, Fill = 3 }
public enum VerticalAlignment { Top = 0, Center = 1, Bottom = 2, Fill = 3 }
public enum InlineAlignment { TopTo = 0, CenterTo = 1, BaselineTo = 3, BottomTo = 2, ToTop = 0, ToCenter = 4, ToBaseline = 8, ToBottom = 12, Top = 0, Center = 5, Baseline = 9, Bottom = 14 }
public enum Side { Left = 0, Top = 1, Right = 2, Bottom = 3 }
public enum MouseButton { None = 0, Left = 1, Right = 2, Middle = 3, WheelUp = 4, WheelDown = 5, WheelLeft = 6, WheelRight = 7 }
public enum JoyAxis { Invalid = -1, LeftX = 0, LeftY = 1, RightX = 2, RightY = 3, TriggerLeft = 4, TriggerRight = 5 }
public enum MethodFlags { Normal = 1, Editor = 2, Const = 4, Virtual = 8, VarArg = 16, Static = 32, ObjectCore = 64, Default = 1 }
public enum PropertyHint { None = 0, Range = 1, Enum = 2, ExpEasing = 4, Length = 5, KeyAccel = 7 }
public enum PropertyUsageFlags : long { None = 0, Storage = 2, Editor = 4, Internal = 8, Checkable = 16, Checked = 32, Group = 64, Category = 128, Subgroup = 256, ClassIsBitfield = 512, NoInstanceState = 1024, RestartIfChanged = 2048, ScriptVariable = 4096, StoreIfNull = 8192, UpdateAllIfModified = 16384, ScriptDefault = 32768, ClassIsEnum = 65536, NilIsVariant = 131072, ArrayMaxSize = 262144, ReadOnly = 524288, Secret = 1048576, AlwaysDuplicate = 2097152, NeverDuplicate = 4194304, HighEndGfx = 8388608, NodePathFromSceneRoot = 16777216, ResourceNotPersistent = 33554432, KeyingIncrements = 67108864, DeferredSetResource = 134217728, EditorInstantiateObject = 268435456, EditorBasicSetting = 536870912, ReadOnlyInEditor = 1073741824, ArrayOfResources = 2147483648L, Default = Storage | Editor }

// ── Value-shaped Godot types used as struct fields by sts2 ──────────────
public readonly partial struct Quaternion
{
    public Quaternion(float _, float __, float ___, float ____) { }
}

public readonly partial struct Transform2D
{
    public Transform2D(float _, Vector2 __) { }
}

public readonly partial struct Rid { }
public readonly partial struct Signal { public Signal(GodotObject _, StringName __) { } }

// ── Attributes (presence-only; never inspected reflectively in headless) ─
[System.AttributeUsage(System.AttributeTargets.Assembly)]
public sealed partial class AssemblyHasScriptsAttribute : System.Attribute { }

[System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Field)]
public sealed partial class ExportAttribute : System.Attribute
{
    public ExportAttribute(PropertyHint _ = PropertyHint.None, string __ = "") { }
}

[System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Field, AllowMultiple = true)]
public sealed partial class ExportGroupAttribute : System.Attribute { public ExportGroupAttribute(string _, string __ = "") { } }

[System.AttributeUsage(System.AttributeTargets.Method)]
public sealed partial class ExportToolButtonAttribute : System.Attribute { public ExportToolButtonAttribute(string _, string __ = "") { } }

[System.AttributeUsage(System.AttributeTargets.Class)]
public sealed partial class GlobalClassAttribute : System.Attribute { }

[System.AttributeUsage(System.AttributeTargets.Class)]
public sealed partial class ScriptPathAttribute : System.Attribute { public ScriptPathAttribute(string _) { } }

[System.AttributeUsage(System.AttributeTargets.Event | System.AttributeTargets.Method, AllowMultiple = true)]
public sealed partial class SignalAttribute : System.Attribute { }

[System.AttributeUsage(System.AttributeTargets.Class)]
public sealed partial class ToolAttribute : System.Attribute { }
