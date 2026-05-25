# pyright: reportUnusedFunction=false
#
# FastMCP's `@mcp.tool()` decorator registers each inner function as an MCP
# tool but does not bind its return value back to a name. Pyright sees a
# nested `def` whose result is discarded and flags it; the registration *is*
# the use. Scoped to this file because nowhere else in the project relies on
# the same pattern.

"""FastMCP server wrapping the headless-in-the-spire wire client.

Each tool maps 1:1 to a `MethodCatalog` entry on the C# side, plus one
convenience `summarize_state` for low-token state polls (see `summary.py`).
Debug tools are only registered when `enable_debug=True` (AD-7) — the same
flag is propagated to the spawned host, so the MCP catalogue and the host
gate move together.

Lifecycle
---------
A `_HostHandle` lazily spawns the host on the first tool call and keeps the
subprocess alive for the lifetime of the MCP server. Closing the FastMCP
server (e.g. the parent dies, stdin closes) triggers `_HostHandle.close()`
via FastMCP's lifespan callback.

Why not "one host per tool call"? AD-2 makes the host stateful (a run is in
progress; combat is on a particular floor) — every tool call has to land on
the same host or the protocol falls apart.

Why not "explicit start_host / stop_host tools"? An AI assistant that
forgets to call `start_host` would see every other tool fail; it's a state
machine the LLM does not need to track. `run_new` already resets the
*game* within the host, which covers every "I want a fresh start"
use case.
"""

import argparse
import contextlib
import logging
import os
import shutil
import sys
import threading
from collections.abc import AsyncGenerator
from contextlib import asynccontextmanager
from pathlib import Path
from typing import Any, Final

# No `from __future__ import annotations` here (and never in this workspace —
# see CLAUDE.md). FastMCP introspects function signatures at registration
# time via `typing.get_type_hints()`; stringified annotations would break the
# auto-generated input schemas for every tool below.
from mcp.server.fastmcp import FastMCP

from headless_in_the_spire import Client
from headless_in_the_spire._models import (
    CardSpec,
    Character,
    ContentDescribeActParams,
    ContentDescribeAfflictionParams,
    ContentDescribeCardParams,
    ContentDescribeEnchantmentParams,
    ContentDescribeEncounterParams,
    ContentDescribeEventParams,
    ContentDescribeModifierParams,
    ContentDescribeMonsterParams,
    ContentDescribePotionParams,
    ContentDescribePowerParams,
    ContentDescribeRelicParams,
    ContentListCardsParams,
    ContentListEncountersForActParams,
    ContentListEventsForActParams,
    ContentListPotionsParams,
    ContentListRelicsParams,
    ContentUnknownNodeOddsParams,
    DebugAfflictCardParams,
    DebugApplyPowerParams,
    DebugEnchantCardParams,
    DebugGainStarsParams,
    DebugGivePotionParams,
    DebugGiveRelicParams,
    DebugReplaceDeckParams,
    DebugSetEnergyParams,
    DebugSetHpParams,
    DebugStartCombatParams,
    DebugStartEventParams,
    RunBuyMerchantItemParams,
    RunNewParams,
    RunPlayCardParams,
    RunSelectEventOptionParams,
    RunSelectMapNodeParams,
    RunSelectRestSiteOptionParams,
    RunSelectRewardParams,
    RunSkipRewardParams,
    RunUsePotionParams,
)

# Path conventions copy `headless_in_the_spire.transport`; we don't import its
# private `_default_command` because the MCP server wants to *append*
# `--enable-debug`, and inlining the few-line builder is clearer than reaching
# through underscore-prefixed sibling internals.
_HOST_DEFAULT_PROJECT: Final[str] = "src/Sts2Headless/Sts2Headless.csproj"
_REPO_MARKER: Final[str] = "GAME_VERSION"

_log = logging.getLogger("headless_in_the_spire_mcp")


# ── Host handle ─────────────────────────────────────────────────────────


