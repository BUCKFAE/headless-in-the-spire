"""Compact, low-token text summary of a `RunStateResult`.

An AI assistant polling the wire after every action quickly drowns in the
full `RunStateResult` JSON. This module renders the same state as a few
hundred bytes of human-readable text — enough to make the next decision,
small enough that ten consecutive turns don't blow the context window.

Scope: read-only. The summary describes *what is*, never what the agent
should do. Picking the next action is the LLM's job; this file just lays
out the legal options it can choose between.
"""

from collections.abc import Sequence
from io import StringIO

from headless_in_the_spire._models import (
    Card,
    CombatState,
    Enemy,
    EventOption,
    Intent,
    IntentKind,
    MapNode,
    MerchantItem,
    OwnedPotion,
    Power,
    Relic,
    RestSiteOption,
    RewardsState,
    RoomType,
    RunStateResult,
)


def summarize_run_state(state: RunStateResult) -> str:
    """Render `state` as a multi-line plain-text summary.

    The structure is roughly:

        Act <N> Floor <F> — <RoomType>[ — <hint>]
        HP <hp>/<max> | Gold <g> | Deck <n>
        [Combat block, if in combat]
        [Phase-specific options: hand / map / event / rewards / …]
        Relics: …
        Potions: …
    """
    out = StringIO()
    _write_header(out, state)
    _write_vitals(out, state)
    if state.combat_state is not None and state.combat_state.is_in_progress:
        _write_combat(out, state.combat_state)
    _write_phase_options(out, state)
    _write_relics(out, state.relics)
    _write_potions(out, state.owned_potions)
    return out.getvalue().rstrip("\n")


# ── Sections ────────────────────────────────────────────────────────────


def _write_header(out: StringIO, state: RunStateResult) -> None:
    hint = ""
    if state.is_game_over:
        hint = " — VICTORY" if state.is_victory else " — DEFEAT"
    out.write(
        f"Act {state.current_act_index} Floor {state.act_floor} — "
        f"{_room_label(state.current_room_type)}{hint}\n"
    )


def _write_vitals(out: StringIO, state: RunStateResult) -> None:
    out.write(f"HP {state.hp}/{state.max_hp} | Gold {state.gold} | Deck {state.deck_size}\n")


def _write_combat(out: StringIO, combat: CombatState) -> None:
    out.write(
        f"Round {combat.round} | Energy {combat.energy}/{combat.max_energy} | "
        f"Block {combat.player_block} | Draw {combat.draw_pile_count} | "
        f"Discard {combat.discard_pile_count}\n"
    )
    if combat.player_powers:
        out.write(f"Player powers: {_powers(combat.player_powers)}\n")
    out.write("\nHand:\n")
    if not combat.hand:
        out.write("  (empty)\n")
    else:
        for card in combat.hand:
            out.write(f"  {_card_line(card)}\n")
    out.write("\nEnemies:\n")
    if not combat.enemies:
        out.write("  (none)\n")
    else:
        for enemy in combat.enemies:
            out.write(f"  {_enemy_line(enemy)}\n")


def _write_phase_options(out: StringIO, state: RunStateResult) -> None:
    # Rewards may be available concurrently with a finished combat — show
    # them whenever they exist, regardless of room.
    if state.rewards_state is not None and state.rewards_state.available:
        out.write("\nRewards:\n")
        _write_rewards(out, state.rewards_state)
        return
    if state.combat_state is not None and state.combat_state.is_in_progress:
        # In-combat decisions are already covered by the hand + enemies
        # block; no extra options list to print.
        return
    if state.available_map_nodes:
        out.write("\nMap options:\n")
        for node in state.available_map_nodes:
            out.write(f"  {_map_node_line(node)}\n")
        return
    if state.available_event_options:
        out.write("\nEvent options:\n")
        for option in state.available_event_options:
            out.write(f"  {_event_option_line(option)}\n")
        return
    if state.available_rest_site_options:
        out.write("\nRest-site options:\n")
        for option in state.available_rest_site_options:
            out.write(f"  {_rest_option_line(option)}\n")
        return
    if state.available_merchant_items:
        out.write("\nMerchant inventory:\n")
        for item in state.available_merchant_items:
            out.write(f"  {_merchant_item_line(item)}\n")
        return
    if state.current_room_type is RoomType.treasure_room:
        if state.available_treasure_relics:
            offering = ", ".join(r.relic_id for r in state.available_treasure_relics)
            out.write(
                f"\nTreasure room — chest offering: {offering}.\n"
                f"  Call `run_take_treasure` to claim the offered relic, "
                f"or `run_skip_treasure` to walk past.\n"
            )
        else:
            out.write(
                "\nTreasure room: call `run_take_treasure` to open the chest "
                "(or `run_skip_treasure` to walk past).\n"
            )
        return
    if state.current_room_type is RoomType.merchant_room:
        out.write("\nMerchant room: nothing to buy; call `run_leave_merchant_room`.\n")
        return


