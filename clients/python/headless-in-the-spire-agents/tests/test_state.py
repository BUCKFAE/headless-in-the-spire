"""Phase detection — the only logic in `state.py`.

Each test pins one rung of the priority ladder (terminal > rewards >
combat > room) so a reorder is obvious in the diff.
"""

from conftest import (
    build_snapshot,
    card_option,
    card_reward,
    event_option,
    in_progress_combat,
    map_node,
)
from headless_in_the_spire_agents import Phase, current_phase

from headless_in_the_spire._models import RewardsState, RoomType


def test_terminal_beats_everything() -> None:
    # Game-over state still has rewards/combat/etc. lingering; the
    # terminal flag must shadow them so the driver doesn't try to act.
    snap = build_snapshot(
        is_game_over=True,
        room=RoomType.combat_room,
        combat=in_progress_combat(),
    )
    assert current_phase(snap) is Phase.terminal


def test_rewards_outrank_combat_room() -> None:
    # Combat ends → rewards surface while room still reads CombatRoom.
    # Picking combat here would dispatch play_card on an empty hand.
    snap = build_snapshot(
        room=RoomType.combat_room,
        combat=in_progress_combat(),  # is_in_progress=True but…
        rewards=RewardsState(available=[card_reward(index=0, cards=[card_option(index=0)])]),
    )
    assert current_phase(snap) is Phase.rewards


def test_combat_when_in_progress() -> None:
    snap = build_snapshot(
        room=RoomType.combat_room,
        combat=in_progress_combat(),
    )
    assert current_phase(snap) is Phase.combat


def test_map_when_room_is_map_and_nodes_exist() -> None:
    snap = build_snapshot(
        room=RoomType.map_room,
        map_nodes=[map_node(col=0, row=0)],
    )
    assert current_phase(snap) is Phase.map


def test_event_when_room_is_event_and_options_exist() -> None:
    snap = build_snapshot(
        room=RoomType.event_room,
        event_options=[event_option(index=0)],
    )
    assert current_phase(snap) is Phase.event


def test_unknown_when_no_decision_is_live() -> None:
    # Combat room but combat not in progress and no rewards → nothing
    # the agent can legally do.
    snap = build_snapshot(room=RoomType.combat_room)
    assert current_phase(snap) is Phase.unknown
