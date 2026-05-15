using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Agents;

// The loop that drives an IAgent against an ITransport. Owns:
//
//   * The initial run/state read.
//   * Cancellation handling.
//   * Game-over detection → returns RunOutcome (does NOT throw).
//   * Stop-predicate handling → returns RunOutcome.
//   * Step-limit handling → returns RunOutcome.
//   * StallDetector — wired automatically; cannot be forgotten by an
//     agent implementation. Catches hangs in ~K steps via snapshot
//     fingerprint comparison; throws StallDetectedException.
//   * Wire dispatch (ApplyAsync) — one branch per AgentAction variant,
//     exhaustively checked by the compiler.
//
// Mirrors Python's `play_run` driver. The split keeps agents as pure
// state→action functions and concentrates every framework concern in
// one reviewable place.
public static class AgentDriver
{
    public const int DefaultMaxSteps = 4000;

    public static async Task<RunOutcome> PlayRunAsync(
        ITransport host,
        IAgent agent,
        Func<RunStateResult, bool>? stopWhen = null,
        int maxSteps = DefaultMaxSteps,
        StallDetector? stallDetector = null,
        Action<int, RunStateResult, AgentAction>? onStep = null,
        CancellationToken ct = default)
    {
        var stall = stallDetector ?? new StallDetector();
        var state = await host.SendAsync<RunStateResult>("run/state");

        for (var step = 0; step < maxSteps; step++)
        {
            ct.ThrowIfCancellationRequested();

            if (state.IsGameOver)
                return new RunOutcome(state, step, TerminationReason.GameOver, AgentStopReason: null);

            if (stopWhen is not null && stopWhen(state))
                return new RunOutcome(state, step, TerminationReason.StopRequested, AgentStopReason: null);

            var action = agent.Decide(state);
            onStep?.Invoke(step, state, action);

            if (action is StopRun stop)
                return new RunOutcome(state, step, TerminationReason.AgentStop, AgentStopReason: stop.Reason);

            state = await ApplyAsync(host, action);
            stall.Observe(state);
        }

        return new RunOutcome(state, maxSteps, TerminationReason.StepLimit, AgentStopReason: null);
    }

    // Dispatch table from AgentAction → wire call. Each branch sends
    // the action's wire request and then re-reads run/state for the
    // post-action snapshot.
    //
    // Why the extra run/state read: each per-action wire result carries
    // most snapshot fields, but not MaxHp / Gold / DeckSize / Seed /
    // Character (those live only on RunStateResult). The StallDetector
    // fingerprint relies on Gold and DeckSize to disambiguate genuine
    // forward progress from "wire returned but state didn't move," so
    // we always re-read to get the complete picture. Negligible cost
    // vs the iteration speedup the detector itself provides.
    //
    // Exhaustiveness: the compiler complains if AgentAction grows a
    // new variant without a branch here.
    public static async Task<RunStateResult> ApplyAsync(ITransport host, AgentAction action)
    {
        switch (action)
        {
            case PlayCard pc:
                _ = await host.SendAsync<RunPlayCardResult>(
                    "run/play_card", new RunPlayCardParams(pc.CardIndex, pc.TargetIndex));
                break;
            case EndTurn:
                _ = await host.SendAsync<RunEndTurnResult>("run/end_turn");
                break;
            case UsePotion up:
                _ = await host.SendAsync<RunUsePotionResult>(
                    "run/use_potion", new RunUsePotionParams(up.PotionIndex, up.TargetIndex));
                break;
            case SelectMapNode m:
                _ = await host.SendAsync<RunSelectMapNodeResult>(
                    "run/select_map_node", new RunSelectMapNodeParams(m.Col, m.Row));
                break;
            case SelectEventOption eo:
                _ = await host.SendAsync<RunSelectEventOptionResult>(
                    "run/select_event_option", new RunSelectEventOptionParams(eo.OptionIndex));
                break;
            case SelectRestSiteOption rs:
                _ = await host.SendAsync<RunSelectRestSiteOptionResult>(
                    "run/select_rest_site_option", new RunSelectRestSiteOptionParams(rs.OptionIndex));
                break;
            case LeaveTreasureRoom:
                _ = await host.SendAsync<RunLeaveTreasureRoomResult>("run/leave_treasure_room");
                break;
            case BuyMerchantItem bm:
                _ = await host.SendAsync<RunBuyMerchantItemResult>(
                    "run/buy_merchant_item", new RunBuyMerchantItemParams(bm.ItemIndex));
                break;
            case LeaveMerchantRoom:
                _ = await host.SendAsync<RunLeaveMerchantRoomResult>("run/leave_merchant_room");
                break;
            case EnterNextAct:
                _ = await host.SendAsync<RunEnterNextActResult>("run/enter_next_act");
                break;
            case SelectReward sr:
                _ = await host.SendAsync<RunSelectRewardResult>(
                    "run/select_reward", new RunSelectRewardParams(sr.RewardIndex, sr.CardIndex));
                break;
            case SkipReward sk:
                _ = await host.SendAsync<RunSkipRewardResult>(
                    "run/skip_reward", new RunSkipRewardParams(sk.RewardIndex));
                break;
            case StopRun:
                // Sentinel handled by PlayRunAsync — never reaches dispatch.
                // Guard against misuse from outside the driver.
                throw new InvalidOperationException(
                    "AgentDriver.ApplyAsync: StopRun is a sentinel that must be handled by the loop, not dispatched.");
            default:
                throw new InvalidOperationException(
                    $"AgentDriver.ApplyAsync: unhandled AgentAction variant {action.GetType().Name}. "
                    + "Add a branch to the switch when you add a new variant.");
        }
        return await host.SendAsync<RunStateResult>("run/state");
    }
}

public enum TerminationReason
{
    // Reached IsGameOver=true (victory or death; check state.IsVictory).
    GameOver,
    // Caller's stopWhen predicate returned true.
    StopRequested,
    // Hit maxSteps without termination.
    StepLimit,
    // Agent returned StopRun(reason).
    AgentStop,
}

public sealed record RunOutcome(
    RunStateResult FinalState,
    int Steps,
    TerminationReason TerminatedBy,
    string? AgentStopReason);
