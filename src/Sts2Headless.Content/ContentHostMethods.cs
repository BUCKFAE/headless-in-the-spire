using System.Text.Json.Nodes;
using Sts2Headless.Protocol;
using Sts2Headless.Protocol.Methods;
using Sts2Headless.Runtime.Bindings;

namespace Sts2Headless.Content;

// Dispatch table for content/* wire methods. Mirrors
// Sts2Headless.Cheats.CheatHostMethods structure: returns
// Func<JsonNode?, JsonNode?> so the host can register entries without
// referencing this project's typed records directly. Handlers
// deserialise/serialise through the shared WireHandlers.Typed adapter.
//
// Lifetime: one instance of `ContentReader` per host process. The host
// owns the Sts2Bindings (and the loaded sts2 Assembly); we accept the
// bindings handle and read its `.Sts2` assembly to seed the reader.
public static class ContentHostMethods
{
    public static IReadOnlyDictionary<string, Func<JsonNode?, JsonNode?>> Build(
        Sts2Bindings bindings, Func<RunHandle?> getRun)
    {
        var reader = new ContentReader(bindings.Sts2);

        return new Dictionary<string, Func<JsonNode?, JsonNode?>>
        {
            ["content/describe_card"] = WireHandlers.Typed<ContentDescribeCardParams, ContentDescribeCardResult>(
                p => DescribeCard(reader, p)),
            ["content/describe_relic"] = WireHandlers.Typed<ContentDescribeRelicParams, ContentDescribeRelicResult>(
                p => DescribeRelic(reader, p)),
            ["content/describe_potion"] = WireHandlers.Typed<ContentDescribePotionParams, ContentDescribePotionResult>(
                p => DescribePotion(reader, p)),
            ["content/describe_power"] = WireHandlers.Typed<ContentDescribePowerParams, ContentDescribePowerResult>(
                p => DescribePower(reader, p)),
            ["content/describe_event"] = WireHandlers.Typed<ContentDescribeEventParams, ContentDescribeEventResult>(
                p => DescribeEvent(reader, p)),
            ["content/describe_encounter"] = WireHandlers.Typed<ContentDescribeEncounterParams, ContentDescribeEncounterResult>(
                p => DescribeEncounter(reader, p)),
            ["content/describe_monster"] = WireHandlers.Typed<ContentDescribeMonsterParams, ContentDescribeMonsterResult>(
                p => DescribeMonster(reader, p)),
            ["content/describe_affliction"] = WireHandlers.Typed<ContentDescribeAfflictionParams, ContentDescribeAfflictionResult>(
                p => DescribeAffliction(reader, p)),
            ["content/describe_enchantment"] = WireHandlers.Typed<ContentDescribeEnchantmentParams, ContentDescribeEnchantmentResult>(
                p => DescribeEnchantment(reader, p)),
            ["content/describe_modifier"] = WireHandlers.Typed<ContentDescribeModifierParams, ContentDescribeModifierResult>(
                p => DescribeModifier(reader, p)),
            ["content/list_cards"] = WireHandlers.Typed<ContentListCardsParams, ContentListCardsResult>(
                p => ListCards(reader, p)),
            ["content/list_relics"] = WireHandlers.Typed<ContentListRelicsParams, ContentListRelicsResult>(
                p => ListRelics(reader, p)),
            ["content/list_potions"] = WireHandlers.Typed<ContentListPotionsParams, ContentListPotionsResult>(
                p => ListPotions(reader, p)),
            ["content/describe_act"] = WireHandlers.Typed<ContentDescribeActParams, ContentDescribeActResult>(
                p => DescribeAct(reader, getRun, p)),
            ["content/encounter_rules"] = WireHandlers.Typed<JsonNode, ContentEncounterRulesResult>(
                _ => EncounterRules()),
            ["content/unknown_node_odds"] = WireHandlers.Typed<ContentUnknownNodeOddsParams, ContentUnknownNodeOddsResult>(
                p => UnknownNodeOdds(reader, getRun, p)),
            ["content/list_events_for_act"] = WireHandlers.Typed<ContentListEventsForActParams, ContentListEventsForActResult>(
                p => ListEventsForAct(reader, getRun, p)),
            ["content/list_encounters_for_act"] = WireHandlers.Typed<ContentListEncountersForActParams, ContentListEncountersForActResult>(
                p => ListEncountersForAct(reader, getRun, p)),
        };
    }

