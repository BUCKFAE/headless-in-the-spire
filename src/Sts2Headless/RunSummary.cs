using System.Text;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless;

// Renders a RunStateResult-shaped snapshot as a compact multi-line
// plain-text summary. Ported from the Python client's summary.py so the
// wire is now the single source of truth for what `summarize_state`
// looks like — every consumer renders identical text.
//
// Read-only by design. The summary describes *what is*, never what the
// agent should do. Picking the next action is the caller's job; this
// helper just lays out the legal options.
internal static class RunSummary
{
    public static string Render(RunStateResult state)
    {
        var sb = new StringBuilder();
        WriteHeader(sb, state);
        WriteVitals(sb, state);
        if (state.CombatState is { IsInProgress: true } combat)
        {
            WriteCombat(sb, combat);
        }
        WritePhaseOptions(sb, state);
        WriteRelics(sb, state.Relics);
        WritePotions(sb, state.OwnedPotions);
        return sb.ToString().TrimEnd('\n');
    }

    private static void WriteHeader(StringBuilder sb, RunStateResult state)
    {
        var hint = string.Empty;
        if (state.IsGameOver) hint = state.IsVictory ? " — VICTORY" : " — DEFEAT";
        sb.Append($"Act {state.CurrentActIndex} Floor {state.ActFloor} — {state.CurrentRoomType}{hint}\n");
    }

    private static void WriteVitals(StringBuilder sb, RunStateResult state)
    {
        sb.Append($"HP {state.Hp}/{state.MaxHp} | Gold {state.Gold} | Deck {state.DeckSize}\n");
    }

    private static void WriteCombat(StringBuilder sb, CombatState combat)
    {
        sb.Append($"Round {combat.Round} | Energy {combat.Energy}/{combat.MaxEnergy} | ")
          .Append($"Block {combat.PlayerBlock} | Draw {combat.DrawPileCount} | ")
          .Append($"Discard {combat.DiscardPileCount}\n");
        if (combat.PlayerPowers.Count > 0)
        {
            sb.Append($"Player powers: {Powers(combat.PlayerPowers)}\n");
        }
        sb.Append("\nHand:\n");
        if (combat.Hand.Count == 0) sb.Append("  (empty)\n");
        else foreach (var c in combat.Hand) sb.Append($"  {CardLine(c)}\n");
        sb.Append("\nEnemies:\n");
        if (combat.Enemies.Count == 0) sb.Append("  (none)\n");
        else foreach (var e in combat.Enemies) sb.Append($"  {EnemyLine(e)}\n");
    }

