"""Drive `GreedyAgent` against a live host and log each step.

Usage:

    uv run python -m headless_in_the_spire_agents.examples.greedy_run \
        --character ironclad --seed 42 --max-steps 500

The script spawns the C# host subprocess via `Client.spawn()`, starts a
new run, and lets `play_run` drive the `GreedyAgent`. Every step is
printed to stdout in a single line so the output is easy to scan or
pipe through `grep`/`tee`.

Per AD-6 this is a Python user tool — useful for ad-hoc exploration of
the wire, not the authoritative answer to "how should greedy behave".
"""

import argparse
import sys
from collections.abc import Sequence

from headless_in_the_spire import Client
from headless_in_the_spire._models import Character, RunNewParams
from headless_in_the_spire_agents import (
    Action,
    EndTurn,
    EnterNextAct,
    GameSnapshot,
    GreedyAgent,
    LeaveMerchantRoom,
    LeaveTreasureRoom,
    PlayCard,
    ProceedEvent,
    RunOutcome,
    SelectEventOption,
    SelectMapNode,
    SelectRestSiteOption,
    SelectReward,
    SkipReward,
    current_phase,
    play_run,
)


def _format_action(action: Action) -> str:
    # Compact one-token-per-field rendering so a long run still fits on
    # one line per step. Matching here mirrors the driver's dispatch
    # table — adding a new Action variant fails pyright until handled.
    match action:
        case PlayCard(card_index=ci, target_index=ti):
            return (
                f"play_card(card={ci}, target={ti})" if ti is not None else f"play_card(card={ci})"
            )
        case EndTurn():
            return "end_turn"
        case SelectMapNode(col=c, row=r):
            return f"select_map_node(col={c}, row={r})"
        case SelectEventOption(option_index=oi):
            return f"select_event_option(option={oi})"
        case SelectReward(reward_index=ri, card_index=ci):
            return (
                f"select_reward(reward={ri}, card={ci})"
                if ci is not None
                else f"select_reward(reward={ri})"
            )
        case SkipReward(reward_index=ri):
            return f"skip_reward(reward={ri})"
        case SelectRestSiteOption(option_index=oi):
            return f"select_rest_site_option(option={oi})"
        case LeaveTreasureRoom():
            return "leave_treasure_room"
        case LeaveMerchantRoom():
            return "leave_merchant_room"
        case EnterNextAct():
            return "enter_next_act"
        case ProceedEvent():
            return "proceed_event"


def _log_step(step: int, state: GameSnapshot, action: Action) -> None:
    phase = current_phase(state).value
    room = state.current_room_type.value
    print(
        f"step={step:04d}  floor={state.act_floor}  hp={state.hp}  "
        f"phase={phase:<8} room={room:<12} -> {_format_action(action)}",
        flush=True,
    )


def _log_outcome(outcome: RunOutcome) -> None:
    s = outcome.final_state
    print(
        f"--- end: terminated_by={outcome.terminated_by}  "
        f"steps={outcome.steps}  hp={s.hp}  floor={s.act_floor}  "
        f"room={s.current_room_type.value}  game_over={s.is_game_over}",
        flush=True,
    )


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        description="Run the reference GreedyAgent against a live host and log each step.",
    )
    parser.add_argument(
        "--character",
        type=Character,
        choices=list(Character),
        default=Character.ironclad,
        metavar="{" + ",".join(c.value for c in Character) + "}",
        help="Character to start the run with.",
    )
    parser.add_argument("--seed", type=int, default=42, help="Run seed.")
    parser.add_argument(
        "--max-steps",
        type=int,
        default=500,
        help="Safety cap so a stuck agent can't spin forever.",
    )
    args = parser.parse_args(argv)

    agent = GreedyAgent()
    with Client.spawn() as client:
        client.run_new(
            RunNewParams(character=args.character, seed=args.seed),
            timeout=120,
        )
        outcome = play_run(
            client,
            agent,
            max_steps=args.max_steps,
            on_step=_log_step,
        )
        _log_outcome(outcome)
    return 0


if __name__ == "__main__":
    sys.exit(main())
