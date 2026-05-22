using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Agents.Contracts;

// Which decision the agent must make next, derived from a snapshot.
// Mirrors the Python `state.Phase` + `current_phase()` pair; the C#
// side adds RestSite / Treasure / Merchant / MapEmpty because our
// agents already handle those rooms (Python's HeuristicAgent today
// only models combat/map/event/rewards).
//
// Priority matters: rewards take precedence over combat (the snapshot
// can show CurrentRoomType=CombatRoom with rewards pending right
// after the killing blow). Terminal trumps everything.
public enum Phase
{
    // No legal decision: IsGameOver=true OR none of the rules below
    // matched. HeuristicAgent throws NoLegalActionException; concrete
    // agents may override to capture context.
    Terminal,
    Unknown,

    // Pending post-combat / post-event rewards to claim or skip.
    Rewards,

    // Combat in progress (CurrentRoomType=CombatRoom or BossRoom AND
    // CombatState.IsInProgress).
    Combat,

    // Map with at least one available next move.
    Map,

    // Map with zero available nodes — the post-boss empty state. The
    // engine has flipped past the boss but hasn't regenerated the
    // next act's map; the default action is EnterNextAct.
    MapEmpty,

    Event,

    // Event surfaced with zero options because the local event's
    // IsFinished flag is true — the engine resolved the event but
    // didn't auto-transition the room back to MapRoom. Default
    // handler returns ProceedEvent to drive the transition; mirrors
    // the post-boss MapEmpty case.
    EventFinished,

    RestSite,
    Treasure,
    Merchant,
}

public static class PhaseDetector
{
    public static Phase CurrentPhase(RunStateResult s)
    {
        if (s.IsGameOver) return Phase.Terminal;

        if (s.RewardsState is { Available.Count: > 0 }) return Phase.Rewards;

        if ((s.CurrentRoomType == RoomType.CombatRoom || s.CurrentRoomType == RoomType.BossRoom)
            && s.CombatState is { IsInProgress: true })
        {
            return Phase.Combat;
        }

        return s.CurrentRoomType switch
        {
            RoomType.MapRoom => s.AvailableMapNodes.Count > 0 ? Phase.Map : Phase.MapEmpty,
            RoomType.EventRoom => s.AvailableEventOptions.Count > 0 ? Phase.Event : Phase.EventFinished,
            RoomType.RestSiteRoom when s.AvailableRestSiteOptions.Count > 0 => Phase.RestSite,
            RoomType.TreasureRoom => Phase.Treasure,
            RoomType.MerchantRoom => Phase.Merchant,
            _ => Phase.Unknown,
        };
    }
}
