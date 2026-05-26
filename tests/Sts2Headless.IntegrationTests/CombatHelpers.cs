using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Shared driver for combat-reward tests. Lives in its own file (rather than
// as a private static on CombatTests) so the reward-related test cases can
// split into their own classes — each class becomes its own xUnit collection
// and runs in parallel, which is how this suite breaks the ~23s
// CombatTests-as-one-collection wall-time floor.
internal static class CombatHelpers
{
    // Walks from a fresh run through the first reachable combat to the point
    // where rewards surface. Returns the RewardsState on the snapshot that
    // first reports rewards. Throws if rewards never surface within the
    // safety iteration cap — that's a regression worth a loud failure.
    //
    // Idempotent on the run state: callers may have already issued run/new
    // before calling (to capture pre-combat state); we re-issue only when the
    // host reports no active run. Every run lands at the Neow EventRoom, so
    // we route via RunFixtures.DismissNeow before walking the map.
    public static async Task<RewardsState> DriveCombatToRewards(HostSubprocess host)
    {
        RunStateResult start;
        try { start = await host.SendAsync<RunStateResult>("run/state"); }
        catch
        {
            await host.SendAsync<RunNewResult>("run/new", new RunNewParams(Seed: 42uL));
            start = await RunFixtures.DismissNeow(host);
        }
        if (start.CurrentRoomType == RoomType.EventRoom)
        {
            start = await RunFixtures.DismissNeow(host);
        }
        if (start.CurrentRoomType != RoomType.MapRoom)
        {
            await host.SendAsync<RunNewResult>("run/new", new RunNewParams(Seed: 42uL));
            start = await RunFixtures.DismissNeow(host);
        }
        var monsterNode = start.AvailableMapNodes.First(n => n.Type == MapNodeType.Monster && n.Row > 0);
        var snap = await host.SendAsync<RunSelectMapNodeResult>(
            "run/select_map_node", new RunSelectMapNodeParams(Col: monsterNode.Col, Row: monsterNode.Row));

        CombatState? combat = snap.CombatState;
        RewardsState? rewards = snap.RewardsState;
        for (var safety = 0; safety < 40 && rewards is null; safety++)
        {
            var attack = combat?.Hand.FirstOrDefault(c =>
                c.CanPlay && c.Cost <= combat.Energy && c.TargetType == TargetType.AnyEnemy);
            if (attack is not null)
            {
                var afterPlay = await host.SendAsync<RunPlayCardResult>(
                    "run/play_card", new RunPlayCardParams(CardIndex: attack.Index, TargetIndex: 0));
                combat = afterPlay.CombatState;
                rewards = afterPlay.RewardsState;
            }
            else
            {
                var afterEnd = await host.SendAsync<RunEndTurnResult>("run/end_turn");
                combat = afterEnd.CombatState;
                rewards = afterEnd.RewardsState;
            }
        }

        Assert.NotNull(rewards);
        return rewards!;
    }
}
