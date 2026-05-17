"""Observation shape and phase detection.

The wire returns one of several `Run*Result` types depending on which
method was called, but every post-action result carries the same set of
"what does the world look like now" fields. `GameSnapshot` is the
structural type that captures that shared shape — agents accept it,
the driver passes whichever DTO it just got back.

`Phase` collapses `current_room_type` + `combat_state.is_in_progress` +
`rewards_state.available` into the single dimension that actually
determines which action vocabulary is legal right now.
"""

from enum import Enum
from typing import Protocol, runtime_checkable

from headless_in_the_spire._models import (
    CombatState,
    EventOption,
    MapNode,
    RewardsState,
    RoomType,
)


@runtime_checkable
class GameSnapshot(Protocol):
    """Structural type for any post-action wire result.

    Every `Run*Result` in the wire client (and `RunStateResult` itself)
    exposes these fields, so an agent that takes `GameSnapshot` can
    consume whichever one the driver last received without an explicit
    coercion step. Members that wire-only types add (gold, max_hp, …)
    are intentionally out of scope here — fetch them via
    `client.run_state()` if a richer agent needs them.
    """

    ok: bool
    is_game_over: bool
    is_victory: bool
    is_dead: bool
    hp: int
    act_floor: int
    current_act_index: int
    current_room_type: RoomType
    available_map_nodes: list[MapNode]
    available_event_options: list[EventOption]
    combat_state: CombatState | None
    rewards_state: RewardsState | None


class Phase(Enum):
    """Which decision is live right now.

    Order matters: rewards block the auto-advance back to MapRoom, so
    when `rewards_state.available` is non-empty we're in `rewards` even
    though `current_room_type` may still read `CombatRoom`. `unknown`
    means no legal action — typically a wire/state bug, surfaced so the
    driver can fail loudly rather than spin.
    """

    combat = "combat"
    rewards = "rewards"
    map = "map"
    event = "event"
    treasure = "treasure"
    terminal = "terminal"
    unknown = "unknown"


def current_phase(state: GameSnapshot) -> Phase:
    """Return the single decision the agent must make next.

    Priority mirrors the host's actual flow: terminal > rewards > combat
    > room. Rewards take precedence over the room because the snapshot
    can show `CombatRoom` with rewards pending right after the killing
    blow lands.
    """
    if state.is_game_over:
        return Phase.terminal
    if state.rewards_state is not None and state.rewards_state.available:
        return Phase.rewards
    if (
        state.current_room_type is RoomType.combat_room
        and state.combat_state is not None
        and state.combat_state.is_in_progress
    ):
        return Phase.combat
    if state.current_room_type is RoomType.map_room and state.available_map_nodes:
        return Phase.map
    if state.current_room_type is RoomType.event_room and state.available_event_options:
        return Phase.event
    if state.current_room_type is RoomType.treasure_room:
        # No options list on the wire — opening the chest is unconditional.
        return Phase.treasure
    return Phase.unknown
