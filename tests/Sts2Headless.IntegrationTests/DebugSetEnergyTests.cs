using Sts2Headless.Protocol;
using Sts2Headless.Cheats;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Coverage for the debug/set_energy wire surface — the cheat MechanicSweep
// .CardSweep relies on to stage cards whose EnergyCost exceeds the
// character default of 3 (BURY 4e, METEOR_STRIKE 5e, BANSHEES_CRY's
// per-combat ratcheted cost, etc.). The fixture passes --enable-debug
// (HostSubprocess.cs); the negative-case "rejected when disabled" lives
// in DebugDisabledTests which spins up its own subprocess without the
// flag.
public class DebugSetEnergyTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;
    public DebugSetEnergyTests(HostSubprocess host) => _host = host;

    [Fact]
    public async Task SetEnergyAndMaxEnergy_UpdatesPlayerCombatState()
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));
        await _host.SendAsync<DebugStartCombatResult>(
            "debug/start_combat", new DebugStartCombatParams(EncounterId: "SLIMES_NORMAL"));

        // Raise both energy AND maxEnergy. After ResetEnergy at next turn-
        // start the cap should hold — Player.MaxEnergy is what ResetEnergy
        // refills from, so set_energy(maxEnergy: 20) outlives a turn-rollover.
        var resp = await _host.SendAsync<DebugSetEnergyResult>(
            "debug/set_energy", new DebugSetEnergyParams(Energy: 15, MaxEnergy: 20));
        Assert.True(resp.Ok);
        Assert.Equal(15, resp.Energy);
        Assert.Equal(20, resp.MaxEnergy);

        var state = await _host.SendAsync<RunStateResult>("run/state");
        Assert.NotNull(state.CombatState);
        Assert.Equal(15, state.CombatState!.Energy);
        Assert.Equal(20, state.CombatState!.MaxEnergy);
    }

    [Fact]
    public async Task SetEnergyOnly_LeavesMaxEnergyUnchanged()
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));
        await _host.SendAsync<DebugStartCombatResult>(
            "debug/start_combat", new DebugStartCombatParams(EncounterId: "SLIMES_NORMAL"));
        var before = await _host.SendAsync<RunStateResult>("run/state");

        // Setting energy alone is the canonical "I want to play one
        // high-cost card this turn" path — MaxEnergy stays at the
        // character default so end-of-turn rollover behaves normally.
        var resp = await _host.SendAsync<DebugSetEnergyResult>(
            "debug/set_energy", new DebugSetEnergyParams(Energy: 7));
        Assert.True(resp.Ok);
        Assert.Equal(7, resp.Energy);
        Assert.Equal(before.CombatState!.MaxEnergy, resp.MaxEnergy);
    }

    [Fact]
    public async Task EmptyParams_ReturnsInvalidParamsError()
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));
        await _host.SendAsync<DebugStartCombatResult>(
            "debug/start_combat", new DebugStartCombatParams(EncounterId: "SLIMES_NORMAL"));

        var err = await _host.ExpectErrorAsync(
            "debug/set_energy", new DebugSetEnergyParams());
        Assert.Equal(WireErrorCode.InvalidParams, err.Code);
        Assert.Contains("at least one", err.Message);
    }

    [Fact]
    public async Task NegativeEnergy_ReturnsInvalidParamsError()
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));
        await _host.SendAsync<DebugStartCombatResult>(
            "debug/start_combat", new DebugStartCombatParams(EncounterId: "SLIMES_NORMAL"));

        var err = await _host.ExpectErrorAsync(
            "debug/set_energy", new DebugSetEnergyParams(Energy: -1));
        Assert.Equal(WireErrorCode.InvalidParams, err.Code);
        Assert.Contains("energy must be >= 0", err.Message);
    }

    [Fact]
    public async Task ZeroMaxEnergy_ReturnsInvalidParamsError()
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));
        await _host.SendAsync<DebugStartCombatResult>(
            "debug/start_combat", new DebugStartCombatParams(EncounterId: "SLIMES_NORMAL"));

        // maxEnergy=0 locks the player out of every cost-bearing play —
        // almost certainly a typo. Reject up-front so the regression
        // signal stays sharp.
        var err = await _host.ExpectErrorAsync(
            "debug/set_energy", new DebugSetEnergyParams(MaxEnergy: 0));
        Assert.Equal(WireErrorCode.InvalidParams, err.Code);
        Assert.Contains("maxEnergy must be >= 1", err.Message);
    }
}