class _HostHandle:
    """Lazy singleton wrapping a `Client`. First tool call spawns the host;
    `close()` shuts it down. Thread-safe so concurrent `tools/call` requests
    can't double-spawn."""

    def __init__(self, *, enable_debug: bool, repo_root: Path) -> None:
        self._enable_debug = enable_debug
        self._repo_root = repo_root
        self._lock = threading.Lock()
        self._client: Client | None = None

    @property
    def enable_debug(self) -> bool:
        return self._enable_debug

    def client(self) -> Client:
        with self._lock:
            if self._client is None:
                cmd = _build_host_cmd(
                    enable_debug=self._enable_debug,
                    repo_root=self._repo_root,
                )
                _log.info("spawning host: %s", " ".join(cmd))
                self._client = Client.spawn(cmd=cmd, cwd=self._repo_root)
            return self._client

    def close(self) -> None:
        with self._lock:
            if self._client is not None:
                with contextlib.suppress(Exception):
                    self._client.close()
                self._client = None


def _build_host_cmd(*, enable_debug: bool, repo_root: Path) -> list[str]:
    explicit = os.environ.get("HEADLESS_IN_THE_SPIRE_HOST")
    if explicit:
        cmd = [explicit, "--stdio"]
    else:
        project = repo_root / _HOST_DEFAULT_PROJECT
        if not project.is_file():
            raise FileNotFoundError(
                f"could not find host project at {project}. Set "
                "HEADLESS_IN_THE_SPIRE_HOST to a prebuilt binary, or run "
                "the MCP server from inside the headless-in-the-spire repo."
            )
        dotnet = shutil.which("dotnet")
        if dotnet is None:
            raise FileNotFoundError(
                "`dotnet` not on PATH. Set HEADLESS_IN_THE_SPIRE_HOST to a "
                "prebuilt binary or install the .NET SDK."
            )
        cmd = [dotnet, "run", "--project", str(project), "--no-build", "--", "--stdio"]
    if enable_debug:
        cmd.append("--enable-debug")
    return cmd


def _locate_repo_root(start: Path | None = None) -> Path:
    base = (start or Path.cwd()).resolve()
    for p in [base, *base.parents]:
        if (p / _REPO_MARKER).is_file():
            return p
    raise FileNotFoundError(
        f"could not locate headless-in-the-spire repo (no {_REPO_MARKER} upwards from {base})"
    )


# ── Server construction ─────────────────────────────────────────────────


def build_server(
    *,
    enable_debug: bool = False,
    repo_root: Path | None = None,
) -> FastMCP:
    """Construct the FastMCP server.

    The host is *not* spawned here — it's lazy via `_HostHandle.client()` so
    `list_tools` works without touching `dotnet`. The first tool that actually
    calls the wire pays the spawn cost.
    """
    root = repo_root or _locate_repo_root()
    handle = _HostHandle(enable_debug=enable_debug, repo_root=root)

    @asynccontextmanager
    async def _lifespan(_server: FastMCP) -> AsyncGenerator[None]:
        try:
            yield
        finally:
            handle.close()

    mcp = FastMCP("headless-in-the-spire", lifespan=_lifespan)

    _register_core_tools(mcp, handle)
    if enable_debug:
        _register_debug_tools(mcp, handle)

    return mcp


