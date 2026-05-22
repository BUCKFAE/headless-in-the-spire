using Sts2Headless.Cheats;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Positive coverage for debug/give_potion. The negative case
// (--enable-debug omitted) is in DebugDisabledTests; this file pins the
// happy path:
//
//   * Granting a known-good potion id lands it in PotionSlots.
//   * The wire result reports a real slot index + an updated count.
//   * The post-call run/state surfaces the potion in ownedPotions.
//   * An unknown potion id surfaces as WireErrorCode.InvalidParams
//     (not InternalError, not MethodNotFound).
//
// Uses PotionIdNames.AllWireNames.First() instead of hard-coding a
// specific potion id — robust against potion-renaming in future game
// versions and against the alphabetical-sort order changing.
public class DebugGivePotionTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;

    public DebugGivePotionTests(HostSubprocess host) => _host = host;

    [Fact]
    public async Task GivePotion_LandsInPotionSlots()
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

        var before = await _host.SendAsync<RunStateResult>("run/state");
        var startingCount = before.OwnedPotions.Count;

        // First id alphabetically — stable enough that the test would have
        // to be edited only if the entire potion catalog disappears.
        var firstPotion = PotionIdNames.AllWireNames
            .OrderBy(s => s, StringComparer.Ordinal)
            .First();

        var resp = await _host.SendAsync<DebugGivePotionResult>(
            "debug/give_potion", new DebugGivePotionParams(PotionId: firstPotion));

        Assert.True(resp.Ok);
        Assert.Equal(firstPotion, resp.PotionId);
        Assert.True(resp.SlotIndex >= 0,
            $"SlotIndex should name the landed slot; got {resp.SlotIndex}");
        Assert.True(resp.PotionCount > startingCount,
            $"PotionCount should grow from {startingCount}; got {resp.PotionCount}");

        var after = await _host.SendAsync<RunStateResult>("run/state");
        Assert.Contains(after.OwnedPotions, p => string.Equals(p.Id, firstPotion, StringComparison.Ordinal));
    }

    [Fact]
    public async Task GivePotion_UnknownId_ReturnsInvalidParams()
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

        var err = await _host.ExpectErrorAsync(
            "debug/give_potion", new DebugGivePotionParams(PotionId: "DEFINITELY_NOT_A_POTION_ID"));

        // Same code as other cheats use for unknown ids — consistent
        // wire contract across debug/start_combat, debug/replace_deck,
        // debug/give_potion.
        Assert.Equal(Sts2Headless.Protocol.WireErrorCode.InvalidParams, err.Code);
    }

    [Fact]
    public async Task GivePotion_EmptyId_ReturnsInvalidParams()
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

        var err = await _host.ExpectErrorAsync(
            "debug/give_potion", new DebugGivePotionParams(PotionId: ""));

        Assert.Equal(Sts2Headless.Protocol.WireErrorCode.InvalidParams, err.Code);
    }
}
