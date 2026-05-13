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

// ── Value types ─────────────────────────────────────────────────────────

public readonly struct Vector2 { }
public readonly struct Vector2I { }
public readonly struct Vector3 { }
public readonly struct Rect2 { }
public readonly struct Color { }
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

public class FileAccess : GodotObject
{
    public new class MethodName : GodotObject.MethodName { }
    public new class PropertyName : GodotObject.PropertyName { }
    public new class SignalName : GodotObject.SignalName { }

    public enum ModeFlags { Read = 1, Write = 2, ReadWrite = 3, WriteRead = 7 }
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
