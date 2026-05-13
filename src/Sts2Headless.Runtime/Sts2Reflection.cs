using System.Reflection;

namespace Sts2Headless.Runtime;

// Small reflection-only helpers for poking sts2.dll without taking a
// compile-time dependency on it (AD-4).
//
// Each lookup tries the candidate fully-qualified name first — this is the
// fast path that matches what sts2-cli's typed code expects. If that misses
// (the game version moved or renamed a namespace), we fall back to scanning
// `assembly.GetTypes()` for any type with the requested simple name. The
// fallback hit (or miss) is reported back to the caller so probe output
// surfaces a real diagnostic instead of a silent NullReferenceException.
public static class Sts2Reflection
{
    public readonly record struct TypeLookup(Type? Type, string Source)
    {
        public bool Found => Type is not null;
    }

    // Try `fullName`, then scan for any non-nested type whose Name matches the
    // last segment of `fullName`. `Source` describes which path resolved it
    // (or, on miss, why nothing matched).
    public static TypeLookup FindType(Assembly assembly, string fullName)
    {
        var direct = assembly.GetType(fullName, throwOnError: false);
        if (direct is not null)
        {
            return new TypeLookup(direct, $"fqn:{fullName}");
        }

        var simple = fullName.Contains('.') ? fullName[(fullName.LastIndexOf('.') + 1)..] : fullName;
        var matches = SafeGetTypes(assembly)
            .OfType<Type>()
            .Where(t => !t.IsNested && t.Name == simple)
            .ToArray();
        return matches.Length switch
        {
            0 => new TypeLookup(null, $"no type {simple} (tried fqn:{fullName})"),
            1 => new TypeLookup(matches[0], $"scan→{matches[0].FullName}"),
            _ => new TypeLookup(null, $"ambiguous: {string.Join(", ", matches.Select(t => t.FullName))}"),
        };
    }

    // GetTypes() throws if any referenced type fails to load — common in
    // sts2.dll because of GodotStubs gaps. The Types list on the exception
    // still contains the successfully-loaded entries, which is plenty for our
    // name scans.
    public static Type?[] SafeGetTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types; }
    }
}
