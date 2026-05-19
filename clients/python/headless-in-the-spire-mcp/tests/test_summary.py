"""Unit tests for the compact `summarize_run_state` view.

Synthetic snapshots only — no live host. These tests pin the *shape* of the
summary so a refactor doesn't quietly change what the LLM sees per turn.
"""

from headless_in_the_spire_mcp.summary import summarize_run_state

from headless_in_the_spire._models import (
    Character,
    CombatState,
    EventOption,
    IntentKind,
    OwnedPotion,
    Relic,
    RewardsState,
    RoomType,
    RunStateResult,
    TargetType,
)
from headless_in_the_spire._models import (
    RewardKind as _RewardKind,
)


def _base_state(**overrides: object) -> RunStateResult:
    """Build a minimum RunStateResult. Tests override individual fields."""
    defaults: dict[str, object] = {
        "ok": True,
        "character": Character.ironclad,
        "seed": 42,
        "hp": 70,
        "maxHp": 80,
        "gold": 100,
        "deckSize": 12,
        "currentRoomType": RoomType.map_room,
        "actFloor": 0,
        "currentActIndex": 1,
        "isGameOver": False,
        "isVictory": False,
        "isDead": False,
        "availableMapNodes": [],
        "availableEventOptions": [],
        "availableRestSiteOptions": [],
        "availableMerchantItems": [],
        "combatState": None,
        "rewardsState": None,
        "relics": [],
        "ownedPotions": [],
        "triggeredSincePrev": [],
        "triggeredDropped": 0,
    }
    defaults.update(overrides)
    return RunStateResult.model_validate(defaults)


def test_header_includes_act_floor_and_room():
    state = _base_state(currentActIndex=2, actFloor=7, currentRoomType=RoomType.event_room)
    text = summarize_run_state(state)
    first_line = text.splitlines()[0]
    assert "Act 2" in first_line
    assert "Floor 7" in first_line
    assert "EventRoom" in first_line


def test_vitals_line_shows_hp_gold_deck():
    state = _base_state(hp=42, maxHp=80, gold=250, deckSize=20)
    text = summarize_run_state(state)
    assert "HP 42/80" in text
    assert "Gold 250" in text
    assert "Deck 20" in text


def test_terminal_states_are_labelled():
    victory = _base_state(isGameOver=True, isVictory=True)
    defeat = _base_state(isGameOver=True, isDead=True)
    assert "VICTORY" in summarize_run_state(victory).splitlines()[0]
    assert "DEFEAT" in summarize_run_state(defeat).splitlines()[0]


def test_combat_section_lists_hand_and_enemies():
    combat = CombatState.model_validate(
        {
            "round": 2,
            "energy": 2,
            "maxEnergy": 3,
            "playerBlock": 5,
            "isPlayPhase": True,
            "isInProgress": True,
            "drawPileCount": 8,
            "discardPileCount": 4,
            "hand": [
                {
                    "index": 0,
                    "id": "Strike",
                    "cost": 1,
                    "canPlay": True,
                    "targetType": TargetType.any_enemy.value,
                },
                {
                    "index": 1,
                    "id": "Bash",
                    "cost": 2,
                    "canPlay": False,
                    "targetType": TargetType.any_enemy.value,
                },
            ],
            "enemies": [
                {
                    "index": 0,
                    "monsterId": "Cultist",
                    "hp": 48,
                    "maxHp": 48,
                    "block": 0,
                    "intendsAttack": True,
                    "intents": [{"kind": IntentKind.attack.value, "damage": 6, "hits": 1}],
                    "powers": [],
                },
            ],
            "playerPowers": [],
        }
    )
    state = _base_state(
        currentRoomType=RoomType.combat_room,
        combatState=combat.model_dump(mode="json", by_alias=True),
    )
    text = summarize_run_state(state)
    assert "Round 2" in text
    assert "Energy 2/3" in text
    assert "Block 5" in text
    assert "[0] Strike (cost 1)" in text
    assert "(cannot play)" in text  # Bash is the unplayable one
    assert "Cultist HP 48/48" in text
    assert "Attack 6" in text


def test_rewards_section_when_available():
    rewards = RewardsState.model_validate(
        {
            "available": [
                {"index": 0, "kind": _RewardKind.gold.value, "canSkip": False, "goldAmount": 25},
                {
                    "index": 1,
                    "kind": _RewardKind.card.value,
                    "canSkip": True,
                    "cards": [{"index": 0, "id": "Anger", "cost": 0}],
                },
            ]
        }
    )
    state = _base_state(
        currentRoomType=RoomType.combat_room,
        rewardsState=rewards.model_dump(mode="json", by_alias=True),
    )
    text = summarize_run_state(state)
    assert "Rewards:" in text
    assert "(25 gold)" in text
    assert "Anger" in text
    assert "(cannot skip)" in text


def test_relics_and_potions_listed():
    state = _base_state(
        relics=[Relic.model_validate({"id": "BurningBlood"}).model_dump(by_alias=True)],
        ownedPotions=[
            OwnedPotion.model_validate(
                {
                    "index": 0,
                    "id": "BlockPotion",
                    # `TargetType.self` is a real enum member; access via lookup so
                    # pyright doesn't conflate it with the Python `self` parameter.
                    "targetType": TargetType["self"].value,
                    "canUse": True,
                }
            ).model_dump(by_alias=True)
        ],
    )
    text = summarize_run_state(state)
    assert "Relics: BurningBlood" in text
    assert "Potions: [0] BlockPotion" in text


def test_event_options_section():
    options = [
        EventOption.model_validate(
            {
                "index": 0,
                "textKey": "Take the deal",
                "isLocked": False,
            }
        ).model_dump(by_alias=True),
        EventOption.model_validate(
            {
                "index": 1,
                "textKey": "Leave",
                "isLocked": True,
            }
        ).model_dump(by_alias=True),
    ]
    state = _base_state(
        currentRoomType=RoomType.event_room,
        availableEventOptions=options,
    )
    text = summarize_run_state(state)
    assert "Event options:" in text
    assert "[0] Take the deal" in text
    assert "(locked)" in text
