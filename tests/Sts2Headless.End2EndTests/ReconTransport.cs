using System.Text;
using Sts2Headless.Agents;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.End2EndTests;

// Records a structured markdown trace of every wire call during a run.
// Heavier than LoggingTransport — emits combat hand contents, full enemy
// intents, reward menus, event option picks, and per-step HP — to ground
// agent-development work in concrete observations rather than guesses.
//
// Used by Seed42ReconTests to produce documentation/research/seed42-recon.md.
public sealed class ReconTransport(ITransport inner) : ITransport
{
    private readonly StringBuilder _md = new();
    private int _lastFloor = -1;
    private RoomType _lastRoom = RoomType.Unknown;
    private int _combatRound = -1;
    private int _floorCombatCounter = 0;
    private string _lastPotionDigest = "";

    public string Markdown => _md.ToString();

    public async Task<TResult> SendAsync<TResult>(string method, object? @params = null)
    {
        var result = await inner.SendAsync<TResult>(method, @params);
        Record(method, @params, result);
        return result;
    }

    private void Record<T>(string method, object? @params, T result)
    {
        (RoomType room, int hp, int maxHp, int floor, bool gameOver, CombatState? combat, RewardsState? rewards, IReadOnlyList<Relic>? relics, IReadOnlyList<MapNode>? nodes, IReadOnlyList<EventOption>? events, IReadOnlyList<OwnedPotion>? potions)
            = result switch
        {
            RunStateResult s => (s.CurrentRoomType, s.Hp, s.MaxHp, s.ActFloor, s.IsGameOver, s.CombatState, s.RewardsState, s.Relics, s.AvailableMapNodes, s.AvailableEventOptions, s.OwnedPotions),
            RunNewResult n => (n.CurrentRoomType, -1, -1, 1, false, n.CombatState, n.RewardsState, n.Relics, n.AvailableMapNodes, n.AvailableEventOptions, n.OwnedPotions),
            RunSelectMapNodeResult m => (m.CurrentRoomType, m.Hp, -1, m.ActFloor, m.IsGameOver, m.CombatState, m.RewardsState, m.Relics, m.AvailableMapNodes, m.AvailableEventOptions, m.OwnedPotions),
            RunSelectEventOptionResult eo => (eo.CurrentRoomType, eo.Hp, -1, eo.ActFloor, eo.IsGameOver, eo.CombatState, eo.RewardsState, eo.Relics, eo.AvailableMapNodes, eo.AvailableEventOptions, eo.OwnedPotions),
            RunPlayCardResult pc => (pc.CurrentRoomType, pc.Hp, -1, pc.ActFloor, pc.IsGameOver, pc.CombatState, pc.RewardsState, pc.Relics, pc.AvailableMapNodes, pc.AvailableEventOptions, pc.OwnedPotions),
            RunEndTurnResult et => (et.CurrentRoomType, et.Hp, -1, et.ActFloor, et.IsGameOver, et.CombatState, et.RewardsState, et.Relics, et.AvailableMapNodes, et.AvailableEventOptions, et.OwnedPotions),
            RunSelectRewardResult sr => (sr.CurrentRoomType, sr.Hp, -1, sr.ActFloor, sr.IsGameOver, sr.CombatState, sr.RewardsState, sr.Relics, sr.AvailableMapNodes, sr.AvailableEventOptions, sr.OwnedPotions),
            RunSkipRewardResult sk => (sk.CurrentRoomType, sk.Hp, -1, sk.ActFloor, sk.IsGameOver, sk.CombatState, sk.RewardsState, sk.Relics, sk.AvailableMapNodes, sk.AvailableEventOptions, sk.OwnedPotions),
            RunSelectRestSiteOptionResult rs => (rs.CurrentRoomType, rs.Hp, -1, rs.ActFloor, rs.IsGameOver, rs.CombatState, rs.RewardsState, rs.Relics, rs.AvailableMapNodes, rs.AvailableEventOptions, rs.OwnedPotions),
            RunLeaveTreasureRoomResult lt => (lt.CurrentRoomType, lt.Hp, -1, lt.ActFloor, lt.IsGameOver, lt.CombatState, lt.RewardsState, lt.Relics, lt.AvailableMapNodes, lt.AvailableEventOptions, lt.OwnedPotions),
            RunLeaveMerchantRoomResult lm => (lm.CurrentRoomType, lm.Hp, -1, lm.ActFloor, lm.IsGameOver, lm.CombatState, lm.RewardsState, lm.Relics, lm.AvailableMapNodes, lm.AvailableEventOptions, lm.OwnedPotions),
            RunUsePotionResult up => (up.CurrentRoomType, up.Hp, -1, up.ActFloor, up.IsGameOver, up.CombatState, up.RewardsState, up.Relics, up.AvailableMapNodes, up.AvailableEventOptions, up.OwnedPotions),
            DebugSetHpResult hp2 => (RoomType.Unknown, hp2.Hp, hp2.MaxHp, -1, hp2.IsGameOver, null, null, null, null, null, null),
            _ => (RoomType.Unknown, -2, -2, -2, false, null, null, null, null, null, null),
        };

        // run/new — opening section.
        if (result is RunNewResult nr)
        {
            _md.AppendLine("# Seed 42 Recon — Ironclad");
            _md.AppendLine();
            _md.AppendLine($"- character: `{nr.Character}`");
            _md.AppendLine($"- seed: `{nr.Seed}`");
            _md.AppendLine($"- starting relics: {FormatRelics(nr.Relics)}");
            _md.AppendLine();
            return;
        }

        // debug/set_hp — annotate inline; no section flip.
        if (result is DebugSetHpResult dh)
        {
            _md.AppendLine($"  - heal → hp={dh.Hp}/{dh.MaxHp}");
            _md.AppendLine();
            return;
        }

        // New floor or new room? Start a section.
        if (floor != _lastFloor || (room != _lastRoom && room != RoomType.Unknown))
        {
            if (floor != _lastFloor) { _floorCombatCounter = 0; _combatRound = -1; }
            _md.AppendLine($"## Floor {floor}: {room}  (hp={hp}{(maxHp > 0 ? "/" + maxHp : "")})");
            _md.AppendLine();
            if (room == RoomType.MapRoom && nodes is { Count: > 0 })
            {
                _md.AppendLine($"  - map options: {string.Join(", ", nodes.Select(n => $"({n.Col},{n.Row}):{n.Type}"))}");
            }
            if (room == RoomType.EventRoom && events is { Count: > 0 })
            {
                _md.AppendLine("  - event options:");
                foreach (var o in events) _md.AppendLine($"    - [{o.Index}{(o.IsLocked ? " locked" : "")}] `{o.TextKey}`");
            }
            _md.AppendLine();
            _lastFloor = floor;
            _lastRoom = room;
        }

        // Method-specific annotations: capture the input decision so the
        // recon shows "agent picked option N", not just the resulting state.
        switch (method)
        {
            case "run/select_map_node" when @params is RunSelectMapNodeParams smnp:
                _md.AppendLine($"  → pick map ({smnp.Col},{smnp.Row}) → {room} floor={floor}");
                break;
            case "run/select_event_option" when @params is RunSelectEventOptionParams seop:
                _md.AppendLine($"  → pick event option [{seop.OptionIndex}] → {room} hp={hp}");
                break;
            case "run/select_rest_site_option" when @params is RunSelectRestSiteOptionParams srsop:
                _md.AppendLine($"  → pick rest option [{srsop.OptionIndex}] → {room} hp={hp}");
                break;
            case "run/select_reward" when @params is RunSelectRewardParams srp:
                _md.AppendLine($"  → claim reward [{srp.RewardIndex}]{(srp.CardIndex is null ? "" : $" card={srp.CardIndex}")} → hp={hp} room={room}");
                break;
            case "run/skip_reward" when @params is RunSkipRewardParams sxp:
                _md.AppendLine($"  → skip reward [{sxp.RewardIndex}] → hp={hp} room={room}");
                break;
            case "run/play_card" when @params is RunPlayCardParams rpcp:
                _md.AppendLine($"  → play card [{rpcp.CardIndex}]{(rpcp.TargetIndex is null ? "" : $" target={rpcp.TargetIndex}")}");
                break;
            case "run/use_potion" when @params is RunUsePotionParams rupp:
                _md.AppendLine($"  → use potion [{rupp.PotionIndex}]{(rupp.TargetIndex is null ? "" : $" target={rupp.TargetIndex}")}");
                break;
            case "run/end_turn":
                _md.AppendLine($"  → end_turn → round transition");
                break;
            case "run/leave_treasure_room":
                _md.AppendLine($"  → leave treasure → {room} relics={FormatRelics(relics)}");
                break;
            case "run/leave_merchant_room":
                _md.AppendLine($"  → leave merchant → {room}");
                break;
        }

        // Diagnostic: any call that lands in a CombatRoom/BossRoom but has no
        // CombatState is a wire-plumbing surprise worth surfacing.
        if ((room == RoomType.CombatRoom || room == RoomType.BossRoom) && combat is null)
        {
            _md.AppendLine($"  [DIAG {method} → room={room} hp={hp} floor={floor} combat=NULL rewards={(rewards is null ? "null" : $"{rewards.Available.Count} pending")}]");
        }

        // Combat: dump per-round detail. New round = a fresh hand was drawn,
        // and the agent is about to make decisions over it.
        if (combat is not null && combat.Round != _combatRound)
        {
            if (_combatRound == -1) { _floorCombatCounter++; _md.AppendLine($"### Combat #{_floorCombatCounter} on floor {floor}"); _md.AppendLine(); }
            _combatRound = combat.Round;
            _md.AppendLine($"#### Round {combat.Round}  (e={combat.Energy}/{combat.MaxEnergy} block={combat.PlayerBlock} draw={combat.DrawPileCount} disc={combat.DiscardPileCount})");
            _md.AppendLine();
            _md.AppendLine("  - hand:");
            foreach (var c in combat.Hand)
            {
                _md.AppendLine($"    - [{c.Index}] `{c.Id}` cost={c.Cost} canPlay={c.CanPlay} target={c.TargetType}");
            }
            _md.AppendLine("  - enemies:");
            foreach (var e in combat.Enemies)
            {
                var intents = e.Intents.Count == 0 ? "(no intent)" : string.Join(" + ", e.Intents.Select(i =>
                    $"{i.Kind}{(i.Damage is int d ? $" {d}×{i.Hits ?? 1}" : "")}{(i.Block is int b ? $" block={b}" : "")}"));
                var powers = e.Powers.Count == 0 ? "" : $"  powers=[{string.Join(",", e.Powers.Select(p => $"{p.Id}:{p.Amount}"))}]";
                _md.AppendLine($"    - [{e.Index}] `{e.MonsterId}` {e.Hp}/{e.MaxHp} block={e.Block} → {intents}{powers}");
            }
            if (combat.PlayerPowers.Count > 0)
            {
                _md.AppendLine($"  - player powers: {string.Join(", ", combat.PlayerPowers.Select(p => $"{p.Id}:{p.Amount}"))}");
            }
            _md.AppendLine();
        }
        else if (combat is null && _combatRound != -1)
        {
            _md.AppendLine($"  - combat ended (hp={hp})");
            _md.AppendLine();
            _combatRound = -1;
        }

        // Bag state — only when it changes.
        if (potions is not null)
        {
            var digest = potions.Count == 0 ? "(empty)" : string.Join(",", potions.Select(p => $"[{p.Index}]{p.Id}/{p.TargetType}"));
            if (digest != _lastPotionDigest)
            {
                _md.AppendLine($"  - bag: {digest}");
                _lastPotionDigest = digest;
            }
        }

        // Rewards menu.
        if (rewards is { Available.Count: > 0 })
        {
            _md.AppendLine("  - rewards offered:");
            foreach (var r in rewards.Available)
            {
                var kind = r.Kind.ToString().ToLowerInvariant();
                var extra = r.Kind switch
                {
                    RewardKind.Gold => $" {r.GoldAmount}g",
                    RewardKind.Relic => $" relic=`{r.RelicId}`",
                    RewardKind.Potion => $" potion=`{r.PotionId}`",
                    RewardKind.Card when r.Cards is not null => " cards=[" + string.Join(", ", r.Cards.Select(c => $"`{c.Id}`(cost={c.Cost})")) + "]",
                    _ => "",
                };
                _md.AppendLine($"    - [{r.Index}] {kind}{extra}  canSkip={r.CanSkip}");
            }
            _md.AppendLine();
        }
    }

    private static string FormatRelics(IReadOnlyList<Relic>? relics) =>
        relics is null || relics.Count == 0 ? "(none)" : string.Join(", ", relics.Select(r => $"`{r.Id}`"));
}