    private static void WritePhaseOptions(StringBuilder sb, RunStateResult state)
    {
        // Rewards may be available concurrently with a finished combat —
        // show them whenever they exist, regardless of room.
        if (state.RewardsState is { Available: { Count: > 0 } } rewards)
        {
            sb.Append("\nRewards:\n");
            foreach (var r in rewards.Available)
            {
                var label = r.Kind.ToString();
                if (r.GoldAmount is int g) label += $" ({g} gold)";
                if (r.Cards is { Count: > 0 } cards)
                    label += ": " + string.Join(", ", cards.Select(c => c.Id.ToString()));
                if (r.RelicId is RelicId rel) label += $": {rel}";
                if (r.PotionId is PotionId pot) label += $": {pot}";
                var skip = r.CanSkip ? string.Empty : " (cannot skip)";
                sb.Append($"  [{r.Index}] {label}{skip}\n");
            }
            return;
        }
        if (state.CombatState is { IsInProgress: true })
        {
            // In-combat decisions are already covered by the hand +
            // enemies block; no extra options list to print.
            return;
        }
        if (state.AvailableMapNodes.Count > 0)
        {
            sb.Append("\nMap options:\n");
            foreach (var n in state.AvailableMapNodes)
                sb.Append($"  col={n.Col} row={n.Row} type={n.Type}\n");
            return;
        }
        if (state.AvailableEventOptions.Count > 0)
        {
            sb.Append("\nEvent options:\n");
            foreach (var o in state.AvailableEventOptions)
            {
                var locked = o.IsLocked ? " (locked)" : string.Empty;
                sb.Append($"  [{o.Index}] {o.TextKey ?? "?"}{locked}\n");
            }
            return;
        }
        if (state.AvailableRestSiteOptions.Count > 0)
        {
            sb.Append("\nRest-site options:\n");
            foreach (var o in state.AvailableRestSiteOptions)
            {
                var disabled = o.IsEnabled ? string.Empty : " (disabled)";
                sb.Append($"  [{o.Index}] {o.OptionId}{disabled}\n");
            }
            return;
        }
        if (state.AvailableMerchantItems.Count > 0)
        {
            sb.Append("\nMerchant inventory:\n");
            foreach (var item in state.AvailableMerchantItems)
            {
                string label = item.CardId?.ToString()
                            ?? item.RelicId?.ToString()
                            ?? item.PotionId?.ToString()
                            ?? "?";
                var stocked = item.IsStocked ? string.Empty : " (out of stock)";
                var affordable = item.IsAffordable ? string.Empty : " (cannot afford)";
                sb.Append($"  [{item.Index}] {item.Kind}: {label} — {item.Cost} gold{stocked}{affordable}\n");
            }
            return;
        }
        if (state.CurrentRoomType == RoomType.TreasureRoom)
        {
            if (state.AvailableTreasureRelics.Count > 0)
            {
                var offering = string.Join(", ", state.AvailableTreasureRelics.Select(r => r.RelicId.ToString()));
                sb.Append($"\nTreasure room — chest offering: {offering}.\n")
                  .Append("  Call `run/take_treasure` to claim the offered relic, or `run/skip_treasure` to walk past.\n");
            }
            else
            {
                sb.Append("\nTreasure room: call `run/take_treasure` to open the chest (or `run/skip_treasure` to walk past).\n");
            }
            return;
        }
        if (state.CurrentRoomType == RoomType.MerchantRoom)
        {
            sb.Append("\nMerchant room: nothing to buy; call `run/leave_merchant_room`.\n");
        }
    }

    private static void WriteRelics(StringBuilder sb, IReadOnlyList<Relic> relics)
    {
        if (relics.Count == 0) return;
        sb.Append($"\nRelics: {string.Join(", ", relics.Select(r => r.Id.ToString()))}\n");
    }

    private static void WritePotions(StringBuilder sb, IReadOnlyList<OwnedPotion> potions)
    {
        if (potions.Count == 0) return;
        sb.Append($"Potions: {string.Join(", ", potions.Select(p => $"[{p.Index}] {p.Id}"))}\n");
    }

    private static string CardLine(Card card)
    {
        var pieces = new List<string> { $"[{card.Index}] {card.Id} (cost {card.Cost})", $"→ {card.TargetType}" };
        if (!card.CanPlay) pieces.Add("(cannot play)");
        return string.Join(" ", pieces);
    }

    private static string EnemyLine(Enemy enemy)
    {
        var block = enemy.Block > 0 ? $" block {enemy.Block}" : string.Empty;
        var intent = IntentSummary(enemy.Intents);
        var powers = enemy.Powers.Count > 0 ? $" — powers: {Powers(enemy.Powers)}" : string.Empty;
        return $"[{enemy.Index}] {enemy.MonsterId} HP {enemy.Hp}/{enemy.MaxHp}{block} — intends {intent}{powers}";
    }

    private static string IntentSummary(IReadOnlyList<Intent> intents)
    {
        if (intents.Count == 0) return "?";
        return string.Join(", ", intents.Select(FormatOneIntent));
    }

    private static string FormatOneIntent(Intent intent)
    {
        switch (intent.Kind)
        {
            case IntentKind.Attack:
            case IntentKind.AttackBuff:
            case IntentKind.AttackDebuff:
            {
                var dmg = intent.Damage ?? 0;
                var hits = intent.Hits ?? 1;
                var suffix = hits > 1 ? $" x{hits}" : string.Empty;
                return $"{intent.Kind} {dmg}{suffix}";
            }
            case IntentKind.Defend:
                return $"Defend {intent.Block ?? 0}";
            case IntentKind.AttackDefend:
                return $"AttackDefend {intent.Damage ?? 0}/{intent.Block ?? 0}";
            default:
                return intent.Kind.ToString();
        }
    }

    private static string Powers(IReadOnlyList<Power> powers)
    {
        if (powers.Count == 0) return "(none)";
        return string.Join(", ", powers.Select(p => $"{p.Id}({p.Amount})"));
    }
}
