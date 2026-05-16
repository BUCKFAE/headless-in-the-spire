using System.Collections;
using System.Reflection;
using Sts2Headless.Runtime;

namespace Sts2Headless.IntegrationTests.Coverage;

// In-process bootstrap of vendor/sts2.dll, shared across the Coverage tests
// so each test class doesn't pay the ~1s ModelDb.Inject loop. Lives in
// IntegrationTests because it needs the proprietary game DLL — Unit tests
// can't run this. xUnit instantiates the fixture once per
// IClassFixture<ContentManifestFixture> usage and disposes it at the end.
//
// The fixture exposes ModelDb's canonical-instance registry directly so
// tests can compare a fresh walk against the on-disk *Id.g.cs manifests
// without spawning a host subprocess.
public sealed class ContentManifestFixture
{
    public Assembly Sts2 { get; }
    public Type ModelDbType { get; }
    public IDictionary ContentById { get; }
    public IReadOnlyList<Type> AllSubtypes { get; }

    public ContentManifestFixture()
    {
        var repoRoot = LocateRepoRoot();
        var vendor = Path.Combine(repoRoot, "vendor");
        var sts2Dll = Path.Combine(vendor, "sts2.dll");
        if (!File.Exists(sts2Dll))
        {
            throw new InvalidOperationException(
                $"vendor/sts2.dll not present at {sts2Dll} — run `just setup` first. " +
                "Coverage tests cannot run without the pinned game DLL.");
        }

        VendorAssemblyResolver.Install(vendor);
        var preamble = RuntimeBootstrap.Run(vendor);
        if (preamble.SetupError is not null)
            throw new InvalidOperationException($"bootstrap setup failed: {preamble.SetupError}");
        var steps = BootstrapSequence.Apply(preamble.Sts2!);
        var failed = steps.Where(s => !s.Ok).ToList();
        if (failed.Count > 0)
            throw new InvalidOperationException(
                "bootstrap steps failed: " + string.Join(" | ", failed.Select(f => $"{f.Label}: {f.Detail}")));

        Sts2 = preamble.Sts2!;
        var (modelDb, byId) = GenerateContentIdsCommand.ResolveModelDb(Sts2);
        ModelDbType = modelDb;
        ContentById = byId;
        AllSubtypes = LoadAllSubtypes(Sts2);
    }

    private static IReadOnlyList<Type> LoadAllSubtypes(Assembly sts2)
    {
        var subtypesType = sts2.GetType("MegaCrit.Sts2.Core.Models.AbstractModelSubtypes")
            ?? throw new InvalidOperationException("AbstractModelSubtypes type not found");
        var raw = subtypesType.GetProperty("All", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
               ?? subtypesType.GetField("All", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        if (raw is not IEnumerable iter)
            throw new InvalidOperationException("AbstractModelSubtypes.All not enumerable");
        var list = new List<Type>();
        foreach (var x in iter) if (x is Type t) list.Add(t);
        return list;
    }

    private static string LocateRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8; i++)
        {
            if (File.Exists(Path.Combine(dir, "Sts2Headless.slnx"))) return dir;
            var p = Directory.GetParent(dir);
            if (p is null) break;
            dir = p.FullName;
        }
        throw new InvalidOperationException("repo root not found");
    }
}