    // ── describe handlers ─────────────────────────────────────────────

    private static ContentDescribeCardResult DescribeCard(ContentReader reader, ContentDescribeCardParams? p)
    {
        var id = p?.CardId ?? CardId.Unknown;
        var model = reader.FindCard(id);
        var targetTypeStr = reader.ReadString(model, "TargetType");
        var targetType = Enum.TryParse<TargetType>(targetTypeStr, ignoreCase: true, out var t) ? t : TargetType.Unknown;
        return new ContentDescribeCardResult(
            Ok: model is not null,
            CardId: id,
            DisplayName: FirstNonEmpty(reader.ReadString(model, "DisplayName"), reader.ReadString(model, "Name"), id.ToString()),
            Description: FirstNonEmpty(reader.ReadString(model, "Description"), reader.ReadString(model, "DescriptionText")),
            Cost: reader.ReadInt(model, "Cost") ?? 0,
            Rarity: reader.ReadString(model, "Rarity"),
            Character: reader.ReadString(model, "Character"),
            TargetType: targetType,
            Type: reader.ReadString(model, "Type"));
    }

    private static ContentDescribeRelicResult DescribeRelic(ContentReader reader, ContentDescribeRelicParams? p)
    {
        var id = p?.RelicId ?? RelicId.Unknown;
        var model = reader.FindRelic(id);
        return new ContentDescribeRelicResult(
            Ok: model is not null,
            RelicId: id,
            DisplayName: FirstNonEmpty(reader.ReadString(model, "DisplayName"), reader.ReadString(model, "Name"), id.ToString()),
            Description: FirstNonEmpty(reader.ReadString(model, "Description"), reader.ReadString(model, "DescriptionText")),
            Rarity: reader.ReadString(model, "Rarity"));
    }

    private static ContentDescribePotionResult DescribePotion(ContentReader reader, ContentDescribePotionParams? p)
    {
        var id = p?.PotionId ?? PotionId.Unknown;
        var model = reader.FindPotion(id);
        var targetTypeStr = reader.ReadString(model, "TargetType");
        var targetType = Enum.TryParse<TargetType>(targetTypeStr, ignoreCase: true, out var t) ? t : TargetType.Unknown;
        return new ContentDescribePotionResult(
            Ok: model is not null,
            PotionId: id,
            DisplayName: FirstNonEmpty(reader.ReadString(model, "DisplayName"), reader.ReadString(model, "Name"), id.ToString()),
            Description: FirstNonEmpty(reader.ReadString(model, "Description"), reader.ReadString(model, "DescriptionText")),
            Rarity: reader.ReadString(model, "Rarity"),
            TargetType: targetType);
    }

    private static ContentDescribePowerResult DescribePower(ContentReader reader, ContentDescribePowerParams? p)
    {
        var id = p?.PowerId ?? PowerId.Unknown;
        var model = reader.FindPower(id);
        return new ContentDescribePowerResult(
            Ok: model is not null,
            PowerId: id,
            DisplayName: FirstNonEmpty(reader.ReadString(model, "DisplayName"), reader.ReadString(model, "Name"), id.ToString()),
            Description: FirstNonEmpty(reader.ReadString(model, "Description"), reader.ReadString(model, "DescriptionText")),
            IsDebuff: reader.ReadBool(model, "IsDebuff"));
    }

    private static ContentDescribeEventResult DescribeEvent(ContentReader reader, ContentDescribeEventParams? p)
    {
        var eventId = p?.EventId ?? string.Empty;
        var model = reader.FindEvent(eventId);
        return new ContentDescribeEventResult(
            Ok: model is not null,
            EventId: eventId,
            DisplayName: FirstNonEmpty(reader.ReadString(model, "DisplayName"), reader.ReadString(model, "Name"), eventId),
            Description: FirstNonEmpty(reader.ReadString(model, "Description"), reader.ReadString(model, "DescriptionText")));
    }

