"""Python-side agent contract.

`Agent` is the one method the run loop speaks: `decide(state) -> Action`.
Tiny on purpose, so an RL policy, a search agent, or a human-in-the-loop
wrapper all satisfy the same Python-side interface.

`HeuristicAgent` is the optional convenience base for hand-written
agents that want per-phase hooks instead of one giant `match` statement.
Defaults pick the first legal option in each phase, so a subclass only
overrides what it cares about. See `agents.greedy` for an illustrative
implementation.

Per AD-6, the canonical agent contract lives in C# under
`src/Sts2Headless.Agents/`. This module describes only the shape used
by Python tooling.
"""

from typing import ClassVar, Protocol, runtime_checkable

from headless_in_the_spire._models import RestSiteOptionId, RewardKind
from headless_in_the_spire_agents.actions import (
    Action,
    EndTurn,
    EnterNextAct,
    LeaveMerchantRoom,
    ProceedEvent,
    SelectEventOption,
    SelectMapNode,
    SelectRestSiteOption,
    SelectReward,
    TakeTreasure,
)
from headless_in_the_spire_agents.state import GameSnapshot, Phase, current_phase


class NoLegalActionError(RuntimeError):
    """Raised when an agent is asked to decide but no legal action exists.

    Surfacing this lets the driver fail loudly instead of dispatching an
    action the host will reject — almost always a sign that the snapshot
    is in a state we haven't modelled yet (a new room type, an unhandled
    null payload). Capture the offending snapshot for a postmortem.
    """

    def __init__(self, message: str, snapshot: GameSnapshot | None = None) -> None:
        super().__init__(message)
        self.snapshot = snapshot


@runtime_checkable
class Agent(Protocol):
    """The single method the driver calls every step.

    Return one `Action` from the closed union in `actions`. The driver
    will dispatch it to the matching `Client.run_*` method and feed the
    resulting snapshot back on the next call. Implementations are
    stateless from the protocol's point of view; carry state on `self`
    if you need it.

    `name` is a stable, filesystem-safe identifier used by replay
    tooling (e.g. `replays/<name>/<timestamp>-<seed>/`). Pick something
    short and slugged — "greedy", "random", "block" — not a class name
    or a sentence. Treat it as a *kind* identifier, not a per-run label.
    """

    name: str

    def decide(self, state: GameSnapshot) -> Action: ...


