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

    public float X { get; }
    public float Y { get; }

    // from: Neow path during EnterAct (PROBE_NEOW=1) — caught by GD.PushError
    //   "Method not found: 'Godot.Vector2 Godot.Vector2.get_Zero()'."
    //   NEventRoom.Create reads this for initial node positioning. Default
    //   struct value is the correct zero.
    public static Vector2 Zero => default;
    public static Vector2 One => default;
    public static Vector2 Up => default;
    public static Vector2 Down => default;
    public static Vector2 Left => default;
    public static Vector2 Right => default;

    public static Vector2 operator +(Vector2 _, Vector2 __) => default;
    public static Vector2 operator -(Vector2 _, Vector2 __) => default;
    public static Vector2 operator -(Vector2 _) => default;
    public static Vector2 operator *(Vector2 _, Vector2 __) => default;
    public static Vector2 operator *(Vector2 _, float __) => default;
    public static Vector2 operator *(float _, Vector2 __) => default;
    public static Vector2 operator /(Vector2 _, Vector2 __) => default;
    public static Vector2 operator /(Vector2 _, float __) => default;
    public static bool operator ==(Vector2 _, Vector2 __) => false;
    public static bool operator !=(Vector2 _, Vector2 __) => true;
    public override bool Equals(object? obj) => false;
    public override int GetHashCode() => 0;
}
public readonly struct Vector2I { }
public readonly struct Vector3 { }
public readonly struct Rect2
{
    public Vector2 Size => default;
    public Vector2 Position => default;
}
public readonly struct Color
{
    // from: 19 ModelDb subtypes (Defect, BouncingFlask, DeprecatedCharacter, …)
    //   MissingMethodException during Inject: "Method not found:
    //   'Void Godot.Color..ctor(System.String)'." — model cctors build
    //   colors from hex strings. Body is a no-op; nothing on this stub is
    //   actually read.
    public Color(string _) { }
}
// Variant is Godot's universal value box. sts2's Tween calls pass strongly-typed
// args (Color, float, Vector2, StringName, …) that the IL implicitly converts
// to Variant. Each conversion is a `call Godot.Variant::op_Implicit(<type>)`
// at runtime — the stub method must exist or the call site throws
// MissingMethodException. None of the conversions are reflected on the wire
// (headless never reads from a Variant), so the body returns default.
public readonly struct Variant
{
    public static Variant From<T>(T _) => default;
    public static implicit operator Variant(bool _) => default;
    public static implicit operator Variant(int _) => default;
    public static implicit operator Variant(long _) => default;
    public static implicit operator Variant(float _) => default;
    public static implicit operator Variant(double _) => default;
    public static implicit operator Variant(string _) => default;
    public static implicit operator Variant(StringName _) => default;
    public static implicit operator Variant(NodePath _) => default;
    public static implicit operator Variant(Vector2 _) => default;
    public static implicit operator Variant(Vector2I _) => default;
    public static implicit operator Variant(Vector3 _) => default;
    public static implicit operator Variant(Rect2 _) => default;
    public static implicit operator Variant(Color _) => default;
    public static implicit operator Variant(Quaternion _) => default;
    public static implicit operator Variant(Transform2D _) => default;
    public static implicit operator Variant(Rid _) => default;
    public static implicit operator Variant(GodotObject? _) => default;
    public static implicit operator Variant(Callable _) => default;
    public static implicit operator Variant(Signal _) => default;
}
public readonly struct Callable
{
    // from: CombatManager.AfterCombatRoomLoaded — `Callable.From(() => …)`
    //   wraps continuations for the engine's animation scheduler. The
    //   resulting Callable is enqueued through Tween in headless mode where
    //   it's a no-op, so the wrapped action never runs — only the surface
    //   needs to exist.
    public Callable(GodotObject _, StringName __) { }
    public static Callable From(System.Action _) => default;
    public static Callable From<T>(System.Action<T> _) => default;
}

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