    private static ContentDescribeEncounterResult DescribeEncounter(ContentReader reader, ContentDescribeEncounterParams? p)
    {
        var encounterId = p?.EncounterId ?? string.Empty;
        var model = reader.FindEncounter(encounterId);
        // Tier inferred from the wire id suffix: SLIMES_WEAK → "WEAK", etc.
        // Keep the canonical engine convention (suffix lives on the id, not
        // on a separate property in every encounter model).
        var tier = encounterId.LastIndexOf('_') is int dash and >= 0
            ? encounterId[(dash + 1)..]
            : string.Empty;
        var monsterWireIds = reader.ReadEncounterMonsterIds(model);
        var monsterIds = monsterWireIds.Select(MonsterIdNames.FromWire).ToList();
        return new ContentDescribeEncounterResult(
            Ok: model is not null,
            EncounterId: encounterId,
            DisplayName: FirstNonEmpty(reader.ReadString(model, "DisplayName"), reader.ReadString(model, "Name"), encounterId),
            MonsterIds: monsterIds,
            Tier: tier);
    }

    private static ContentDescribeMonsterResult DescribeMonster(ContentReader reader, ContentDescribeMonsterParams? p)
    {
        var id = p?.MonsterId ?? MonsterId.Unknown;
        var model = reader.FindMonster(id);
        return new ContentDescribeMonsterResult(
            Ok: model is not null,
            MonsterId: id,
            DisplayName: FirstNonEmpty(reader.ReadString(model, "DisplayName"), reader.ReadString(model, "Name"), id.ToString()),
            BaseHp: reader.ReadInt(model, "BaseHp") ?? reader.ReadInt(model, "BaseHpMin") ?? 0,
            BaseMaxHp: reader.ReadInt(model, "BaseHpMax") ?? reader.ReadInt(model, "BaseMaxHp") ?? reader.ReadInt(model, "BaseHp") ?? 0);
    }

    private static ContentDescribeAfflictionResult DescribeAffliction(ContentReader reader, ContentDescribeAfflictionParams? p)
    {
        var id = p?.AfflictionId ?? AfflictionId.Unknown;
        var model = reader.FindAffliction(id);
        return new ContentDescribeAfflictionResult(
            Ok: model is not null,
            AfflictionId: id,
            DisplayName: FirstNonEmpty(reader.ReadString(model, "DisplayName"), reader.ReadString(model, "Name"), id.ToString()),
            Description: FirstNonEmpty(reader.ReadString(model, "Description"), reader.ReadString(model, "DescriptionText")));
    }

    private static ContentDescribeEnchantmentResult DescribeEnchantment(ContentReader reader, ContentDescribeEnchantmentParams? p)
    {
        var id = p?.EnchantmentId ?? EnchantmentId.Unknown;
        var model = reader.FindEnchantment(id);
        return new ContentDescribeEnchantmentResult(
            Ok: model is not null,
            EnchantmentId: id,
            DisplayName: FirstNonEmpty(reader.ReadString(model, "DisplayName"), reader.ReadString(model, "Name"), id.ToString()),
            Description: FirstNonEmpty(reader.ReadString(model, "Description"), reader.ReadString(model, "DescriptionText")));
    }

    private static ContentDescribeModifierResult DescribeModifier(ContentReader reader, ContentDescribeModifierParams? p)
    {
        var id = p?.ModifierId ?? ModifierId.Unknown;
        var model = reader.FindModifier(id);
        return new ContentDescribeModifierResult(
            Ok: model is not null,
            ModifierId: id,
            DisplayName: FirstNonEmpty(reader.ReadString(model, "DisplayName"), reader.ReadString(model, "Name"), id.ToString()),
            Description: FirstNonEmpty(reader.ReadString(model, "Description"), reader.ReadString(model, "DescriptionText")));
    }

    // ── list handlers ─────────────────────────────────────────────────

