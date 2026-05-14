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

public sealed record MethodEntry(
    string Name,
    Type? ParamsType,
    Type ResultType,
    string Summary);

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

        new("run/end_turn",
            ParamsType: null,
            ResultType: typeof(RunEndTurnResult),
            Summary: "End the player's turn in the active combat."),

        new("run/play_card",
            ParamsType: typeof(RunPlayCardParams),
            ResultType: typeof(RunPlayCardResult),
            Summary: "Play a card from the current hand."),

        new("run/select_reward",
            ParamsType: typeof(RunSelectRewardParams),
            ResultType: typeof(RunSelectRewardResult),
            Summary: "Claim a pending post-combat reward."),

        new("run/skip_reward",
            ParamsType: typeof(RunSkipRewardParams),
            ResultType: typeof(RunSkipRewardResult),
            Summary: "Skip a skippable pending reward."),

        new("debug/give_relic",
            ParamsType: typeof(DebugGiveRelicParams),
            ResultType: typeof(DebugGiveRelicResult),
            Summary: "Test affordance — grant a relic via RelicCmd.Obtain (engine path)."),
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
