using Sts2Headless.Protocol;
using Sts2Headless.Cheats;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Coverage for the debug/gain_stars wire surface — the cheat MechanicSweep
// .CardSweep relies on to stage Regent's Stars budget so high-cost cards
// (COMET 5*, SEVEN_STARS 7*, NEUTRON_AEGIS 5*, …) can exercise their OnPlay
// without driving multi-turn Stars accrual from the single DivineRight
// relic Regent starts with. Mirrors DebugSetEnergyTests' shape and
// DebugDisabledTests covers the AD-7 negative case.
public class DebugGainStarsTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;
    public DebugGainStarsTests(HostSubprocess host) => _host = host;

    [Fact]
    public async Task GainStars_AccumulatesOnPlayerCombatState()
    {
        await RunFixtures.StartFreshRunAtMap(_host, character: Character.Regent, seed: 42uL);
        await _host.SendAsync<DebugStartCombatResult>(
            "debug/start_combat", new DebugStartCombatParams(EncounterId: "SLIMES_NORMAL"));

        // Stars isn't on the wire's CombatState DTO (yet); read the
        // starting value via a 0-grant. Regent starts with stars from
        // DivineRight so the baseline is non-zero.
        var baseline = await _host.SendAsync<DebugGainStarsResult>(
            "debug/gain_stars", new DebugGainStarsParams(Amount: 0));

        // Grant 7 stars — enough for SEVEN_STARS (the highest-cost Stars
        // card in the manifest). Result reports the post-write total so a
        // caller can verify accumulation.
        var resp = await _host.SendAsync<DebugGainStarsResult>(
            "debug/gain_stars", new DebugGainStarsParams(Amount: 7));
        Assert.True(resp.Ok);
        Assert.Equal(baseline.Stars + 7, resp.Stars);
    }

    [Fact]
    public async Task GainStarsZero_IsNoop()
    {
        await RunFixtures.StartFreshRunAtMap(_host, character: Character.Regent, seed: 42uL);
        await _host.SendAsync<DebugStartCombatResult>(
            "debug/start_combat", new DebugStartCombatParams(EncounterId: "SLIMES_NORMAL"));

        // Amount=0 is a permitted no-op. Useful for callers that compute
        // a grant from a dynamic source (e.g. "grant the cost of this
        // card") and want a 0 path that doesn't error. Calling twice in
        // a row with 0 should report the same total.
        var first = await _host.SendAsync<DebugGainStarsResult>(
            "debug/gain_stars", new DebugGainStarsParams(Amount: 0));
        var second = await _host.SendAsync<DebugGainStarsResult>(
            "debug/gain_stars", new DebugGainStarsParams(Amount: 0));
        Assert.Equal(first.Stars, second.Stars);
    }

    [Fact]
    public async Task NegativeAmount_ReturnsInvalidParamsError()
    {
        await RunFixtures.StartFreshRunAtMap(_host, character: Character.Regent, seed: 42uL);
        await _host.SendAsync<DebugStartCombatResult>(
            "debug/start_combat", new DebugStartCombatParams(EncounterId: "SLIMES_NORMAL"));

        // Negative grants would silently bypass UnplayableReason
        // .NotEnoughStars (a card could drain the player below 0 then
        // re-pass the cost check). Reject — callers wanting to test spend
        // should drive a real card play.
        var err = await _host.ExpectErrorAsync(
            "debug/gain_stars", new DebugGainStarsParams(Amount: -1));
        Assert.Equal(WireErrorCode.InvalidParams, err.Code);
        Assert.Contains("amount must be >= 0", err.Message);
    }
}
