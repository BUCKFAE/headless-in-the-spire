// Godot.NativeInterop ABI types. Real GodotSharp uses these as the
// marshalling boundary between managed and native Godot — they hold raw
// handles to engine-side strings/variants. In headless mode no native
// engine exists, so these collapse to empty placeholders whose only job
// is to be assignable and equality-comparable.

namespace Godot.NativeInterop;

// from: MegaCrit.Sts2.Core uses of StringName.op_Equality(ref godot_string_name, StringName)
//   — `just runner::probe::list-members Godot.StringName` shows the overload. The struct
//   exists solely so the StringName ==/!= overloads have a parameter type
//   to bind. No fields, no comparisons — sts2 never reads contents.
public partial struct godot_string_name { }

// from: sts2's SG-emitted InvokeGodotClassMethod overrides, every node-like
//   class has `(ref godot_string_name, NativeVariantPtrArgs, ref godot_variant)`.
//   No MemberRef against godot_variant itself, so the generator's auto-
//   discovery surfaces it only as a parameter type — we declare it here so
//   the ref-parameter resolves at type-load time.
public partial struct godot_variant { }
