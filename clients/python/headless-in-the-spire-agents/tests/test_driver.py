"""Driver loop dispatch and termination conditions.

A scripted fake client lets us assert the exact wire methods + params
the driver invokes for each `Action`, plus the loop's behaviour at the
terminal-state, step-cap, and agent-stop boundaries — all without
spawning a real subprocess.
"""

from collections.abc import Callable

import pytest
from conftest import (
    build_snapshot,
    card,
    card_option,
    card_reward,
    enemy,
    event_option,
    in_progress_combat,
    map_node,
)
from headless_in_the_spire_agents import (
    Action,
    Agent,
    EndTurn,
    GameSnapshot,
    PlayCard,
    SelectEventOption,
    SelectMapNode,
    SelectReward,
    SkipReward,
    apply_action,
    play_run,
)

from headless_in_the_spire._models import (
    RewardsState,
    RoomType,
    RunEndTurnResult,
    RunPlayCardParams,
    RunPlayCardResult,
    RunSelectEventOptionParams,
    RunSelectEventOptionResult,
    RunSelectMapNodeParams,
    RunSelectMapNodeResult,
    RunSelectRewardParams,
    RunSelectRewardResult,
    RunSkipRewardParams,
    RunSkipRewardResult,
    RunStateResult,
)


def _make_result_like(snap: GameSnapshot) -> RunEndTurnResult:
    """Wrap a snapshot as a `RunEndTurnResult` for the fake client to
    return. The driver only consumes the `GameSnapshot` Protocol
    fields so the exact result type doesn't matter."""
    return RunEndTurnResult(
        ok=True,
        current_room_type=snap.current_room_type,
        act_floor=snap.act_floor,
        current_act_index=snap.current_act_index,
        is_game_over=snap.is_game_over,
        is_victory=snap.is_victory,
        is_dead=snap.is_dead,
        hp=snap.hp,
        available_map_nodes=snap.available_map_nodes,
        available_event_options=snap.available_event_options,
        available_rest_site_options=[],
        available_merchant_items=[],
        combat_state=snap.combat_state,
        rewards_state=snap.rewards_state,
        relics=[],
        owned_potions=[],
    )