def _register_core_tools(mcp: FastMCP, handle: _HostHandle) -> None:
    """Register one MCP tool per `MethodCatalog.Core` entry plus
    `summarize_state`. Bodies are wafer-thin so the wire client stays the
    single source of (de)serialisation."""

    @mcp.tool()
    def host_ping() -> dict[str, Any]:
        """Round-trip ping: confirm the host is alive and pinned. Returns
        ok, game_version, game_sha256.
        """
        return _dump(handle.client().host_ping())

    @mcp.tool()
    def host_methods() -> dict[str, Any]:
        """List every wire method this host exposes (name, summary,
        hasParams, isDebugOnly). Cheaper than parsing `protocol/openrpc.json`
        when a client just needs the method list. Debug-only entries are
        listed but only callable when the host was started with
        `--enable-debug`.
        """
        return _dump(handle.client().host_methods())

    @mcp.tool()
    def run_new(
        character: Character | None = None,
        seed: int | None = None,
        with_neow: bool | None = None,
        ascension: int | None = None,
        modifiers: list[str] | None = None,
    ) -> dict[str, Any]:
        """Start a new run. Resets the host's current run if one is active.
        Defaults: Ironclad, random seed, with the Neow blessing.
        """
        params = RunNewParams(
            character=character,
            seed=seed,
            with_neow=with_neow,
            ascension=ascension,
            modifiers=modifiers,
        )
        return _dump(handle.client().run_new(params))

    @mcp.tool()
    def run_state() -> dict[str, Any]:
        """Return the full current state of the active run as a JSON object.
        Prefer `summarize_state` for low-token polls during play.
        """
        return _dump(handle.client().run_state())

    @mcp.tool()
    def run_summarize_state() -> dict[str, Any]:
        """Same as `summarize_state` but returns the full
        `RunSummarizeStateResult` dict (ok + summary). Use when you need
        to confirm `ok` programmatically; otherwise prefer
        `summarize_state` for the bare text.
        """
        return _dump(handle.client().run_summarize_state())

    @mcp.tool()
    def summarize_state() -> str:
        """Compact, human-readable text summary of the current run state.
        Designed for AI assistants to poll cheaply between actions; use
        `run_state` when full structural data is needed. Delegates to
        the wire's `run/summarize_state` so every MCP client renders
        identical text (no client-side re-derivation).
        """
        return handle.client().run_summarize_state().summary

    @mcp.tool()
    def run_select_map_node(col: int, row: int) -> dict[str, Any]:
        """Travel to a map node by (col, row). Coordinates come from
        `available_map_nodes` in the current state.
        """
        return _dump(
            handle.client().run_select_map_node(
                RunSelectMapNodeParams(col=col, row=row),
            )
        )

    @mcp.tool()
    def run_select_event_option(option_index: int) -> dict[str, Any]:
        """Choose an event option by index from `available_event_options`."""
        return _dump(
            handle.client().run_select_event_option(
                RunSelectEventOptionParams(option_index=option_index),
            )
        )

    @mcp.tool()
    def run_select_rest_site_option(
        option_index: int,
        card_select_indices: list[list[int]] | None = None,
    ) -> dict[str, Any]:
        """Pick a rest-site option (rest / smith / lift / …) by index.
        `card_select_indices` carries follow-up card picks (e.g. which card
        to upgrade at the smith), one inner list per follow-up prompt.
        """
        return _dump(
            handle.client().run_select_rest_site_option(
                RunSelectRestSiteOptionParams(
                    option_index=option_index,
                    card_select_indices=card_select_indices,
                )
            )
        )

    @mcp.tool()
    def run_take_treasure() -> dict[str, Any]:
        """Open the chest in a TreasureRoom, grant the offered relic, and
        proceed back to the map. The offered relic is visible in
        `available_treasure_relics` before deciding.
        """
        return _dump(handle.client().run_take_treasure())

    @mcp.tool()
    def run_skip_treasure() -> dict[str, Any]:
        """Walk past a TreasureRoom chest without granting the offered
        relic, then proceed back to the map. Useful when the offering
        is undesirable.
        """
        return _dump(handle.client().run_skip_treasure())

    @mcp.tool()
    def run_buy_merchant_item(item_index: int) -> dict[str, Any]:
        """Buy a merchant inventory item by index from
        `available_merchant_items`."""
        return _dump(
            handle.client().run_buy_merchant_item(
                RunBuyMerchantItemParams(item_index=item_index),
            )
        )

    @mcp.tool()
    def run_leave_merchant_room() -> dict[str, Any]:
        """Leave the merchant and return to the map."""
        return _dump(handle.client().run_leave_merchant_room())

    @mcp.tool()
    def run_end_turn() -> dict[str, Any]:
        """End the player's combat turn. Enemies then resolve their intents."""
        return _dump(handle.client().run_end_turn())

    @mcp.tool()
    def run_play_card(
        card_index: int,
        target_index: int | None = None,
        card_select_indices: list[list[int]] | None = None,
    ) -> dict[str, Any]:
        """Play a card from hand. `card_index` indexes into
        `combat_state.hand`; `target_index` indexes into
        `combat_state.enemies` (omit for self-targeted or all-enemy cards).
        `card_select_indices` carries any in-card follow-up picks (e.g. a
        choose-from-discard prompt), one inner list per prompt.
        """
        return _dump(
            handle.client().run_play_card(
                RunPlayCardParams(
                    card_index=card_index,
                    target_index=target_index,
                    card_select_indices=card_select_indices,
                )
            )
        )

    @mcp.tool()
    def run_use_potion(
        potion_index: int,
        target_index: int | None = None,
    ) -> dict[str, Any]:
        """Drink a potion. `potion_index` indexes into `owned_potions`;
        `target_index` indexes into `combat_state.enemies` for enemy-targeted
        potions (omit otherwise).
        """
        return _dump(
            handle.client().run_use_potion(
                RunUsePotionParams(
                    potion_index=potion_index,
                    target_index=target_index,
                )
            )
        )

    @mcp.tool()
    def run_select_reward(
        reward_index: int,
        card_index: int | None = None,
    ) -> dict[str, Any]:
        """Claim a reward by index from `rewards_state.available`. For card
        rewards, also pass `card_index` to pick which card from the
        offered set.
        """
        return _dump(
            handle.client().run_select_reward(
                RunSelectRewardParams(
                    reward_index=reward_index,
                    card_index=card_index,
                )
            )
        )

    @mcp.tool()
    def run_skip_reward(reward_index: int) -> dict[str, Any]:
        """Skip a reward by index. Some rewards (gold, relics) cannot be
        skipped — check `can_skip` first.
        """
        return _dump(
            handle.client().run_skip_reward(
                RunSkipRewardParams(reward_index=reward_index),
            )
        )

    @mcp.tool()
    def run_enter_next_act() -> dict[str, Any]:
        """Advance from a finished act (post-boss MapRoom with no nodes left)
        into the next act.
        """
        return _dump(handle.client().run_enter_next_act())

    @mcp.tool()
    def run_proceed_event() -> dict[str, Any]:
        """Advance past an event whose options are exhausted (no transition
        back to MapRoom yet).
        """
        return _dump(handle.client().run_proceed_event())

    @mcp.tool()
    def run_history() -> dict[str, Any]:
        """Read the game's RunHistory JSON for the most recently ended run
        (AD-8). Available only when the host is recording
        (STS2_REPLAY_OUT) and the run has actually ended.
        """
        return _dump(handle.client().run_history())

    # ── content/* ────────────────────────────────────────────────────────

    @mcp.tool()
    def content_describe_card(card_id: str, upgrade_level: int | None = None) -> dict[str, Any]:
        """Describe a single card by its wire id (e.g. "BASH", "STRIKE_RED").
        Returns name, description, cost, rarity, character, target type.
        Static content — no run required.
        """
        return _dump(
            handle.client().content_describe_card(
                ContentDescribeCardParams(card_id=card_id, upgrade_level=upgrade_level),
            )
        )

    @mcp.tool()
    def content_describe_relic(relic_id: str) -> dict[str, Any]:
        """Describe a single relic by its wire id. Static content."""
        return _dump(
            handle.client().content_describe_relic(
                ContentDescribeRelicParams(relic_id=relic_id),
            )
        )

    @mcp.tool()
    def content_describe_potion(potion_id: str) -> dict[str, Any]:
        """Describe a single potion by its wire id. Static content."""
        return _dump(
            handle.client().content_describe_potion(
                ContentDescribePotionParams(potion_id=potion_id),
            )
        )

    @mcp.tool()
    def content_describe_power(power_id: str) -> dict[str, Any]:
        """Describe a single power (buff/debuff) by its wire id. Static content."""
        return _dump(
            handle.client().content_describe_power(
                ContentDescribePowerParams(power_id=power_id),
            )
        )

    @mcp.tool()
    def content_describe_event(event_id: str) -> dict[str, Any]:
        """Describe an event by its wire id. Static content; option branches
        roll seed-deterministically at choice time and aren't surfaced here.
        """
        return _dump(
            handle.client().content_describe_event(
                ContentDescribeEventParams(event_id=event_id),
            )
        )

    @mcp.tool()
    def content_describe_encounter(encounter_id: str) -> dict[str, Any]:
        """Describe a monster encounter (pack) by its wire id. Returns the
        pack composition; per-monster HP rolls happen at combat-start.
        """
        return _dump(
            handle.client().content_describe_encounter(
                ContentDescribeEncounterParams(encounter_id=encounter_id),
            )
        )

    @mcp.tool()
    def content_describe_monster(monster_id: str) -> dict[str, Any]:
        """Describe a single monster by its wire id."""
        return _dump(
            handle.client().content_describe_monster(
                ContentDescribeMonsterParams(monster_id=monster_id),
            )
        )

    @mcp.tool()
    def content_describe_affliction(affliction_id: str) -> dict[str, Any]:
        """Describe a card affliction by its wire id."""
        return _dump(
            handle.client().content_describe_affliction(
                ContentDescribeAfflictionParams(affliction_id=affliction_id),
            )
        )

    @mcp.tool()
    def content_describe_enchantment(enchantment_id: str) -> dict[str, Any]:
        """Describe a card enchantment by its wire id."""
        return _dump(
            handle.client().content_describe_enchantment(
                ContentDescribeEnchantmentParams(enchantment_id=enchantment_id),
            )
        )

    @mcp.tool()
    def content_describe_modifier(modifier_id: str) -> dict[str, Any]:
        """Describe a run modifier by its wire id."""
        return _dump(
            handle.client().content_describe_modifier(
                ContentDescribeModifierParams(modifier_id=modifier_id),
            )
        )

    @mcp.tool()
    def content_list_cards(
        character: str | None = None,
        rarity: str | None = None,
        include_colorless: bool | None = None,
    ) -> dict[str, Any]:
        """List all cards in the static pool, optionally filtered by character /
        rarity / colorless inclusion.
        """
        params = ContentListCardsParams(
            character=Character(character) if character else None,
            rarity=rarity,
            include_colorless=include_colorless,
        )
        return _dump(handle.client().content_list_cards(params))

    @mcp.tool()
    def content_list_relics(rarity: str | None = None) -> dict[str, Any]:
        """List all relics in the static pool, optionally filtered by rarity."""
        return _dump(handle.client().content_list_relics(ContentListRelicsParams(rarity=rarity)))

    @mcp.tool()
    def content_list_potions(rarity: str | None = None) -> dict[str, Any]:
        """List all potions in the static pool, optionally filtered by rarity."""
        return _dump(handle.client().content_list_potions(ContentListPotionsParams(rarity=rarity)))

    @mcp.tool()
    def content_describe_act(act_index: int) -> dict[str, Any]:
        """Per-act content pools (weak, regular, elite, boss, event) and
        structural counts. Static content — the pool, not the rolled answer.
        For this run's specific schedule see debug/reveal_act_schedule.
        """
        return _dump(
            handle.client().content_describe_act(
                ContentDescribeActParams(act_index=act_index),
            )
        )

    @mcp.tool()
    def content_encounter_rules() -> dict[str, Any]:
        """Static rules describing how the engine builds an act's encounter
        schedule (weak-first, elite roll count, no-adjacent-shared-tags).
        """
        return _dump(handle.client().content_encounter_rules())

    @mcp.tool()
    def content_unknown_node_odds(act_index: int | None = None) -> dict[str, Any]:
        """Base odds distribution for `?` map nodes. Prior; actual resolved
        room is rolled at entry (debug/peek_unknown_resolution, gated).
        """
        return _dump(
            handle.client().content_unknown_node_odds(
                ContentUnknownNodeOddsParams(act_index=act_index),
            )
        )

    @mcp.tool()
    def content_list_events_for_act(act_index: int) -> dict[str, Any]:
        """Per-act event pool (events draftable in this act, union of the
        act's AllEvents and ModelDb.AllSharedEvents). Static content;
        for the rolled sequence see debug/reveal_act_schedule.
        """
        return _dump(
            handle.client().content_list_events_for_act(
                ContentListEventsForActParams(act_index=act_index),
            )
        )

    @mcp.tool()
    def content_list_encounters_for_act(
        act_index: int,
        tier: str | None = None,
    ) -> dict[str, Any]:
        """Per-act encounter pool, optionally filtered to one tier
        ("weak" | "normal" | "elite" | "boss"). Static content; for the
        rolled sequence see debug/reveal_act_schedule.
        """
        from headless_in_the_spire._models import EncounterTier

        params = ContentListEncountersForActParams(
            act_index=act_index,
            tier=EncounterTier(tier) if tier else None,
        )
        return _dump(handle.client().content_list_encounters_for_act(params))


