using Sts2Headless.Cheats;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Coverage for the debug/kill_all_enemies wire surface. The cheat exists
// as a forcing function for full-game end-to-end tests where the agent
// can't honestly clear every combat but the test still needs to exercise
// the post-combat pipeline (rewards, map progression, Neow/event choices
// in the replay). The contract: in combat → every alive enemy drops to
// 0 HP, the engine flips IsInProgress=false, rewards generate naturally;
// outside combat → no-op that returns killed=0 (NOT an error, because
// full-run drivers fire this on every tick).
//
// Negative cases (gate behaviour when --enable-debug is off) live in
// DebugDisabledTests — same pattern as the other cheats.
public class DebugKillAllEnemiesTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;

    public DebugKillAllEnemiesTests(HostSubprocess host) => _host = host;

    [Fact]
    public async Task OutsideCombat_ReturnsKilledZero()
    {
        // Fresh run is on the map, not in combat. The cheat must succeed
        // (Ok=true) with killed=0; tests fire this on every state read
        // and don't want a spurious exception when combat hasn't started.
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

        var resp = await _host.SendAsync<DebugKillAllEnemiesResult>(
            "debug/kill_all_enemies", new DebugKillAllEnemiesParams());

        Assert.True(resp.Ok);
        Assert.Equal(0, resp.Killed);
        Assert.False(resp.CombatEnded);
    }

    [Fact]
    public async Task InCombat_KillsEveryAliveEnemy_AndCombatEnds()
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

        // Walk into the first reachable combat. We deliberately don't use
        // CombatHelpers here — that helper drives to rewards, which means
        // combat is already over before we fire the cheat. We want to
        // arrive INSIDE combat and have the cheat be what ends it.
        var start = await _host.SendAsync<RunStateResult>("run/state");
        var monsterNode = start.AvailableMapNodes.First(n => n.Type == MapNodeType.Monster && n.Row > 0);
        var entered = await _host.SendAsync<RunSelectMapNodeResult>(
            "run/select_map_node", new RunSelectMapNodeParams(Col: monsterNode.Col, Row: monsterNode.Row));

        Assert.NotNull(entered.CombatState);
        Assert.True(entered.CombatState!.IsInProgress, "expected to land in an active combat");
        // Wire Enemy doesn't surface IsAlive; Hp>0 is the proxy. Same
        // predicate the existing CombatPowersTests etc. use.
        var aliveBefore = entered.CombatState.Enemies.Count(e => e.Hp > 0);
        Assert.True(aliveBefore > 0, "expected at least one alive enemy on entry");

        var resp = await _host.SendAsync<DebugKillAllEnemiesResult>(
            "debug/kill_all_enemies", new DebugKillAllEnemiesParams());

        Assert.True(resp.Ok);
        Assert.Equal(aliveBefore, resp.Killed);
        Assert.True(resp.CombatEnded, "engine should have flipped IsInProgress=false after every enemy hit 0 HP");

        // Post-state confirms the engine routed through EndCombatInternal —
        // either rewards are pending or we're back on the map. Either way,
        // combat is no longer in progress.
        var post = await _host.SendAsync<RunStateResult>("run/state");
        Assert.False(post.CombatState?.IsInProgress ?? false);
    }

    [Fact]
    public async Task SecondCall_AfterCombatEnded_IsNoOp()
    {
        // Idempotency: full-run drivers fire the cheat on every tick. Once
        // combat is over, the next call has nothing to do and must report
        // killed=0 / combatEnded=false (not "true, because combat isn't in
        // progress" — combatEnded is "the cheat just ended a combat", not
        // "combat is currently ended").
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));
        var start = await _host.SendAsync<RunStateResult>("run/state");
        var monsterNode = start.AvailableMapNodes.First(n => n.Type == MapNodeType.Monster && n.Row > 0);
        await _host.SendAsync<RunSelectMapNodeResult>(
            "run/select_map_node", new RunSelectMapNodeParams(Col: monsterNode.Col, Row: monsterNode.Row));

        var first = await _host.SendAsync<DebugKillAllEnemiesResult>(
            "debug/kill_all_enemies", new DebugKillAllEnemiesParams());
        Assert.True(first.CombatEnded);

        var second = await _host.SendAsync<DebugKillAllEnemiesResult>(
            "debug/kill_all_enemies", new DebugKillAllEnemiesParams());
        Assert.True(second.Ok);
        Assert.Equal(0, second.Killed);
        Assert.False(second.CombatEnded);
    }
}