class FakeClient:
    """Minimal Client stand-in for driver tests.

    Each `run_*` method records what was called and returns the next
    snapshot from a script. The driver only depends on the public
    method shapes (not on `Transport`), so we don't subclass `Client`.
    """

    def __init__(self, scripted: list[GameSnapshot]) -> None:
        self._script = list(scripted)
        self.calls: list[tuple[str, object]] = []

    def _pop(self) -> GameSnapshot:
        if not self._script:
            raise AssertionError("FakeClient ran out of scripted snapshots")
        return self._script.pop(0)

    def run_state(self) -> RunStateResult:
        self.calls.append(("run/state", None))
        snap = self._pop()
        # Driver typed `run_state()` against `RunStateResult` — coerce.
        if isinstance(snap, RunStateResult):
            return snap
        raise AssertionError("first scripted snapshot must be a RunStateResult")

    def run_play_card(self, params: RunPlayCardParams) -> RunPlayCardResult:
        self.calls.append(("run/play_card", params))
        s = self._pop()
        return RunPlayCardResult(
            ok=True,
            card_index=params.card_index,
            target_index=params.target_index,
            current_room_type=s.current_room_type,
            act_floor=s.act_floor,
            current_act_index=s.current_act_index,
            is_game_over=s.is_game_over,
            is_victory=s.is_victory,
            is_dead=s.is_dead,
            hp=s.hp,
            available_map_nodes=s.available_map_nodes,
            available_event_options=s.available_event_options,
            available_rest_site_options=[],
            available_merchant_items=[],
            combat_state=s.combat_state,
            rewards_state=s.rewards_state,
            relics=[],
            owned_potions=[],
        )

    def run_end_turn(self) -> RunEndTurnResult:
        self.calls.append(("run/end_turn", None))
        return _make_result_like(self._pop())

    def run_select_map_node(self, params: RunSelectMapNodeParams) -> RunSelectMapNodeResult:
        self.calls.append(("run/select_map_node", params))
        s = self._pop()
        return RunSelectMapNodeResult(
            ok=True,
            col=params.col,
            row=params.row,
            current_room_type=s.current_room_type,
            act_floor=s.act_floor,
            current_act_index=s.current_act_index,
            is_game_over=s.is_game_over,
            is_victory=s.is_victory,
            is_dead=s.is_dead,
            hp=s.hp,
            available_map_nodes=s.available_map_nodes,
            available_event_options=s.available_event_options,
            available_rest_site_options=[],
            available_merchant_items=[],
            combat_state=s.combat_state,
            rewards_state=s.rewards_state,
            relics=[],
            owned_potions=[],
        )

    def run_select_event_option(
        self, params: RunSelectEventOptionParams
    ) -> RunSelectEventOptionResult:
        self.calls.append(("run/select_event_option", params))
        s = self._pop()
        return RunSelectEventOptionResult(
            ok=True,
            option_index=params.option_index,
            current_room_type=s.current_room_type,
            act_floor=s.act_floor,
            current_act_index=s.current_act_index,
            is_game_over=s.is_game_over,
            is_victory=s.is_victory,
            is_dead=s.is_dead,
            hp=s.hp,
            available_map_nodes=s.available_map_nodes,
            available_event_options=s.available_event_options,
            available_rest_site_options=[],
            available_merchant_items=[],
            combat_state=s.combat_state,
            rewards_state=s.rewards_state,
            relics=[],
            owned_potions=[],
        )

    def run_select_reward(self, params: RunSelectRewardParams) -> RunSelectRewardResult:
        self.calls.append(("run/select_reward", params))
        s = self._pop()
        return RunSelectRewardResult(
            ok=True,
            reward_index=params.reward_index,
            card_index=params.card_index,
            current_room_type=s.current_room_type,
            act_floor=s.act_floor,
            current_act_index=s.current_act_index,
            is_game_over=s.is_game_over,
            is_victory=s.is_victory,
            is_dead=s.is_dead,
            hp=s.hp,
            available_map_nodes=s.available_map_nodes,
            available_event_options=s.available_event_options,
            available_rest_site_options=[],
            available_merchant_items=[],
            combat_state=s.combat_state,
            rewards_state=s.rewards_state,
            relics=[],
            owned_potions=[],
        )

    def run_skip_reward(self, params: RunSkipRewardParams) -> RunSkipRewardResult:
        self.calls.append(("run/skip_reward", params))
        s = self._pop()
        return RunSkipRewardResult(
            ok=True,
            reward_index=params.reward_index,
            current_room_type=s.current_room_type,
            act_floor=s.act_floor,
            current_act_index=s.current_act_index,
            is_game_over=s.is_game_over,
            is_victory=s.is_victory,
            is_dead=s.is_dead,
            hp=s.hp,
            available_map_nodes=s.available_map_nodes,
            available_event_options=s.available_event_options,
            available_rest_site_options=[],
            available_merchant_items=[],
            combat_state=s.combat_state,
            rewards_state=s.rewards_state,
            relics=[],
            owned_potions=[],
        )


class _ScriptedAgent:
    """Agent that emits a pre-canned sequence of actions."""

    def __init__(self, actions: list[Action]) -> None:
        self._actions = list(actions)

    def decide(self, state: GameSnapshot) -> Action:
        del state
        if not self._actions:
            raise AssertionError("ScriptedAgent exhausted")
        return self._actions.pop(0)


# Sanity: structural conformance to the Protocol.
def test_scripted_agent_satisfies_agent_protocol() -> None:
    agent: Agent = _ScriptedAgent([EndTurn()])
    assert callable(agent.decide)


# ── apply_action dispatch table ──────────────────────────────────────