def _register_debug_tools(mcp: FastMCP, handle: _HostHandle) -> None:
    """Debug methods (AD-7). Only registered when `enable_debug=True`. The
    host enforces the gate independently — these are *load-bearing for
    tests*, never for production."""

    @mcp.tool()
    def debug_give_relic(relic_id: str) -> dict[str, Any]:
        """Grant a relic without triggering its in-game source event. Debug
        only; corrupts replay fidelity.
        """
        return _dump(
            handle.client().debug_give_relic(
                DebugGiveRelicParams(relic_id=relic_id),
            )
        )

    @mcp.tool()
    def debug_give_potion(potion_id: str) -> dict[str, Any]:
        """Grant a potion by wire id. Debug only; corrupts replay fidelity."""
        return _dump(
            handle.client().debug_give_potion(
                DebugGivePotionParams(potion_id=potion_id),
            )
        )

    @mcp.tool()
    def debug_start_event(event_id: str) -> dict[str, Any]:
        """Force-start a specific event by wire id. Debug only; corrupts
        replay fidelity.
        """
        return _dump(
            handle.client().debug_start_event(
                DebugStartEventParams(event_id=event_id),
            )
        )

    @mcp.tool()
    def debug_apply_power(
        power_id: str,
        amount: int | None = 1,
        enemy_index: int | None = None,
    ) -> dict[str, Any]:
        """Apply a power to the player, or to an enemy when `enemy_index` is
        supplied. Debug only; corrupts replay fidelity.
        """
        return _dump(
            handle.client().debug_apply_power(
                DebugApplyPowerParams(
                    power_id=power_id,
                    amount=amount,
                    enemy_index=enemy_index,
                ),
            )
        )

    @mcp.tool()
    def debug_afflict_card(
        affliction_id: str,
        hand_index: int | None = 0,
        amount: int | None = 1,
    ) -> dict[str, Any]:
        """Attach an affliction to a hand card. Debug only; corrupts replay
        fidelity.
        """
        return _dump(
            handle.client().debug_afflict_card(
                DebugAfflictCardParams(
                    affliction_id=affliction_id,
                    hand_index=hand_index,
                    amount=amount,
                ),
            )
        )

    @mcp.tool()
    def debug_enchant_card(
        enchantment_id: str,
        hand_index: int | None = 0,
        amount: int | None = 1,
    ) -> dict[str, Any]:
        """Attach an enchantment to a hand card. Debug only; corrupts replay
        fidelity.
        """
        return _dump(
            handle.client().debug_enchant_card(
                DebugEnchantCardParams(
                    enchantment_id=enchantment_id,
                    hand_index=hand_index,
                    amount=amount,
                ),
            )
        )

    @mcp.tool()
    def debug_set_hp(hp: int, max_hp: int | None = None) -> dict[str, Any]:
        """Set the player's HP (and optionally max HP) directly. Debug only;
        corrupts replay fidelity.
        """
        return _dump(
            handle.client().debug_set_hp(
                DebugSetHpParams(hp=hp, max_hp=max_hp),
            )
        )

    @mcp.tool()
    def debug_replace_deck(cards: list[dict[str, Any]]) -> dict[str, Any]:
        """Replace the deck with the given list of cards. Each card is a
        `{"cardId": str, "upgradeLevel": int}` object. Debug only; corrupts
        replay fidelity.
        """
        specs = [CardSpec.model_validate(c) for c in cards]
        return _dump(
            handle.client().debug_replace_deck(
                DebugReplaceDeckParams(cards=specs),
            )
        )

    @mcp.tool()
    def debug_read_deck() -> dict[str, Any]:
        """Inspect the current deck contents (read-only). Available only with
        `--enable-debug` because the surface is intended for test-time
        introspection.
        """
        return _dump(handle.client().debug_read_deck())

    @mcp.tool()
    def debug_start_combat(encounter_id: str) -> dict[str, Any]:
        """Force-start a specific combat against the chosen encounter,
        bypassing map progression. `encounter_id` is the wire string id
        (matches `EncounterId`, e.g. `"SLIMES_NORMAL"`). Debug only;
        corrupts replay fidelity.
        """
        return _dump(
            handle.client().debug_start_combat(
                DebugStartCombatParams(encounter_id=encounter_id),
            )
        )

    @mcp.tool()
    def debug_kill_all_enemies() -> dict[str, Any]:
        """Instantly kill every enemy in the current combat. Debug only;
        corrupts replay fidelity.
        """
        return _dump(handle.client().debug_kill_all_enemies())

    @mcp.tool()
    def debug_set_energy(
        energy: int | None = None,
        max_energy: int | None = None,
    ) -> dict[str, Any]:
        """Set the player's current Energy and/or per-run MaxEnergy cap
        directly. Requires an active combat. Debug only.
        """
        return _dump(
            handle.client().debug_set_energy(
                DebugSetEnergyParams(energy=energy, max_energy=max_energy),
            )
        )

    @mcp.tool()
    def debug_gain_stars(amount: int) -> dict[str, Any]:
        """Grant N Stars (Regent's per-combat resource). Requires an active
        combat. Debug only.
        """
        return _dump(handle.client().debug_gain_stars(DebugGainStarsParams(amount=amount)))

    @mcp.tool()
    def debug_reveal_act_schedule() -> dict[str, Any]:
        """Reveal the current act's pre-rolled schedule (boss / second boss /
        ancient / normal-encounter list / elite-encounter list / event list).
        Seed-deterministic info the engine normally hides; combined with the
        visited counters this answers "what comes next" exactly. Debug only.
        """
        return _dump(handle.client().debug_reveal_act_schedule())

    @mcp.tool()
    def debug_reveal_map_layout() -> dict[str, Any]:
        """Reveal the full pre-rolled map layout for the current act
        (every (col, row, type) point + outgoing edges). Unknown nodes
        stay Unknown — their runtime room is rolled lazily on first
        entry via UnknownMapPointOdds.Roll which needs visit history.
        Seed-deterministic info normally hidden from the player. Debug
        only.
        """
        return _dump(handle.client().debug_reveal_map_layout())

    @mcp.tool()
    def debug_peek_card_reward(encounter_id: str | None = None) -> dict[str, Any]:
        """Peek at the post-combat card-reward *pool* (not the rolled
        triplet) without committing state. Returns every card the engine
        would consider, keyed off (player, room=CombatRoom). Debug only.
        """
        from headless_in_the_spire._models import DebugPeekCardRewardParams

        return _dump(
            handle.client().debug_peek_card_reward(
                DebugPeekCardRewardParams(encounter_id=encounter_id),
            )
        )

    @mcp.tool()
    def debug_peek_event_outcome(event_id: str, option_index: int) -> dict[str, Any]:
        """Peek at the outcome of choosing an event option. Currently
        scoped to existence-check + diagnostic notes (clone-and-forward
        infrastructure not implemented yet). Returns ok=false with a
        notes string describing the limitation. Debug only.
        """
        from headless_in_the_spire._models import DebugPeekEventOutcomeParams

        return _dump(
            handle.client().debug_peek_event_outcome(
                DebugPeekEventOutcomeParams(event_id=event_id, option_index=option_index),
            )
        )


