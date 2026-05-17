"""Attack-prioritising agent — plays enemy-targeted cards first.

Attack-ness is inferred from `card.target_type` (any_enemy /
all_enemies). That's a closer proxy than id-substring matching since
nearly every enemy-targeted card in StS deals damage. Among the
attack-leaning playable set, pick the most expensive card (cost as a
rough damage proxy) and target the lowest-hp alive enemy.

Per AD-6 this is illustrative, not a behaviour oracle.
"""

from typing import ClassVar

from headless_in_the_spire._models import Card, Enemy, TargetType
from headless_in_the_spire_agents.actions import Action, EndTurn, PlayCard
from headless_in_the_spire_agents.agent import HeuristicAgent
from headless_in_the_spire_agents.state import GameSnapshot

_ATTACK_TARGETS: frozenset[TargetType] = frozenset({TargetType.any_enemy, TargetType.all_enemies})
_NEEDS_ENEMY_TARGET: frozenset[TargetType] = frozenset({TargetType.any_enemy})


class AttackAgent(HeuristicAgent):
    name: ClassVar[str] = "attack"

    def decide_combat(self, state: GameSnapshot) -> Action:
        assert state.combat_state is not None
        combat = state.combat_state
        playable = [c for c in combat.hand if c.can_play and c.cost <= combat.energy]
        if not playable:
            return EndTurn()
        # Attack-leaning first; among ties, highest cost (damage proxy)
        # with a deterministic index tiebreak.
        attack_first = sorted(
            playable,
            key=lambda c: (c.target_type not in _ATTACK_TARGETS, -c.cost, c.index),
        )
        chosen = attack_first[0]
        target_index = self._pick_target(chosen, combat.enemies)
        return PlayCard(card_index=chosen.index, target_index=target_index)

    @staticmethod
    def _pick_target(card: Card, enemies: list[Enemy]) -> int | None:
        if card.target_type not in _NEEDS_ENEMY_TARGET:
            return None
        alive = [e for e in enemies if e.hp > 0]
        if not alive:
            return None
        target = min(alive, key=lambda e: (e.hp, e.index))
        return target.index
