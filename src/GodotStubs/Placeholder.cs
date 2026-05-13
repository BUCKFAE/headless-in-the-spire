// Scaffold only. Real type surface is added incrementally as sts2.dll's
// references force it — adding a stub here is cheaper than maintaining a
// speculative ~1200 LOC mirror of GodotSharp up front.
//
// Process for adding a stub:
//   1. Run the host, watch the TypeLoadException / MissingMethodException.
//   2. Add the missing type/member with the smallest no-op body that
//      satisfies the signature.
//   3. Note in a `// from: <sts2 type>.<member>` comment so we know who
//      pulled it in.

namespace Godot;

internal static class _Marker
{
    // Presence of this internal type lets us assert at runtime that our
    // GodotStubs assembly (not the real one) was loaded — see the
    // sanity check in the host's startup.
    public const string Tag = "sts2-headless-stubs";
}
