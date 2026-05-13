using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Xunit;

namespace Sts2Headless.UnitTests;

// AD-4: nothing we ship may take a compile-time dependency on sts2.dll. All
// interaction with the game DLL goes through reflection so a game-version
// bump can't force a recompile cascade. The discipline is easy to break by
// accident — one `using MegaCrit.Sts2.…` and the invariant is gone — so we
// guard it with a test that inspects the AssemblyReference table of every
// production output.
//
// PEReader-based rather than Assembly.GetReferencedAssemblies() so we don't
// have to load our own outputs, and so it survives #if/macros and any other
// source-level hiding. The metadata is the truth.
public class Ad4InvariantTests
{
    private static readonly string[] ProductionAssemblies =
    {
        "Sts2Headless.dll",
        "Sts2Headless.Runtime.dll",
        "Sts2Headless.Protocol.dll",
        "GodotSharp.dll", // GodotStubs builds with AssemblyName=GodotSharp
    };

    [Fact]
    public void No_Production_Assembly_References_Sts2()
    {
        // Test outputs and production outputs land in the same bin/ during
        // dotnet test (test project ProjectReferences pull them along).
        var binDir = AppContext.BaseDirectory;

        foreach (var asmFile in ProductionAssemblies)
        {
            var path = Path.Combine(binDir, asmFile);
            Assert.True(File.Exists(path), $"{asmFile} not found in test output at {binDir}");

            var refs = ReadAssemblyReferences(path);
            // Guard against a silent vacuous pass: any real .NET assembly has
            // at least System.Runtime in its reference table.
            Assert.True(refs.Count > 0, $"{asmFile}: AssemblyReferences table is empty — test is broken, not the invariant.");
            Assert.DoesNotContain(refs, r =>
                string.Equals(r, "sts2", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(refs, r =>
                r.StartsWith("MegaCrit.", StringComparison.OrdinalIgnoreCase));
        }
    }

    private static List<string> ReadAssemblyReferences(string path)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var md = pe.GetMetadataReader();
        var refs = new List<string>(md.AssemblyReferences.Count);
        foreach (var handle in md.AssemblyReferences)
        {
            refs.Add(md.GetString(md.GetAssemblyReference(handle).Name));
        }
        return refs;
    }
}
