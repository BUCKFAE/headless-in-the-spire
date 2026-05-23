// Godot value types. Struct-vs-class is load-bearing here — metadata uses
// ELEMENT_TYPE_VALUETYPE vs ELEMENT_TYPE_CLASS, and a mismatch fails type
// resolution. Keep the kind matching real GodotSharp.

namespace Godot;

public readonly partial struct Vector2
{
    // from: MegaCrit.Sts2.Core.Nodes.NGame..cctor (and likely siblings)
    //   MissingMethodException during EnterAct: "Method not found:
    //   'Void Godot.Vector2..ctor(Single, Single)'." — static fields on
    //   node classes initialise Vector2 size/position constants. Stored
    //   but never read; no-op body is fine.
    public Vector2(float x, float y) { X = x; Y = y; }

    // from: NCreature.PerformIntent → reads Vector2.X (FIELD, not property)
    //   for VFX positioning. MissingFieldException if these are auto-props.
    //   Match real Godot's struct shape: public readonly fields.
    public readonly float X;
    public readonly float Y;

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
public readonly partial struct Vector2I
{
    // from: GodotStubsCoverageTests audit — sts2 holds a MemberRef against
    //   `Vector2I.get_One()` (same shape as Vector2.get_Zero). Static
    //   value-type getters are JIT-resolved at call-site, so the gap would
    //   surface as MissingMethodException the first time any code path
    //   touched it. Default-struct return is the correct One semantically
    //   (no consumer reads x/y back in headless mode).
    public static Vector2I One => default;
}
public readonly partial struct Vector3
{
    // from: monster move VFX constructs world-space targets via
    //   `new Vector3(x, y, z)`. Body is a no-op; no consumer reads back.
    public Vector3(float _, float __, float ___) { }

    // from: GodotStubsCoverageTests audit — paired with Vector2's static
    //   getters; sts2 references both Up and Zero (e.g. camera/anchor
    //   defaults in scenes never instantiated in headless mode). No-op
    //   defaults are safe because no consumer reads the components back.
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
    // from: 19 ModelDb subtypes (Defect, BouncingFlask, DeprecatedCharacter, …)
    //   MissingMethodException during Inject: "Method not found:
    //   'Void Godot.Color..ctor(System.String)'." — model cctors build
    //   colors from hex strings. Body is a no-op; nothing on this stub is
    //   actually read.
    public Color(string _) { }

    // from: VFX/UI code that constructs colors numerically — e.g. a monster
    //   move tinting its sprite by RGBA components. MissingMethodException
    //   on `.ctor(Single, Single, Single, Single)` surfaces as Unhandled
    //   when the call site is on the main thread. Body is a no-op; nothing
    //   on this stub is actually read.
    public Color(float _, float __, float ___, float ____ = 1f) { }

    // from: run/use_potion VFX path — discovered 2026-05-18 via 50-seed
    //   A0 sweep, 6/50 seeds crashed with "Method not found: 'Void
    //   Godot.Color..ctor(Godot.Color, Single)'.". Real Godot uses this
    //   to derive an alpha-shifted variant of an existing color.
    public Color(Color _, float __) { }

    // from: event-room paths (e.g. NSimpleCardSelectScreen) and monster move
    //   VFX code read individual channels off a Color. Real Godot exposes
    //   R/G/B/A as readwrite fields (the IL is a `ldfld`, not `call get_*`),
    //   so the stub mirrors the same shape — `readonly` would make `stfld`
    //   sites throw FieldAccessException at JIT time.
    public readonly float R;
    public readonly float G;
    public readonly float B;
    public readonly float A;

    // from: ModelDb subtypes that parse colors out of hex strings expressed
    //   as `ReadOnlySpan<char>` (the span overload is the IL-resolved path
    //   for `Color.FromHtml("…")` literals). Surfaced via the
    //   GodotStubsCoverageTests audit alongside the Black/Cyan/… gap.
    public static Color FromHtml(System.ReadOnlySpan<char> _) => default;

    // from: VFX/UI lerp tweens that interpolate between two colors over a
    //   tween parameter. Body is no-op; the returned color is fed back into
    //   Tween which is itself a no-op in headless mode.
    public Color Lerp(Color _, float __) => default;

    // from: equality checks against constants (`color == Colors.Black`) and
    //   VFX multiply paths (`tint * 0.5f`, `tint * other`). Same surfacing
    //   pattern as Vector2: define `==`/`!=` together with Equals/GetHashCode
    //   to satisfy CS0660/CS0661, even though only op_Equality and op_Multiply
    //   are MemberRef'd by sts2.dll.
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
    // from: CombatManager.AfterCombatRoomLoaded — `Callable.From(() => …)`
    //   wraps continuations for the engine's animation scheduler. The
    //   resulting Callable is enqueued through Tween in headless mode where
    //   it's a no-op, so the wrapped action never runs — only the surface
    //   needs to exist.
    public Callable(GodotObject _, StringName __) { }
    public static Callable From(System.Action _) => default;
    public static Callable From<T>(System.Action<T> _) => default;

    // from: CreatureCmd.Stun → schedules a `CallDeferred(Variant[])` on the
    //   creature's animator. The eel's STUNNED state hits this on the agent
    //   path after Pommel Strike (TERROR_EEL_ELITE in the encounter sweep).
    //   No-op'd in headless — the engine doesn't need the deferred call
    //   to actually run since the visible side effects are UI-only.
    public void CallDeferred(params Variant[] _) { }
}

// from: MegaCrit.Sts2.Core.Events.EventOption.Chosen → chain into
//   RunManager hooks that compare relic ids. TypeLoadException: "Could not
//   load type 'Godot.StringName'". `just list-members Godot.StringName`
//   shows the 6 surfaces: ctor, op_Equality (×2), op_Inequality, op_Implicit
//   (×2). Stored but never compared meaningfully; equality on the stub
//   collapses to string equality on the inner Name.
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
