using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Protocol;

// Single source of truth for the wire's method catalogue (AD-5).
//
// Both the host dispatch table (HostMethods.Build) and the schema emitter
// (Sts2Headless.SchemaExport) consume this list. HostMethods asserts that
// the dictionary it constructs matches AssertParity at startup, so a method
// added to the catalogue without a handler — or a handler without an entry
// here — fails fast rather than silently drifting the wire from the schema.
//
// Adding a method: append a MethodEntry. The schema artefact picks it up on
// the next `just export-schema`; the host fails to start until a handler
// is registered under the same key.

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
    public static IReadOnlyList<MethodEntry> All { get; } = new MethodEntry[]
    {
        new("host/ping",
            ParamsType: null,
            ResultType: typeof(HostPingResult),
            Summary: "Liveness check. Returns the pinned game version and SHA-256 (AD-3)."),

        new("run/new",
            ParamsType: typeof(RunNewParams),
            ResultType: typeof(RunNewResult),
            Summary: "Start a new run. Defaults: Ironclad, seed=1, withNeow=false."),

        new("run/state",
            ParamsType: null,
            ResultType: typeof(RunStateResult),
            Summary: "Read the current run snapshot."),

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
            Summary: "Pick an option on the current rest site (HEAL, SMITH, …). HEAL exits to MapRoom; SMITH branches into card-select which is not wired yet."),

        new("run/leave_treasure_room",
            ParamsType: null,
            ResultType: typeof(RunLeaveTreasureRoomResult),
            Summary: "Open the chest in the current treasure room and exit to MapRoom. No params — chests have a single relic offering and the host auto-picks it via the engine's TreasureRoomRelicSynchronizer (greedy default; a future slice can split this into previewable pick/skip)."),

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

        new("debug/give_relic",
            ParamsType: typeof(DebugGiveRelicParams),
            ResultType: typeof(DebugGiveRelicResult),
            Summary: "Test affordance — grant a relic via RelicCmd.Obtain (engine path). Requires --enable-debug.",
            IsDebugOnly: true),

        new("debug/set_hp",
            ParamsType: typeof(DebugSetHpParams),
            ResultType: typeof(DebugSetHpResult),
            Summary: "Test affordance — set the player's CurrentHp (and optionally MaxHp) by writing the engine's backing fields. Bypasses damage events, on-hit relics, and game-over detection; the resulting state is not authoritative. Requires --enable-debug.",
            IsDebugOnly: true),
    };

    // Throws if the supplied dispatch-table keys differ from the catalogue
    // names. Called from HostMethods.Build at host startup; a mismatch means
    // either the catalogue or the dispatch table was edited in isolation.
    public static void AssertParity(IEnumerable<string> dispatchKeys)
    {
        var catalog = All.Select(e => e.Name).ToHashSet(StringComparer.Ordinal);
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
