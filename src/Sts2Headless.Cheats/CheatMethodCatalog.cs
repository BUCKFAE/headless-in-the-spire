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

        new("debug/give_potion",
            ParamsType: typeof(DebugGivePotionParams),
            ResultType: typeof(DebugGivePotionResult),
            Summary: "Test affordance — grant a potion via PotionCmd.TryToProcure (engine path). Lands in the first empty PotionSlots entry; returns the chosen slot index. Requires --enable-debug.",
            IsDebugOnly: true),

        new("debug/start_event",
            ParamsType: typeof(DebugStartEventParams),
            ResultType: typeof(DebugStartEventResult),
            Summary: "Test affordance — force-start a specific event via EventRoom(model) + RunManager.EnterRoom. Bypasses map progression. Returns the post-EnterRoom room type and current options count. Requires --enable-debug.",
            IsDebugOnly: true),

        new("debug/apply_power",
            ParamsType: typeof(DebugApplyPowerParams),
            ResultType: typeof(DebugApplyPowerResult),
            Summary: "Test affordance — apply a power to a creature via PowerCmd.Apply (engine path). Requires an active combat. Target: enemyIndex null → player; enemyIndex set → enemies[i]. Requires --enable-debug.",
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

        new("debug/read_deck",
            ParamsType: typeof(DebugReadDeckParams),
            ResultType: typeof(DebugReadDeckResult),
            Summary: "Test affordance — read every card in the player's deck as (CardId, UpgradeLevel) pairs. Mirrors debug/replace_deck's input shape so tests can round-trip. Requires --enable-debug.",
            IsDebugOnly: true),

        new("debug/kill_all_enemies",
            ParamsType: typeof(DebugKillAllEnemiesParams),
            ResultType: typeof(DebugKillAllEnemiesResult),
            Summary: "Test affordance — drop every alive enemy in the current combat to 0 HP by writing the engine's Creature._currentHp backing field, then drain and auto-advance so rewards generate through the normal path. No-op (killed=0) outside combat. Bypasses on-kill listeners. Requires --enable-debug.",
            IsDebugOnly: true),

        new("debug/start_combat",
            ParamsType: typeof(DebugStartCombatParams),
            ResultType: typeof(DebugStartCombatResult),
            Summary: "Test affordance — force-start a specific combat against the chosen encounter id (e.g. \"SLIMES_NORMAL\"), bypassing map progression. Constructs CombatRoom(EncounterModel.ToMutable(), runState) and drives RunManager.EnterRoom; the engine does not validate act/character compatibility. Returns InvalidParams for unknown ids. Requires --enable-debug.",
            IsDebugOnly: true),
    };
}
