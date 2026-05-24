// Godot value types. Struct-vs-class is load-bearing here — metadata uses
// ELEMENT_TYPE_VALUETYPE vs ELEMENT_TYPE_CLASS, and a mismatch fails type
// resolution. Keep the kind matching real GodotSharp.

namespace Godot;

public readonly partial struct Vector2
{
    public Vector2(float x, float y) { X = x; Y = y; }

    public readonly float X;
    public readonly float Y;

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
public readonly partial struct Vector2I
{
    public static Vector2I One => default;
}
public readonly partial struct Vector3
{
    public Vector3(float _, float __, float ___) { }

    public static Vector3 Up => default;
    public static Vector3 Zero => default;
}
public readonly partial struct Rect2
{
    public Vector2 Size => default;
    public Vector2 Position => default;
}
public readonly partial struct Color
{
    public Color(string _) { }

    public Color(float _, float __, float ___, float ____ = 1f) { }

    public Color(Color _, float __) { }

    public readonly float R;
    public readonly float G;
    public readonly float B;
    public readonly float A;

    public static Color FromHtml(System.ReadOnlySpan<char> _) => default;

    public Color Lerp(Color _, float __) => default;

    public static bool operator ==(Color _, Color __) => false;
    public static bool operator !=(Color _, Color __) => true;
    public static Color operator *(Color _, Color __) => default;
    public static Color operator *(Color _, float __) => default;
    public override bool Equals(object? obj) => false;
    public override int GetHashCode() => 0;
}
// Variant is Godot's universal value box. sts2's Tween calls pass strongly-typed
// args (Color, float, Vector2, StringName, …) that the IL implicitly converts
// to Variant. Each conversion is a `call Godot.Variant::op_Implicit(<type>)`
// at runtime — the stub method must exist or the call site throws
// MissingMethodException. None of the conversions are reflected on the wire
// (headless never reads from a Variant), so the body returns default.
public readonly partial struct Variant
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
public readonly partial struct Callable
{
    public Callable(GodotObject _, StringName __) { }
    public static Callable From(System.Action _) => default;
    public static Callable From<T>(System.Action<T> _) => default;

    public void CallDeferred(params Variant[] _) { }
}

public sealed partial class StringName
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