    private static ContentListCardsResult ListCards(ContentReader reader, ContentListCardsParams? p)
    {
        var character = p?.Character;
        var rarity = p?.Rarity;
        var includeColorless = p?.IncludeColorless ?? true;
        var characterFilter = character?.ToString();

        var summaries = new List<ContentCardSummary>();
        foreach (var card in reader.AllCards)
        {
            var wireId = reader.ReadEntryId(card);
            if (wireId is null) continue;
            var cardId = CardIdNames.FromWire(wireId);
            var cardChar = reader.ReadString(card, "Character");
            var cardRarity = reader.ReadString(card, "Rarity");

            // Character filter — match Ironclad/Silent/etc. case-
            // insensitively; "colorless" / "shared" pass when
            // includeColorless=true.
            if (characterFilter is not null)
            {
                var isColorless = string.Equals(cardChar, "Colorless", StringComparison.OrdinalIgnoreCase)
                                  || string.Equals(cardChar, "Shared", StringComparison.OrdinalIgnoreCase)
                                  || string.IsNullOrEmpty(cardChar);
                var matchesCharacter = string.Equals(cardChar, characterFilter, StringComparison.OrdinalIgnoreCase);
                if (!matchesCharacter && !(isColorless && includeColorless))
                    continue;
            }
            if (rarity is not null && !string.Equals(cardRarity, rarity, StringComparison.OrdinalIgnoreCase))
                continue;

            summaries.Add(new ContentCardSummary(
                Id: cardId,
                DisplayName: FirstNonEmpty(reader.ReadString(card, "DisplayName"), reader.ReadString(card, "Name"), wireId),
                Rarity: cardRarity,
                Cost: reader.ReadInt(card, "Cost") ?? 0,
                Character: cardChar));
        }
        return new ContentListCardsResult(Ok: true, Count: summaries.Count, Cards: summaries);
    }

    private static ContentListRelicsResult ListRelics(ContentReader reader, ContentListRelicsParams? p)
    {
        var rarity = p?.Rarity;
        var summaries = new List<ContentRelicSummary>();
        foreach (var relic in reader.AllRelics)
        {
            var wireId = reader.ReadEntryId(relic);
            if (wireId is null) continue;
            var rarityStr = reader.ReadString(relic, "Rarity");
            if (rarity is not null && !string.Equals(rarityStr, rarity, StringComparison.OrdinalIgnoreCase))
                continue;
            summaries.Add(new ContentRelicSummary(
                Id: RelicIdNames.FromWire(wireId),
                DisplayName: FirstNonEmpty(reader.ReadString(relic, "DisplayName"), reader.ReadString(relic, "Name"), wireId),
                Rarity: rarityStr));
        }
        return new ContentListRelicsResult(Ok: true, Count: summaries.Count, Relics: summaries);
    }

    private static ContentListPotionsResult ListPotions(ContentReader reader, ContentListPotionsParams? p)
    {
        var rarity = p?.Rarity;
        var summaries = new List<ContentPotionSummary>();
        foreach (var potion in reader.AllPotions)
        {
            var wireId = reader.ReadEntryId(potion);
            if (wireId is null) continue;
            var rarityStr = reader.ReadString(potion, "Rarity");
            if (rarity is not null && !string.Equals(rarityStr, rarity, StringComparison.OrdinalIgnoreCase))
                continue;
            var targetStr = reader.ReadString(potion, "TargetType");
            var target = Enum.TryParse<TargetType>(targetStr, ignoreCase: true, out var t) ? t : TargetType.Unknown;
            summaries.Add(new ContentPotionSummary(
                Id: PotionIdNames.FromWire(wireId),
                DisplayName: FirstNonEmpty(reader.ReadString(potion, "DisplayName"), reader.ReadString(potion, "Name"), wireId),
                Rarity: rarityStr,
                TargetType: target));
        }
        return new ContentListPotionsResult(Ok: true, Count: summaries.Count, Potions: summaries);
    }

