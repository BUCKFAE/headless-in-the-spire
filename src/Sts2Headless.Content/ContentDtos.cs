using System.Text.Json.Serialization;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Content;

// Wire DTOs for the content/* namespace. Mirror discipline from
// Sts2Headless.Protocol.Methods.Methods.cs: explicit [JsonPropertyName]
// on every field, typed enums everywhere they apply, `Ok` flag for log
// readability.
//
// Why a separate project? Symmetric with Sts2Headless.Cheats — agents
// that want to drive a run but not snoop content can reference only
// Protocol; lifting content access to its own ref makes the surface
// explicit rather than ambient.

// ── content/describe_card ────────────────────────────────────────────────

public sealed record ContentDescribeCardParams(
    [property: JsonPropertyName("cardId")] CardId CardId,
    // Upgrade level — 0 (base) by default. The wire reflects the
    // card model's per-level stats when the engine exposes per-level
    // overrides; otherwise upgradeLevel>0 may return the same stats as
    // 0 (the model is unaware of the upgrade dimension).
    [property: JsonPropertyName("upgradeLevel")] int? UpgradeLevel = null);

public sealed record ContentDescribeCardResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("cardId")] CardId CardId,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("cost")] int Cost,
    [property: JsonPropertyName("rarity")] string Rarity,
    [property: JsonPropertyName("character")] string Character,
    [property: JsonPropertyName("targetType")] TargetType TargetType,
    [property: JsonPropertyName("type")] string Type);

// ── content/describe_relic ───────────────────────────────────────────────

public sealed record ContentDescribeRelicParams(
    [property: JsonPropertyName("relicId")] RelicId RelicId);

public sealed record ContentDescribeRelicResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("relicId")] RelicId RelicId,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("rarity")] string Rarity);

// ── content/describe_potion ──────────────────────────────────────────────

public sealed record ContentDescribePotionParams(
    [property: JsonPropertyName("potionId")] PotionId PotionId);

public sealed record ContentDescribePotionResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("potionId")] PotionId PotionId,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("rarity")] string Rarity,
    [property: JsonPropertyName("targetType")] TargetType TargetType);

// ── content/describe_power ───────────────────────────────────────────────

public sealed record ContentDescribePowerParams(
    [property: JsonPropertyName("powerId")] PowerId PowerId);

public sealed record ContentDescribePowerResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("powerId")] PowerId PowerId,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("isDebuff")] bool IsDebuff);

// ── content/describe_event ───────────────────────────────────────────────

public sealed record ContentDescribeEventParams(
    [property: JsonPropertyName("eventId")] string EventId);

public sealed record ContentDescribeEventResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("eventId")] string EventId,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("description")] string Description);

// ── content/describe_encounter ───────────────────────────────────────────

public sealed record ContentDescribeEncounterParams(
    [property: JsonPropertyName("encounterId")] string EncounterId);

public sealed record ContentDescribeEncounterResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("encounterId")] string EncounterId,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("monsterIds")] IReadOnlyList<MonsterId> MonsterIds,
    [property: JsonPropertyName("tier")] string Tier);

// ── content/describe_monster ─────────────────────────────────────────────

public sealed record ContentDescribeMonsterParams(
    [property: JsonPropertyName("monsterId")] MonsterId MonsterId);

public sealed record ContentDescribeMonsterResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("monsterId")] MonsterId MonsterId,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("baseHp")] int BaseHp,
    [property: JsonPropertyName("baseMaxHp")] int BaseMaxHp);

// ── content/describe_affliction ──────────────────────────────────────────

public sealed record ContentDescribeAfflictionParams(
    [property: JsonPropertyName("afflictionId")] AfflictionId AfflictionId);

public sealed record ContentDescribeAfflictionResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("afflictionId")] AfflictionId AfflictionId,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("description")] string Description);

// ── content/describe_enchantment ─────────────────────────────────────────

public sealed record ContentDescribeEnchantmentParams(
    [property: JsonPropertyName("enchantmentId")] EnchantmentId EnchantmentId);

public sealed record ContentDescribeEnchantmentResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("enchantmentId")] EnchantmentId EnchantmentId,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("description")] string Description);

// ── content/describe_modifier ────────────────────────────────────────────

public sealed record ContentDescribeModifierParams(
    [property: JsonPropertyName("modifierId")] ModifierId ModifierId);

public sealed record ContentDescribeModifierResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("modifierId")] ModifierId ModifierId,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("description")] string Description);

// ── content/list_cards ───────────────────────────────────────────────────

// One row in the filtered card-pool listing.
public sealed record ContentCardSummary(
    [property: JsonPropertyName("id")] CardId Id,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("rarity")] string Rarity,
    [property: JsonPropertyName("cost")] int Cost,
    [property: JsonPropertyName("character")] string Character);

