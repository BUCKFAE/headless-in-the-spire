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
    have `is_enabled=false` on the wire and should never be picked.
    `card_select_indices` is the same hint-array shape `PlayCard` uses;
    it routes through the host's ICardSelector queue so the engine's
    SMITH prompt (CardSelectCmd.FromDeckForUpgrade over the deck's
    upgradable subset) resolves headlessly. Pass `[[0]]` for SMITH to
    upgrade the first upgradable card; omit for HEAL and other options
    that don't prompt for cards.
    """

    option_index: int
    card_select_indices: tuple[tuple[int, ...], ...] | None = None


@dataclass(frozen=True, slots=True)
class TakeTreasure:
    """Open the treasure-room chest, grant the offered relic, leave.

    Maps to `run/take_treasure`. Most agents pick this — the offered
    relic is visible via `available_treasure_relics` and the greedy
    posture is "always claim". Override with `SkipTreasure` when the
    offered relic is undesirable.
    """


@dataclass(frozen=True, slots=True)
class SkipTreasure:
    """Walk past the chest without granting the offered relic.

    Maps to `run/skip_treasure`. Useful for relic-conflict avoidance,
    SilverCrucible-style modifiers, or any agent that prefers a known-
    bad offering over an empty Player.Relics slot.
    """


@dataclass(frozen=True, slots=True)
class LeaveMerchantRoom:
    """Leave the merchant without buying anything.

    The host exposes a separate run/buy_merchant_item call for purchases;
    `LeaveMerchantRoom` is the "no thanks" path that the default
    HeuristicAgent picks (greedy posture: don't speculate gold). A
    purpose-built shopping agent should add a `BuyMerchantItem` action.
    """


@dataclass(frozen=True, slots=True)
class EnterNextAct:
    """Advance from a depleted map (the post-boss empty state) to the next act.

    Mirrors C# `EnterNextAct`. The engine flips `current_room_type=MapRoom`
    with an empty `available_map_nodes` once the boss falls; the agent
    must drive `run/enter_next_act` to regenerate the next act's map.
    """


@dataclass(frozen=True, slots=True)
class ProceedEvent:
    """Acknowledge a finished event whose room hasn't auto-transitioned yet.

    Mirrors C# `ProceedEvent`. Some events leave the engine in
    `current_room_type=EventRoom` with an empty `available_event_options`
    after the choice resolved — the agent must drive `run/proceed_event`
    to fall through to MapRoom.
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
    | TakeTreasure
    | SkipTreasure
    | LeaveMerchantRoom
    | EnterNextAct
    | ProceedEvent
)
