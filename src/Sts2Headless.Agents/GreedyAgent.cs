using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Agents;

// "Play whatever's in front of you" agent. Picks the first reasonable option
// at every decision point, never looks ahead, never plans energy. The
// purpose is forward progress through a run so end-to-end tests can drive
// multi-room arcs — not to actually win.
//
// Design discipline:
//   * Every branch is a private method named after the room it handles, so
//     adding "shop" or "rest" becomes a one-line dispatch entry.
//   * Unhandled rooms throw with a message that names the wire-call gap.
//     A red end-to-end test then attributes the failure to the missing
//     method, not to a mysterious deadlock.
//   * Safety counters bound every inner loop so a malformed snapshot fails
//     fast rather than hanging the test run.
public sealed class GreedyAgent : IAgent
{
    // Caps the outer drive loop. A full Act 1 is ~50–80 decisions; 2000 is
    // generous enough to absorb the occasional re-read while still failing
    // fast on a true loop.
    private const int MaxSteps = 2000;
    private const int MaxRewardsDrain = 50;

    public async Task<RunStateResult> DriveUntilAsync(
        ITransport host,
        Func<RunStateResult, bool> stopWhen,
        CancellationToken ct = default)
    {
        var state = await host.SendAsync<RunStateResult>("run/state");
        for (var step = 0; !stopWhen(state); step++)
        {
            ct.ThrowIfCancellationRequested();
            if (state.IsGameOver)
            {
                throw new InvalidOperationException(
                    "GreedyAgent: run ended (game over) before stop condition matched. " +
                    $"Last state: floor={state.ActFloor}, room={state.CurrentRoomType}, " +
                    $"hp={state.Hp}/{state.MaxHp}.");
            }
            if (step >= MaxSteps)
            {
                throw new InvalidOperationException(
                    $"GreedyAgent: exceeded {MaxSteps} steps without matching stop condition. " +
                    $"Last state: floor={state.ActFloor}, room={state.CurrentRoomType}. " +
                    "This usually means the stop condition is unreachable or the agent " +
                    "is looping on a room it can't leave.");
            }
            state = await StepAsync(host, state);
        }
        return state;
    }

    private static Task<RunStateResult> StepAsync(ITransport host, RunStateResult s) =>
        s.CurrentRoomType switch
        {
            RoomType.MapRoom => StepMapAsync(host, s),
            // BossRoom is just a combat room with the act boss inside; the same
            // greedy combat loop handles it. Callers who want to *stop* at the
            // boss room pass a stop condition that fires on BossRoom; callers
            // who want the agent to fight the boss let it through.
            RoomType.CombatRoom or RoomType.BossRoom => StepCombatAsync(host, s),
            RoomType.EventRoom => StepEventAsync(host, s),
            RoomType.RestSiteRoom => StepRestSiteAsync(host, s),
            RoomType.MerchantRoom => throw NoWireExitYet("merchant"),
            RoomType.TreasureRoom => throw NoWireExitYet("treasure"),
            _ => throw new InvalidOperationException(
                $"GreedyAgent: unhandled room type {s.CurrentRoomType}. " +
                "This is either a new RoomType the wire surface added without " +
                "a corresponding agent branch, or the room is Unknown — check " +
                "the snapshot."),
        };

    private static Exception NoWireExitYet(string roomLabel) =>
        new NotSupportedException(
            $"GreedyAgent reached a {roomLabel} room, but the wire protocol has no " +
            "method to leave it yet. Add the corresponding run/* method (and its " +
            "single-slice integration test) before this agent can route through here.");

    private static async Task<RunStateResult> StepMapAsync(ITransport host, RunStateResult s)
    {
        if (s.AvailableMapNodes.Count == 0)
        {
            throw new InvalidOperationException(
                "GreedyAgent: in MapRoom but availableMapNodes is empty. Either the " +
                "snapshot was read before the engine populated the row, or we're on " +
                "a row with no legal moves.");
        }
        // Bias the pick toward rooms the agent can actually handle. Lower
        // priority numbers are preferred. Merchant/Treasure are still
        // deprioritised below routable rooms — but if those are the *only*
        // options on the row, the agent will still pick one and fail at
        // StepAsync with a "wire call missing" message that names the exact
        // gap. RestSite is routable now (HEAL exits cleanly).
        static int Priority(MapNodeType t) => t switch
        {
            MapNodeType.Monster => 0,
            MapNodeType.Elite => 1,
            MapNodeType.Event => 2,
            MapNodeType.Unknown => 3,   // sts2's "?" rooms — resolve on entry
            MapNodeType.Boss => 4,
            MapNodeType.RestSite => 5,
            MapNodeType.Merchant => 100,
            MapNodeType.Treasure => 100,
            _ => 200,
        };
        var pick = s.AvailableMapNodes
            .OrderBy(n => Priority(n.Type))
            .ThenBy(n => n.Col)
            .First();
        await host.SendAsync<RunSelectMapNodeResult>(
            "run/select_map_node",
            new RunSelectMapNodeParams(Col: pick.Col, Row: pick.Row));
        return await host.SendAsync<RunStateResult>("run/state");
    }

