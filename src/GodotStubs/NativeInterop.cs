// Godot.NativeInterop ABI types. Real GodotSharp uses these as the
// marshalling boundary between managed and native Godot — they hold raw
// handles to engine-side strings/variants. In headless mode no native
// engine exists, so these collapse to empty placeholders whose only job
// is to be assignable and equality-comparable.

namespace Godot.NativeInterop;

// from: MegaCrit.Sts2.Core uses of StringName.op_Equality(ref godot_string_name, StringName)
//   — `just list-members Godot.StringName` shows the overload. The struct
//   exists solely so the StringName ==/!= overloads have a parameter type
//   to bind. No fields, no comparisons — sts2 never reads contents.
public struct godot_string_name { }
