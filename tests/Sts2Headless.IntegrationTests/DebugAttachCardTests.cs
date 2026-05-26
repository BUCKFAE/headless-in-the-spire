using Sts2Headless.Cheats;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Positive coverage for the two card-modifier cheats:
//   * debug/afflict_card   — CardCmd.Afflict(AfflictionModel, CardModel, Decimal)
//   * debug/enchant_card   — CardCmd.Enchant(EnchantmentModel, CardModel, Decimal)
//
// Both share an internal AttachToCard binding so the tests share a
// fixture: start a combat with a known opening hand (Strike+Defend),
// attach the chosen affliction/enchantment to hand index 0, verify the
// wire response names the right target card. Unknown / empty / wrong-
// state inputs surface as InvalidParams.
//
// The negative cases (--enable-debug missing) live in DebugDisabledTests.
public class DebugAttachCardTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;

    public DebugAttachCardTests(HostSubprocess host) => _host = host;

    private async Task SetupCombatAsync()
    {
        await RunFixtures.StartFreshRunAtMap(_host, character: Character.Ironclad, seed: 42uL);
        await _host.SendAsync<DebugSetHpResult>(
            "debug/set_hp", new DebugSetHpParams(Hp: 999, MaxHp: 999));
        await _host.SendAsync<DebugReplaceDeckResult>(
            "debug/replace_deck",
            new DebugReplaceDeckParams(new[]
            {
                new CardSpec("STRIKE_IRONCLAD", 0),
                new CardSpec("DEFEND_IRONCLAD", 0),
            }));
        await _host.SendAsync<DebugStartCombatResult>(
            "debug/start_combat", new DebugStartCombatParams(EncounterId: "SLIMES_NORMAL"));
    }

    [Fact]
    public async Task AfflictCard_LandsOnFirstHandCard()
    {
        await SetupCombatAsync();

        var firstAffliction = AfflictionIdNames.AllWireNames
            .OrderBy(s => s, StringComparer.Ordinal)
            .First();

        var resp = await _host.SendAsync<DebugAfflictCardResult>(
            "debug/afflict_card",
            new DebugAfflictCardParams(AfflictionId: firstAffliction, HandIndex: 0, Amount: 1));

        Assert.True(resp.Ok);
        Assert.Equal(firstAffliction, resp.AfflictionId);
        Assert.Equal(0, resp.HandIndex);
        Assert.False(string.IsNullOrEmpty(resp.CardId));
    }

    [Fact]
    public async Task AfflictCard_UnknownId_ReturnsInvalidParams()
    {
        await SetupCombatAsync();

        var err = await _host.ExpectErrorAsync(
            "debug/afflict_card",
            new DebugAfflictCardParams(AfflictionId: "DEFINITELY_NOT_AN_AFFLICTION"));

        Assert.Equal(Sts2Headless.Protocol.WireErrorCode.InvalidParams, err.Code);
    }

    [Fact]
    public async Task AfflictCard_OutOfRangeHandIndex_ReturnsInvalidParams()
    {
        await SetupCombatAsync();

        var firstAffliction = AfflictionIdNames.AllWireNames
            .OrderBy(s => s, StringComparer.Ordinal)
            .First();

        var err = await _host.ExpectErrorAsync(
            "debug/afflict_card",
            new DebugAfflictCardParams(AfflictionId: firstAffliction, HandIndex: 999));

        Assert.Equal(Sts2Headless.Protocol.WireErrorCode.InvalidParams, err.Code);
    }

    [Fact]
    public async Task EnchantCard_LandsOnFirstHandCard()
    {
        await SetupCombatAsync();

        var firstEnchantment = EnchantmentIdNames.AllWireNames
            .OrderBy(s => s, StringComparer.Ordinal)
            .First();

        var resp = await _host.SendAsync<DebugEnchantCardResult>(
            "debug/enchant_card",
            new DebugEnchantCardParams(EnchantmentId: firstEnchantment, HandIndex: 0, Amount: 1));

        Assert.True(resp.Ok);
        Assert.Equal(firstEnchantment, resp.EnchantmentId);
        Assert.Equal(0, resp.HandIndex);
        Assert.False(string.IsNullOrEmpty(resp.CardId));
    }

    [Fact]
    public async Task EnchantCard_UnknownId_ReturnsInvalidParams()
    {
        await SetupCombatAsync();

        var err = await _host.ExpectErrorAsync(
            "debug/enchant_card",
            new DebugEnchantCardParams(EnchantmentId: "DEFINITELY_NOT_AN_ENCHANTMENT"));

        Assert.Equal(Sts2Headless.Protocol.WireErrorCode.InvalidParams, err.Code);
    }
}