    private static async Task<RunStateResult> StepCombatAsync(ITransport host, RunStateResult s)
    {
        var combat = s.CombatState
            ?? throw new InvalidOperationException(
                $"GreedyAgent: in {s.CurrentRoomType} but combatState is null. " +
                "The wire contract says combat-bearing rooms must populate this slot.");

        // Find any affordable card we can legally play. The wire's `canPlay`
        // already encodes engine rules (X-cost, perma-disabled, retain, …),
        // so we trust it and only re-check Cost as a defence in depth.
        var playable = combat.Hand.FirstOrDefault(c => c.CanPlay && c.Cost >= 0 && c.Cost <= combat.Energy);
        if (playable is not null)
        {
            // AnyEnemy is the only target mode that requires a caller-supplied
            // targetIndex; for everything else the wire ignores it. Targeting
            // enemy 0 isn't smart but is always legal so long as at least one
            // enemy is alive — which is guaranteed by IsInProgress.
            var target = playable.TargetType == TargetType.AnyEnemy ? (int?)0 : null;
            var resp = await host.SendAsync<RunPlayCardResult>(
                "run/play_card",
                new RunPlayCardParams(CardIndex: playable.Index, TargetIndex: target));
            return await DrainRewardsAsync(host, resp.RewardsState);
        }

        var ended = await host.SendAsync<RunEndTurnResult>("run/end_turn");
        return await DrainRewardsAsync(host, ended.RewardsState);
    }

    private static async Task<RunStateResult> StepRestSiteAsync(ITransport host, RunStateResult s)
    {
        if (s.AvailableRestSiteOptions.Count == 0)
        {
            throw new InvalidOperationException(
                "GreedyAgent: in RestSiteRoom but availableRestSiteOptions is empty. " +
                "This usually means the engine has already accepted a pick and the room is " +
                "mid-transition — the next re-snapshot should flip CurrentRoomType to MapRoom.");
        }
        // Preference order:
        //   1. HEAL — always exits to MapRoom cleanly via the engine's auto-
        //      advance, so the agent makes forward progress.
        //   2. Any other enabled option that isn't SMITH — DIG, recovery, etc.
        //      may be safe; we cross our fingers and let the failure name the
        //      next gap if not.
        //   3. SMITH — last resort. Will stall on the card-select sub-flow
        //      we haven't wired yet, but at least the failure points at a
        //      concrete next slice.
        var pick = s.AvailableRestSiteOptions.FirstOrDefault(o =>
                       o.IsEnabled
                       && string.Equals(o.OptionId, "HEAL", StringComparison.OrdinalIgnoreCase))
                   ?? s.AvailableRestSiteOptions.FirstOrDefault(o =>
                       o.IsEnabled
                       && !string.Equals(o.OptionId, "SMITH", StringComparison.OrdinalIgnoreCase))
                   ?? s.AvailableRestSiteOptions.FirstOrDefault(o => o.IsEnabled);

        if (pick is null)
        {
            throw new InvalidOperationException(
                "GreedyAgent: in RestSiteRoom but no enabled options surfaced. " +
                $"Options seen: [{string.Join(", ", s.AvailableRestSiteOptions.Select(o => $"{o.OptionId}({(o.IsEnabled ? "on" : "off")})"))}].");
        }

        await host.SendAsync<RunSelectRestSiteOptionResult>(
            "run/select_rest_site_option",
            new RunSelectRestSiteOptionParams(OptionIndex: pick.Index));
        return await host.SendAsync<RunStateResult>("run/state");
    }

    private static async Task<RunStateResult> StepEventAsync(ITransport host, RunStateResult s)
    {
        if (s.AvailableEventOptions.Count == 0)
        {
            throw new InvalidOperationException(
                "GreedyAgent: in EventRoom but availableEventOptions is empty. " +
                "The current page has no picks — either the wire is mid-transition " +
                "or this event auto-resolves and the wire surface hasn't routed " +
                "around it yet.");
        }
        // Pick the first unlocked option; fall back to index 0 if every option
        // is locked (sts2 will refuse the click silently, the loop's safety
        // counter catches the stall, and the error message names the room).
        var pick = s.AvailableEventOptions.FirstOrDefault(o => !o.IsLocked)
            ?? s.AvailableEventOptions[0];
        var resp = await host.SendAsync<RunSelectEventOptionResult>(
            "run/select_event_option",
            new RunSelectEventOptionParams(OptionIndex: pick.Index));
        // `?`-rooms can resolve straight into combat or hand out rewards on
        // the same tick — drain whatever the engine surfaced before the next
        // iteration re-reads.
        return await DrainRewardsAsync(host, resp.RewardsState);
    }

    private static async Task<RunStateResult> DrainRewardsAsync(ITransport host, RewardsState? rewards)
    {
        var rs = rewards;
        for (var i = 0; i < MaxRewardsDrain && rs is not null && rs.Available.Count > 0; i++)
        {
            var pick = rs.Available[0];
            if (pick.Kind == RewardKind.Card)
            {
                if (pick.CanSkip)
                {
                    var r = await host.SendAsync<RunSkipRewardResult>(
                        "run/skip_reward",
                        new RunSkipRewardParams(RewardIndex: pick.Index));
                    rs = r.RewardsState;
                }
                else
                {
                    // Forced card pick — take the first card the engine offered.
                    // Pre-AD-6 helpers used CardIndex: null here, but a forced
                    // (non-skippable) card reward needs an explicit pick.
                    var cardIdx = (pick.Cards?.Count ?? 0) > 0 ? pick.Cards![0].Index : 0;
                    var r = await host.SendAsync<RunSelectRewardResult>(
                        "run/select_reward",
                        new RunSelectRewardParams(RewardIndex: pick.Index, CardIndex: cardIdx));
                    rs = r.RewardsState;
                }
            }
            else
            {
                var r = await host.SendAsync<RunSelectRewardResult>(
                    "run/select_reward",
                    new RunSelectRewardParams(RewardIndex: pick.Index, CardIndex: null));
                rs = r.RewardsState;
            }
        }
        return await host.SendAsync<RunStateResult>("run/state");
    }
}