# ── Helpers ─────────────────────────────────────────────────────────────


def _dump(model: Any) -> dict[str, Any]:
    """Serialise a pydantic result model to a wire-shape dict. The wire
    client returns pydantic models; MCP tools must return JSON-compatible
    dicts (or other structured-content types). `by_alias=True` keeps wire
    naming (camelCase) so the MCP output matches what `protocol/openrpc.json`
    documents — important for clients that already consult that schema.
    """
    return model.model_dump(mode="json", by_alias=True)


# ── CLI ─────────────────────────────────────────────────────────────────


def main(argv: list[str] | None = None) -> None:
    parser = argparse.ArgumentParser(
        prog="headless-in-the-spire-mcp",
        description=(
            "MCP server bridging AI assistants to the headless-in-the-spire "
            "Slay the Spire 2 runner."
        ),
    )
    parser.add_argument(
        "--enable-debug",
        action="store_true",
        help=(
            "Register debug/* tools and pass --enable-debug to the spawned "
            "host (AD-7). Never use in production — debug calls corrupt "
            "replay fidelity."
        ),
    )
    parser.add_argument(
        "--log-level",
        default=os.environ.get("HEADLESS_IN_THE_SPIRE_MCP_LOG_LEVEL", "INFO"),
        help="Logging level (default: INFO). Logs go to stderr only.",
    )
    args = parser.parse_args(argv)

    # MCP servers MUST write only JSON-RPC envelopes to stdout. Send logs to
    # stderr; the MCP host typically pipes them through to the user.
    logging.basicConfig(level=args.log_level.upper(), stream=sys.stderr)

    if args.enable_debug:
        _log.warning(
            "headless-in-the-spire-mcp: debug methods ENABLED via "
            "--enable-debug (development/test only — never use in production)"
        )

    server = build_server(enable_debug=args.enable_debug)
    server.run()


if __name__ == "__main__":
    main()
