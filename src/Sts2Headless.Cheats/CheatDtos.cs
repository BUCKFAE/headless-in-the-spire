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
