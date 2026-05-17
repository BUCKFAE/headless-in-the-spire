"""Default per-phase hooks on `HeuristicAgent`.

A bare `HeuristicAgent()` is itself a usable (if boring) agent — these
tests pin that "no override" runs a complete arc.
"""

import pytest
from conftest import (
    build_snapshot,
    card_option,
    card_reward,
    event_option,
    in_progress_combat,
    map_node,
)
from headless_in_the_spire_agents import (
    EndTurn,
    HeuristicAgent,
    LeaveTreasureRoom,
    NoLegalActionError,
    SelectEventOption,
    SelectMapNode,
    SelectReward,
)

from headless_in_the_spire._models import RewardsState, RoomType


def test_default_combat_ends_turn() -> None:
    snap = build_snapshot(room=RoomType.combat_room, combat=in_progress_combat())
    assert HeuristicAgent().decide(snap) == EndTurn()


def test_default_map_picks_first_node() -> None:
    snap = build_snapshot(
        room=RoomType.map_room,
        map_nodes=[map_node(col=1, row=2), map_node(col=2, row=2)],
    )
    assert HeuristicAgent().decide(snap) == SelectMapNode(col=1, row=2)


def test_default_event_skips_locked_options() -> None:
    snap = build_snapshot(
        room=RoomType.event_room,
        event_options=[event_option(index=0, is_locked=True), event_option(index=1)],
    )
    assert HeuristicAgent().decide(snap) == SelectEventOption(option_index=1)


def test_default_event_raises_when_all_locked() -> None:
    snap = build_snapshot(
        room=RoomType.event_room,
        event_options=[event_option(index=0, is_locked=True)],
    )
    with pytest.raises(NoLegalActionError):
        HeuristicAgent().decide(snap)


def test_default_rewards_claims_head_with_first_card() -> None:
    snap = build_snapshot(
        room=RoomType.combat_room,
        rewards=RewardsState(
            available=[card_reward(index=0, cards=[card_option(index=0), card_option(index=1)])]
        ),
    )
    assert HeuristicAgent().decide(snap) == SelectReward(reward_index=0, card_index=0)


def test_default_treasure_leaves_room() -> None:
    snap = build_snapshot(room=RoomType.treasure_room)
    assert HeuristicAgent().decide(snap) == LeaveTreasureRoom()


def test_terminal_raises() -> None:
    snap = build_snapshot(is_game_over=True)
    with pytest.raises(NoLegalActionError):
        HeuristicAgent().decide(snap)


def test_unknown_phase_raises_with_snapshot() -> None:
    snap = build_snapshot(room=RoomType.combat_room)  # combat not in progress
    with pytest.raises(NoLegalActionError) as exc:
        HeuristicAgent().decide(snap)
    assert exc.value.snapshot is snap
