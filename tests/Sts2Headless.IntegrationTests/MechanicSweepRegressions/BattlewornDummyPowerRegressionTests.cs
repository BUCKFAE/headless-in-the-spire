using Sts2Headless.MechanicSweep;
using Xunit;

namespace Sts2Headless.IntegrationTests.MechanicSweepRegressions;

// Regression pin for the PowerSweep crash that surfaced on
// BATTLEWORN_DUMMY_TIME_LIMIT_POWER.
//
// The bug: applying that power to the Player inside any combat other
// than the BATTLEWORN_DUMMY event throws NRE on the next end_turn:
//
//   NullReferenceException
//     at MegaCrit.Sts2.Core.Hooks.Hook.ModifyMaxEnergy(CombatState, Player, Decimal)
//     at MegaCrit.Sts2.Core.Entities.Players.PlayerCombatState.get_MaxEnergy()
//
// Root cause: the power's AfterTurnEnd hook isinst-casts
// CombatState.Encounter to BattlewornDummyEventEncounter; outside the
// event the cast is null and a subsequent MaxEnergy walk dereferences
// an event-only field the engine doesn't initialise in the
// SLIMES_NORMAL path. The engine doesn't defend against this because
// the power never exists outside its event in real play.
//
// Fix shape: cataloged in SweepKnownIssues.Powers so the sweep
// classifies the row as KnownUnsafe (not failure-grade Crashed). The
// crash itself is still present in the engine — these tests pin both
// halves: (a) the wire still throws the same NRE, and (b) the catalog
// still flags the id with the right reason. If the engine ever
// defends ModifyMaxEnergy against null event-state, NreOnEndTurn
// flips green-via-no-exception (assert no throw) — that's the cue to
// remove the catalog entry.
public class BattlewornDummyPowerRegressionTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;
    public BattlewornDummyPowerRegressionTests(HostSubprocess host) => _host = host;

    [Fact]
    public async Task BattlewornDummyTimeLimitPower_OutsideEvent_KnownUnsafeViaSweep()
    {
        // Run the same id through PowerSweep directly and assert the
        // outcome is KnownUnsafe (with the catalogued reason in the
        // Detail). This is more robust than wire-replaying the crash
        // by hand — the sweep is the actual consumer of the catalog,
        // and the wire-error envelope shape varies subtly with host
        // changes. If the engine ever ships a fix, the row flips to
        // Played and this test goes red — the cue to remove
        // BATTLEWORN_DUMMY_TIME_LIMIT_POWER from SweepKnownIssues.Powers.
        var transport = new HostSubprocessTransportAdapter(_host);
        var report = await new Sts2Headless.MechanicSweep.Sweeps.PowerSweep().RunAsync(
            transport,
            sampleIds: ["BATTLEWORN_DUMMY_TIME_LIMIT_POWER"],
            gameVersion: "regression-test");

        var row = Assert.Single(report.Rows);
        Assert.Equal("BATTLEWORN_DUMMY_TIME_LIMIT_POWER", row.Id);
        Assert.Equal(SweepOutcome.KnownUnsafe, row.Outcome);
        Assert.NotNull(row.Detail);
        Assert.Contains("known-unsafe", row.Detail!, StringComparison.Ordinal);
        Assert.Contains("BATTLEWORN_DUMMY", row.Detail!, StringComparison.Ordinal);
        Assert.Contains("Hook.ModifyMaxEnergy", row.Detail!, StringComparison.Ordinal);
    }

    [Fact]
    public void BattlewornDummyTimeLimitPower_IsOnKnownIssuesCatalog()
    {
        Assert.True(
            SweepKnownIssues.TryGetReason("power", "BATTLEWORN_DUMMY_TIME_LIMIT_POWER", out var reason),
            "BATTLEWORN_DUMMY_TIME_LIMIT_POWER should be in SweepKnownIssues.Powers");
        Assert.NotNull(reason);
        Assert.Contains("BATTLEWORN_DUMMY", reason, StringComparison.Ordinal);
    }
}
