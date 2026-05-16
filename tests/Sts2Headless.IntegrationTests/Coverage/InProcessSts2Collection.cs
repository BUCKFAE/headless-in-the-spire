using Xunit;

namespace Sts2Headless.IntegrationTests.Coverage;

// Serialises every test class that loads vendor/sts2.dll *in-process*
// (BootstrapSequenceTests, CardIdEnumerationProbeTests, the Coverage
// tests). Required because:
//
//   * sts2's bootstrap mutates static state (ModelDb._contentById,
//     ProgressState's seen-card collection, etc.) that is NOT thread-safe.
//     Two fixtures bootstrapping in parallel corrupt those collections
//     with "Operations that change non-concurrent collections must have
//     exclusive access" errors.
//   * xUnit v3 runs classes in *different* collections in parallel by
//     default; without a shared collection name, my new fixture races
//     against BootstrapSequenceTests in the same dotnet test invocation.
//
// HostSubprocess-based tests don't need this — each subprocess gets its
// own AppDomain and its own ModelDb state, so they're already isolated.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class InProcessSts2Collection
{
    public const string Name = "InProcessSts2";
}
