"""Block-prioritising agent — plays anything that smells like a block card first.

The wire doesn't yet expose a "category" on `Card` (no
attack/skill/power), so this agent inspects `card.id` against a short
list of block-y substrings. That's deliberately a heuristic: when the
wire grows a proper category field this whole module collapses to a
one-line filter. Until then, missing a non-standard block card is the
expected failure mode — the agent will still finish runs, just less
defensively.

Per AD-6 this is illustrative, not a behaviour oracle.
"""

from typing import ClassVar

from headless_in_the_spire._models import Card, Enemy, TargetType
from headless_in_the_spire_agents.actions import Action, EndTurn, PlayCard
from headless_in_the_spire_agents.agent import HeuristicAgent
from headless_in_the_spire_agents.state import GameSnapshot

# Substrings (case-insensitive, matched against `card.id`) that classify
# a card as "block-leaning". Conservative on purpose: a false positive
# wastes a turn, while a false negative just falls back to the playable
# pool. Grow this list when integration tests surface a missed card.
_BLOCK_TOKENS: tuple[str, ...] = (
    "DEFEND",
    "BLOCK",
    "SHRUG",
    "ARMOR",
    "BARRICADE",
    "IMPERVIOUS",
    "ENTRENCH",
    "METALLICIZE",
    "TRUE_GRIT",
)

_NEEDS_ENEMY_TARGET: frozenset[TargetType] = frozenset({TargetType.any_enemy})


def _is_block_card(card: Card) -> bool:
    cid = card.id.upper()
    return any(tok in cid for tok in _BLOCK_TOKENS)


class BlockAgent(HeuristicAgent):
    name: ClassVar[str] = "block"

    def decide_combat(self, state: GameSnapshot) -> Action:
        assert state.combat_state is not None
        combat = state.combat_state
        playable = [c for c in combat.hand if c.can_play and c.cost <= combat.energy]
        if not playable:
            return EndTurn()
        # Block-leaning first; among ties, prefer the most expensive
        # (typically more block per card) with a deterministic tiebreak.
        block_first = sorted(
            playable,
            key=lambda c: (not _is_block_card(c), -c.cost, c.index),
        )
        chosen = block_first[0]
        target_index = self._pick_target(chosen, combat.enemies)
        return PlayCard(card_index=chosen.index, target_index=target_index)

    @staticmethod
    def _pick_target(card: Card, enemies: list[Enemy]) -> int | None:
        if card.target_type not in _NEEDS_ENEMY_TARGET:
            return None
        # Lowest-hp alive enemy — matches the greedy convention so a
        # mixed block/attack hand still finishes off wounded targets.
        alive = [e for e in enemies if e.hp > 0]
        if not alive:
            return None
        target = min(alive, key=lambda e: (e.hp, e.index))
        return target.index
