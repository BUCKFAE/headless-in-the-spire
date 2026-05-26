using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// FightToCompletion is the heaviest combat test in the suite (full fight +
// reward drain back to MapRoom, ~5s on a warm host). It lives alone so it
// can run in parallel with the other reward-cycle tests rather than
// stacking serially behind them.
public class CombatFightTests
{
    [Fact]
    public async Task FightToCompletion_SurfacesRewards_ThenAdvancesBackToMapRoom()
    {
        // Drive a full combat: spam attack cards every turn until either the
        // enemy dies (combat ends → rewards surface) or we run out of safety
        // iterations. The Ironclad starting deck plus Strike+Bash damage
        // outpaces the Crawler's incoming damage, so the fight resolves long
        // before the iteration cap. After rewards are consumed (either by
        // skipping or claiming the first option) the host advances back to
        // MapRoom on the next snapshot.
        await using var host = new HostSubprocess();

        var start = await RunFixtures.StartFreshRunAtMap(host, seed: 42uL);
        var monsterNode = start.AvailableMapNodes.First(n => n.Type == MapNodeType.Monster && n.Row > 0);
        var snap = await host.SendAsync<RunSelectMapNodeResult>(
            "run/select_map_node", new RunSelectMapNodeParams(Col: monsterNode.Col, Row: monsterNode.Row));

        RoomType currentRoom = snap.CurrentRoomType;
        CombatState? combat = snap.CombatState;
        RewardsState? rewards = snap.RewardsState;
        Assert.NotNull(combat);
        Assert.Null(rewards); // No rewards while the fight is live.

        for (var safety = 0; safety < 40 && currentRoom == RoomType.CombatRoom && rewards is null; safety++)
        {
            // Play any playable attack card we can afford, targeting enemy 0.
            var attack = combat!.Hand.FirstOrDefault(c =>
                c.CanPlay && c.Cost <= combat.Energy && c.TargetType == TargetType.AnyEnemy);
            if (attack is not null)
            {
                var afterPlay = await host.SendAsync<RunPlayCardResult>(
                    "run/play_card", new RunPlayCardParams(CardIndex: attack.Index, TargetIndex: 0));
                currentRoom = afterPlay.CurrentRoomType;
                combat = afterPlay.CombatState;
                rewards = afterPlay.RewardsState;
                if (rewards is not null || currentRoom != RoomType.CombatRoom) break;
                continue;
            }

            // Out of attack options — end the turn.
            var afterEnd = await host.SendAsync<RunEndTurnResult>("run/end_turn");
            currentRoom = afterEnd.CurrentRoomType;
            combat = afterEnd.CombatState;
            rewards = afterEnd.RewardsState;
        }

        // Combat resolved: rewards now block the auto-advance to MapRoom.
        Assert.NotNull(rewards);
        Assert.NotEmpty(rewards!.Available);

        // Drain every pending reward — claim non-card rewards (gold/relic/
        // potion can't be skipped); claim the first card option for any card
        // reward (the test asserts the cycle runs to completion, not which
        // pick is "best"). After the last reward consumes, the next snapshot
        // should report MapRoom.
        RoomType room = currentRoom;
        for (var safety = 0; safety < 12 && rewards is not null && rewards.Available.Count > 0; safety++)
        {
            // Always operate on index 0 — the host re-numbers the available
            // list after each consumption, so the new "first reward" advances
            // through whatever's left.
            var head = rewards.Available[0];
            int? cardIndex = head.Kind == RewardKind.Card && head.Cards is { Count: > 0 } ? 0 : null;
            var afterSelect = await host.SendAsync<RunSelectRewardResult>(
                "run/select_reward", new RunSelectRewardParams(RewardIndex: 0, CardIndex: cardIndex));
            room = afterSelect.CurrentRoomType;
            rewards = afterSelect.RewardsState;
        }

        Assert.Null(rewards);
        Assert.Equal(RoomType.MapRoom, room);
    }
}
