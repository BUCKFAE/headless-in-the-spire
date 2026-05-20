using System.Text.Json.Serialization;

namespace Sts2Headless.Cheats;

// Wire DTOs for the cheat surface — every method here is a test affordance
// gated behind --enable-debug (AD-7) and lives in its own assembly so the
// Sts2Headless.Agents project, which only references Protocol, cannot
// reach a cheat type even by `using` it accidentally.
//
// Naming follows Protocol.Methods convention: <Method>Params / <Method>Result,
// PascalCase records with explicit [JsonPropertyName] aliases so the wire
// shape is grep-able from this file alone.

// ── debug/give_relic ─────────────────────────────────────────────────────

// Grants a relic to the active player via the engine path (RelicCmd.Obtain,
// the same path RelicReward.OnSelectWrapper uses). Lives behind --enable-debug
// because regression tests use this to inject relics with observable on-event
// side effects (e.g. LuckyFysh's +15 gold on AfterCardChangedPiles) so the
// test can pin engine-pipeline behaviour that direct mutation would silently
// bypass.
public sealed record DebugGiveRelicParams(
    [property: JsonPropertyName("relicId")] string RelicId);

public sealed record DebugGiveRelicResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("relicId")] string RelicId,
    [property: JsonPropertyName("hp")] int Hp,
    [property: JsonPropertyName("maxHp")] int MaxHp,
    [property: JsonPropertyName("gold")] int Gold,
    [property: JsonPropertyName("deckSize")] int DeckSize);

// ── debug/set_hp ─────────────────────────────────────────────────────────

// Writes the player's current HP (and optionally Max HP) directly into the
// engine's backing fields. **Bypasses** the damage event path, on-hit relic
// listeners (Burning Blood's heal, etc.), the death pipeline, and any other
// side effect a "real" HP change would trigger.
//
// Validation:
//   * hp >= 0
//   * maxHp, when provided, >= 1
//   * hp <= maxHp (the resulting maxHp — either the provided value or the
//     current one if maxHp was omitted)
// Validation failures return WireErrorCode.InvalidParams (-32602).
//
// Setting hp to 0 does NOT trigger game-over by itself — the engine's
// IsGameOver flag is set elsewhere on the death pipeline. Callers wanting
// to test "what does run/state look like at zero HP" can use this; callers
// wanting to test the death transition itself need to drive damage events
// through combat.
public sealed record DebugSetHpParams(
    [property: JsonPropertyName("hp")] int Hp,
    [property: JsonPropertyName("maxHp")] int? MaxHp = null);

public sealed record DebugSetHpResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("hp")] int Hp,
    [property: JsonPropertyName("maxHp")] int MaxHp,
    [property: JsonPropertyName("isGameOver")] bool IsGameOver);

// ── debug/replace_deck ───────────────────────────────────────────────────

// A card the caller wants placed into the player's deck. `cardId` is the
// wire string id (matches the CardId enum's wire form, e.g. "POMMEL_STRIKE");
// `upgradeLevel` is 0 for the base card, 1 for the first upgrade, etc.
// Engine-side this routes through CardModel.UpgradeInternal +
// FinalizeUpgradeInternal applied upgradeLevel times.
public sealed record CardSpec(
    [property: JsonPropertyName("cardId")] string CardId,
    [property: JsonPropertyName("upgradeLevel")] int UpgradeLevel = 0);

// Replace the player's deck with a curated list. Hard write — every
// existing card is untracked from RunState, the deck is cleared, and
// the new cards are added through RunState.CreateCard + Deck.AddInternal.
// Bypasses on-deck-change listeners (matches the spirit of debug/set_hp
// and debug/give_relic, which also write through events).
//
// Use case: pin a combat to a specific opening hand so a test can assert
// behavior (e.g. force a deterministic infinite combo) without relying on
// the full game-progression path to produce a particular deck.
public sealed record DebugReplaceDeckParams(
    [property: JsonPropertyName("cards")] IReadOnlyList<CardSpec> Cards);

