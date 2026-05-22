using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;

namespace Sts2Headless.Commands;

// Loads vendor/sts2.dll and tries to enumerate its types. The point is to
// surface what GodotSharp surface area sts2.dll actually depends on, by
// harvesting the LoaderExceptions from ReflectionTypeLoadException and
// aggregating them so the worst offenders rise to the top.
//
// This is a one-shot diagnostic, not part of the normal host loop. We do not
// attempt to invoke anything on sts2.dll here — that would require the sync-
// context / yield neutralisation work, which isn't in place yet.
internal static class InspectCommand
{
    public static int Run(string vendorDir)
    {
        var sts2Path = Path.Combine(vendorDir, "sts2.dll");
        if (!File.Exists(sts2Path))
        {
            Console.Error.WriteLine($"vendor/sts2.dll missing — run `just setup`.");
            return 1;
        }

        Console.WriteLine($"loading: {sts2Path}");
        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(sts2Path);
        Console.WriteLine($"  name:        {assembly.GetName().FullName}");
        Console.WriteLine($"  runtime:     {assembly.ImageRuntimeVersion}");
        Console.WriteLine($"  references:  {assembly.GetReferencedAssemblies().Length}");
        foreach (var r in assembly.GetReferencedAssemblies().OrderBy(a => a.Name))
        {
            Console.WriteLine($"    - {r.Name}, Version={r.Version}");
        }
        Console.WriteLine();

        Type?[] types;
        Exception?[] loaderExceptions;
        try
        {
            types = assembly.GetTypes();
            loaderExceptions = [];
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types;
            loaderExceptions = ex.LoaderExceptions;
        }

        var loaded = types.Count(t => t is not null);
        var failed = types.Length - loaded;
        Console.WriteLine($"types:       {types.Length} declared");
        Console.WriteLine($"  loaded:    {loaded}");
        Console.WriteLine($"  failed:    {failed}");
        Console.WriteLine($"  exceptions:{loaderExceptions.Length}");
        Console.WriteLine();

        if (loaderExceptions.Length == 0)
        {
            Console.WriteLine("✅ no loader exceptions — every type resolved.");
            return 0;
        }

        // Group exceptions by their canonical message so 500 copies of
        // "Could not load type 'Godot.Node'" collapse to a single line with
        // count=500. Sort highest-count first so the worst gaps are obvious.
        var groups = loaderExceptions
            .Where(e => e is not null)
            .GroupBy(e => CanonicalKey(e!))
            .Select(g => (Key: g.Key, Count: g.Count(), Sample: g.First()!))
            .OrderByDescending(g => g.Count)
            .ToArray();

        Console.WriteLine($"unique loader exceptions: {groups.Length}");
        Console.WriteLine();
        foreach (var (key, count, sample) in groups.Take(50))
        {
            Console.WriteLine($"  [{count,4}] {sample.GetType().Name}: {key}");
        }
        if (groups.Length > 50)
        {
            Console.WriteLine($"  ... {groups.Length - 50} more unique exceptions");
        }

        // For nested types, the exception message strips the enclosing
        // context. Resolve it from the metadata so we know which class to
        // hang the stub off.
        DumpNestedTypeRefs(sts2Path, groups.Select(g => g.Sample));

        return 0;
    }

    // Walk sts2.dll's TypeReference table and, for each loader exception
    // whose target is a nested type (no namespace in the message), print the
    // fully-qualified path including the enclosing types. This is how we
    // know where to nest a stub like `ModeFlags` or `EventType`.
    private static void DumpNestedTypeRefs(string sts2Path, IEnumerable<Exception> samples)
    {
        var nestedTargets = samples
            .OfType<TypeLoadException>()
            .Select(e => ExtractTypeName(e.Message))
            .Where(n => n is not null && !n.Contains('.'))
            .Select(n => n!)
            .ToHashSet();
        if (nestedTargets.Count == 0) return;

        using var pe = new PEReader(File.OpenRead(sts2Path));
        var md = pe.GetMetadataReader();
        var found = new List<string>();
        foreach (var handle in md.TypeReferences)
        {
            var tr = md.GetTypeReference(handle);
            var name = md.GetString(tr.Name);
            if (!nestedTargets.Contains(name)) continue;
            if (tr.ResolutionScope.Kind != HandleKind.TypeReference) continue;

            var path = new List<string> { name };
            var scope = tr.ResolutionScope;
            while (scope.Kind == HandleKind.TypeReference)
            {
                var parent = md.GetTypeReference((TypeReferenceHandle)scope);
                var ns = md.GetString(parent.Namespace);
                path.Add(string.IsNullOrEmpty(ns)
                    ? md.GetString(parent.Name)
                    : $"{ns}.{md.GetString(parent.Name)}");
                scope = parent.ResolutionScope;
            }
            path.Reverse();
            found.Add(string.Join("/", path));
        }

        if (found.Count == 0) return;
        Console.WriteLine();
        Console.WriteLine("resolved nested-type paths:");
        foreach (var p in found.Distinct().OrderBy(s => s))
        {
            Console.WriteLine($"  - {p}");
        }
    }

    // Pull the single-quoted type name out of a TypeLoadException message:
    //   "Could not load type 'Foo.Bar' from assembly 'X'."
    private static string? ExtractTypeName(string msg)
    {
        var start = msg.IndexOf('\'');
        if (start < 0) return null;
        var end = msg.IndexOf('\'', start + 1);
        return end < 0 ? null : msg.Substring(start + 1, end - start - 1);
    }

    // Strip the volatile bits (paths, hex addresses) so semantically-identical
    // exceptions group together. Keep the type/assembly name that identifies
    // *what* is missing.
    private static string CanonicalKey(Exception e)
    {
        var msg = e.Message;
        var nl = msg.IndexOf('\n');
        return (nl >= 0 ? msg[..nl] : msg).Trim();
    }
}
