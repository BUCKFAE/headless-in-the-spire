using Sts2Headless.Cheats;
using Sts2Headless.Content;
using Sts2Headless.Protocol;
using Xunit;

namespace Sts2Headless.UnitTests;

// Pin host/methods against the source-of-truth catalogues (Core ∪
// Content ∪ Cheats). Same posture as the Python METHOD_NAMES parity
// test, but on the C# side — if a new wire method gets added to one
// catalogue without the host's discovery surface picking it up, this
// test fails first.
//
// Two invariants:
//   1. The methods returned by HostMethods.Methods are exactly the
//      union of MethodCatalog.Core ∪ ContentMethodCatalog.All ∪
//      CheatMethodCatalog.All — no missing entries, no extras.
//   2. Each method's flags match its source catalogue: isDebugOnly
//      flips to true iff the entry came from CheatMethodCatalog with
//      IsDebugOnly=true; hasParams iff ParamsType is non-null.
public class HostMethodsParityTests
{
    [Fact]
    public void Methods_ReturnsMergedCatalogue_DebugEnabled()
    {
        var catalog = MethodCatalog.Core
            .Concat(CheatMethodCatalog.All)
            .Concat(ContentMethodCatalog.All)
            .ToList();

        var result = HostMethods.Methods(catalog, debugEnabled: true);

        Assert.True(result.Ok);
        Assert.True(result.DebugEnabled);

        // Set equality on names.
        var expectedNames = catalog.Select(e => e.Name).ToHashSet(StringComparer.Ordinal);
        var actualNames = result.Methods.Select(m => m.Name).ToHashSet(StringComparer.Ordinal);
        var missing = expectedNames.Except(actualNames).OrderBy(s => s).ToList();
        var extra = actualNames.Except(expectedNames).OrderBy(s => s).ToList();
        Assert.True(missing.Count == 0 && extra.Count == 0,
            $"host/methods drift. Missing: [{string.Join(", ", missing)}]; extra: [{string.Join(", ", extra)}].");
    }

    [Fact]
    public void Methods_FlagsMatchCatalogueEntry()
    {
        var catalog = MethodCatalog.Core
            .Concat(CheatMethodCatalog.All)
            .Concat(ContentMethodCatalog.All)
            .ToList();
        var byName = catalog.ToDictionary(e => e.Name, StringComparer.Ordinal);

        var result = HostMethods.Methods(catalog, debugEnabled: true);

        foreach (var m in result.Methods)
        {
            Assert.True(byName.TryGetValue(m.Name, out var entry),
                $"host/methods returned unknown name {m.Name}");
            Assert.Equal(entry!.IsDebugOnly, m.IsDebugOnly);
            Assert.Equal(entry.ParamsType is not null, m.HasParams);
            Assert.Equal(entry.Summary, m.Summary);
        }
    }

    [Fact]
    public void Methods_DebugEnabledFlag_PropagatesToResult()
    {
        // The wire never *hides* debug methods (clients want to render
        // a gated-but-known badge), but the top-level debugEnabled flag
        // tells them at a glance whether those entries are callable.
        var catalog = MethodCatalog.Core.ToList();

        var enabled = HostMethods.Methods(catalog, debugEnabled: true);
        var disabled = HostMethods.Methods(catalog, debugEnabled: false);

        Assert.True(enabled.DebugEnabled);
        Assert.False(disabled.DebugEnabled);
        // Identical method-list shape regardless of the flag.
        Assert.Equal(
            enabled.Methods.Select(m => m.Name).OrderBy(s => s),
            disabled.Methods.Select(m => m.Name).OrderBy(s => s));
    }

    [Fact]
    public void Methods_AreSortedDeterministically()
    {
        // The wire returns methods in a stable order so clients (e.g.
        // an MCP UI that lists them) don't see spurious diffs across
        // host restarts.
        var catalog = MethodCatalog.Core
            .Concat(CheatMethodCatalog.All)
            .Concat(ContentMethodCatalog.All)
            .ToList();

        var first = HostMethods.Methods(catalog, debugEnabled: false);
        var second = HostMethods.Methods(catalog, debugEnabled: false);

        Assert.Equal(
            first.Methods.Select(m => m.Name),
            second.Methods.Select(m => m.Name));

        // The implementation sorts by ordinal name; pin that.
        Assert.Equal(
            first.Methods.Select(m => m.Name).OrderBy(s => s, StringComparer.Ordinal),
            first.Methods.Select(m => m.Name));
    }
}
