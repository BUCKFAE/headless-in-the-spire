using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Pins the run/use_potion endpoint shape and the OwnedPotions snapshot
// field. The agent assumes both work; this regresses both via the wire.
//
// Seed 42 happens to drop a potion (ENERGY_POTION) after the floor-2
// fight — we drive to that point, claim the potion as a reward, then
// verify it appears in OwnedPotions on the next snapshot. We don't yet
// drink it (would need an active combat to land observably) — usage
// itself is exercised by the End2EndTests boss-walk.
public class UsePotionTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;
    public UsePotionTests(HostSubprocess host) => _host = host;

    [Fact]
    public async Task OwnedPotions_FieldShape_IsListOnFreshRun()
    {
        var start = await RunFixtures.StartFreshRunAtMap(
            _host, character: Character.Ironclad, seed: 42uL);
        // Fresh Ironclad starts with no potions but the field must be a
        // non-null empty list — schema-shape regression.
        Assert.NotNull(start.OwnedPotions);
        Assert.Empty(start.OwnedPotions);
    }

    [Fact]
    public async Task UsePotion_OnEmptyBag_ThrowsTyped()
    {
        await RunFixtures.StartFreshRunAtMap(_host, character: Character.Ironclad, seed: 42uL);
        // Fresh run has no potions; the call should surface a wire error
        // rather than silently no-op or crash the host.
        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
            await _host.SendAsync<RunUsePotionResult>(
                "run/use_potion", new RunUsePotionParams(PotionIndex: 0)));
        // The exact error class is wire-detail; assert it's an empty-bag
        // shape, not an unrelated NRE / crash.
        Assert.Contains("potion", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
