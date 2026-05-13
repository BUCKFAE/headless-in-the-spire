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
// This file holds the GodotObject → Node → CanvasItem → Node2D hierarchy.
// Resource-rooted types live in Resources.cs; the Control branch lives in
// Controls.cs.

namespace Godot;

public class GodotObject
{
    public class MethodName { }
    public class PropertyName { }
    public class SignalName { }
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
