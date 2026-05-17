"""Uniform-random Python agent — the baseline every other agent should beat.

Useful as a sanity check (does the wire/driver survive a chaotic
client?) and as a coverage scattergun. Per AD-6 it is *not* a behaviour
oracle; behavioural correctness is owned by the C# suite.

Determinism: pass an explicit `seed` to make a run reproducible. Two
agents constructed with the same seed against the same host seed produce
identical action streams.
"""

import random
from typing import ClassVar

from headless_in_the_spire._models import RewardKind, TargetType
from headless_in_the_spire_agents.actions import (
    Action,
    EndTurn,
    PlayCard,
    SelectEventOption,
    SelectMapNode,
    SelectReward,
    SkipReward,
)
from headless_in_the_spire_agents.agent import HeuristicAgent, NoLegalActionError
from headless_in_the_spire_agents.state import GameSnapshot

_NEEDS_ENEMY_TARGET: frozenset[TargetType] = frozenset({TargetType.any_enemy})


class RandomAgent(HeuristicAgent):
    name: ClassVar[str] = "random"

    def __init__(self, seed: int | None = None) -> None:
        # A dedicated Random instance — never touch the global rng, which
        # would entangle this agent with anything else seeding `random`.
        self._rng = random.Random(seed)

    def decide_combat(self, state: GameSnapshot) -> Action:
        assert state.combat_state is not None
        combat = state.combat_state
        options: list[Action] = [EndTurn()]
        for c in combat.hand:
            if not c.can_play or c.cost > combat.energy:
                continue
            if c.target_type in _NEEDS_ENEMY_TARGET:
                alive = [e for e in combat.enemies if e.hp > 0]
                if not alive:
                    # Card needs an enemy but there's none alive — drop
                    # it from the option set rather than send a None
                    # target the host will reject.
                    continue
                target = self._rng.choice(alive)
                options.append(PlayCard(card_index=c.index, target_index=target.index))
            else:
                options.append(PlayCard(card_index=c.index, target_index=None))
        return self._rng.choice(options)

    def decide_map(self, state: GameSnapshot) -> Action:
        if not state.available_map_nodes:
            raise NoLegalActionError("map phase with empty node list", state)
        node = self._rng.choice(state.available_map_nodes)
        return SelectMapNode(col=node.col, row=node.row)

    def decide_event(self, state: GameSnapshot) -> Action:
        unlocked = [o for o in state.available_event_options if not o.is_locked]
        if not unlocked:
            raise NoLegalActionError("event phase with no unlocked options", state)
        opt = self._rng.choice(unlocked)
        return SelectEventOption(option_index=opt.index)

    def decide_rewards(self, state: GameSnapshot) -> Action:
        assert state.rewards_state is not None
        rewards = state.rewards_state.available
        if not rewards:
            raise NoLegalActionError("rewards phase with empty list", state)
        reward = self._rng.choice(rewards)
        if reward.kind is RewardKind.card and reward.cards:
            # Randomly skip when allowed, otherwise pick a random card.
            if reward.can_skip and self._rng.random() < 0.5:
                return SkipReward(reward_index=reward.index)
            chosen = self._rng.choice(reward.cards)
            return SelectReward(reward_index=reward.index, card_index=chosen.index)
        if reward.can_skip and self._rng.random() < 0.5:
            return SkipReward(reward_index=reward.index)
        return SelectReward(reward_index=reward.index)
