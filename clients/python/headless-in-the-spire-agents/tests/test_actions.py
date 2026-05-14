"""Action algebra — equality, immutability, slots."""

import pytest
from headless_in_the_spire_agents import (
    EndTurn,
    PlayCard,
    SelectEventOption,
    SelectMapNode,
    SelectReward,
    SkipReward,
)


def test_play_card_defaults_target_to_none() -> None:
    a = PlayCard(card_index=2)
    assert a.target_index is None


def test_actions_compare_by_value() -> None:
    assert PlayCard(card_index=1, target_index=0) == PlayCard(card_index=1, target_index=0)
    assert PlayCard(card_index=1) != PlayCard(card_index=2)
    assert EndTurn() == EndTurn()
    assert SelectMapNode(col=0, row=0) == SelectMapNode(col=0, row=0)
    assert SelectMapNode(col=0, row=0) != SelectMapNode(col=0, row=1)


def test_actions_are_frozen() -> None:
    a = PlayCard(card_index=1)
    with pytest.raises(AttributeError):
        a.card_index = 2  # type: ignore[misc]


def test_actions_are_hashable() -> None:
    # Frozen+slots dataclasses are hashable. Useful for storing in
    # sets — e.g. de-duplicating tried-actions in a search agent.
    s = {EndTurn(), EndTurn(), PlayCard(card_index=1)}
    assert len(s) == 2


def test_select_reward_card_index_defaults_to_none() -> None:
    a = SelectReward(reward_index=0)
    assert a.card_index is None


def test_skip_reward_requires_index() -> None:
    a = SkipReward(reward_index=3)
    assert a.reward_index == 3


def test_select_event_option_requires_index() -> None:
    a = SelectEventOption(option_index=1)
    assert a.option_index == 1
