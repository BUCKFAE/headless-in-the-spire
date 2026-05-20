using Sts2Headless.Protocol;
using Sts2Headless.Cheats;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Coverage for debug/start_combat. Mirrors the DebugSetHpTests / DebugReplaceDeckTests
// shape: a fresh run, then the cheat, then assert the engine actually
// flipped into combat against the requested encounter. The end-to-end
// EveryEncounterSmokeTests sweep (End2EndTests) iterates every encounter
// id; this file is the single-slice contract for the wire surface itself.
public class DebugStartCombatTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;

    public DebugStartCombatTests(HostSubprocess host) => _host = host;

    [Fact]
    public async Task StartCombat_FromMapRoom_FlipsRoomAndPopulatesCombatState()
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

        // Sanity: a fresh run lands in MapRoom, not Combat. If this ever
        // changes, the cheat-from-map invariant below needs revisiting.
        var beforeState = await _host.SendAsync<RunStateResult>("run/state");
        Assert.Equal(RoomType.MapRoom, beforeState.CurrentRoomType);
        Assert.Null(beforeState.CombatState);

        var resp = await _host.SendAsync<DebugStartCombatResult>(
            "debug/start_combat", new DebugStartCombatParams(EncounterId: "SLIMES_NORMAL"));
        Assert.True(resp.Ok);
        Assert.Equal("SLIMES_NORMAL", resp.EncounterId);
        Assert.True(resp.InProgress, "engine should report combat in progress after start_combat");
        Assert.True(resp.EnemyCount > 0, $"expected at least one alive enemy, got {resp.EnemyCount}");

        // The wire snapshot should now show CombatState present with enemies.
        var post = await _host.SendAsync<RunStateResult>("run/state");
        Assert.NotNull(post.CombatState);
        Assert.NotEmpty(post.CombatState!.Enemies);
    }

    [Fact]
    public async Task StartCombat_UnknownEncounter_ReturnsInvalidParamsError()
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

        var err = await _host.ExpectErrorAsync(
            "debug/start_combat", new DebugStartCombatParams(EncounterId: "NOT_A_REAL_ENCOUNTER"));
        Assert.Equal(WireErrorCode.InvalidParams, err.Code);
        Assert.Contains("NOT_A_REAL_ENCOUNTER", err.Message);
    }

    [Fact]
    public async Task StartCombat_EmptyEncounterId_ReturnsInvalidParamsError()
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

        var err = await _host.ExpectErrorAsync(
            "debug/start_combat", new DebugStartCombatParams(EncounterId: ""));
        Assert.Equal(WireErrorCode.InvalidParams, err.Code);
        Assert.Contains("encounterId", err.Message);
    }

    [Fact]
    public async Task StartCombat_ReentryOverwritesPreviousCombat()
    {
        // Goal: prove that start_combat is idempotent enough to be called
        // back-to-back. The sweep test in End2EndTests relies on this — it
        // doesn't run/new per encounter, just refreshes deck/HP and starts
        // the next combat.
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

        var first = await _host.SendAsync<DebugStartCombatResult>(
            "debug/start_combat", new DebugStartCombatParams(EncounterId: "SLIMES_NORMAL"));
        Assert.True(first.InProgress);

        var second = await _host.SendAsync<DebugStartCombatResult>(
            "debug/start_combat", new DebugStartCombatParams(EncounterId: "EXOSKELETONS_NORMAL"));
        Assert.True(second.InProgress);
        Assert.Equal("EXOSKELETONS_NORMAL", second.EncounterId);
    }
}
