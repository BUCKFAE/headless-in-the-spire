using System.Reflection;
using System.Runtime.Loader;

namespace Sts2Headless;

// Resolves managed assembly references against vendor/, which holds the
// pinned game DLLs (see AD-3) plus our GodotStubs build output. Wired into
// AssemblyLoadContext.Default so any Assembly.Load / type resolution that
// would otherwise hit the framework probing path falls through to here.
//
// Lookup order:
//   1. vendor/<name>.dll
//
// We intentionally do not probe the game's full data directory at runtime:
// vendor/ is the curated set and anything outside it should fail loudly.
internal sealed class VendorAssemblyResolver
{
    private readonly string _vendorDir;
    private readonly Dictionary<string, Assembly> _cache = new(StringComparer.OrdinalIgnoreCase);

    public VendorAssemblyResolver(string vendorDir)
    {
        _vendorDir = vendorDir;
    }

    public static VendorAssemblyResolver Install(string vendorDir)
    {
        var resolver = new VendorAssemblyResolver(vendorDir);
        AssemblyLoadContext.Default.Resolving += resolver.Resolve;
        return resolver;
    }

    private Assembly? Resolve(AssemblyLoadContext ctx, AssemblyName name)
    {
        if (name.Name is null)
        {
            return null;
        }

        if (_cache.TryGetValue(name.Name, out var cached))
        {
            return cached;
        }

        var path = Path.Combine(_vendorDir, name.Name + ".dll");
        if (!File.Exists(path))
        {
            return null;
        }

        var loaded = ctx.LoadFromAssemblyPath(path);
        _cache[name.Name] = loaded;
        return loaded;
    }
}