def test_apply_dispatches_each_action_to_correct_method() -> None:
    snap = build_snapshot()
    cases: list[tuple[Action, str, Callable[[object], bool]]] = [
        (
            PlayCard(card_index=2, target_index=1),
            "run/play_card",
            lambda p: (
                isinstance(p, RunPlayCardParams) and p.card_index == 2 and p.target_index == 1
            ),
        ),
        (EndTurn(), "run/end_turn", lambda p: p is None),
        (
            SelectMapNode(col=1, row=2),
            "run/select_map_node",
            lambda p: isinstance(p, RunSelectMapNodeParams) and p.col == 1 and p.row == 2,
        ),
        (
            SelectEventOption(option_index=3),
            "run/select_event_option",
            lambda p: isinstance(p, RunSelectEventOptionParams) and p.option_index == 3,
        ),
        (
            SelectReward(reward_index=0, card_index=2),
            "run/select_reward",
            lambda p: (
                isinstance(p, RunSelectRewardParams) and p.reward_index == 0 and p.card_index == 2
            ),
        ),
        (
            SkipReward(reward_index=1),
            "run/skip_reward",
            lambda p: isinstance(p, RunSkipRewardParams) and p.reward_index == 1,
        ),
    ]
    for action, expected_method, params_check in cases:
        fake = FakeClient([snap])
        apply_action(fake, action)  # type: ignore[arg-type]
        assert len(fake.calls) == 1
        method, params = fake.calls[0]
        assert method == expected_method
        assert params_check(params), f"params mismatch for {action!r}: got {params!r}"


# ── play_run loop ────────────────────────────────────────────────────


def test_play_run_stops_immediately_on_game_over() -> None:
    snap = build_snapshot(is_game_over=True)
    fake = FakeClient([snap])
    outcome = play_run(fake, _ScriptedAgent([]))  # type: ignore[arg-type]
    assert outcome.terminated_by == "game_over"
    assert outcome.steps == 0
    assert fake.calls == [("run/state", None)]


def test_play_run_dispatches_until_game_over() -> None:
    # Step 1: agent in combat → play a card.
    # Step 2: snapshot post-play, still in combat → end turn.
    # Step 3: snapshot post-end-turn, is_game_over=True → stop.
    combat = in_progress_combat(
        hand=[card(index=0, cost=1, can_play=True)],
        enemies=[enemy(index=0, hp=10)],
    )
    snap_initial = build_snapshot(room=RoomType.combat_room, combat=combat)
    snap_after_play = build_snapshot(room=RoomType.combat_room, combat=combat)
    snap_after_end = build_snapshot(is_game_over=True)

    fake = FakeClient([snap_initial, snap_after_play, snap_after_end])
    agent = _ScriptedAgent([PlayCard(card_index=0, target_index=0), EndTurn()])

    outcome = play_run(fake, agent)  # type: ignore[arg-type]
    assert outcome.terminated_by == "game_over"
    assert outcome.steps == 2
    methods = [c[0] for c in fake.calls]
    assert methods == ["run/state", "run/play_card", "run/end_turn"]


def test_play_run_observer_sees_pre_action_state() -> None:
    snap_initial = build_snapshot(room=RoomType.map_room, map_nodes=[map_node(col=0, row=0)])
    snap_final = build_snapshot(is_game_over=True)
    fake = FakeClient([snap_initial, snap_final])
    agent = _ScriptedAgent([SelectMapNode(col=0, row=0)])

    seen: list[tuple[int, GameSnapshot, Action]] = []

    def observer(i: int, s: GameSnapshot, a: Action) -> None:
        seen.append((i, s, a))

    play_run(fake, agent, on_step=observer)  # type: ignore[arg-type]
    assert len(seen) == 1
    i, s, a = seen[0]
    assert i == 0
    assert s.current_room_type is RoomType.map_room
    assert a == SelectMapNode(col=0, row=0)


