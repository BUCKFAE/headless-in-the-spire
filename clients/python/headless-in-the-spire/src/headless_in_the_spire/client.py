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
    "run/new": "run_new",
    "run/state": "run_state",
    "run/select_map_node": "run_select_map_node",
    "run/select_event_option": "run_select_event_option",
    "run/select_rest_site_option": "run_select_rest_site_option",
    "run/leave_treasure_room": "run_leave_treasure_room",
    "run/buy_merchant_item": "run_buy_merchant_item",
    "run/leave_merchant_room": "run_leave_merchant_room",
    "run/end_turn": "run_end_turn",
    "run/play_card": "run_play_card",
    "run/use_potion": "run_use_potion",
    "run/select_reward": "run_select_reward",
    "run/skip_reward": "run_skip_reward",
    "run/enter_next_act": "run_enter_next_act",
    "run/proceed_event": "run_proceed_event",
    "debug/give_relic": "debug_give_relic",
    "debug/set_hp": "debug_set_hp",
    "debug/replace_deck": "debug_replace_deck",
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

    def run_leave_treasure_room(
        self, *, timeout: float | None = None
    ) -> m.RunLeaveTreasureRoomResult:
        result = self._transport.call("run/leave_treasure_room", None, timeout=timeout)
        return m.RunLeaveTreasureRoomResult.model_validate(result)

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

    def debug_give_relic(
        self,
        params: m.DebugGiveRelicParams,
        *,
        timeout: float | None = None,
    ) -> m.DebugGiveRelicResult:
        result = self._transport.call("debug/give_relic", _dump(params), timeout=timeout)
        return m.DebugGiveRelicResult.model_validate(result)

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
