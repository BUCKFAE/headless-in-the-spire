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

// from: MegaCrit.Sts2.Core.Events.EventOption.Chosen → chain into
//   RunManager hooks that compare relic ids. TypeLoadException: "Could not
//   load type 'Godot.StringName'". `just list-members Godot.StringName`
//   shows the 6 surfaces: ctor, op_Equality (×2), op_Inequality, op_Implicit
//   (×2). Stored but never compared meaningfully; equality on the stub
//   collapses to string equality on the inner Name.
public sealed class StringName
{
    public string Name { get; }

    public StringName(string name) => Name = name ?? string.Empty;

    public static bool operator ==(StringName? a, StringName? b)
        => ReferenceEquals(a, b) || (a is not null && b is not null && a.Name == b.Name);
    public static bool operator !=(StringName? a, StringName? b) => !(a == b);
    public static bool operator ==(in NativeInterop.godot_string_name _, StringName? __) => false;
    public static bool operator !=(in NativeInterop.godot_string_name a, StringName? b) => !(a == b);

    public static implicit operator string(StringName n) => n.Name;
    public static implicit operator StringName(string s) => new(s);

    public override bool Equals(object? obj) => obj is StringName sn && sn.Name == Name;
    public override int GetHashCode() => Name.GetHashCode();
}
