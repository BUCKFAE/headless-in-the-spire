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
    DebugGiveRelicParams,
    DebugReplaceDeckParams,
    DebugSetHpParams,
    DebugStartCombatParams,
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
from headless_in_the_spire_mcp.summary import summarize_run_state

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
    def summarize_state() -> str:
        """Compact, human-readable text summary of the current run state.
        Designed for AI assistants to poll cheaply between actions; use
        `run_state` when full structural data is needed.
        """
        return summarize_run_state(handle.client().run_state())

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
    def run_leave_treasure_room() -> dict[str, Any]:
        """Open the chest in a TreasureRoom and proceed."""
        return _dump(handle.client().run_leave_treasure_room())

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