def test_play_run_respects_max_steps() -> None:
    # Agent loops forever; cap stops it.
    snap = build_snapshot(room=RoomType.combat_room, combat=in_progress_combat())
    fake = FakeClient([snap] * 100)
    agent = _ScriptedAgent([EndTurn()] * 100)
    outcome = play_run(fake, agent, max_steps=3)  # type: ignore[arg-type]
    assert outcome.terminated_by == "step_limit"
    assert outcome.steps == 3
    # 1 run_state + 3 end_turn
    assert [c[0] for c in fake.calls] == [
        "run/state",
        "run/end_turn",
        "run/end_turn",
        "run/end_turn",
    ]


def test_play_run_should_continue_stops_run() -> None:
    snap = build_snapshot(room=RoomType.combat_room, combat=in_progress_combat())
    fake = FakeClient([snap])
    agent = _ScriptedAgent([EndTurn()])
    outcome = play_run(
        fake,  # type: ignore[arg-type]
        agent,
        should_continue=lambda i, s: False,
    )
    assert outcome.terminated_by == "agent_stop"
    assert outcome.steps == 0


def test_play_run_with_greedy_completes_simple_combat_into_rewards() -> None:
    # Integration-ish: real GreedyAgent against a scripted host that
    # mimics combat → rewards → game-over.
    from headless_in_the_spire_agents import GreedyAgent

    combat_alive = in_progress_combat(
        hand=[card(index=0, cost=1, can_play=True)],
        enemies=[enemy(index=0, hp=10)],
    )
    snap_in_combat = build_snapshot(room=RoomType.combat_room, combat=combat_alive)
    snap_with_rewards = build_snapshot(
        room=RoomType.combat_room,
        rewards=RewardsState(
            available=[card_reward(index=0, cards=[card_option(index=0, cost=1)])]
        ),
    )
    snap_game_over = build_snapshot(is_game_over=True)

    fake = FakeClient([snap_in_combat, snap_with_rewards, snap_game_over])
    outcome = play_run(fake, GreedyAgent())  # type: ignore[arg-type]

    assert outcome.terminated_by == "game_over"
    methods = [c[0] for c in fake.calls]
    assert methods == ["run/state", "run/play_card", "run/select_reward"]


def test_event_dispatch_through_play_run() -> None:
    # Cover the event branch end-to-end: SelectEventOption hits
    # run/select_event_option.
    snap_event = build_snapshot(room=RoomType.event_room, event_options=[event_option(index=2)])
    snap_done = build_snapshot(is_game_over=True)
    fake = FakeClient([snap_event, snap_done])
    agent = _ScriptedAgent([SelectEventOption(option_index=2)])
    play_run(fake, agent)  # type: ignore[arg-type]
    assert [c[0] for c in fake.calls] == ["run/state", "run/select_event_option"]
    _, params = fake.calls[1]
    assert isinstance(params, RunSelectEventOptionParams)
    assert params.option_index == 2


def test_skip_reward_dispatch_through_play_run() -> None:
    snap_rewards = build_snapshot(
        rewards=RewardsState(available=[card_reward(index=0, cards=[card_option(index=0)])])
    )
    snap_done = build_snapshot(is_game_over=True)
    fake = FakeClient([snap_rewards, snap_done])
    agent = _ScriptedAgent([SkipReward(reward_index=0)])
    play_run(fake, agent)  # type: ignore[arg-type]
    _, params = fake.calls[1]
    assert isinstance(params, RunSkipRewardParams)
    assert params.reward_index == 0


def test_play_run_propagates_decide_exceptions() -> None:
    snap = build_snapshot(room=RoomType.combat_room, combat=in_progress_combat())
    fake = FakeClient([snap])

    class _Boom:
        def decide(self, state: GameSnapshot) -> Action:
            del state
            raise RuntimeError("agent failed")

    with pytest.raises(RuntimeError, match="agent failed"):
        play_run(fake, _Boom())  # type: ignore[arg-type]
