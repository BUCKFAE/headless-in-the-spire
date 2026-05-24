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
        // 1. Cards in our A/S tier — clear win and we always know what we're getting.
        foreach (var item in items)
        {
            if (item.Kind != MerchantKind.Card) continue;
            if (!item.IsStocked || !item.IsAffordable) continue;
            if (item.CardId is null) continue;
            if (!IsHighTierMerchantCard(item.CardId)) continue;
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

    // High-tier cards worth shop gold for Ironclad. Mirrors the
    // DraftPolicy's S/A tier names (kept as strings here because
    // MerchantItem.CardId is the wire string id, not the typed CardId).
    private static bool IsHighTierMerchantCard(string id) => id switch
    {
        "DEMON_FORM"
            or "BARRICADE"
            or "BLUDGEON"
            or "CORRUPTION"
            or "INFLAME"
            or "FEEL_NO_PAIN"
            or "DARK_EMBRACE"
            or "POMMEL_STRIKE"
            or "SHRUG_IT_OFF"
            or "IMPERVIOUS"
            or "OFFERING"
            or "JUGGERNAUT"
            or "WHIRLWIND"
            or "TREMBLE"
            or "BULLY"
            or "DISMANTLE"
            or "PACTS_END" => true,
        _ => false,
    };
}
