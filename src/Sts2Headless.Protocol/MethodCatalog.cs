using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Protocol;

// Single source of truth for the wire's *core* method catalogue (AD-5).
// Cheat/debug-only entries live in Sts2Headless.Cheats.CheatMethodCatalog
// and are merged in at host startup + schema export time. The merge keeps
// AssertParity authoritative over the union while Protocol stays cheat-free
// so the Agents project (which only references Protocol) can't see them.
//
// Both the host dispatch table (HostMethods.Build) and the schema emitter
// (Sts2Headless.SchemaExport) consume MethodCatalog.Core ∪ CheatMethodCatalog.All.
// HostMethods asserts via AssertParity at startup, so a method added to a
// catalogue without a handler — or a handler without an entry — fails fast
// rather than silently drifting the wire from the schema.
//
// Adding a core method: append a MethodEntry to Core. Adding a cheat method:
// append to CheatMethodCatalog.All instead.

// IsDebugOnly marks the method as a test affordance that must never be
// served in production. The host's --enable-debug flag (AD-7) is required
// to make it callable; the schema emitter labels debug methods with
// `x-debugOnly: true` so generated clients can segregate them. Default
// false — additions are opt-in.
public sealed record MethodEntry(
    string Name,
    Type? ParamsType,
    Type ResultType,
    string Summary,
    bool IsDebugOnly = false);

public static class MethodCatalog
{
    public static IReadOnlyList<MethodEntry> Core { get; } = new MethodEntry[]
    {
        new("host/ping",
            ParamsType: null,
            ResultType: typeof(HostPingResult),
            Summary: "Liveness check. Returns the pinned game version and SHA-256 (AD-3)."),

        new("host/methods",
            ParamsType: null,
            ResultType: typeof(HostMethodsResult),
            Summary: "Enumerate every wire method this host exposes (name, summary, hasParams, isDebugOnly). Mirrors the merged MethodCatalog (Core ∪ debug cheats) — debug entries are always listed but only callable when the host was started with --enable-debug (AD-7). Cheaper than parsing protocol/openrpc.json when a client just needs the method list."),

        new("run/new",
            ParamsType: typeof(RunNewParams),
            ResultType: typeof(RunNewResult),
            Summary: "Start a new run. Defaults: Ironclad, seed=1, ascension=0. Always lands at the Neow blessing EventRoom — dispatch run/select_event_option to advance to MapRoom."),

        new("run/state",
            ParamsType: null,
            ResultType: typeof(RunStateResult),
            Summary: "Read the current run snapshot."),

        new("run/summarize_state",
            ParamsType: null,
            ResultType: typeof(RunSummarizeStateResult),
            Summary: "Compact human-readable text rendering of the current run state. A few hundred bytes vs. the full RunStateResult — designed for AI assistants and chat clients to poll cheaply between actions. Read-only; describes what is, not what to do. Identical text wherever the wire is consumed (no client-side re-derivation)."),

        new("run/select_map_node",
            ParamsType: typeof(RunSelectMapNodeParams),
            ResultType: typeof(RunSelectMapNodeResult),
            Summary: "Enter the map node at (col, row)."),

        new("run/select_event_option",
            ParamsType: typeof(RunSelectEventOptionParams),
            ResultType: typeof(RunSelectEventOptionResult),
            Summary: "Pick an option on the current event room."),

        new("run/select_rest_site_option",
            ParamsType: typeof(RunSelectRestSiteOptionParams),
            ResultType: typeof(RunSelectRestSiteOptionResult),
            Summary: "Pick an option on the current rest site (HEAL, SMITH, …). HEAL exits to MapRoom; SMITH triggers an upgrade card-select via CardSelectCmd.FromDeckForUpgrade — pre-queue picks through cardSelectIndices, or the default HeadlessCardSelector picks the first upgradable card. Options empty after a single-pick option; the host force-advances to MapRoom."),

        new("run/take_treasure",
            ParamsType: null,
            ResultType: typeof(RunTakeTreasureResult),
            Summary: "Open the current treasure room's chest, grant the offered relic via RelicCmd.Obtain, and transition back to MapRoom. The chest's offering is exposed in availableTreasureRelics on every snapshot while CurrentRoomType=TreasureRoom (populated by driving TreasureRoom.DoNormalRewards on first read). Closes the synchronizer session, runs DoExtraRewardsIfNeeded (act-3 / ascension extras), and flips the room."),

        new("run/skip_treasure",
            ParamsType: null,
            ResultType: typeof(RunSkipTreasureResult),
            Summary: "Walk past the current treasure-room chest without granting the offered relic, then transition back to MapRoom. Same teardown chain as run/take_treasure (synchronizer close, DoExtraRewardsIfNeeded, room flip). Useful for relic-conflict avoidance or modifiers where leaving the slot empty is intentional."),

        new("run/buy_merchant_item",
            ParamsType: typeof(RunBuyMerchantItemParams),
            ResultType: typeof(RunBuyMerchantItemResult),
            Summary: "Purchase a merchant item by its index in availableMerchantItems. Routes through MerchantEntry.OnTryPurchaseWrapper (engine path); insufficient gold or sold-out slot returns InvalidParams."),

        new("run/leave_merchant_room",
            ParamsType: null,
            ResultType: typeof(RunLeaveMerchantRoomResult),
            Summary: "Exit the current merchant room to MapRoom. No params — merchant rooms have no engine auto-exit, so callers explicitly drive the transition."),

        new("run/end_turn",
            ParamsType: null,
            ResultType: typeof(RunEndTurnResult),
            Summary: "End the player's turn in the active combat."),

        new("run/play_card",
            ParamsType: typeof(RunPlayCardParams),
            ResultType: typeof(RunPlayCardResult),
            Summary: "Play a card from the current hand."),

        new("run/use_potion",
            ParamsType: typeof(RunUsePotionParams),
            ResultType: typeof(RunUsePotionResult),
            Summary: "Drink a potion from the player's belt. potionIndex is the wire index into ownedPotions; targetIndex is required for AnyEnemy potions and ignored otherwise."),

        new("run/select_reward",
            ParamsType: typeof(RunSelectRewardParams),
            ResultType: typeof(RunSelectRewardResult),
            Summary: "Claim a pending post-combat reward."),

        new("run/skip_reward",
            ParamsType: typeof(RunSkipRewardParams),
            ResultType: typeof(RunSkipRewardResult),
            Summary: "Skip a skippable pending reward."),

        new("run/enter_next_act",
            ParamsType: null,
            ResultType: typeof(RunEnterNextActResult),
            Summary: "Advance from the post-boss MapRoom to the next act. Only legal after defeating an act boss and draining rewards; bumps RunState.CurrentActIndex and regenerates the next act's map. Returns InvalidParams when called outside that window."),

        new("run/proceed_event",
            ParamsType: null,
            ResultType: typeof(RunProceedEventResult),
            Summary: "Auto-advance past a finished event when the engine leaves CurrentRoomType=EventRoom with no options surfaced. Only legal when the local event's IsFinished flag is true (i.e. AvailableEventOptions is empty while still in EventRoom). Returns InvalidParams when called outside that window."),

        new("run/history",
            ParamsType: null,
            ResultType: typeof(RunHistoryDocument),
            Summary: "Read the game's `RunHistory` for the most recently ended run (AD-8). Available only when recording is active (STS2_REPLAY_OUT) and the run has ended (RunManager.OnEnded — death or victory). Returns the same shape the retail game writes to its `.run` history files: snake_case fields, schema_version=9 at the v0.103.2 pin. Throws InvalidParams when no history is available yet."),

        new("run/read_deck",
            ParamsType: typeof(RunReadDeckParams),
            ResultType: typeof(RunReadDeckResult),
            Summary: "Read every card in the player's deck as (cardId, upgradeLevel, displayName) entries. Order matches the engine's Deck.Cards list (insertion order). Call this at decision points (drafting, rest-site upgrade picks, card-removal) — the deck is intentionally kept off per-snapshot RunStateResult to keep polls cheap. Full card descriptions stay behind content/describe_card."),

    };

