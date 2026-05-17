"""Action algebra agents emit — one frozen dataclass per legal host call.

Agents return `Action`s; the driver maps each one to the matching
`Client.run_*` method. Keeping the action vocabulary separate from the
generated wire DTOs means agent code never imports `_models`, and a
codegen rename doesn't churn every agent.
"""

from dataclasses import dataclass


@dataclass(frozen=True, slots=True)
class PlayCard:
    """Play a card from hand, optionally targeting a specific enemy."""

    card_index: int
    target_index: int | None = None


@dataclass(frozen=True, slots=True)
class EndTurn:
    """End the player's turn in combat."""


@dataclass(frozen=True, slots=True)
class SelectMapNode:
    """Pick the next room from the act map."""

    col: int
    row: int


@dataclass(frozen=True, slots=True)
class SelectEventOption:
    """Choose an option presented by an event room."""

    option_index: int


@dataclass(frozen=True, slots=True)
class SelectReward:
    """Claim a pending reward. `card_index` is required for card rewards
    (the index into `RewardOption.cards`) and ignored otherwise."""

    reward_index: int
    card_index: int | None = None


@dataclass(frozen=True, slots=True)
class SkipReward:
    """Skip a skippable reward (card rewards; never gold/relic/potion)."""

    reward_index: int


@dataclass(frozen=True, slots=True)
class SelectRestSiteOption:
    """Pick one of the rest-site options (HEAL, SMITH, ...) by its wire index.

    The host enforces which options are legal — locked / depleted options
    have `is_enabled=false` on the wire and should never be picked. The
    SMITH branch additionally opens a card-select sub-flow that is not
    yet driven from Python; subclasses that pick SMITH must implement
    the follow-up themselves.
    """

    option_index: int


@dataclass(frozen=True, slots=True)
class LeaveTreasureRoom:
    """Auto-resolve the treasure room (open the chest, grab the relic, leave).

    The host exposes treasure as a single no-arg call — there is no
    pre-open preview, no "skip the chest" option. If we later need to
    surface a relic choice we'll add a follow-up action; today this is
    the only legal move once `Phase.treasure` is live.
    """


# Closed union — agents always return one of these. Pyright checks
# match-statement exhaustiveness against this alias.
type Action = (
    PlayCard
    | EndTurn
    | SelectMapNode
    | SelectEventOption
    | SelectReward
    | SkipReward
    | SelectRestSiteOption
    | LeaveTreasureRoom
)
