"""Test helpers shared across the agents test suite.

`build_snapshot` constructs a real `RunStateResult` (the fattest wire
DTO that satisfies `GameSnapshot`) with sensible defaults. Tests fill
in only the fields that matter to the case under test, keeping each
test's intent obvious.
"""

from headless_in_the_spire._models import (
    Card,
    CardRewardOption,
    Character,
    CombatState,
    Enemy,
    EventOption,
    MapNode,
    MapNodeType,
    RestSiteOption,
    RestSiteOptionId,
    RewardKind,
    RewardOption,
    RewardsState,
    RoomType,
    RunStateResult,
    TargetType,
)


def empty_combat() -> CombatState:
    """A 'no combat in progress' CombatState — zero-valued, empty lists."""
    return CombatState(
        round=0,
        energy=0,
        max_energy=0,
        player_block=0,
        is_play_phase=False,
        is_in_progress=False,
        draw_pile_count=0,
        discard_pile_count=0,
        hand=[],
        enemies=[],
        player_powers=[],
    )


def empty_rewards() -> RewardsState:
    return RewardsState(available=[])


def card(
    *,
    index: int,
    cost: int = 1,
    can_play: bool = True,
    target_type: TargetType = TargetType.any_enemy,
    card_id: str = "test_card",
    upgraded: bool = False,
) -> Card:
    return Card(
        index=index,
        id=card_id,
        cost=cost,
        can_play=can_play,
        target_type=target_type,
        upgraded=upgraded,
    )


def enemy(*, index: int, hp: int, max_hp: int | None = None) -> Enemy:
    return Enemy(
        index=index,
        monster_id="test_monster",
        hp=hp,
        max_hp=max_hp if max_hp is not None else hp,
        block=0,
        intends_attack=False,
        intents=[],
        powers=[],
    )


def card_reward(*, index: int, cards: list[CardRewardOption]) -> RewardOption:
    return RewardOption(
        index=index,
        kind=RewardKind.card,
        can_skip=True,
        cards=cards,
    )


def gold_reward(*, index: int, amount: int = 25) -> RewardOption:
    return RewardOption(
        index=index,
        kind=RewardKind.gold,
        can_skip=False,
        gold_amount=amount,
    )


def card_option(*, index: int, cost: int = 1, card_id: str = "test_card") -> CardRewardOption:
    return CardRewardOption(index=index, id=card_id, cost=cost)


def rest_site_option(
    *,
    index: int,
    option_id: str = "HEAL",
    is_enabled: bool = True,
) -> RestSiteOption:
    return RestSiteOption(index=index, option_id=RestSiteOptionId(option_id), is_enabled=is_enabled)


def build_snapshot(
    *,
    room: RoomType = RoomType.map_room,
    is_game_over: bool = False,
    is_victory: bool = False,
    is_dead: bool = False,
    hp: int = 80,
    act_floor: int = 0,
    current_act_index: int = 0,
    map_nodes: list[MapNode] | None = None,
    event_options: list[EventOption] | None = None,
    rest_site_options: list[RestSiteOption] | None = None,
    combat: CombatState | None = None,
    rewards: RewardsState | None = None,
) -> RunStateResult:
    """Compose a snapshot for a single agent test.

    `RunStateResult` carries every field a `GameSnapshot` Protocol
    requires plus a few extra (gold, max_hp, ...) — agents only consume
    the Protocol subset, so the extras don't influence behaviour.
    """
    return RunStateResult(
        ok=True,
        character=Character.ironclad,
        seed=0,
        hp=hp,
        max_hp=80,
        gold=0,
        deck_size=10,
        current_room_type=room,
        act_floor=act_floor,
        current_act_index=current_act_index,
        is_game_over=is_game_over,
        is_victory=is_victory,
        is_dead=is_dead,
        available_map_nodes=map_nodes if map_nodes is not None else [],
        available_event_options=event_options if event_options is not None else [],
        available_merchant_items=[],
        available_treasure_relics=[],
        available_rest_site_options=rest_site_options if rest_site_options is not None else [],
        combat_state=combat if combat is not None else empty_combat(),
        rewards_state=rewards if rewards is not None else empty_rewards(),
        relics=[],
        owned_potions=[],
        triggered_since_prev=[],
        triggered_dropped=0,
    )


def map_node(*, col: int, row: int, kind: MapNodeType = MapNodeType.monster) -> MapNode:
    return MapNode(col=col, row=row, type=kind)


def event_option(*, index: int, is_locked: bool = False) -> EventOption:
    return EventOption(index=index, text_key=None, is_locked=is_locked)


def in_progress_combat(
    *,
    energy: int = 3,
    hand: list[Card] | None = None,
    enemies: list[Enemy] | None = None,
) -> CombatState:
    return CombatState(
        round=1,
        energy=energy,
        max_energy=3,
        player_block=0,
        is_play_phase=True,
        is_in_progress=True,
        draw_pile_count=10,
        discard_pile_count=0,
        hand=hand if hand is not None else [],
        enemies=enemies if enemies is not None else [],
        player_powers=[],
    )
