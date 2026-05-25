using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Diagnostic — enumerate the Act 1 boss ids across the 50-seed
// IroncladAgentA0Clear corpus. Output goes to test stdout; useful for
// downstream policy work that wants to know the pool of bosses the
// agent will actually face.
public class BossDiscoveryProbe
{
    private readonly ITestOutputHelper _output;
    public BossDiscoveryProbe(ITestOutputHelper output) => _output = output;

    [Fact]
    [Trait("Category", "Diagnostic")]
    public async Task EnumerateAct1Bosses_AcrossCorpus()
    {
        var bossesBySeed = new Dictionary<ulong, EncounterId?>();
        for (ulong seed = 1; seed <= 50; seed++)
        {
            await using var host = new HostSubprocess();
            await host.SendAsync<RunNewResult>(
                "run/new",
                new RunNewParams(Character: Character.Ironclad, Seed: seed, Ascension: 0));
            var state = await host.SendAsync<RunStateResult>("run/state");
            bossesBySeed[seed] = state.BossEncounterId;
        }

        _output.WriteLine("Seed → Act 1 boss:");
        foreach (var kv in bossesBySeed.OrderBy(kv => kv.Key))
            _output.WriteLine($"  {kv.Key}: {kv.Value?.ToString() ?? "<null>"}");

        _output.WriteLine("");
        _output.WriteLine("Boss frequency:");
        var counts = bossesBySeed.Values
            .GroupBy(b => b?.ToString() ?? "<null>")
            .Select(g => (Boss: g.Key, Count: g.Count()))
            .OrderByDescending(t => t.Count);
        foreach (var (boss, count) in counts)
            _output.WriteLine($"  {boss}: {count}");

        Assert.Equal(50, bossesBySeed.Count);
    }
}
