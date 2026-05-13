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
public class GridContainer : Container
{
    public new class MethodName : Container.MethodName { }
    public new class PropertyName : Container.PropertyName { }
    public new class SignalName : Container.SignalName { }
}

public class VFlowContainer : FlowContainer
{
    public new class MethodName : FlowContainer.MethodName { }
    public new class PropertyName : FlowContainer.PropertyName { }
    public new class SignalName : FlowContainer.SignalName { }
}

public class NinePatchRect : Control
{
    public new class MethodName : Control.MethodName { }
    public new class PropertyName : Control.PropertyName { }
    public new class SignalName : Control.SignalName { }
}

public class BaseButton : Control
{
    public new class MethodName : Control.MethodName { }
    public new class PropertyName : Control.PropertyName { }
    public new class SignalName : Control.SignalName
    {
        public static readonly StringName ButtonUp = new("button_up");
    }
}

public class Button : BaseButton
{
    public new class MethodName : BaseButton.MethodName { }
    public new class PropertyName : BaseButton.PropertyName { }
    public new class SignalName : BaseButton.SignalName { }
}

public class FileDialog : Window
{
    public new class MethodName : Window.MethodName { }
    public new class PropertyName : Window.PropertyName { }
    public new class SignalName : Window.SignalName
    {
        public static readonly StringName FileSelected = new("file_selected");
    }
}

public class SubViewportContainer : Container
{
    public new class MethodName : Container.MethodName { }
    public new class PropertyName : Container.PropertyName { }
    public new class SignalName : Container.SignalName { }
}

// ── Node-tree (non-Control) ──────────────────────────────────────────────
public class Viewport : Node
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

public class SubViewport : Viewport
{
    public new class MethodName : Viewport.MethodName { }
    public new class PropertyName : Viewport.PropertyName { }
    public new class SignalName : Viewport.SignalName { }
}

public class Timer : Node
{
    public new class MethodName : Node.MethodName { }
    public new class PropertyName : Node.PropertyName { }
    public new class SignalName : Node.SignalName
    {
        public static readonly StringName Timeout = new("timeout");
    }
}

public class SceneTreeTimer : GodotObject
{
    public new class MethodName : GodotObject.MethodName { }
    public new class PropertyName : GodotObject.PropertyName { }
    public new class SignalName : GodotObject.SignalName
    {
        public static readonly StringName Timeout = new("timeout");
    }
}

public class SceneTree : MainLoop
{
    public new class MethodName : MainLoop.MethodName { }
    public new class PropertyName : MainLoop.PropertyName { }
    public new class SignalName : MainLoop.SignalName
    {
        public static readonly StringName ProcessFrame = new("process_frame");
    }

    public Window? Root => null;
    public SceneTreeTimer CreateTimer(double _) => new();
}

public class AnimationMixer : Node
{
    public new class MethodName : Node.MethodName { }
    public new class PropertyName : Node.PropertyName { }
    public new class SignalName : Node.SignalName
    {
        public static readonly StringName AnimationFinished = new("animation_finished");
    }
}

public class AnimationPlayer : AnimationMixer
{
    public new class MethodName : AnimationMixer.MethodName { }
    public new class PropertyName : AnimationMixer.PropertyName { }
    public new class SignalName : AnimationMixer.SignalName { }
}

public class AudioStreamPlayer : Node
{
    public new class MethodName : Node.MethodName { }
    public new class PropertyName : Node.PropertyName { }
    public new class SignalName : Node.SignalName
    {
        public static readonly StringName Finished = new("finished");
    }
}

public class WorldEnvironment : Node
{
    public new class MethodName : Node.MethodName { }
    public new class PropertyName : Node.PropertyName { }
    public new class SignalName : Node.SignalName { }
}

public class CanvasGroup : Node2D
{
    public new class MethodName : Node2D.MethodName { }
    public new class PropertyName : Node2D.PropertyName { }
    public new class SignalName : Node2D.SignalName { }
}

public class Marker2D : Node2D
{
    public new class MethodName : Node2D.MethodName { }
    public new class PropertyName : Node2D.PropertyName { }
    public new class SignalName : Node2D.SignalName { }
}

public class Path2D : Node2D
{
    public new class MethodName : Node2D.MethodName { }
    public new class PropertyName : Node2D.PropertyName { }
    public new class SignalName : Node2D.SignalName { }
}

// ── Resource-tree ────────────────────────────────────────────────────────
public class Material : Resource
{
    public new class MethodName : Resource.MethodName { }
    public new class PropertyName : Resource.PropertyName { }
    public new class SignalName : Resource.SignalName { }
}

public class CanvasItemMaterial : Material
{
    public new class MethodName : Material.MethodName { }
    public new class PropertyName : Material.PropertyName { }
    public new class SignalName : Material.SignalName { }
}

