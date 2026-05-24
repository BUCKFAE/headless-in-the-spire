using Sts2Headless.MechanicSweep;
using Xunit;

namespace Sts2Headless.IntegrationTests.MechanicSweepRegressions;

// Regression pin for the CardSweep resource-boost fixture.
//
// Before the fix, CardSweep started SLIMES_NORMAL with the character's
// default 3-energy budget and Regent's zero-baseline Stars meter. Result:
//   * 7 Regent cards (COMET 5*, SEVEN_STARS 7*, NEUTRON_AEGIS 5*, DEVASTATE
//     4*, DECISIONS_DECISIONS 6*, ROYAL_GAMBLE 5*, THE_SMITH 4*) refused
//     with bitflag=32 NotEnoughStars.
//   * 3 character cards (BURY 4e, METEOR_STRIKE 5e, BANSHEES_CRY's ratcheted
//     cost) refused with bitflag=16 NotEnoughEnergy.
//
// The fix wires two cheats (debug/set_energy + debug/gain_stars) and calls
// them at fixture setup (CardSweep.ResourceBoost = 20). The 10 cards above
// now Play through CardSweep.
//
// These tests pin one card per resource axis under the sweep itself —
// a regression in the fixture's cheat plumbing (e.g. SetEnergyAsync or
// GainStarsAsync stops being called, or the binding loses _pcsStars /
// _playerMaxEnergy) surfaces here ahead of the slow full sweep.
public class CardSweepResourceBoostTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;
    public CardSweepResourceBoostTests(HostSubprocess host) => _host = host;

    // One card per bitflag bucket. SEVEN_STARS is the highest Stars cost
    // in the manifest (7); METEOR_STRIKE is a high-energy Defect attack
    // (5); a third card from a different character keeps coverage broad.
    [Theory]
    [InlineData("SEVEN_STARS")]   // Stars cost 7 — needs gain_stars
    [InlineData("METEOR_STRIKE")] // Energy cost 5 — needs set_energy
    [InlineData("BURY")]          // Energy cost 4, different character — confirms cheat works under non-Defect
    public async Task ResourceBoosted_PreviouslyUnplayableCard_NowPlays(string cardId)
    {
        var transport = new HostSubprocessTransportAdapter(_host);
        var report = await new Sts2Headless.MechanicSweep.Sweeps.CardSweep().RunAsync(
            transport,
            sampleIds: new[] { cardId },
            gameVersion: "regression-test");

        var row = Assert.Single(report.Rows);
        Assert.Equal(cardId, row.Id);
        // The sweep's resource boost (debug/set_energy 20 + debug/gain_stars 20)
        // should make all three cards play cleanly on turn 1.
        Assert.Equal(SweepOutcome.Played, row.Outcome);
    }

    [Fact]
    public async Task AnyAllyCard_StillRefuses_WithCorrectEmpiricalDetail()
    {
        // Negative pin: the resource boost does NOT unblock co-op AnyAlly
        // cards (they need a second human Player, not more energy). Confirm
        // the row stays Unplayable AND the Detail carries the empirical
        // catalog reason — drift here means SweepKnownIssues.TryGetExpectedRefusal
        // wiring broke, OR the catalog entry was accidentally removed.
        var transport = new HostSubprocessTransportAdapter(_host);
        var report = await new Sts2Headless.MechanicSweep.Sweeps.CardSweep().RunAsync(
            transport,
            sampleIds: new[] { "MIMIC" },
            gameVersion: "regression-test");

        var row = Assert.Single(report.Rows);
        Assert.Equal(SweepOutcome.Unplayable, row.Outcome);
        Assert.NotNull(row.Detail);
        Assert.Contains("expected-refusal", row.Detail!, StringComparison.Ordinal);
        Assert.Contains("AnyAlly", row.Detail!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PactsEndCard_StillRefuses_OnIsPlayableOverride()
    {
        // Negative pin: PACTS_END's IsPlayable virtual gates on the
        // Exhaust pile count, which the smoke fixture leaves empty. The
        // resource boost doesn't help. Confirm the row stays Unplayable
        // AND the Detail carries the empirical reason citing the IL
        // (Exhaust pile + DynamicVars.Cards predicate).
        var transport = new HostSubprocessTransportAdapter(_host);
        var report = await new Sts2Headless.MechanicSweep.Sweeps.CardSweep().RunAsync(
            transport,
            sampleIds: new[] { "PACTS_END" },
            gameVersion: "regression-test");

        var row = Assert.Single(report.Rows);
        Assert.Equal(SweepOutcome.Unplayable, row.Outcome);
        Assert.NotNull(row.Detail);
        Assert.Contains("IsPlayable", row.Detail!, StringComparison.Ordinal);
        Assert.Contains("Exhaust", row.Detail!, StringComparison.Ordinal);
    }
}
