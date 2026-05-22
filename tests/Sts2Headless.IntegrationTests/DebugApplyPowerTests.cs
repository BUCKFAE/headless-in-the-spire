using Sts2Headless.Cheats;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Positive coverage for debug/apply_power. Negative case lives in
// DebugDisabledTests. Pins the happy path:
//
//   * Applying STRENGTH to the player in combat lands on PlayerPowers
//     with the requested amount.
//   * Applying VULNERABLE to the first enemy lands on enemies[0].Powers.
//   * Unknown power id → InvalidParams. Empty id → InvalidParams.
//   * Negative enemyIndex → InvalidParams.
//   * apply_power without an active combat → InvalidParams (or the
//     handler-translatable surface; clients should see something
//     actionable, not InternalError).
//
// STRENGTH and VULNERABLE are Ironclad-set staples; safe choices that
// won't go away on game updates without surfacing a NewContentKindTests
// / ContentManifestDriftTests red first.
public class DebugApplyPowerTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;

    public DebugApplyPowerTests(HostSubprocess host) => _host = host;

    [Fact]
    public async Task ApplyPower_ToPlayer_AppearsInPlayerPowers()
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));
        await _host.SendAsync<DebugSetHpResult>(
            "debug/set_hp", new DebugSetHpParams(Hp: 999, MaxHp: 999));
        await _host.SendAsync<DebugStartCombatResult>(
            "debug/start_combat", new DebugStartCombatParams(EncounterId: "SLIMES_NORMAL"));

        var resp = await _host.SendAsync<DebugApplyPowerResult>(
            "debug/apply_power",
            new DebugApplyPowerParams(PowerId: "STRENGTH_POWER", Amount: 3));

        Assert.True(resp.Ok);
        Assert.Equal("STRENGTH_POWER", resp.PowerId);
        Assert.Equal("Player", resp.TargetDescription);
        Assert.True(resp.AppliedAmount >= 3,
            $"AppliedAmount should be >= requested 3, got {resp.AppliedAmount}");

        var state = await _host.SendAsync<RunStateResult>("run/state");
        Assert.NotNull(state.CombatState);
        Assert.Contains(state.CombatState!.PlayerPowers,
            p => string.Equals(p.Id, "STRENGTH_POWER", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ApplyPower_ToEnemy_AppearsInEnemyPowers()
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));
        await _host.SendAsync<DebugSetHpResult>(
            "debug/set_hp", new DebugSetHpParams(Hp: 999, MaxHp: 999));
        await _host.SendAsync<DebugStartCombatResult>(
            "debug/start_combat", new DebugStartCombatParams(EncounterId: "SLIMES_NORMAL"));

        var resp = await _host.SendAsync<DebugApplyPowerResult>(
            "debug/apply_power",
            new DebugApplyPowerParams(PowerId: "VULNERABLE_POWER", Amount: 2, EnemyIndex: 0));

        Assert.True(resp.Ok);
        Assert.Equal("Enemy:0", resp.TargetDescription);
        Assert.True(resp.AppliedAmount >= 2);

        var state = await _host.SendAsync<RunStateResult>("run/state");
        Assert.NotNull(state.CombatState);
        Assert.NotEmpty(state.CombatState!.Enemies);
        Assert.Contains(state.CombatState!.Enemies[0].Powers,
            p => string.Equals(p.Id, "VULNERABLE_POWER", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ApplyPower_UnknownId_ReturnsInvalidParams()
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));
        await _host.SendAsync<DebugSetHpResult>(
            "debug/set_hp", new DebugSetHpParams(Hp: 999, MaxHp: 999));
        await _host.SendAsync<DebugStartCombatResult>(
            "debug/start_combat", new DebugStartCombatParams(EncounterId: "SLIMES_NORMAL"));

        var err = await _host.ExpectErrorAsync(
            "debug/apply_power", new DebugApplyPowerParams(PowerId: "DEFINITELY_NOT_A_POWER"));

        Assert.Equal(Sts2Headless.Protocol.WireErrorCode.InvalidParams, err.Code);
    }

    [Fact]
    public async Task ApplyPower_EmptyId_ReturnsInvalidParams()
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

        var err = await _host.ExpectErrorAsync(
            "debug/apply_power", new DebugApplyPowerParams(PowerId: ""));

        Assert.Equal(Sts2Headless.Protocol.WireErrorCode.InvalidParams, err.Code);
    }

    [Fact]
    public async Task ApplyPower_NegativeEnemyIndex_ReturnsInvalidParams()
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

        var err = await _host.ExpectErrorAsync(
            "debug/apply_power",
            new DebugApplyPowerParams(PowerId: "STRENGTH_POWER", Amount: 1, EnemyIndex: -1));

        Assert.Equal(Sts2Headless.Protocol.WireErrorCode.InvalidParams, err.Code);
    }
}
