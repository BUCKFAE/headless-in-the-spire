using Sts2Headless.Agents.Contracts;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.BattleAgent;

// Buy useful items at the merchant. Priority order:
//   1. Card removal — always-take if we still have starter Strikes/Defends
//      in the deck (DeckSize is read from RunStateResult).
//   2. Relics — always take any affordable relic.
//   3. Key archetype cards (matching the DraftPolicy's S/A tier list).
//   4. Potions — only when we have a free belt slot.
//
// Stops shopping once we run out of affordable items or once gold drops
// below a small reserve (we want some gold for Act 2 shops too, but only
// in Act 1).
public sealed class IroncladMerchantPolicy : IMerchantPolicy
{
    public AgentAction Choose(RunStateResult state)
    {
        var items = state.AvailableMerchantItems;
        if (items.Count == 0) return new LeaveMerchantRoom();

        var pick = SelectBest(state, items);
        return pick is not null ? new BuyMerchantItem(pick.Index) : new LeaveMerchantRoom();
    }

    private static MerchantItem? SelectBest(RunStateResult state, IReadOnlyList<MerchantItem> items)
    {
        // 1a. Boss-counter cards — if the act boss is known and the
        //     shop offers a card on its counter list, take it first.
        //     Same trap-only philosophy as BossDraftBias: only force
        //     a buy when the card *specifically* counters this boss
        //     (Whirlwind / Twin Strike vs Vantom, Whirlwind / Inferno
        //     vs Kin, Inflame / Tremble vs Beast).
        if (state.BossEncounterId is { } bossId)
        {
            foreach (var item in items)
            {
                if (item.Kind != MerchantKind.Card) continue;
                if (!item.IsStocked || !item.IsAffordable) continue;
                if (item.CardId is not CardId cId) continue;
                if (!IsBossCounter(bossId, cId)) continue;
                if (state.Gold - item.Cost < 30) continue;
                return item;
            }
        }

        // 1b. Cards in our A/S tier — clear win and we always know what we're getting.
        foreach (var item in items)
        {
            if (item.Kind != MerchantKind.Card) continue;
            if (!item.IsStocked || !item.IsAffordable) continue;
            if (item.CardId is not CardId cId2) continue;
            if (!IsHighTierMerchantCard(cId2)) continue;
            if (state.Gold - item.Cost < 30) continue;  // leave reserve
            return item;
        }

        // 2. Card removal — strong priority once we have non-starter cards
        //    to be selective about. Skip if deck still tiny (we won't have
        //    enough non-starters to choose from).
        var removal = FirstAffordable(items, MerchantKind.CardRemoval);
        if (removal is not null && state.DeckSize >= 12
            && state.Gold - removal.Cost >= 30)
        {
            return removal;
        }

        // 3. Relics — DISABLED for now. The merchant relic pool can include
        //    devastating choices like Ectoplasm (locks gold) or
        //    Cursed-pool relics. Without a relic-quality whitelist we burned
        //    a 5/50 → 4/50 Act 1 win-rate by greedy-buying relics. Re-enable
        //    once we have a relic whitelist.

        // 4. Potions — only when belt has room and we have a comfortable
        //    gold reserve. Pre-boss potion stockpiling is valuable.
        if ((state.OwnedPotions?.Count ?? 0) < 3)
        {
            var potion = FirstAffordable(items, MerchantKind.Potion);
            if (potion is not null && state.Gold - potion.Cost >= 100)
                return potion;
        }

        return null;
    }

    private static MerchantItem? FirstAffordable(IReadOnlyList<MerchantItem> items, MerchantKind kind)
    {
        foreach (var item in items)
        {
            if (item.Kind == kind && item.IsStocked && item.IsAffordable)
                return item;
        }
        return null;
    }

    // Specific counter-cards per Act-1 boss. Same source as
    // BossDraftBias (research-act1-bosses.md §5). Only cards that are
    // *strict* counters for the boss — every entry here is also at
    // A or B tier in the general draft policy, so we're not pulling
    // garbage just because the boss is bad news. bossId is still the
    // wire-shape string (RunStateResult.BossEncounterId hasn't migrated
    // to a typed enum yet); cardId is the typed CardId.
    private static bool IsBossCounter(string bossId, CardId cardId) => bossId switch
    {
        // Beast: Strength + Vulnerable cluster. 252-HP HP race.
        "CEREMONIAL_BEAST_BOSS" => cardId switch
        {
            CardId.Inflame or CardId.Tremble or CardId.Bash or CardId.Bully
                or CardId.Dismantle or CardId.DemonForm => true,
            _ => false,
        },
        // Vantom: multi-hit clears 9 Slippery. Whirlwind > all.
        "VANTOM_BOSS" => cardId switch
        {
            CardId.Whirlwind or CardId.TwinStrike or CardId.PommelStrike
                or CardId.Anger or CardId.Hellraiser or CardId.SwordBoomerang => true,
            _ => false,
        },
        // Kin: AoE or single-target rush on Priest with Vuln.
        "THE_KIN_BOSS" => cardId switch
        {
            CardId.Whirlwind or CardId.Inferno or CardId.PactsEnd
                or CardId.Tremble or CardId.Inflame or CardId.Corruption => true,
            _ => false,
        },
        _ => false,
    };

    // High-tier cards worth shop gold for Ironclad. Mirrors the
    // DraftPolicy's S/A tier names.
    private static bool IsHighTierMerchantCard(CardId id) => id switch
    {
        CardId.DemonForm
            or CardId.Barricade
            or CardId.Bludgeon
            or CardId.Corruption
            or CardId.Inflame
            or CardId.FeelNoPain
            or CardId.DarkEmbrace
            or CardId.PommelStrike
            or CardId.ShrugItOff
            or CardId.Impervious
            or CardId.Offering
            or CardId.Juggernaut
            or CardId.Whirlwind
            or CardId.Tremble
            or CardId.Bully
            or CardId.Dismantle
            or CardId.PactsEnd => true,
        _ => false,
    };
}