public class ShaderMaterial : Material
{
    public new class MethodName : Material.MethodName { }
    public new class PropertyName : Material.PropertyName { }
    public new class SignalName : Material.SignalName { }
}

public class ParticleProcessMaterial : Material
{
    public new class MethodName : Material.MethodName { }
    public new class PropertyName : Material.PropertyName { }
    public new class SignalName : Material.SignalName { }
}

public class StyleBox : Resource
{
    public new class MethodName : Resource.MethodName { }
    public new class PropertyName : Resource.PropertyName { }
    public new class SignalName : Resource.SignalName { }
}

public class StyleBoxEmpty : StyleBox
{
    public new class MethodName : StyleBox.MethodName { }
    public new class PropertyName : StyleBox.PropertyName { }
    public new class SignalName : StyleBox.SignalName { }
}

public class Font : Resource
{
    public new class MethodName : Resource.MethodName { }
    public new class PropertyName : Resource.PropertyName { }
    public new class SignalName : Resource.SignalName { }
}

public class TextParagraph : RefCounted
{
    public new class MethodName : RefCounted.MethodName { }
    public new class PropertyName : RefCounted.PropertyName { }
    public new class SignalName : RefCounted.SignalName { }
}

public class RefCounted : GodotObject
{
    public new class MethodName : GodotObject.MethodName { }
    public new class PropertyName : GodotObject.PropertyName { }
    public new class SignalName : GodotObject.SignalName { }
}

public class CompressedTexture2D : Texture2D
{
    public new class MethodName : Texture2D.MethodName { }
    public new class PropertyName : Texture2D.PropertyName { }
    public new class SignalName : Texture2D.SignalName { }
}

public class AtlasTexture : Texture2D
{
    public new class MethodName : Texture2D.MethodName { }
    public new class PropertyName : Texture2D.PropertyName { }
    public new class SignalName : Texture2D.SignalName { }
}

public class ImageTexture : Texture2D
{
    public new class MethodName : Texture2D.MethodName { }
    public new class PropertyName : Texture2D.PropertyName { }
    public new class SignalName : Texture2D.SignalName { }
}

public class ViewportTexture : Texture2D
{
    public new class MethodName : Texture2D.MethodName { }
    public new class PropertyName : Texture2D.PropertyName { }
    public new class SignalName : Texture2D.SignalName { }
}

public class Curve : Resource
{
    public new class MethodName : Resource.MethodName { }
    public new class PropertyName : Resource.PropertyName { }
    public new class SignalName : Resource.SignalName { }
}

public class Curve2D : Resource
{
    public new class MethodName : Resource.MethodName { }
    public new class PropertyName : Resource.PropertyName { }
    public new class SignalName : Resource.SignalName { }
}

public class Gradient : Resource
{
    public new class MethodName : Resource.MethodName { }
    public new class PropertyName : Resource.PropertyName { }
    public new class SignalName : Resource.SignalName { }
}

public class GradientTexture2D : Texture2D
{
    public new class MethodName : Texture2D.MethodName { }
    public new class PropertyName : Texture2D.PropertyName { }
    public new class SignalName : Texture2D.SignalName { }
}

public class Noise : Resource
{
    public new class MethodName : Resource.MethodName { }
    public new class PropertyName : Resource.PropertyName { }
    public new class SignalName : Resource.SignalName { }
}

public class FastNoiseLite : Noise
{
    public new class MethodName : Noise.MethodName { }
    public new class PropertyName : Noise.PropertyName { }
    public new class SignalName : Noise.SignalName { }
}

public class NoiseTexture2D : Texture2D
{
    public new class MethodName : Texture2D.MethodName { }
    public new class PropertyName : Texture2D.PropertyName { }
    public new class SignalName : Texture2D.SignalName { }
}

public class AudioStream : Resource
{
    public new class MethodName : Resource.MethodName { }
    public new class PropertyName : Resource.PropertyName { }
    public new class SignalName : Resource.SignalName { }
}

public class CharFXTransform : RefCounted
{
    public new class MethodName : RefCounted.MethodName { }
    public new class PropertyName : RefCounted.PropertyName { }
    public new class SignalName : RefCounted.SignalName { }
}

// ── InputEvent hierarchy ─────────────────────────────────────────────────
public class InputEvent : Resource
{
    public new class MethodName : Resource.MethodName { }
    public new class PropertyName : Resource.PropertyName { }
    public new class SignalName : Resource.SignalName { }
}

public class InputEventAction : InputEvent
{
    public new class MethodName : InputEvent.MethodName { }
    public new class PropertyName : InputEvent.PropertyName { }
    public new class SignalName : InputEvent.SignalName { }
}

public class InputEventWithModifiers : InputEvent
{
    public new class MethodName : InputEvent.MethodName { }
    public new class PropertyName : InputEvent.PropertyName { }
    public new class SignalName : InputEvent.SignalName { }
}

