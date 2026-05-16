using Sts2Headless.Protocol;

namespace Sts2Headless.Cheats;

// Catalog entries for the cheat (debug-only) wire surface. Kept separate
// from Sts2Headless.Protocol.MethodCatalog so the Agents project — which
// references Protocol but not Cheats — can't observe the cheat shape.
//
// The host (Sts2Headless) merges MethodCatalog.Core with CheatMethodCatalog.All
// at startup and feeds the union through MethodCatalog.AssertParity, so
// catalog/dispatch drift remains a fail-fast invariant.
//
// The schema emitter (Sts2Headless.SchemaExport) does the same merge so
// protocol/openrpc.json keeps describing the full wire surface.
public static class CheatMethodCatalog
{
    public static IReadOnlyList<MethodEntry> All { get; } = new MethodEntry[]
    {
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

        new("debug/replace_deck",
            ParamsType: typeof(DebugReplaceDeckParams),
            ResultType: typeof(DebugReplaceDeckResult),
            Summary: "Test affordance — replace the player's deck with a curated list of (CardId, UpgradeLevel) pairs. Routes through RunState.CreateCard so the new cards are properly tracked; bypasses on-deck-change listeners. Requires --enable-debug.",
            IsDebugOnly: true),
    };
}
