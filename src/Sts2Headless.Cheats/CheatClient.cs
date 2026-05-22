using Sts2Headless.Agents.Contracts;

namespace Sts2Headless.Cheats;

// Typed extension methods over ITransport for callers (tests, harnesses)
// that want to invoke the cheat surface without hand-rolling wire-string
// payloads. Lives in this project so a `using Sts2Headless.Cheats;` is
// the deliberate, grep-able opt-in for cheat access; agents — which only
// reference Protocol, not Cheats — can't resolve any of these extensions.
//
// Naming mirrors the wire method: `SetHpAsync` for `debug/set_hp`, etc.
// Arguments are PascalCase C# parameters, not wire-shaped dictionaries,
// so a wire rename is a compile error here, not a silently-passing test.
public static class CheatClient
{
    // debug/set_hp — set the player's CurrentHp (and optionally MaxHp).
    // Bypasses damage events, on-hit relics, and game-over detection;
    // see DebugSetHpParams for the validation rules the host enforces.
    public static Task<DebugSetHpResult> SetHpAsync(
        this ITransport transport, int hp, int? maxHp = null) =>
        transport.SendAsync<DebugSetHpResult>("debug/set_hp", new DebugSetHpParams(hp, maxHp));

    // debug/give_relic — grant a relic by id via RelicCmd.Obtain (engine path).
    public static Task<DebugGiveRelicResult> GiveRelicAsync(
        this ITransport transport, string relicId) =>
        transport.SendAsync<DebugGiveRelicResult>("debug/give_relic", new DebugGiveRelicParams(relicId));

    // debug/give_potion — grant a potion by id via PotionCmd.TryToProcure
    // (engine path). Lands in the first empty PotionSlots entry.
    public static Task<DebugGivePotionResult> GivePotionAsync(
        this ITransport transport, string potionId) =>
        transport.SendAsync<DebugGivePotionResult>("debug/give_potion", new DebugGivePotionParams(potionId));

    // debug/replace_deck — wholesale-replace the player's deck. `cards` is
    // a list of (cardId, upgradeLevel) tuples; upgradeLevel defaults to 0
    // (base card). Pass tuples for convenience; the wire shape uses
    // CardSpec records under the hood.
    public static Task<DebugReplaceDeckResult> ReplaceDeckAsync(
        this ITransport transport, IEnumerable<(string CardId, int UpgradeLevel)> cards) =>
        transport.SendAsync<DebugReplaceDeckResult>(
            "debug/replace_deck",
            new DebugReplaceDeckParams(cards.Select(c => new CardSpec(c.CardId, c.UpgradeLevel)).ToList()));

    // debug/read_deck — read every card in the player's deck as a list of
    // (cardId, upgradeLevel) tuples, in deck insertion order. Mirrors
    // ReplaceDeckAsync's input shape so tests can round-trip cleanly.
    public static Task<DebugReadDeckResult> ReadDeckAsync(this ITransport transport) =>
        transport.SendAsync<DebugReadDeckResult>("debug/read_deck", new DebugReadDeckParams());

    // debug/kill_all_enemies — drop every alive enemy in the current combat
    // to 0 HP via the engine's Creature._currentHp backing field. No-op
    // outside combat. Bypasses on-kill listeners.
    public static Task<DebugKillAllEnemiesResult> KillAllEnemiesAsync(this ITransport transport) =>
        transport.SendAsync<DebugKillAllEnemiesResult>("debug/kill_all_enemies", new DebugKillAllEnemiesParams());

    // debug/start_combat — force-start a combat against the chosen encounter
    // id (matches the EncounterId enum's wire string, e.g. "SLIMES_NORMAL").
    // Bypasses map progression. Unknown ids surface as InvalidParams; the
    // engine does not validate act/character compatibility, so the caller
    // owns the choice of which combats make sense for the current run.
    public static Task<DebugStartCombatResult> StartCombatAsync(
        this ITransport transport, string encounterId) =>
        transport.SendAsync<DebugStartCombatResult>(
            "debug/start_combat", new DebugStartCombatParams(encounterId));
}