    private static ContentDescribeActResult DescribeAct(ContentReader reader, Func<RunHandle?> getRun, ContentDescribeActParams? p)
    {
        var actIndex = p?.ActIndex ?? 0;
        // ActModel instances live on the live RunState — there is no
        // ModelDb.AllActs collection at the current pin. We can only
        // describe an act when the host has an active run *and* the
        // caller asked for the current act index. Out-of-range requests
        // (and pre-run hosts) drop through to the empty/ok=false result.
        var act = reader.FindAct(actIndex, getRun());
        if (act is null)
        {
            return new ContentDescribeActResult(
                Ok: false,
                ActIndex: actIndex,
                NumFloors: 0, NumRooms: 0,
                NumberOfWeakEncounters: 0, EliteRollCount: 15,
                WeakEncounterPool: Array.Empty<string>(),
                NormalEncounterPool: Array.Empty<string>(),
                ElitePool: Array.Empty<string>(),
                BossPool: Array.Empty<string>(),
                EventPool: Array.Empty<string>());
        }

        return new ContentDescribeActResult(
            Ok: true,
            ActIndex: actIndex,
            NumFloors: reader.ReadInt(act, "NumFloors") ?? 0,
            NumRooms: reader.ReadInt(act, "NumRooms") ?? 0,
            NumberOfWeakEncounters: reader.ReadInt(act, "NumberOfWeakEncounters") ?? 0,
            // EliteRollCount is the literal 15 in ActModel.GenerateRooms IL
            // at the current pin — no per-act property exposes it.
            EliteRollCount: 15,
            WeakEncounterPool: reader.ReadIdList(act, "AllWeakEncounters"),
            NormalEncounterPool: reader.ReadIdList(act, "AllRegularEncounters"),
            ElitePool: reader.ReadIdList(act, "AllEliteEncounters"),
            BossPool: reader.ReadIdList(act, "AllBossEncounters"),
            EventPool: reader.ReadIdList(act, "AllEvents"));
    }

    private static ContentEncounterRulesResult EncounterRules() => new(
        Ok: true,
        WeakEncountersFirst: true,
        EliteRollCount: 15,
        NoAdjacentSharedTags: true,
        Notes:
            "The engine builds each act's encounter sequence in three passes from disjoint pools: " +
            "first `NumberOfWeakEncounters` from `AllWeakEncounters` (no boss-tagged ids), then " +
            "regulars from `AllRegularEncounters` until the room count is reached, then 15 elites " +
            "pre-rolled from `AllEliteEncounters`. Within each pass the engine refuses to draw a " +
            "candidate that shares any tags with the previously drawn entry (AddWithoutRepeatingTags). " +
            "Players see the boss as a fixed icon at the top of the act map (one of `AllBossEncounters`), " +
            "but the specific monster slot HP / intent rolls happen at combat-start " +
            "(EncounterModel.GenerateMonstersWithSlots) and are not part of the schedule.");

    private static ContentUnknownNodeOddsResult UnknownNodeOdds(ContentReader reader, Func<RunHandle?> getRun, ContentUnknownNodeOddsParams? p)
    {
        var actIndex = p?.ActIndex;

        // Preferred path: read the live RunState's
        // `Odds.UnknownMapPoint._baseOdds` directly. The engine populates
        // it from `UnknownMapPointOdds.SetBaseOdds` (DeadlyEvents and
        // future modifiers may also mutate it post-run-start), so this
        // reflects whatever priors are currently in effect, not just the
        // shipped defaults.
        var handle = getRun();
        if (handle is not null)
        {
            var live = reader.ReadUnknownNodeOdds(handle);
            if (live is { Count: > 0 })
            {
                var rows = live
                    .Select(t => new ContentUnknownNodeOddsRow(t.RoomType, t.Weight))
                    .ToList();
                return new ContentUnknownNodeOddsResult(Ok: true, ActIndex: actIndex, BaseOdds: rows);
            }
        }

        // Fallback: a host without an active run still answers usefully
        // by surfacing the documented base distribution from the
        // wuhao21/sts2-cli research notes. These match the engine's
        // shipped `SetBaseOdds` defaults at the current pin (heavy on
        // events, light on combat). Run-time modifiers can move them —
        // a caller that needs the *actual* priors should run/new first.
        var odds = new[]
        {
            new ContentUnknownNodeOddsRow(RoomType.CombatRoom, 0.10),
            new ContentUnknownNodeOddsRow(RoomType.EventRoom, 0.72),
            new ContentUnknownNodeOddsRow(RoomType.TreasureRoom, 0.02),
            new ContentUnknownNodeOddsRow(RoomType.MerchantRoom, 0.03),
            new ContentUnknownNodeOddsRow(RoomType.RestSiteRoom, 0.13),
        };
        return new ContentUnknownNodeOddsResult(Ok: true, ActIndex: actIndex, BaseOdds: odds);
    }

