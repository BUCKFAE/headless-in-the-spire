"""Typed `Client` over the subprocess transport.

One method per `MethodCatalog` entry on the C# side. Wire names map to
Python identifiers via a pinned table (`METHOD_NAMES`) so a future codegen
behavioural change can't silently rename the public API; if `MethodCatalog`
gains an entry, an integration test will fail until this table is updated.

DTOs in `_models` are generated; everything in this module is hand-rolled.
"""

from collections.abc import Mapping
from types import TracebackType
from typing import Any, Self

from pydantic import BaseModel

from headless_in_the_spire import _models as m
from headless_in_the_spire.transport import Transport

# Wire-name ↔ Python-identifier mapping. Mirrors MethodCatalog on the C#
# side. Adding a method here is a deliberate review step.
METHOD_NAMES: dict[str, str] = {
    "host/ping": "host_ping",
    "host/methods": "host_methods",
    "run/new": "run_new",
    "run/state": "run_state",
    "run/summarize_state": "run_summarize_state",
    "run/select_map_node": "run_select_map_node",
    "run/select_event_option": "run_select_event_option",
    "run/select_rest_site_option": "run_select_rest_site_option",
    "run/take_treasure": "run_take_treasure",
    "run/skip_treasure": "run_skip_treasure",
    "run/buy_merchant_item": "run_buy_merchant_item",
    "run/leave_merchant_room": "run_leave_merchant_room",
    "run/end_turn": "run_end_turn",
    "run/play_card": "run_play_card",
    "run/use_potion": "run_use_potion",
    "run/select_reward": "run_select_reward",
    "run/skip_reward": "run_skip_reward",
    "run/enter_next_act": "run_enter_next_act",
    "run/proceed_event": "run_proceed_event",
    "run/history": "run_history",
    "content/describe_card": "content_describe_card",
    "content/describe_relic": "content_describe_relic",
    "content/describe_potion": "content_describe_potion",
    "content/describe_power": "content_describe_power",
    "content/describe_event": "content_describe_event",
    "content/describe_encounter": "content_describe_encounter",
    "content/describe_monster": "content_describe_monster",
    "content/describe_affliction": "content_describe_affliction",
    "content/describe_enchantment": "content_describe_enchantment",
    "content/describe_modifier": "content_describe_modifier",
    "content/list_cards": "content_list_cards",
    "content/list_relics": "content_list_relics",
    "content/list_potions": "content_list_potions",
    "content/describe_act": "content_describe_act",
    "content/encounter_rules": "content_encounter_rules",
    "content/unknown_node_odds": "content_unknown_node_odds",
    "debug/gain_stars": "debug_gain_stars",
    "debug/give_relic": "debug_give_relic",
    "debug/reveal_act_schedule": "debug_reveal_act_schedule",
    "debug/set_energy": "debug_set_energy",
    "debug/give_potion": "debug_give_potion",
    "debug/start_event": "debug_start_event",
    "debug/apply_power": "debug_apply_power",
    "debug/afflict_card": "debug_afflict_card",
    "debug/enchant_card": "debug_enchant_card",
    "debug/set_hp": "debug_set_hp",
    "debug/replace_deck": "debug_replace_deck",
    "debug/read_deck": "debug_read_deck",
    "debug/start_combat": "debug_start_combat",
    "debug/kill_all_enemies": "debug_kill_all_enemies",
}


def _dump(params: BaseModel | None) -> Mapping[str, Any] | None:
    """Serialise a pydantic params model to wire-shape dict (camelCase
    aliases, drop unset fields)."""
    if params is None:
        return None
    return params.model_dump(by_alias=True, exclude_unset=True, mode="json")


