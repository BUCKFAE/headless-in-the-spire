using System.Text.Json.Nodes;
using Sts2Headless.Protocol.Methods;
using Sts2Headless.Replay;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Locks in the post-fix shape of run.json's per-floor player_stats.
// The engine's `UpdatePlayerStatsInMapPointHistory` is gated on
// `if (TestMode.IsOn || State == null) return;` — and our bootstrap
// turns TestMode on. Without intervention, every map_point_history
// entry has `current_hp: 0, max_hp: 0, current_gold: 0`, which the
// viewer then surfaces as "0/0 — died" on every floor. The recorder
// must bypass that gate so HP / gold actually populate.
//
// The test drives one combat to rewards under a RecordingHost, then
// forces a flush via a second run/new, then reads back the freshly
// written run.json. The first map_point_history entry corresponds to
// the combat that just resolved; its player_stats[0] is what the
// viewer renders on its first floor row.
public class RunHistoryFloorStatsTests
{
    [Fact]
    public async Task RecordedRun_PopulatesHpAndGold_InMapPointHistory()
    {
        using var tempReplays = new TempReplayRoot();
        await using var host = RecordingHost.Start(tempReplays.Path);

        await host.SendAsync<RunNewResult>("run/new", new RunNewParams(Seed: 42uL));
        var snap = await host.SendAsync<RunStateResult>("run/state");
        var monsterNode = snap.AvailableMapNodes.First(n => n.Type == MapNodeType.Monster && n.Row > 0);
        var entered = await host.SendAsync<RunSelectMapNodeResult>(
            "run/select_map_node", new RunSelectMapNodeParams(Col: monsterNode.Col, Row: monsterNode.Row));

        CombatState? combat = entered.CombatState;
        RewardsState? rewards = entered.RewardsState;
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

        // Drain every reward so the card-pick path
        // (Sts2Bindings.ClaimCardReward → StampCardChoices) actually
        // fires. The agent picks index 0 of every card reward; non-
        // card rewards (gold / relic / potion) just get claimed.
        for (var safety = 0; safety < 12 && rewards is not null && rewards.Available.Count > 0; safety++)
        {
            var head = rewards.Available[0];
            int? cardIndex = head.Kind == RewardKind.Card && head.Cards is { Count: > 0 } ? 0 : null;
            var afterSelect = await host.SendAsync<RunSelectRewardResult>(
                "run/select_reward", new RunSelectRewardParams(RewardIndex: 0, CardIndex: cardIndex));
            rewards = afterSelect.RewardsState;
        }
        Assert.Null(rewards);

        // Force flush of the first run's manifest + run.json by
        // starting a second run.
        await host.SendAsync<RunNewResult>("run/new", new RunNewParams(Seed: 7uL));

        var manifestPaths = Directory.GetFiles(tempReplays.Path, ReplayLayout.ManifestFileName, SearchOption.AllDirectories);
        Assert.NotEmpty(manifestPaths);
        var runDir = Path.GetDirectoryName(manifestPaths[0])!;
        var runJsonPath = Path.Combine(runDir, ReplayLayout.RunHistoryFileName);
        Assert.True(File.Exists(runJsonPath), $"run.json missing at {runJsonPath}");
        var runJson = JsonNode.Parse(File.ReadAllText(runJsonPath))!.AsObject();

        var mapPointHistory = runJson["map_point_history"]!.AsArray();
        Assert.NotEmpty(mapPointHistory);
        var act1 = mapPointHistory[0]!.AsArray();
        Assert.NotEmpty(act1);

        // First map_point_history entry corresponds to the combat we
        // just won. Its player_stats[0] must carry real HP / max_hp /
        // gold — not the 0/0/0 the TestMode gate would leave behind.
        var firstEntry = act1[0]!.AsObject();
        var playerStats = firstEntry["player_stats"]!.AsArray()[0]!.AsObject();

        var maxHp = (int)playerStats["max_hp"]!;
        Assert.True(maxHp > 0, $"max_hp should be populated; got {maxHp}");

        // current_hp may be missing (the game's JSON omits zero-valued
        // ints by default), but if present must be ≥ 0 and ≤ max_hp.
        // The agent didn't die in floor 2, so current_hp > 0.
        var currentHp = playerStats.TryGetPropertyValue("current_hp", out var hp) && hp is not null ? (int)hp! : 0;
        Assert.True(currentHp > 0, $"current_hp should be > 0 after a survived combat; got {currentHp} (max_hp={maxHp})");
        Assert.True(currentHp <= maxHp, $"current_hp ({currentHp}) exceeds max_hp ({maxHp})");

        // Ironclad starts at 99 gold. After one monster floor the
        // player has 99 + reward (typically 10–20). Even if gold
        // generation regresses, the floor-end gold must be > 0.
        var currentGold = playerStats.TryGetPropertyValue("current_gold", out var g) && g is not null ? (int)g! : 0;
        Assert.True(currentGold > 0, $"current_gold should be > 0 after the first combat; got {currentGold}");

        // Card rewards: the agent picked a card after the first
        // combat. card_choices on that floor's player_stats should
        // list every offered card with `was_picked` toggling exactly
        // one to true. The engine's CardReward.OnSelectWrapper would
        // do this; we bypass it (UI calls aren't headless-safe), so
        // ClaimCardReward stamps it ourselves. Without that, only
        // `cards_gained` populates and the viewer can't show the
        // user "what were the options?".
        var cardChoicesNode = playerStats["card_choices"];
        Assert.NotNull(cardChoicesNode);
        var cardChoices = cardChoicesNode!.AsArray();
        Assert.True(cardChoices.Count >= 2,
            $"card_choices should list every offered card (typically 3); got {cardChoices.Count}");
        var pickedCount = cardChoices.Count(c => (bool)c!["was_picked"]!);
        Assert.Equal(1, pickedCount);
    }

    private sealed class TempReplayRoot : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sts2-replays-" + Guid.NewGuid().ToString("N"));
        public TempReplayRoot() => Directory.CreateDirectory(Path);
        public void Dispose() { try { Directory.Delete(Path, recursive: true); } catch { /* best-effort */ } }
    }
}