public class InputEventKey : InputEventWithModifiers
{
    public new class MethodName : InputEventWithModifiers.MethodName { }
    public new class PropertyName : InputEventWithModifiers.PropertyName { }
    public new class SignalName : InputEventWithModifiers.SignalName { }
}

public class InputEventMouse : InputEventWithModifiers
{
    public new class MethodName : InputEventWithModifiers.MethodName { }
    public new class PropertyName : InputEventWithModifiers.PropertyName { }
    public new class SignalName : InputEventWithModifiers.SignalName { }
}

public class InputEventMouseButton : InputEventMouse
{
    public new class MethodName : InputEventMouse.MethodName { }
    public new class PropertyName : InputEventMouse.PropertyName { }
    public new class SignalName : InputEventMouse.SignalName { }
}

public class InputEventMouseMotion : InputEventMouse
{
    public new class MethodName : InputEventMouse.MethodName { }
    public new class PropertyName : InputEventMouse.PropertyName { }
    public new class SignalName : InputEventMouse.SignalName { }
}

public class InputEventJoypadMotion : InputEvent
{
    public new class MethodName : InputEvent.MethodName { }
    public new class PropertyName : InputEvent.PropertyName { }
    public new class SignalName : InputEvent.SignalName { }
}

public class InputEventPanGesture : InputEventWithModifiers
{
    public new class MethodName : InputEventWithModifiers.MethodName { }
    public new class PropertyName : InputEventWithModifiers.PropertyName { }
    public new class SignalName : InputEventWithModifiers.SignalName { }
}

// ── Network ──────────────────────────────────────────────────────────────
public class PacketPeer : RefCounted
{
    public new class MethodName : RefCounted.MethodName { }
    public new class PropertyName : RefCounted.PropertyName { }
    public new class SignalName : RefCounted.SignalName { }
}

public class ENetPacketPeer : PacketPeer
{
    public new class MethodName : PacketPeer.MethodName { }
    public new class PropertyName : PacketPeer.PropertyName { }
    public new class SignalName : PacketPeer.SignalName { }
}

// ── Static singletons / servers (no instances created in headless) ──────
public static class Input { }
public static class AudioServer { public class AudioServerInstance { } }
public class AudioServerInstance { }
public static class ClassDB { }
public static class Colors { }
public static class DisplayServer { }
public static class Geometry2D { }
public static class Performance { }
public static class ProjectSettings { }
public static class RenderingDevice { }
public static class RenderingServer { }
public static class ResourceLoader { }
public static class StringExtensions { }
public static class TextServer { }
public static class TextServerManager { public class TextServerManagerInstance { } }
public class TextServerManagerInstance { }
public static class Time
{
    private static readonly System.Diagnostics.Stopwatch _sw = System.Diagnostics.Stopwatch.StartNew();
    public static ulong GetTicksMsec() => (ulong)_sw.ElapsedMilliseconds;
    public static ulong GetTicksUsec() => (ulong)(_sw.ElapsedTicks * 1_000_000L / System.Diagnostics.Stopwatch.Frequency);
}
public static class TranslationServer { }

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
public readonly struct Quaternion
{
    public Quaternion(float _, float __, float ___, float ____) { }
}

public readonly struct Transform2D
{
    public Transform2D(float _, Vector2 __) { }
}

public readonly struct Rid { }
public readonly struct Signal { public Signal(GodotObject _, StringName __) { } }

// ── Attributes (presence-only; never inspected reflectively in headless) ─
[System.AttributeUsage(System.AttributeTargets.Assembly)]
public sealed class AssemblyHasScriptsAttribute : System.Attribute { }

[System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Field)]
public sealed class ExportAttribute : System.Attribute
{
    public ExportAttribute(PropertyHint _ = PropertyHint.None, string __ = "") { }
}

[System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Field, AllowMultiple = true)]
public sealed class ExportGroupAttribute : System.Attribute { public ExportGroupAttribute(string _, string __ = "") { } }

[System.AttributeUsage(System.AttributeTargets.Method)]
public sealed class ExportToolButtonAttribute : System.Attribute { public ExportToolButtonAttribute(string _, string __ = "") { } }

[System.AttributeUsage(System.AttributeTargets.Class)]
public sealed class GlobalClassAttribute : System.Attribute { }

[System.AttributeUsage(System.AttributeTargets.Class)]
public sealed class ScriptPathAttribute : System.Attribute { public ScriptPathAttribute(string _) { } }

[System.AttributeUsage(System.AttributeTargets.Event | System.AttributeTargets.Method, AllowMultiple = true)]
public sealed class SignalAttribute : System.Attribute { }

[System.AttributeUsage(System.AttributeTargets.Class)]
public sealed class ToolAttribute : System.Attribute { }
