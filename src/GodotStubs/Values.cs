// Godot value types. Struct-vs-class is load-bearing here — metadata uses
// ELEMENT_TYPE_VALUETYPE vs ELEMENT_TYPE_CLASS, and a mismatch fails type
// resolution. Keep the kind matching real GodotSharp.

namespace Godot;

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