class HeuristicAgent:
    """Convenience base for rule-based agents.

    Splits the single `decide()` call into one hook per `Phase`.
    Subclasses override only the phases they want; the defaults pick the
    first legal option in each phase, which is the dumbest action that
    keeps a run moving. Override behaviour is intentional, not stub:
    a subclass that only customises combat still produces complete runs.

    Subclasses MUST declare a `name` class attribute — replay tooling
    uses it to bucket runs by agent kind. The check fires at class
    definition time so a missing name is a loud import error, not a
    silent "agent-unknown" folder at runtime.
    """

    name: ClassVar[str]

    def __init_subclass__(cls, **kwargs: object) -> None:
        super().__init_subclass__(**kwargs)
        if "name" not in cls.__dict__:
            raise TypeError(
                f"{cls.__module__}.{cls.__qualname__} must declare a `name` "
                "class attribute (used by replay tooling)."
            )

    def decide(self, state: GameSnapshot) -> Action:
        phase = current_phase(state)
        match phase:
            case Phase.combat:
                return self.decide_combat(state)
            case Phase.rewards:
                return self.decide_rewards(state)
            case Phase.map:
                return self.decide_map(state)
            case Phase.map_empty:
                return self.decide_map_empty(state)
            case Phase.event:
                return self.decide_event(state)
            case Phase.event_finished:
                return self.decide_event_finished(state)
            case Phase.rest_site:
                return self.decide_rest_site(state)
            case Phase.treasure:
                return self.decide_treasure(state)
            case Phase.merchant:
                return self.decide_merchant(state)
            case Phase.terminal:
                raise NoLegalActionError("game over — no action available", state)
            case Phase.unknown:
                combat_in_progress = (
                    state.combat_state.is_in_progress if state.combat_state is not None else None
                )
                rewards_count = (
                    len(state.rewards_state.available) if state.rewards_state is not None else 0
                )
                raise NoLegalActionError(
                    f"no legal action: room={state.current_room_type.value}, "
                    f"combat_in_progress={combat_in_progress}, "
                    f"rewards={rewards_count}",
                    state,
                )

    # ── Per-phase hooks. Defaults below; override what you need. ──────────

    def decide_combat(self, state: GameSnapshot) -> Action:
        # Ending the turn is always legal in the play phase and never
        # voids a run. A subclass that doesn't override this still
        # completes runs, just slowly.
        return EndTurn()

    def decide_map(self, state: GameSnapshot) -> Action:
        if not state.available_map_nodes:
            raise NoLegalActionError("map phase with empty node list", state)
        node = state.available_map_nodes[0]
        return SelectMapNode(col=node.col, row=node.row)

    def decide_map_empty(self, state: GameSnapshot) -> Action:
        # Reached when the engine flipped past the act boss but hasn't
        # generated the next act's map. The only legal call is to enter
        # the next act, which regenerates available_map_nodes.
        return EnterNextAct()

    def decide_event(self, state: GameSnapshot) -> Action:
        for opt in state.available_event_options:
            if not opt.is_locked:
                return SelectEventOption(option_index=opt.index)
        raise NoLegalActionError("event phase with no unlocked options", state)

    def decide_event_finished(self, state: GameSnapshot) -> Action:
        # Some events leave the room as EventRoom with no options after
        # the choice resolved. ProceedEvent drives the transition back
        # to MapRoom (mirrors the post-boss MapEmpty case).
        return ProceedEvent()

    def decide_rewards(self, state: GameSnapshot) -> Action:
        # Reached only when current_phase returned Phase.rewards, which
        # already confirmed rewards_state is non-null and non-empty.
        assert state.rewards_state is not None
        head = state.rewards_state.available[0]
        if head.kind is RewardKind.card and head.cards:
            return SelectReward(reward_index=head.index, card_index=head.cards[0].index)
        return SelectReward(reward_index=head.index)

    def decide_rest_site(self, state: GameSnapshot) -> Action:
        # Prefer HEAL → SMITH → any enabled option. SMITH's card-pick
        # prompt is routed via the host's ICardSelector — passing [[0]]
        # tells it to upgrade the first upgradable card
        # (CardSelectCmd.FromDeckForUpgrade pre-filters the deck, so
        # index 0 is always legal when SMITH itself is enabled).
        #
        # The C# HeuristicAgent.DecideRestSite uses RunStateResult and
        # branches on Hp/MaxHp (SMITH at full HP); GameSnapshot is a
        # narrower Protocol that doesn't carry MaxHp, so the Python
        # default goes a step less smart but stays consistent.
        options = state.available_rest_site_options
        heal = next(
            (o for o in options if o.is_enabled and o.option_id is RestSiteOptionId.heal),
            None,
        )
        if heal is not None:
            return SelectRestSiteOption(option_index=heal.index)
        smith = next(
            (o for o in options if o.is_enabled and o.option_id is RestSiteOptionId.smith),
            None,
        )
        if smith is not None:
            return SelectRestSiteOption(option_index=smith.index, card_select_indices=((0,),))
        any_enabled = next((o for o in options if o.is_enabled), None)
        if any_enabled is not None:
            return SelectRestSiteOption(option_index=any_enabled.index)
        raise NoLegalActionError("rest site with no enabled options", state)

    def decide_treasure(self, state: GameSnapshot) -> Action:
        return TakeTreasure()

    def decide_merchant(self, state: GameSnapshot) -> Action:
        # Default: leave without buying. Mirrors C# HeuristicAgent —
        # speculative gold spending needs a model the heuristic lacks.
        # Shopping agents override to inspect available_merchant_items.
        return LeaveMerchantRoom()
