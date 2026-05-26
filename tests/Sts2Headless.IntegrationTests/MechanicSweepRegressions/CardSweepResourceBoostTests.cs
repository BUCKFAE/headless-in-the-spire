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
    public async Task AnyAllyCard_IsFilteredFromSweep()
    {
        // AnyAlly cards (co-op multiplayer only) are skipped at iteration
        // time — they're structurally impossible to exercise in single-
        // player STS2 (PlayerCreatures.Count is always 1). Asking the
        // sweep for MIMIC should produce zero rows. Drift here means
        // CardSweep.AnyAllyMultiplayerOnly broke or the filter site moved.
        var transport = new HostSubprocessTransportAdapter(_host);
        var report = await new Sts2Headless.MechanicSweep.Sweeps.CardSweep().RunAsync(
            transport,
            sampleIds: new[] { "MIMIC" },
            gameVersion: "regression-test");

        Assert.Empty(report.Rows);
    }

    [Fact]
    public async Task PactsEnd_PlaysWithStagedExhaustPile()
    {
        // PACTS_END's IsPlayable virtual gates on Exhaust pile count
        // (>= ~3). CardSweep now stages BLOODLETTING x3 in the deck and
        // pre-plays them via PreStageHandAsync to satisfy the predicate.
        // Drift here means CustomStagingDeckFor lost the PACTS_END entry
        // or PreStageHandAsync stopped playing the staged cards.
        var transport = new HostSubprocessTransportAdapter(_host);
        var report = await new Sts2Headless.MechanicSweep.Sweeps.CardSweep().RunAsync(
            transport,
            sampleIds: new[] { "PACTS_END" },
            gameVersion: "regression-test");

        var row = Assert.Single(report.Rows);
        Assert.True(row.Outcome == SweepOutcome.Played,
            $"Expected Played but got {row.Outcome}: {row.Detail}");
    }

    [Fact]
    public async Task Clash_PlaysWithStagedAllAttackHand()
    {
        // CLASH's IsPlayable virtual requires every card in hand to be
        // an Attack. CardSweep stages an all-STRIKE deck so the predicate
        // holds on turn 1. Drift here means CustomStagingDeckFor lost the
        // CLASH entry.
        var transport = new HostSubprocessTransportAdapter(_host);
        var report = await new Sts2Headless.MechanicSweep.Sweeps.CardSweep().RunAsync(
            transport,
            sampleIds: new[] { "CLASH" },
            gameVersion: "regression-test");

        var row = Assert.Single(report.Rows);
        Assert.Equal(SweepOutcome.Played, row.Outcome);
    }
}
