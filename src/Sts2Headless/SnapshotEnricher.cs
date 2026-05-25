using Sts2Headless.Content;
using Sts2Headless.Protocol.Methods;
using Sts2Headless.Runtime.Bindings;

namespace Sts2Headless;

// Walk a RunSnapshot and fill every inline DisplayName field via the
// shared NameLookup. The Runtime.Bindings layer emits records with
// empty-string DisplayNames (the wire's "unset" sentinel); the host
// owns the enrichment because it sits at the only layer that has both
// the snapshot and the ContentReader. Runtime.Bindings → Content would
// be a circular project reference.
//
// All projections are pure: a record `with` rebuild costs one
// allocation per item, but the items themselves are small (~50 bytes)
// and the snapshot path runs once per wire response, so this is
// well below noise compared to the reflection cost of building the
// snapshot in the first place.
//
// Missing names fall through as empty strings — callers (and the
// summary renderer) treat "" as "not available" and skip the label.
internal static class SnapshotEnricher
{
    public static RunSnapshot WithDisplayNames(this RunSnapshot s, NameLookup names) => s with
    {
        Relics = EnrichRelics(s.Relics, names),
        OwnedPotions = EnrichOwnedPotions(s.OwnedPotions, names),
        AvailableTreasureRelics = EnrichTreasureRelics(s.AvailableTreasureRelics, names),
        AvailableMerchantItems = EnrichMerchantItems(s.AvailableMerchantItems, names),
        CombatState = EnrichCombatState(s.CombatState, names),
        RewardsState = EnrichRewardsState(s.RewardsState, names),
    };

    private static IReadOnlyList<Relic> EnrichRelics(IReadOnlyList<Relic> relics, NameLookup names)
    {
        if (relics.Count == 0) return relics;
        var result = new List<Relic>(relics.Count);
        foreach (var r in relics) result.Add(r with { DisplayName = names.Relic(r.Id) });
        return result;
    }

    private static IReadOnlyList<OwnedPotion> EnrichOwnedPotions(IReadOnlyList<OwnedPotion> potions, NameLookup names)
    {
        if (potions.Count == 0) return potions;
        var result = new List<OwnedPotion>(potions.Count);
        foreach (var p in potions) result.Add(p with { DisplayName = names.Potion(p.Id) });
        return result;
    }

    private static IReadOnlyList<TreasureRelic> EnrichTreasureRelics(IReadOnlyList<TreasureRelic> relics, NameLookup names)
    {
        if (relics.Count == 0) return relics;
        var result = new List<TreasureRelic>(relics.Count);
        foreach (var r in relics) result.Add(r with { DisplayName = names.Relic(r.RelicId) });
        return result;
    }

    private static IReadOnlyList<MerchantItem> EnrichMerchantItems(IReadOnlyList<MerchantItem> items, NameLookup names)
    {
        if (items.Count == 0) return items;
        var result = new List<MerchantItem>(items.Count);
        foreach (var item in items)
        {
            // Per-item displayName is the matching kind's label. CardRemoval
            // has no id (it's a service); leave its name empty.
            var name = item.CardId is { } c
                ? names.Card(c)
                : item.RelicId is { } r
                    ? names.Relic(r)
                    : item.PotionId is { } p
                        ? names.Potion(p)
                        : string.Empty;
            result.Add(item with { DisplayName = name });
        }
        return result;
    }

    private static CombatState? EnrichCombatState(CombatState? combat, NameLookup names)
    {
        if (combat is null) return null;
        return combat with
        {
            Hand = EnrichHand(combat.Hand, names),
            Enemies = EnrichEnemies(combat.Enemies, names),
            PlayerPowers = EnrichPowers(combat.PlayerPowers, names),
        };
    }

    private static IReadOnlyList<Card> EnrichHand(IReadOnlyList<Card> hand, NameLookup names)
    {
        if (hand.Count == 0) return hand;
        var result = new List<Card>(hand.Count);
        foreach (var c in hand) result.Add(c with { DisplayName = names.Card(c.Id) });
        return result;
    }

    private static IReadOnlyList<Enemy> EnrichEnemies(IReadOnlyList<Enemy> enemies, NameLookup names)
    {
        if (enemies.Count == 0) return enemies;
        var result = new List<Enemy>(enemies.Count);
        foreach (var e in enemies)
        {
            result.Add(e with
            {
                DisplayName = names.Monster(e.MonsterId),
                Powers = EnrichPowers(e.Powers, names),
            });
        }
        return result;
    }

    private static IReadOnlyList<Power> EnrichPowers(IReadOnlyList<Power> powers, NameLookup names)
    {
        if (powers.Count == 0) return powers;
        var result = new List<Power>(powers.Count);
        foreach (var p in powers) result.Add(p with { DisplayName = names.Power(p.Id) });
        return result;
    }

    private static RewardsState? EnrichRewardsState(RewardsState? rewards, NameLookup names)
    {
        if (rewards is null) return null;
        if (rewards.Available.Count == 0) return rewards;
        var result = new List<RewardOption>(rewards.Available.Count);
        foreach (var opt in rewards.Available)
        {
            var name = opt.RelicId is { } r
                ? names.Relic(r)
                : opt.PotionId is { } p
                    ? names.Potion(p)
                    : string.Empty;
            var cards = opt.Cards is null
                ? null
                : EnrichCardRewards(opt.Cards, names);
            result.Add(opt with { DisplayName = name, Cards = cards });
        }
        return rewards with { Available = result };
    }

    private static IReadOnlyList<CardRewardOption> EnrichCardRewards(IReadOnlyList<CardRewardOption> cards, NameLookup names)
    {
        if (cards.Count == 0) return cards;
        var result = new List<CardRewardOption>(cards.Count);
        foreach (var c in cards) result.Add(c with { DisplayName = names.Card(c.Id) });
        return result;
    }
}