    private static ContentListEventsForActResult ListEventsForAct(ContentReader reader, Func<RunHandle?> getRun, ContentListEventsForActParams? p)
    {
        var actIndex = p?.ActIndex ?? 0;
        var act = reader.FindAct(actIndex, getRun());
        if (act is null)
        {
            return new ContentListEventsForActResult(
                Ok: false,
                ActIndex: actIndex,
                Events: Array.Empty<ContentEventForActSummary>());
        }

        // Per-act event ids from ActModel.AllEvents, plus the shared event
        // pool from ModelDb.AllSharedEvents (which the engine draws from in
        // every act in GenerateRooms).
        var actEventIds = reader.ReadIdList(act, "AllEvents");
        var sharedEventIds = reader.AllSharedEvents
            .Select(reader.ReadEntryId)
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id!)
            .ToList();

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var events = new List<ContentEventForActSummary>();
        foreach (var id in actEventIds.Concat(sharedEventIds))
        {
            if (string.IsNullOrEmpty(id) || !seen.Add(id)) continue;
            var model = reader.FindEvent(id);
            events.Add(new ContentEventForActSummary(
                Id: id,
                DisplayName: FirstNonEmpty(reader.ReadString(model, "DisplayName"), reader.ReadString(model, "Name"), id)));
        }

        return new ContentListEventsForActResult(
            Ok: true,
            ActIndex: actIndex,
            Events: events);
    }

    private static ContentListEncountersForActResult ListEncountersForAct(ContentReader reader, Func<RunHandle?> getRun, ContentListEncountersForActParams? p)
    {
        var actIndex = p?.ActIndex ?? 0;
        var tierFilter = p?.Tier;
        var act = reader.FindAct(actIndex, getRun());
        if (act is null)
        {
            return new ContentListEncountersForActResult(
                Ok: false,
                ActIndex: actIndex,
                Encounters: Array.Empty<ContentEncounterForActSummary>());
        }

        // (sourceProp, tier) tuples — tier is inferred from which property
        // the encounter came from, not from id suffix parsing.
        var pools = new (string Prop, EncounterTier Tier)[]
        {
            ("AllWeakEncounters", EncounterTier.Weak),
            ("AllRegularEncounters", EncounterTier.Normal),
            ("AllEliteEncounters", EncounterTier.Elite),
            ("AllBossEncounters", EncounterTier.Boss),
        };

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var encounters = new List<ContentEncounterForActSummary>();
        foreach (var (prop, tier) in pools)
        {
            if (tierFilter is not null && tierFilter != EncounterTier.Unknown && tier != tierFilter)
                continue;
            foreach (var id in reader.ReadIdList(act, prop))
            {
                if (string.IsNullOrEmpty(id) || !seen.Add(id)) continue;
                var model = reader.FindEncounter(id);
                var monsterWireIds = reader.ReadEncounterMonsterIds(model);
                var monsterIds = monsterWireIds.Select(MonsterIdNames.FromWire).ToList();
                encounters.Add(new ContentEncounterForActSummary(
                    Id: id,
                    DisplayName: FirstNonEmpty(reader.ReadString(model, "DisplayName"), reader.ReadString(model, "Name"), id),
                    Tier: tier,
                    MonsterIds: monsterIds));
            }
        }

        return new ContentListEncountersForActResult(
            Ok: true,
            ActIndex: actIndex,
            Encounters: encounters);
    }

    // ── helpers ───────────────────────────────────────────────────────

    private static string FirstNonEmpty(params string?[] candidates)
    {
        foreach (var c in candidates)
        {
            if (!string.IsNullOrEmpty(c)) return c!;
        }
        return string.Empty;
    }
}
