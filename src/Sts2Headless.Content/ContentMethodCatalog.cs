using Sts2Headless.Protocol;

namespace Sts2Headless.Content;

// Catalog entries for the content/* wire surface. Kept separate from
// Sts2Headless.Protocol.MethodCatalog and Sts2Headless.Cheats.CheatMethodCatalog
// so projects can choose how much of the wire to depend on:
//   - Sts2Headless.Agents references only Protocol → can drive a run.
//   - Adding a Content ref enables content/describe_* introspection.
//   - Adding a Cheats ref enables debug/* (AD-7).
//
// The host (Sts2Headless) merges all three catalogs at startup and feeds
// the union through MethodCatalog.AssertParity, so a method registered
// without a catalog entry — or vice versa — fails fast.
//
// The schema emitter (Sts2Headless.SchemaExport) merges the same way so
// protocol/openrpc.json describes the full wire surface.
//
// All entries here describe **player-visible content** (or static rules /
// pool listings that are not seed-dependent). Seed-deterministic reveals
// belong in CheatMethodCatalog with IsDebugOnly=true.
public static class ContentMethodCatalog
{
    public static IReadOnlyList<MethodEntry> All { get; } = new MethodEntry[]
    {
        new("content/describe_card",
            ParamsType: typeof(ContentDescribeCardParams),
            ResultType: typeof(ContentDescribeCardResult),
            Summary: "Describe a single card by its CardId: name, description, cost, rarity, character, target type. Static content (read from ModelDb.AllCards); no run required."),

        new("content/describe_relic",
            ParamsType: typeof(ContentDescribeRelicParams),
            ResultType: typeof(ContentDescribeRelicResult),
            Summary: "Describe a single relic by its RelicId: name, description, rarity. Static content (read from ModelDb.AllRelics); no run required."),

        new("content/describe_potion",
            ParamsType: typeof(ContentDescribePotionParams),
            ResultType: typeof(ContentDescribePotionResult),
            Summary: "Describe a single potion by its PotionId: name, description, rarity, target type. Static content; no run required."),

        new("content/describe_power",
            ParamsType: typeof(ContentDescribePowerParams),
            ResultType: typeof(ContentDescribePowerResult),
            Summary: "Describe a single power (buff/debuff) by its PowerId: name, description, isDebuff flag. Static content; no run required."),

        new("content/describe_event",
            ParamsType: typeof(ContentDescribeEventParams),
            ResultType: typeof(ContentDescribeEventResult),
            Summary: "Describe an event by its wire id: title and description. Static content (read from ModelDb.AllEvents ∪ AllSharedEvents); option branches roll seed-deterministically at choice time and are NOT surfaced here — use debug/peek_event_outcome (gated) for that."),

        new("content/describe_encounter",
            ParamsType: typeof(ContentDescribeEncounterParams),
            ResultType: typeof(ContentDescribeEncounterResult),
            Summary: "Describe a monster encounter (pack) by its wire id: display name, monster id list, tier (_WEAK / _NORMAL / _ELITE / _BOSS). Static content; specific monster HP is rolled by EncounterModel.GenerateMonstersWithSlots at combat-start and is not part of this answer."),

        new("content/describe_monster",
            ParamsType: typeof(ContentDescribeMonsterParams),
            ResultType: typeof(ContentDescribeMonsterResult),
            Summary: "Describe a single monster by its MonsterId: display name and base HP range. Static content; per-encounter HP rolls are not surfaced."),

        new("content/describe_affliction",
            ParamsType: typeof(ContentDescribeAfflictionParams),
            ResultType: typeof(ContentDescribeAfflictionResult),
            Summary: "Describe a card affliction (negative tag attached to a card) by its AfflictionId."),

        new("content/describe_enchantment",
            ParamsType: typeof(ContentDescribeEnchantmentParams),
            ResultType: typeof(ContentDescribeEnchantmentResult),
            Summary: "Describe a card enchantment (positive tag attached to a card) by its EnchantmentId."),

        new("content/describe_modifier",
            ParamsType: typeof(ContentDescribeModifierParams),
            ResultType: typeof(ContentDescribeModifierResult),
            Summary: "Describe a run modifier (DRAFT, SEALED_DECK, HOARDER, …) by its ModifierId. Modifiers alter starting conditions; pass them to run/new via the modifiers list."),

        new("content/list_cards",
            ParamsType: typeof(ContentListCardsParams),
            ResultType: typeof(ContentListCardsResult),
            Summary: "List all cards in the static pool, optionally filtered by character / rarity / colorless inclusion. Static content (read from ModelDb.AllCards filtered by character class)."),

        new("content/list_relics",
            ParamsType: typeof(ContentListRelicsParams),
            ResultType: typeof(ContentListRelicsResult),
            Summary: "List all relics in the static pool, optionally filtered by rarity."),

        new("content/list_potions",
            ParamsType: typeof(ContentListPotionsParams),
            ResultType: typeof(ContentListPotionsResult),
            Summary: "List all potions in the static pool, optionally filtered by rarity."),

        new("content/describe_act",
            ParamsType: typeof(ContentDescribeActParams),
            ResultType: typeof(ContentDescribeActResult),
            Summary: "Per-act content pools (weak, regular, elite, boss, event) and structural counts (NumberOfWeakEncounters, num floors / rooms, elite roll count). Static content — the *pool*, not the rolled answer. For this run's specific schedule see debug/reveal_act_schedule (gated)."),

        new("content/encounter_rules",
            ParamsType: null,
            ResultType: typeof(ContentEncounterRulesResult),
            Summary: "Static rules describing how the engine builds an act's encounter schedule (weak-first pass, elite roll count, no-adjacent-shared-tags constraint). Useful as a one-shot prompt for planning agents."),

        new("content/unknown_node_odds",
            ParamsType: typeof(ContentUnknownNodeOddsParams),
            ResultType: typeof(ContentUnknownNodeOddsResult),
            Summary: "Base odds distribution for resolving an `Unknown` (`?`) map node into a concrete room type. The runtime conditions on visit history; this is the prior, which is content-knowable. The resolved value is rolled at entry and is NOT surfaced — use debug/peek_unknown_resolution (gated)."),
    };
}
