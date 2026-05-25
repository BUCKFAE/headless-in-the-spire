"""Run loop that drives an `Agent` against a `Client`.

The flow is dead-simple: fetch the current snapshot, ask the agent for
an action, dispatch the action to the matching `Client.run_*` method,
feed the resulting snapshot back. Loop until terminal, the agent stops,
or a step cap trips.

The dispatch lives here (not on `Action` or `Agent`) so that agents stay
ignorant of the wire client — swap `Client` for a fake in tests and the
agent code is unchanged.
"""

from collections.abc import Callable
from dataclasses import dataclass
from typing import Literal

from headless_in_the_spire import Client
from headless_in_the_spire._models import (
    RunPlayCardParams,
    RunSelectEventOptionParams,
    RunSelectMapNodeParams,
    RunSelectRestSiteOptionParams,
    RunSelectRewardParams,
    RunSkipRewardParams,
)
from headless_in_the_spire_agents.actions import (
    Action,
    EndTurn,
    EnterNextAct,
    LeaveMerchantRoom,
    PlayCard,
    ProceedEvent,
    SelectEventOption,
    SelectMapNode,
    SelectRestSiteOption,
    SelectReward,
    SkipReward,
    SkipTreasure,
    TakeTreasure,
)
from headless_in_the_spire_agents.agent import Agent
from headless_in_the_spire_agents.state import GameSnapshot

# Why the driver doesn't loop on `RunStateResult` shape: every run/*
# method already returns a structurally-equivalent snapshot, so we can
# pass the action result straight to the agent without an extra round-
# trip. `client.run_state()` is only used for the very first fetch.

TerminationReason = Literal["game_over", "step_limit", "agent_stop"]


@dataclass(frozen=True, slots=True)
class RunOutcome:
    """Result of `play_run`. `terminated_by` distinguishes a natural end
    (run finished, game-over flag set), a safety stop (we hit `max_steps`
    without termination — usually a bug or a stuck agent), and the agent
    asking to stop via the `should_continue` callback."""

    final_state: GameSnapshot
    steps: int
    terminated_by: TerminationReason


StepObserver = Callable[[int, GameSnapshot, Action], None]
"""Called once per step with `(step_index, pre_action_snapshot, action)`.
Useful for logging, replay capture, or progress bars."""


def play_run(
    client: Client,
    agent: Agent,
    *,
    max_steps: int = 10_000,
    on_step: StepObserver | None = None,
    should_continue: Callable[[int, GameSnapshot], bool] | None = None,
) -> RunOutcome:
    """Drive `agent` against `client` until the run ends.

    Caller is responsible for `client.run_new(...)` first — the driver
    starts from whatever run is currently active and reads its snapshot
    via `client.run_state()`. That keeps `play_run` reusable for
    mid-run scenarios (resuming a saved state, replaying after a fault).

    `should_continue(step_index, snapshot)` is checked before each
    decision; return `False` to bail out with `terminated_by="agent_stop"`.
    """
    state: GameSnapshot = client.run_state()
    for i in range(max_steps):
        if state.is_game_over:
            return RunOutcome(final_state=state, steps=i, terminated_by="game_over")
        if should_continue is not None and not should_continue(i, state):
            return RunOutcome(final_state=state, steps=i, terminated_by="agent_stop")
        action = agent.decide(state)
        if on_step is not None:
            on_step(i, state, action)
        state = apply_action(client, action)
    return RunOutcome(final_state=state, steps=max_steps, terminated_by="step_limit")


def apply_action(client: Client, action: Action) -> GameSnapshot:
    """Dispatch one `Action` to the wire client and return the snapshot.

    Public because it's a useful unit on its own — interactive REPL
    sessions, replay tools, and tests can drive single actions without
    the `play_run` loop. Exhaustiveness is checked by pyright; adding a
    new `Action` variant fails type checking here until the matching
    branch is written."""
    match action:
        case PlayCard(card_index=ci, target_index=ti):
            return client.run_play_card(RunPlayCardParams(card_index=ci, target_index=ti))
        case EndTurn():
            return client.run_end_turn()
        case SelectMapNode(col=c, row=r):
            return client.run_select_map_node(RunSelectMapNodeParams(col=c, row=r))
        case SelectEventOption(option_index=oi):
            return client.run_select_event_option(RunSelectEventOptionParams(option_index=oi))
        case SelectReward(reward_index=ri, card_index=ci):
            return client.run_select_reward(RunSelectRewardParams(reward_index=ri, card_index=ci))
        case SkipReward(reward_index=ri):
            return client.run_skip_reward(RunSkipRewardParams(reward_index=ri))
        case SelectRestSiteOption(option_index=oi, card_select_indices=csi):
            hints = [list(prompt) for prompt in csi] if csi is not None else None
            return client.run_select_rest_site_option(
                RunSelectRestSiteOptionParams(option_index=oi, card_select_indices=hints)
            )
        case TakeTreasure():
            return client.run_take_treasure()
        case SkipTreasure():
            return client.run_skip_treasure()
        case LeaveMerchantRoom():
            return client.run_leave_merchant_room()
        case EnterNextAct():
            return client.run_enter_next_act()
        case ProceedEvent():
            return client.run_proceed_event()