def _write_rewards(out: StringIO, rewards: RewardsState) -> None:
    for reward in rewards.available:
        label = f"{reward.kind.value}"
        if reward.gold_amount is not None:
            label += f" ({reward.gold_amount} gold)"
        if reward.cards:
            label += ": " + ", ".join(c.id for c in reward.cards)
        if reward.relic_id is not None:
            label += f": {reward.relic_id}"
        if reward.potion_id is not None:
            label += f": {reward.potion_id}"
        skip = "" if reward.can_skip else " (cannot skip)"
        out.write(f"  [{reward.index}] {label}{skip}\n")


def _write_relics(out: StringIO, relics: Sequence[Relic]) -> None:
    if not relics:
        return
    ids = ", ".join(r.id for r in relics)
    out.write(f"\nRelics: {ids}\n")


def _write_potions(out: StringIO, potions: Sequence[OwnedPotion]) -> None:
    if not potions:
        return
    parts = [f"[{p.index}] {p.id}" for p in potions]
    out.write(f"Potions: {', '.join(parts)}\n")


# ── Line formatters ─────────────────────────────────────────────────────


def _room_label(room: RoomType) -> str:
    # Enum values like "CombatRoom"; the literal value is more informative
    # to the LLM than the snake-case Python identifier.
    return room.value


def _card_line(card: Card) -> str:
    pieces = [f"[{card.index}] {card.id} (cost {card.cost})"]
    pieces.append(f"→ {card.target_type.value}")
    if not card.can_play:
        pieces.append("(cannot play)")
    return " ".join(pieces)


def _enemy_line(enemy: Enemy) -> str:
    name = enemy.monster_id or "?"
    block = f" block {enemy.block}" if enemy.block else ""
    intent = _intent_summary(enemy.intents)
    powers = f" — powers: {_powers(enemy.powers)}" if enemy.powers else ""
    return f"[{enemy.index}] {name} HP {enemy.hp}/{enemy.max_hp}{block} — intends {intent}{powers}"


def _intent_summary(intents: Sequence[Intent]) -> str:
    if not intents:
        return "?"
    parts: list[str] = []
    for intent in intents:
        parts.append(_format_one_intent(intent))
    return ", ".join(parts)


def _format_one_intent(intent: Intent) -> str:
    if intent.kind in (IntentKind.attack, IntentKind.attack_buff, IntentKind.attack_debuff):
        dmg = intent.damage if intent.damage is not None else 0
        hits = intent.hits if intent.hits is not None else 1
        suffix = f" x{hits}" if hits > 1 else ""
        return f"{intent.kind.value} {dmg}{suffix}"
    if intent.kind is IntentKind.defend:
        block = intent.block if intent.block is not None else 0
        return f"Defend {block}"
    if intent.kind is IntentKind.attack_defend:
        dmg = intent.damage if intent.damage is not None else 0
        block = intent.block if intent.block is not None else 0
        return f"AttackDefend {dmg}/{block}"
    return intent.kind.value


def _powers(powers: Sequence[Power]) -> str:
    if not powers:
        return "(none)"
    return ", ".join(f"{p.id}({p.amount})" for p in powers)


def _map_node_line(node: MapNode) -> str:
    return f"col={node.col} row={node.row} type={node.type.value}"


def _event_option_line(option: EventOption) -> str:
    locked = " (locked)" if option.is_locked else ""
    text = option.text_key or "?"
    return f"[{option.index}] {text}{locked}"


def _rest_option_line(option: RestSiteOption) -> str:
    disabled = "" if option.is_enabled else " (disabled)"
    return f"[{option.index}] {option.option_id}{disabled}"


def _merchant_item_line(item: MerchantItem) -> str:
    label = item.card_id or item.relic_id or item.potion_id or "?"
    stocked = "" if item.is_stocked else " (out of stock)"
    affordable = "" if item.is_affordable else " (cannot afford)"
    return f"[{item.index}] {item.kind.value}: {label} — {item.cost} gold{stocked}{affordable}"


__all__ = ["summarize_run_state"]
