"""Illustrative Python `GreedyAgent` — a reference, not the canon.

Per AD-6, the canonical greedy agent lives in C# under
`src/Sts2Headless.Agents/`. This class exists to (a) demonstrate the
Python `HeuristicAgent` shape and (b) give Python-side users a runnable
agent for ad-hoc experiments. Tests pin the local heuristics, not "what
greedy is supposed to do" — that question is answered by the C# suite.

Heuristics, by phase:
- combat: play the most-expensive *playable* card every step (cost as a
  rough value proxy); target the lowest-hp enemy when the card needs an
  enemy target. Out of playable cards → end turn.
- rewards: among card rewards, take the cheapest card; for everything
  else, claim the head reward (gold/relic/potion can't be skipped).
- map / event: inherit the `HeuristicAgent` defaults (first legal
  option) — no signal on the wire to be smarter without lookahead.
"""

from headless_in_the_spire._models import (
    Card,
    Enemy,
    RewardKind,
    TargetType,
)
from headless_in_the_spire_agents.actions import (
    Action,
    EndTurn,
    PlayCard,
    SelectReward,
    SkipReward,
)
from headless_in_the_spire_agents.agent import HeuristicAgent, NoLegalActionError
from headless_in_the_spire_agents.state import GameSnapshot

# TargetTypes where the wire expects a concrete enemy index. Everything
# else (Self, AllEnemies, None, …) takes target_index=None and the host
# resolves the target itself.
_NEEDS_ENEMY_TARGET: frozenset[TargetType] = frozenset({TargetType.any_enemy})


class GreedyAgent(HeuristicAgent):
    name = "greedy"

    def decide_combat(self, state: GameSnapshot) -> Action:
        # Dispatched only when Phase.combat, which guarantees combat_state.
        assert state.combat_state is not None
        combat = state.combat_state
        playable = [c for c in combat.hand if c.can_play and c.cost <= combat.energy]
        if not playable:
            return EndTurn()
        # Highest-cost playable card first. Tie-break on `index` so the
        # decision is deterministic when two cards cost the same.
        card = max(playable, key=lambda c: (c.cost, -c.index))
        target = self._pick_target(card, combat.enemies)
        return PlayCard(card_index=card.index, target_index=target)

    def decide_rewards(self, state: GameSnapshot) -> Action:
        # Dispatched only when Phase.rewards, which guarantees rewards_state.
        assert state.rewards_state is not None
        rewards = state.rewards_state.available
        if not rewards:
            raise NoLegalActionError("rewards phase with empty list", state)
        head = rewards[0]
        if head.kind is RewardKind.card:
            if not head.cards:
                # Card reward with an empty card list — should not happen
                # in practice. Skip if we can; otherwise let the host
                # decide (claim with no card_index).
                if head.can_skip:
                    return SkipReward(reward_index=head.index)
                return SelectReward(reward_index=head.index)
            cheapest = min(head.cards, key=lambda c: (c.cost, c.index))
            return SelectReward(reward_index=head.index, card_index=cheapest.index)
        return SelectReward(reward_index=head.index)

    @staticmethod
    def _pick_target(card: Card, enemies: list[Enemy]) -> int | None:
        # Non-enemy targets resolve server-side; sending an index would
        # be wrong (the host doesn't expect one for Self/None/etc.).
        if card.target_type not in _NEEDS_ENEMY_TARGET:
            return None
        alive = [e for e in enemies if e.hp > 0]
        if not alive:
            # No legal target — return None and let the host raise. We
            # could fall back to EndTurn() here, but masking the
            # situation hides a real "agent thinks combat is live but no
            # enemies remain" bug.
            return None
        target = min(alive, key=lambda e: (e.hp, e.index))
        return target.index