public sealed record DebugReplaceDeckResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("deckSize")] int DeckSize,
    [property: JsonPropertyName("cardIds")] IReadOnlyList<string> CardIds);

// ── debug/read_deck ──────────────────────────────────────────────────────

// Read every card in the player's deck as (cardId, upgradeLevel) pairs.
// Mirrors `debug/replace_deck` shape so tests can round-trip:
// `replace_deck([...])` → … → `read_deck()` and compare. Order is the
// deck's insertion order (the engine's Deck.Cards list).
//
// Use case: pinning the effect of an upgrade — e.g. asserting that after
// `run/select_rest_site_option(SMITH, cardSelectIndices: [[0]])` a card
// in the deck has upgradeLevel > 0. The core wire surface deliberately
// keeps the deck out of `RunStateResult` (it's a coverage-heavy field
// agents rarely consume); this debug method gives tests the inspection
// hook without putting the same data on every snapshot.
public sealed record DebugReadDeckParams();

public sealed record DebugReadDeckResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("deckSize")] int DeckSize,
    [property: JsonPropertyName("cards")] IReadOnlyList<CardSpec> Cards);

// ── debug/start_combat ───────────────────────────────────────────────────

// Force-start a specific combat against the chosen encounter, bypassing
// map progression. Mirrors the sts2-cli "/enter_room combat ..." path:
// resolves the EncounterModel via ModelDb.GetById, mutates it for the run,
// constructs CombatRoom(encounter, runState), and drives
// RunManager.Instance.EnterRoom to flip the room. Bypasses the natural
// map-selection event chain — listeners that react to "player entered a
// monster node from the map" will not fire.
//
// Use case: the EveryEncounterSmokeTests sweep places the Ironclad with a
// known deck in front of every encounter id sts2 ships, verifying that
// nothing in the combat surface (cards, monster intents, powers, scenes)
// crashes with MissingMethodException / MissingFieldException — the same
// shape as the treasure-room chest-open bug. Losses are expected for
// some encounters; the test signal is "no crash."
//
// `encounterId` is the wire string id (matches EncounterId enum's wire
// form, e.g. "SLIMES_NORMAL"). Unknown ids return InvalidParams.
public sealed record DebugStartCombatParams(
    [property: JsonPropertyName("encounterId")] string EncounterId);

public sealed record DebugStartCombatResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("encounterId")] string EncounterId,
    [property: JsonPropertyName("inProgress")] bool InProgress,
    [property: JsonPropertyName("enemyCount")] int EnemyCount);

// ── debug/kill_all_enemies ───────────────────────────────────────────────

// Drops every alive enemy in the current combat to 0 HP by writing the
// engine's Creature._currentHp backing field (Enemy : Creature in sts2's
// hierarchy). Bypasses the damage-event pipeline and on-kill listeners —
// matches the spirit of debug/set_hp, which writes the same backing field
// on the player. Use case: forcing function for end-to-end full-game
// playthroughs where the agent isn't strong enough to clear every combat
// honestly but the test needs to exercise the post-combat path (rewards,
// map progression, Neow/event choices in the replay).
//
// Side effects in the engine:
//   * Each enemy whose HP we zero is then ignored by the engine's "alive
//     enemies" predicate, so CombatManager flips IsInProgress=false on the
//     next tick.
//   * After the writes, the helper drains the action executor and runs
//     AutoAdvancePostCombat so rewards generate through the normal path
//     (same surface UsePotion / PlayCard land on). The caller's next
//     `run/state` will see the post-combat room or pending rewards.
//
// No params — the cheat operates on whatever combat is currently in
// progress on the active run. Calling outside combat is a no-op that
// returns killed=0, combatEnded=false (not an error: tests routinely fire
// this on every state observation and we don't want spurious failures).
public sealed record DebugKillAllEnemiesParams();

public sealed record DebugKillAllEnemiesResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("killed")] int Killed,
    [property: JsonPropertyName("combatEnded")] bool CombatEnded);