public sealed record ContentListCardsParams(
    // Filter to this character's pool. Null = no character filter (all).
    [property: JsonPropertyName("character")] Character? Character = null,
    // Filter to this rarity (string match; "common", "uncommon", "rare",
    // "starter", etc.). Null = no rarity filter.
    [property: JsonPropertyName("rarity")] string? Rarity = null,
    // Include the colorless / shared pool even when character is set.
    // Defaults to true — colorless cards are draftable for every class.
    [property: JsonPropertyName("includeColorless")] bool? IncludeColorless = null);

public sealed record ContentListCardsResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("cards")] IReadOnlyList<ContentCardSummary> Cards);

// ── content/list_relics ──────────────────────────────────────────────────

public sealed record ContentRelicSummary(
    [property: JsonPropertyName("id")] RelicId Id,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("rarity")] string Rarity);

public sealed record ContentListRelicsParams(
    [property: JsonPropertyName("rarity")] string? Rarity = null);

public sealed record ContentListRelicsResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("relics")] IReadOnlyList<ContentRelicSummary> Relics);

// ── content/list_potions ─────────────────────────────────────────────────

public sealed record ContentPotionSummary(
    [property: JsonPropertyName("id")] PotionId Id,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("rarity")] string Rarity,
    [property: JsonPropertyName("targetType")] TargetType TargetType);

public sealed record ContentListPotionsParams(
    [property: JsonPropertyName("rarity")] string? Rarity = null);

public sealed record ContentListPotionsResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("potions")] IReadOnlyList<ContentPotionSummary> Potions);

// ── content/describe_act ─────────────────────────────────────────────────

// Per-act content pools (the *pool*, not the rolled-for-this-run answer).
// `actIndex` is the 0-based act number (0 = Act 1). Encounter / event ids
// are stable strings the engine surfaces (e.g. "SLIMES_NORMAL",
// "DOORMAKER_BOSS"); we keep them as raw strings rather than typed enums
// because the EncounterId enum is generated and the wire stays usable
// even when this list isn't fully populated.
public sealed record ContentDescribeActParams(
    [property: JsonPropertyName("actIndex")] int ActIndex);

public sealed record ContentDescribeActResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("actIndex")] int ActIndex,
    [property: JsonPropertyName("numFloors")] int NumFloors,
    [property: JsonPropertyName("numRooms")] int NumRooms,
    [property: JsonPropertyName("numberOfWeakEncounters")] int NumberOfWeakEncounters,
    [property: JsonPropertyName("eliteRollCount")] int EliteRollCount,
    [property: JsonPropertyName("weakEncounterPool")] IReadOnlyList<string> WeakEncounterPool,
    [property: JsonPropertyName("normalEncounterPool")] IReadOnlyList<string> NormalEncounterPool,
    [property: JsonPropertyName("elitePool")] IReadOnlyList<string> ElitePool,
    [property: JsonPropertyName("bossPool")] IReadOnlyList<string> BossPool,
    [property: JsonPropertyName("eventPool")] IReadOnlyList<string> EventPool);

// ── content/encounter_rules ──────────────────────────────────────────────

// Static rules text describing how the engine builds an act's encounter
// schedule. Same content for every run; useful as a one-shot agent prompt.
public sealed record ContentEncounterRulesResult(
    [property: JsonPropertyName("ok")] bool Ok,
    // Whether the engine drains the weak-encounter pool before the
    // regular-encounter pool (true at the current pin — `NumberOfWeakEncounters`
    // weak fights are scheduled first, then regulars fill the rest).
    [property: JsonPropertyName("weakEncountersFirst")] bool WeakEncountersFirst,
    // Number of elite ids pre-rolled per act (literal 15 in
    // ActModel.GenerateRooms IL at the current pin).
    [property: JsonPropertyName("eliteRollCount")] int EliteRollCount,
    // True if the engine refuses to schedule two consecutive encounters
    // that share any tags (AddWithoutRepeatingTags predicate).
    [property: JsonPropertyName("noAdjacentSharedTags")] bool NoAdjacentSharedTags,
    // Narrative summary clients can include verbatim in a prompt.
    [property: JsonPropertyName("notes")] string Notes);

// ── content/unknown_node_odds ────────────────────────────────────────────

// One row of the base odds table for resolving a `?` map node into a
// concrete room type. These are *priors* — the runtime conditions on
// visit history (`UnknownMapPointOdds.Roll(history, runState)`), which
// is hidden from the player. The base distribution itself is content.
public sealed record ContentUnknownNodeOddsRow(
    [property: JsonPropertyName("roomType")] RoomType RoomType,
    [property: JsonPropertyName("weight")] double Weight);

public sealed record ContentUnknownNodeOddsParams(
    [property: JsonPropertyName("actIndex")] int? ActIndex = null);

public sealed record ContentUnknownNodeOddsResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("actIndex")] int? ActIndex,
    [property: JsonPropertyName("baseOdds")] IReadOnlyList<ContentUnknownNodeOddsRow> BaseOdds);