    // Throws if the supplied dispatch-table keys differ from the catalogue
    // names. The caller supplies the merged catalogue (Core ∪ cheats) so this
    // function stays authoritative over whatever the host actually serves.
    // Called from HostMethods.Build at host startup; a mismatch means either
    // the catalogue or the dispatch table was edited in isolation.
    public static void AssertParity(IEnumerable<MethodEntry> entries, IEnumerable<string> dispatchKeys)
    {
        var catalog = entries.Select(e => e.Name).ToHashSet(StringComparer.Ordinal);
        var dispatch = dispatchKeys.ToHashSet(StringComparer.Ordinal);

        var inCatalogOnly = catalog.Except(dispatch).OrderBy(s => s, StringComparer.Ordinal).ToList();
        var inDispatchOnly = dispatch.Except(catalog).OrderBy(s => s, StringComparer.Ordinal).ToList();
        if (inCatalogOnly.Count == 0 && inDispatchOnly.Count == 0) return;

        var msg = "MethodCatalog drift detected.";
        if (inCatalogOnly.Count > 0) msg += $" In catalog but missing handler: [{string.Join(", ", inCatalogOnly)}].";
        if (inDispatchOnly.Count > 0) msg += $" Handler registered without catalog entry: [{string.Join(", ", inDispatchOnly)}].";
        throw new InvalidOperationException(msg);
    }
}
