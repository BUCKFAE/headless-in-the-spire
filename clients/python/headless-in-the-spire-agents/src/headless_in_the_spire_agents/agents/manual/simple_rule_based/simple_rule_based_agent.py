from headless_in_the_spire_agents import HeuristicAgent, GameSnapshot, Action, EndTurn


class SimpleRuleBasedAgent(HeuristicAgent):

    def __init__(self):
        pass

    def decide_combat(self, state: GameSnapshot) -> Action:
        if not state.combat_state:
            raise ValueError

        if not self._has_actions_available(state):
            return EndTurn()

        if self._incoming_damage(state) > state.combat_state.player_block:
            raise NotImplementedError("Play card with most block")


        raise NotImplementedError()

    @staticmethod
    def _incoming_damage(state: GameSnapshot) -> int:
        assert state.combat_state
        incoming_damage = 0
        for enemy in state.combat_state.enemies:
            for enemy_intent in enemy.intents:
                incoming_damage += enemy_intent.damage
        return incoming_damage

    @staticmethod
    def _has_actions_available(state: GameSnapshot) -> bool:
        assert state.combat_state
        if state.combat_state.energy == 0:
            return False
        if any(c.can_play for c in state.combat_state.hand):
            return False
        return True