class Client:
    """Thin typed wrapper around a `Transport`.

    Acquire via:

        with Client.spawn() as c:
            c.host_ping()
    """

    def __init__(self, transport: Transport) -> None:
        self._transport = transport

    @classmethod
    def spawn(cls, **kwargs: Any) -> Self:
        """Spawn the host subprocess and wrap it in a Client. `kwargs` are
        forwarded to `Transport.spawn` (e.g. `cwd=`, `cmd=`)."""
        return cls(Transport.spawn(**kwargs))

    @property
    def transport(self) -> Transport:
        return self._transport

    def close(self) -> int | None:
        return self._transport.close()

    def __enter__(self) -> Self:
        return self

    def __exit__(
        self,
        exc_type: type[BaseException] | None,
        exc: BaseException | None,
        tb: TracebackType | None,
    ) -> None:
        self.close()

    # ── Generated-style dispatch helpers ──────────────────────────────────
    # The bodies are hand-written. Generating them is a follow-up; with 10
    # methods, the duplication is cheap and the explicit (de)serialisation
    # is easier to grep than a metaclass.

    def host_ping(self, *, timeout: float | None = None) -> m.HostPingResult:
        result = self._transport.call("host/ping", None, timeout=timeout)
        return m.HostPingResult.model_validate(result)

    def host_methods(self, *, timeout: float | None = None) -> m.HostMethodsResult:
        result = self._transport.call("host/methods", None, timeout=timeout)
        return m.HostMethodsResult.model_validate(result)

    def run_new(
        self,
        params: m.RunNewParams | None = None,
        *,
        timeout: float | None = None,
    ) -> m.RunNewResult:
        result = self._transport.call("run/new", _dump(params), timeout=timeout)
        return m.RunNewResult.model_validate(result)

    def run_state(self, *, timeout: float | None = None) -> m.RunStateResult:
        result = self._transport.call("run/state", None, timeout=timeout)
        return m.RunStateResult.model_validate(result)

    def run_summarize_state(self, *, timeout: float | None = None) -> m.RunSummarizeStateResult:
        result = self._transport.call("run/summarize_state", None, timeout=timeout)
        return m.RunSummarizeStateResult.model_validate(result)

    def run_select_map_node(
        self,
        params: m.RunSelectMapNodeParams,
        *,
        timeout: float | None = None,
    ) -> m.RunSelectMapNodeResult:
        result = self._transport.call("run/select_map_node", _dump(params), timeout=timeout)
        return m.RunSelectMapNodeResult.model_validate(result)

    def run_select_event_option(
        self,
        params: m.RunSelectEventOptionParams,
        *,
        timeout: float | None = None,
    ) -> m.RunSelectEventOptionResult:
        result = self._transport.call(
            "run/select_event_option",
            _dump(params),
            timeout=timeout,
        )
        return m.RunSelectEventOptionResult.model_validate(result)

    def run_select_rest_site_option(
        self,
        params: m.RunSelectRestSiteOptionParams,
        *,
        timeout: float | None = None,
    ) -> m.RunSelectRestSiteOptionResult:
        result = self._transport.call(
            "run/select_rest_site_option",
            _dump(params),
            timeout=timeout,
        )
        return m.RunSelectRestSiteOptionResult.model_validate(result)

    def run_take_treasure(self, *, timeout: float | None = None) -> m.RunTakeTreasureResult:
        result = self._transport.call("run/take_treasure", None, timeout=timeout)
        return m.RunTakeTreasureResult.model_validate(result)

    def run_skip_treasure(self, *, timeout: float | None = None) -> m.RunSkipTreasureResult:
        result = self._transport.call("run/skip_treasure", None, timeout=timeout)
        return m.RunSkipTreasureResult.model_validate(result)

    def run_buy_merchant_item(
        self,
        params: m.RunBuyMerchantItemParams,
        *,
        timeout: float | None = None,
    ) -> m.RunBuyMerchantItemResult:
        result = self._transport.call(
            "run/buy_merchant_item",
            _dump(params),
            timeout=timeout,
        )
        return m.RunBuyMerchantItemResult.model_validate(result)

    def run_leave_merchant_room(
        self, *, timeout: float | None = None
    ) -> m.RunLeaveMerchantRoomResult:
        result = self._transport.call("run/leave_merchant_room", None, timeout=timeout)
        return m.RunLeaveMerchantRoomResult.model_validate(result)

    def run_end_turn(self, *, timeout: float | None = None) -> m.RunEndTurnResult:
        result = self._transport.call("run/end_turn", None, timeout=timeout)
        return m.RunEndTurnResult.model_validate(result)

    def run_play_card(
        self,
        params: m.RunPlayCardParams,
        *,
        timeout: float | None = None,
    ) -> m.RunPlayCardResult:
        result = self._transport.call("run/play_card", _dump(params), timeout=timeout)
        return m.RunPlayCardResult.model_validate(result)

    def run_use_potion(
        self,
        params: m.RunUsePotionParams,
        *,
        timeout: float | None = None,
    ) -> m.RunUsePotionResult:
        result = self._transport.call("run/use_potion", _dump(params), timeout=timeout)
        return m.RunUsePotionResult.model_validate(result)

    def run_select_reward(
        self,
        params: m.RunSelectRewardParams,
        *,
        timeout: float | None = None,
    ) -> m.RunSelectRewardResult:
        result = self._transport.call("run/select_reward", _dump(params), timeout=timeout)
        return m.RunSelectRewardResult.model_validate(result)

    def run_skip_reward(
        self,
        params: m.RunSkipRewardParams,
        *,
        timeout: float | None = None,
    ) -> m.RunSkipRewardResult:
        result = self._transport.call("run/skip_reward", _dump(params), timeout=timeout)
        return m.RunSkipRewardResult.model_validate(result)

    def run_enter_next_act(
        self,
        *,
        timeout: float | None = None,
    ) -> m.RunEnterNextActResult:
        result = self._transport.call("run/enter_next_act", None, timeout=timeout)
        return m.RunEnterNextActResult.model_validate(result)

    def run_proceed_event(
        self,
        *,
        timeout: float | None = None,
    ) -> m.RunProceedEventResult:
        result = self._transport.call("run/proceed_event", None, timeout=timeout)
        return m.RunProceedEventResult.model_validate(result)

    def run_history(self, *, timeout: float | None = None) -> m.RunHistoryDocument:
        """Read the game's RunHistory for the most recently ended run (AD-8).

        Available only when the host is recording (STS2_REPLAY_OUT) and the
        run has actually ended. Mirrors the game's own snake_case shape.
        """
        result = self._transport.call("run/history", None, timeout=timeout)
        return m.RunHistoryDocument.model_validate(result)

    def debug_give_relic(
        self,
        params: m.DebugGiveRelicParams,
        *,
        timeout: float | None = None,
    ) -> m.DebugGiveRelicResult:
        result = self._transport.call("debug/give_relic", _dump(params), timeout=timeout)
        return m.DebugGiveRelicResult.model_validate(result)

    def debug_give_potion(
        self,
        params: m.DebugGivePotionParams,
        *,
        timeout: float | None = None,
    ) -> m.DebugGivePotionResult:
        result = self._transport.call("debug/give_potion", _dump(params), timeout=timeout)
        return m.DebugGivePotionResult.model_validate(result)

    def debug_start_event(
        self,
        params: m.DebugStartEventParams,
        *,
        timeout: float | None = None,
    ) -> m.DebugStartEventResult:
        result = self._transport.call("debug/start_event", _dump(params), timeout=timeout)
        return m.DebugStartEventResult.model_validate(result)

    def debug_apply_power(
        self,
        params: m.DebugApplyPowerParams,
        *,
        timeout: float | None = None,
    ) -> m.DebugApplyPowerResult:
        result = self._transport.call("debug/apply_power", _dump(params), timeout=timeout)
        return m.DebugApplyPowerResult.model_validate(result)

    def debug_afflict_card(
        self,
        params: m.DebugAfflictCardParams,
        *,
        timeout: float | None = None,
    ) -> m.DebugAfflictCardResult:
        result = self._transport.call("debug/afflict_card", _dump(params), timeout=timeout)
        return m.DebugAfflictCardResult.model_validate(result)

    def debug_enchant_card(
        self,
        params: m.DebugEnchantCardParams,
        *,
        timeout: float | None = None,
    ) -> m.DebugEnchantCardResult:
        result = self._transport.call("debug/enchant_card", _dump(params), timeout=timeout)
        return m.DebugEnchantCardResult.model_validate(result)

    def debug_set_hp(
        self,
        params: m.DebugSetHpParams,
        *,
        timeout: float | None = None,
    ) -> m.DebugSetHpResult:
        result = self._transport.call("debug/set_hp", _dump(params), timeout=timeout)
        return m.DebugSetHpResult.model_validate(result)

    def debug_replace_deck(
        self,
        params: m.DebugReplaceDeckParams,
        *,
        timeout: float | None = None,
    ) -> m.DebugReplaceDeckResult:
        result = self._transport.call("debug/replace_deck", _dump(params), timeout=timeout)
        return m.DebugReplaceDeckResult.model_validate(result)

    def debug_read_deck(
        self,
        *,
        timeout: float | None = None,
    ) -> m.DebugReadDeckResult:
        result = self._transport.call("debug/read_deck", None, timeout=timeout)
        return m.DebugReadDeckResult.model_validate(result)

    def debug_start_combat(
        self,
        params: m.DebugStartCombatParams,
        *,
        timeout: float | None = None,
    ) -> m.DebugStartCombatResult:
        result = self._transport.call("debug/start_combat", _dump(params), timeout=timeout)
        return m.DebugStartCombatResult.model_validate(result)

    def debug_kill_all_enemies(
        self,
        *,
        timeout: float | None = None,
    ) -> m.DebugKillAllEnemiesResult:
        result = self._transport.call("debug/kill_all_enemies", None, timeout=timeout)
        return m.DebugKillAllEnemiesResult.model_validate(result)

    def debug_set_energy(
        self,
        params: m.DebugSetEnergyParams,
        *,
        timeout: float | None = None,
    ) -> m.DebugSetEnergyResult:
        result = self._transport.call("debug/set_energy", _dump(params), timeout=timeout)
        return m.DebugSetEnergyResult.model_validate(result)

    def debug_gain_stars(
        self,
        params: m.DebugGainStarsParams,
        *,
        timeout: float | None = None,
    ) -> m.DebugGainStarsResult:
        result = self._transport.call("debug/gain_stars", _dump(params), timeout=timeout)
        return m.DebugGainStarsResult.model_validate(result)

    def debug_reveal_act_schedule(
        self,
        *,
        timeout: float | None = None,
    ) -> m.DebugRevealActScheduleResult:
        result = self._transport.call("debug/reveal_act_schedule", None, timeout=timeout)
        return m.DebugRevealActScheduleResult.model_validate(result)

    # ── content/* ─────────────────────────────────────────────────────────

    def content_describe_card(
        self,
        params: m.ContentDescribeCardParams,
        *,
        timeout: float | None = None,
    ) -> m.ContentDescribeCardResult:
        result = self._transport.call("content/describe_card", _dump(params), timeout=timeout)
        return m.ContentDescribeCardResult.model_validate(result)

    def content_describe_relic(
        self,
        params: m.ContentDescribeRelicParams,
        *,
        timeout: float | None = None,
    ) -> m.ContentDescribeRelicResult:
        result = self._transport.call("content/describe_relic", _dump(params), timeout=timeout)
        return m.ContentDescribeRelicResult.model_validate(result)

    def content_describe_potion(
        self,
        params: m.ContentDescribePotionParams,
        *,
        timeout: float | None = None,
    ) -> m.ContentDescribePotionResult:
        result = self._transport.call("content/describe_potion", _dump(params), timeout=timeout)
        return m.ContentDescribePotionResult.model_validate(result)

    def content_describe_power(
        self,
        params: m.ContentDescribePowerParams,
        *,
        timeout: float | None = None,
    ) -> m.ContentDescribePowerResult:
        result = self._transport.call("content/describe_power", _dump(params), timeout=timeout)
        return m.ContentDescribePowerResult.model_validate(result)

    def content_describe_event(
        self,
        params: m.ContentDescribeEventParams,
        *,
        timeout: float | None = None,
    ) -> m.ContentDescribeEventResult:
        result = self._transport.call("content/describe_event", _dump(params), timeout=timeout)
        return m.ContentDescribeEventResult.model_validate(result)

    def content_describe_encounter(
        self,
        params: m.ContentDescribeEncounterParams,
        *,
        timeout: float | None = None,
    ) -> m.ContentDescribeEncounterResult:
        result = self._transport.call("content/describe_encounter", _dump(params), timeout=timeout)
        return m.ContentDescribeEncounterResult.model_validate(result)

    def content_describe_monster(
        self,
        params: m.ContentDescribeMonsterParams,
        *,
        timeout: float | None = None,
    ) -> m.ContentDescribeMonsterResult:
        result = self._transport.call("content/describe_monster", _dump(params), timeout=timeout)
        return m.ContentDescribeMonsterResult.model_validate(result)

    def content_describe_affliction(
        self,
        params: m.ContentDescribeAfflictionParams,
        *,
        timeout: float | None = None,
    ) -> m.ContentDescribeAfflictionResult:
        result = self._transport.call("content/describe_affliction", _dump(params), timeout=timeout)
        return m.ContentDescribeAfflictionResult.model_validate(result)

    def content_describe_enchantment(
        self,
        params: m.ContentDescribeEnchantmentParams,
        *,
        timeout: float | None = None,
    ) -> m.ContentDescribeEnchantmentResult:
        result = self._transport.call(
            "content/describe_enchantment", _dump(params), timeout=timeout
        )
        return m.ContentDescribeEnchantmentResult.model_validate(result)

    def content_describe_modifier(
        self,
        params: m.ContentDescribeModifierParams,
        *,
        timeout: float | None = None,
    ) -> m.ContentDescribeModifierResult:
        result = self._transport.call("content/describe_modifier", _dump(params), timeout=timeout)
        return m.ContentDescribeModifierResult.model_validate(result)

    def content_list_cards(
        self,
        params: m.ContentListCardsParams | None = None,
        *,
        timeout: float | None = None,
    ) -> m.ContentListCardsResult:
        result = self._transport.call("content/list_cards", _dump(params), timeout=timeout)
        return m.ContentListCardsResult.model_validate(result)

    def content_list_relics(
        self,
        params: m.ContentListRelicsParams | None = None,
        *,
        timeout: float | None = None,
    ) -> m.ContentListRelicsResult:
        result = self._transport.call("content/list_relics", _dump(params), timeout=timeout)
        return m.ContentListRelicsResult.model_validate(result)

    def content_list_potions(
        self,
        params: m.ContentListPotionsParams | None = None,
        *,
        timeout: float | None = None,
    ) -> m.ContentListPotionsResult:
        result = self._transport.call("content/list_potions", _dump(params), timeout=timeout)
        return m.ContentListPotionsResult.model_validate(result)

    def content_describe_act(
        self,
        params: m.ContentDescribeActParams,
        *,
        timeout: float | None = None,
    ) -> m.ContentDescribeActResult:
        result = self._transport.call("content/describe_act", _dump(params), timeout=timeout)
        return m.ContentDescribeActResult.model_validate(result)

    def content_encounter_rules(
        self,
        *,
        timeout: float | None = None,
    ) -> m.ContentEncounterRulesResult:
        result = self._transport.call("content/encounter_rules", None, timeout=timeout)
        return m.ContentEncounterRulesResult.model_validate(result)

    def content_unknown_node_odds(
        self,
        params: m.ContentUnknownNodeOddsParams | None = None,
        *,
        timeout: float | None = None,
    ) -> m.ContentUnknownNodeOddsResult:
        result = self._transport.call("content/unknown_node_odds", _dump(params), timeout=timeout)
        return m.ContentUnknownNodeOddsResult.model_validate(result)
