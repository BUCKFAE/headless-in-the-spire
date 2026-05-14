"""Greedy heuristics: combat (priciest playable → lowest-hp enemy),
rewards (cheapest card). Map/event inherit from `HeuristicAgent` and
are covered by the heuristic-base tests.
"""

from conftest import (
    build_snapshot,
    card,
    card_option,
    card_reward,
    enemy,
    in_progress_combat,
)
from headless_in_the_spire_agents import (
    EndTurn,
    GreedyAgent,
    PlayCard,
    SelectReward,
)

from headless_in_the_spire._models import RewardsState, RoomType, TargetType


def test_combat_plays_priciest_playable_card() -> None:
    # Hand has a 2-cost playable (Bash-like), a 1-cost playable, and an
    # unplayable card. Greedy picks the 2-cost.
    hand = [
        card(index=0, cost=1, can_play=True),
        card(index=1, cost=2, can_play=True),
        card(index=2, cost=3, can_play=False),
    ]
    enemies = [enemy(index=0, hp=20)]
    snap = build_snapshot(
        room=RoomType.combat_room,
        combat=in_progress_combat(energy=3, hand=hand, enemies=enemies),
    )
    assert GreedyAgent().decide(snap) == PlayCard(card_index=1, target_index=0)


def test_combat_skips_cards_that_exceed_energy() -> None:
    # 2-cost card is "playable" but costs more than current energy —
    # greedy should ignore it and end turn rather than pretend.
    hand = [card(index=0, cost=2, can_play=True)]
    snap = build_snapshot(
        room=RoomType.combat_room,
        combat=in_progress_combat(energy=1, hand=hand, enemies=[enemy(index=0, hp=10)]),
    )
    assert GreedyAgent().decide(snap) == EndTurn()


def test_combat_targets_lowest_hp_enemy() -> None:
    hand = [card(index=0, cost=1, can_play=True, target_type=TargetType.any_enemy)]
    enemies = [enemy(index=0, hp=30), enemy(index=1, hp=12), enemy(index=2, hp=20)]
    snap = build_snapshot(
        room=RoomType.combat_room,
        combat=in_progress_combat(hand=hand, enemies=enemies),
    )
    assert GreedyAgent().decide(snap) == PlayCard(card_index=0, target_index=1)


def test_combat_no_target_index_for_non_enemy_targets() -> None:
    # Self-target cards (e.g. Defend) must NOT send a target_index —
    # the host doesn't expect one and would reject.
    hand = [card(index=0, cost=1, can_play=True, target_type=TargetType.self)]
    snap = build_snapshot(
        room=RoomType.combat_room,
        combat=in_progress_combat(hand=hand, enemies=[enemy(index=0, hp=10)]),
    )
    assert GreedyAgent().decide(snap) == PlayCard(card_index=0, target_index=None)


def test_combat_ends_turn_with_no_playable_cards() -> None:
    hand = [card(index=0, cost=1, can_play=False)]
    snap = build_snapshot(
        room=RoomType.combat_room,
        combat=in_progress_combat(hand=hand),
    )
    assert GreedyAgent().decide(snap) == EndTurn()


def test_combat_empty_hand_ends_turn() -> None:
    snap = build_snapshot(
        room=RoomType.combat_room,
        combat=in_progress_combat(hand=[]),
    )
    assert GreedyAgent().decide(snap) == EndTurn()


def test_combat_skips_dead_enemies_when_targeting() -> None:
    hand = [card(index=0, cost=1, can_play=True, target_type=TargetType.any_enemy)]
    enemies = [enemy(index=0, hp=0), enemy(index=1, hp=15)]
    snap = build_snapshot(
        room=RoomType.combat_room,
        combat=in_progress_combat(hand=hand, enemies=enemies),
    )
    # index=0 is dead — pick the only alive enemy.
    assert GreedyAgent().decide(snap) == PlayCard(card_index=0, target_index=1)


def test_rewards_picks_cheapest_card() -> None:
    rewards = RewardsState(
        available=[
            card_reward(
                index=0,
                cards=[
                    card_option(index=0, cost=2),
                    card_option(index=1, cost=1),
                    card_option(index=2, cost=3),
                ],
            )
        ]
    )
    snap = build_snapshot(rewards=rewards)
    assert GreedyAgent().decide(snap) == SelectReward(reward_index=0, card_index=1)


def test_rewards_non_card_just_claims() -> None:
    from conftest import gold_reward

    snap = build_snapshot(rewards=RewardsState(available=[gold_reward(index=0, amount=42)]))
    assert GreedyAgent().decide(snap) == SelectReward(reward_index=0, card_index=None)
