using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Pin the SnapshotEnricher → NameLookup wiring: every inline record on a
// snapshot (Card, Relic, OwnedPotion, Power, …) should carry a non-empty
// `displayName` once the host has wired its ContentReader. A regression
// here means agents fall back to opaque SCREAMING_SNAKE_CASE ids when
// reasoning about cards/relics — which is exactly the failure mode that
// motivated this refactor.
public class InlineDisplayNameTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;
    public InlineDisplayNameTests(HostSubprocess host) => _host = host;

    [Fact]
    public async Task RunState_AfterRunNew_RelicsCarryDisplayName()
    {
        await RunFixtures.StartFreshRunAtMap(_host, character: Character.Ironclad, seed: 1uL);

        var state = await _host.SendAsync<RunStateResult>("run/state");

        // Ironclad starts with Burning Blood — the starter relic surfaces
        // on the first snapshot. We don't pin the exact display name string
        // (sts2 owns it via locale tables) but we do pin that the field
        // is populated.
        Assert.NotEmpty(state.Relics);
        Assert.All(state.Relics, r => Assert.False(string.IsNullOrEmpty(r.DisplayName),
            $"Relic {r.Id} has empty displayName — SnapshotEnricher likely not wired"));
    }
}
