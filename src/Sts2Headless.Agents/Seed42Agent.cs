using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Agents;

// Heuristic Ironclad agent tuned for seed 42 (Act 1). Goal: beat VANTOM
// at floor 17. The recon at documentation/research/seed42-recon.md
// describes the seed's terrain — enemies seen, rewards offered, the boss
// stat block — and this agent's heuristics are picked to handle the
// specific shapes that path surfaces.
//
// Decision discipline (different from GreedyAgent):
//   * In combat: priority queue per turn. Bash for Vulnerable before
//     committing energy to Strikes. Defends when incoming damage exceeds
//     a HP-relative threshold. Otherwise pour energy into damage.
//   * On rewards: pick cards by `CardEffects.DraftScore` rather than
//     skipping. Skip only when every offering is at-or-below neutral.
//   * On map / events / rest sites: same as GreedyAgent (path is mostly
//     linear on this seed, events are picked last-unlocked, rest-site
//     heal).
//
// Why a separate class instead of refactoring GreedyAgent: GreedyAgent is
// a regression baseline — many tests assert its behaviour. Seed42Agent is
// an experimental forward-progress agent. They share the IAgent contract
// but diverge sharply in *what* they do at every decision; collapsing
// them later is a cleanup slice once the heuristic stabilises.
public sealed class Seed42Agent : IAgent
{
    private const int MaxSteps = 4000;
    private const int MaxRewardsDrain = 50;

    public async Task<RunStateResult> DriveUntilAsync(
        ITransport host,
        Func<RunStateResult, bool> stopWhen,
        CancellationToken ct = default)
    {
        var state = await host.SendAsync<RunStateResult>("run/state");
        for (var step = 0; !stopWhen(state); step++)
        {
            ct.ThrowIfCancellationRequested();
            if (state.IsGameOver)
            {
                throw new InvalidOperationException(
                    "Seed42Agent: run ended (game over) before stop condition matched. " +
                    $"Last state: floor={state.ActFloor}, room={state.CurrentRoomType}, " +
                    $"hp={state.Hp}/{state.MaxHp}.");
            }
            if (step >= MaxSteps)
            {
                throw new InvalidOperationException(
                    $"Seed42Agent: exceeded {MaxSteps} steps without matching stop condition.");
            }
            state = await StepAsync(host, state);
        }
        return state;
    }

    private static Task<RunStateResult> StepAsync(ITransport host, RunStateResult s) =>
        s.CurrentRoomType switch
        {
            RoomType.MapRoom => StepMapAsync(host, s),
            RoomType.CombatRoom or RoomType.BossRoom => StepCombatAsync(host, s),
            RoomType.EventRoom => StepEventAsync(host, s),
            RoomType.RestSiteRoom => StepRestSiteAsync(host, s),
            RoomType.TreasureRoom => StepTreasureAsync(host, s),
            RoomType.MerchantRoom => StepMerchantAsync(host, s),
            _ => throw new InvalidOperationException(
                $"Seed42Agent: unhandled room type {s.CurrentRoomType}."),
        };

    // ── Map ────────────────────────────────────────────────────────────────

    private static async Task<RunStateResult> StepMapAsync(ITransport host, RunStateResult s)
    {
        if (s.AvailableMapNodes.Count == 0)
            throw new InvalidOperationException("Seed42Agent: MapRoom with no available nodes.");

        // Path bias: when HP is healthy, prefer combats (gold + cards);
        // when wounded, prefer rest sites. Boss/Elite always taken if it's
        // the only forward option (which is the case on seed 42's mostly
        // linear path).
        var hpPct = s.MaxHp > 0 ? (double)s.Hp / s.MaxHp : 1.0;
        int Priority(MapNodeType t) => (t, hpPct < 0.5) switch
        {
            (MapNodeType.RestSite, true) => 0,
            (MapNodeType.Monster, _) => 1,
            (MapNodeType.Elite, _) => 2,
            (MapNodeType.Event, _) => 3,
            (MapNodeType.Unknown, _) => 4,
            (MapNodeType.RestSite, false) => 5,
            (MapNodeType.Boss, _) => 6,
            (MapNodeType.Merchant, _) => 7,
            (MapNodeType.Treasure, _) => 8,
            _ => 100,
        };
        var pick = s.AvailableMapNodes.OrderBy(n => Priority(n.Type)).ThenBy(n => n.Col).First();
        await host.SendAsync<RunSelectMapNodeResult>(
            "run/select_map_node",
            new RunSelectMapNodeParams(Col: pick.Col, Row: pick.Row));
        return await host.SendAsync<RunStateResult>("run/state");
    }

    // ── Combat ─────────────────────────────────────────────────────────────

    private static async Task<RunStateResult> StepCombatAsync(ITransport host, RunStateResult s)
    {
        var combat = s.CombatState
            ?? throw new InvalidOperationException(
                $"Seed42Agent: {s.CurrentRoomType} with combatState=null.");

        if (!combat.IsPlayPhase || !combat.IsInProgress || combat.Enemies.Count == 0)
        {
            // Out-of-phase: end the turn so the engine can flip phases.
            var ended = await host.SendAsync<RunEndTurnResult>("run/end_turn");
            return await DrainRewardsAsync(host, ended.RewardsState);
        }

        var pick = ChoosePlay(combat, s.Hp, s.MaxHp);
        if (pick is null)
        {
            var ended = await host.SendAsync<RunEndTurnResult>("run/end_turn");
            return await DrainRewardsAsync(host, ended.RewardsState);
        }

        var resp = await host.SendAsync<RunPlayCardResult>(
            "run/play_card",
            new RunPlayCardParams(CardIndex: pick.Value.cardIndex, TargetIndex: pick.Value.targetIndex));
        return await DrainRewardsAsync(host, resp.RewardsState);
    }

    // Heart of the combat agent: pick a single (cardIndex, targetIndex)
    // to play this tick, or null to end turn. Called once per host
    // round-trip; the loop keeps calling until null or out of energy.
    //
    // Strategy (top-priority first):
    //   1. If a target lacks Vulnerable AND has no SLIPPERY: cast Bash
    //      for the +50% Vulnerable mark. (Vulnerable doesn't help while
    //      SLIPPERY-1-cap is active — every attack lands 1 damage
    //      regardless of Vulnerable. Save Bash for after SLIPPERY clears.)
    //   2. Defend on threatened turns (incoming damage would drop HP
    //      below 50% maxHp).
    //   3. *SLIPPERY-aware drain*: if any enemy carries SLIPPERY_POWER,
    //      pour energy into the cheapest playable attacks (Strike beats
    //      Bludgeon vs SLIPPERY:9 — 3 strikes drain 3 stacks for 3 damage
    //      vs Bludgeon's 1-hit 1-damage drain-of-1). Prefer multi-hit
    //      cards (Sword Boomerang's 3 hits = 3 drains per cost-1).
    //   4. Post-SLIPPERY (or non-SLIPPERY enemies): play the highest-
    //      damage attack we can afford.
    //   5. Burn remaining energy on any playable card so we cycle through
    //      hand toward the next draw.
    private static (int cardIndex, int? targetIndex)? ChoosePlay(
        CombatState combat, int hp, int maxHp)
    {
        var hand = combat.Hand;
        var enemies = combat.Enemies;

        var primaryTarget = enemies.OrderBy(e => e.Hp).First();
        var primaryTargetVuln = primaryTarget.Powers.Any(p => p.Id == "VULNERABLE_POWER");

        // Aggregate flag: any enemy carrying SLIPPERY. Empirically the
        // power caps damage-per-attack at 1 while it's stacked; the
        // strategy below treats it as a binary "drain mode" gate.
        var slipperyOnBoard = enemies.Any(e => e.Powers.Any(p => p.Id == "SLIPPERY_POWER"));

        // Step 1: Bash for Vulnerable when it actually pays off — i.e.
        // SLIPPERY isn't capping damage. Otherwise the Vuln is wasted
        // and we should be draining instead.
        if (!primaryTargetVuln && !slipperyOnBoard)
        {
            var bash = hand.FirstOrDefault(c => c.CanPlay && c.Id == "BASH" && c.Cost <= combat.Energy);
            if (bash is not null)
                return (bash.Index, primaryTarget.Index);
        }

        // Step 2: defend on threatened turns. Stack defends until unblocked
        // damage is small (<5) or no defends remain. Two trigger gates,
        // OR-combined:
        //   a) Unblocked damage > 0 AND would leave us at < 50% maxHp.
        //   b) HP <= 60% maxHp AND we have under 10 block — blanket
        //      "low HP, top up block" fallback for turns where the
        //      intent damage signal under-reads (the host-side fallback
        //      from before intent.Damage was wired; kept as belt-and-
        //      braces in case future enemies surface non-AttackIntent
        //      shapes we haven't typed yet).
        var incoming = CardEffects.IncomingDamage(combat);
        var unblocked = Math.Max(0, incoming - combat.PlayerBlock);
        // Defend whenever any incoming would land AND we're not already
        // comfortably above ~70% maxHp. The previous `unblocked >= 5`
        // floor missed fights where Mawler-style 14-damage hits chip the
        // last 4 unblocked through after a couple of defends — the agent
        // stopped defending too early because the residual was < 5.
        var threatenedByDamage = unblocked > 0 && hp - unblocked < maxHp * 7 / 10;
        var threatenedByHpPct = combat.PlayerBlock < 10 && hp <= maxHp * 6 / 10;
        if (threatenedByDamage || threatenedByHpPct)
        {
            var defendish = hand
                .Where(c => c.CanPlay && c.Cost <= combat.Energy
                            && CardEffects.Get(c.Id).Block > 0)
                .OrderByDescending(c => CardEffects.Get(c.Id).Block)
                .FirstOrDefault();
            if (defendish is not null)
                return (defendish.Index, defendish.TargetType == TargetType.AnyEnemy ? primaryTarget.Index : (int?)null);
        }

        // Step 3: SLIPPERY drain mode — cheapest attack, prefer multi-hit.
        if (slipperyOnBoard)
        {
            var slipperyTarget = enemies
                .FirstOrDefault(e => e.Powers.Any(p => p.Id == "SLIPPERY_POWER"))
                ?? primaryTarget;
            var drain = hand
                .Where(c => c.CanPlay && c.Cost <= combat.Energy)
                .Where(c =>
                {
                    var eff = CardEffects.Get(c.Id);
                    return eff.Damage > 0 || eff.BlockToDamage;
                })
                // hits per energy first (multi-hit cheap is gold), then cost asc
                .OrderByDescending(c => CardEffects.Get(c.Id).Hits)
                .ThenBy(c => c.Cost)
                .FirstOrDefault();
            if (drain is not null)
            {
                int? target = drain.TargetType == TargetType.AnyEnemy ? slipperyTarget.Index : (int?)null;
                return (drain.Index, target);
            }
        }

        // Step 4: pour energy into damage (post-SLIPPERY or non-SLIPPERY).
        var bestAttack = hand
            .Where(c => c.CanPlay && c.Cost <= combat.Energy)
            .Select(c => (card: c, dmg: CardEffects.EstimateDamage(c, primaryTarget, combat)))
            .Where(t => t.dmg > 0)
            .OrderByDescending(t => t.dmg)
            .FirstOrDefault();
        if (bestAttack.card is not null)
        {
            int? target = bestAttack.card.TargetType == TargetType.AnyEnemy ? primaryTarget.Index : (int?)null;
            return (bestAttack.card.Index, target);
        }

        // Step 5: burn off any playable card to cycle hand.
        var anyPlayable = hand
            .Where(c => c.CanPlay && c.Cost <= combat.Energy)
            .OrderByDescending(c => CardEffects.Get(c.Id).Block)
            .FirstOrDefault();
        if (anyPlayable is not null)
        {
            int? target = anyPlayable.TargetType == TargetType.AnyEnemy ? primaryTarget.Index : (int?)null;
            return (anyPlayable.Index, target);
        }

        return null;
    }

    // ── Rooms with no per-seed-42 nuance ──────────────────────────────────

    private static async Task<RunStateResult> StepRestSiteAsync(ITransport host, RunStateResult s)
    {
        var pick = s.AvailableRestSiteOptions.FirstOrDefault(o =>
                       o.IsEnabled && string.Equals(o.OptionId, "HEAL", StringComparison.OrdinalIgnoreCase))
                   ?? s.AvailableRestSiteOptions.FirstOrDefault(o =>
                       o.IsEnabled && !string.Equals(o.OptionId, "SMITH", StringComparison.OrdinalIgnoreCase))
                   ?? s.AvailableRestSiteOptions.FirstOrDefault(o => o.IsEnabled)
                   ?? throw new InvalidOperationException("Seed42Agent: rest site with no enabled options.");
        await host.SendAsync<RunSelectRestSiteOptionResult>(
            "run/select_rest_site_option",
            new RunSelectRestSiteOptionParams(OptionIndex: pick.Index));
        return await host.SendAsync<RunStateResult>("run/state");
    }

    private static async Task<RunStateResult> StepMerchantAsync(ITransport host, RunStateResult s)
    {
        _ = await host.SendAsync<RunLeaveMerchantRoomResult>("run/leave_merchant_room");
        return await host.SendAsync<RunStateResult>("run/state");
    }

    private static async Task<RunStateResult> StepTreasureAsync(ITransport host, RunStateResult s)
    {
        var resp = await host.SendAsync<RunLeaveTreasureRoomResult>("run/leave_treasure_room");
        return await DrainRewardsAsync(host, resp.RewardsState);
    }

    private static async Task<RunStateResult> StepEventAsync(ITransport host, RunStateResult s)
    {
        if (s.AvailableEventOptions.Count == 0)
            throw new InvalidOperationException("Seed42Agent: event with no options.");
        var pick = s.AvailableEventOptions.LastOrDefault(o => !o.IsLocked) ?? s.AvailableEventOptions[^1];
        var resp = await host.SendAsync<RunSelectEventOptionResult>(
            "run/select_event_option",
            new RunSelectEventOptionParams(OptionIndex: pick.Index));
        return await DrainRewardsAsync(host, resp.RewardsState);
    }

    // ── Rewards ───────────────────────────────────────────────────────────

    private static async Task<RunStateResult> DrainRewardsAsync(ITransport host, RewardsState? rewards)
    {
        var rs = rewards;
        for (var i = 0; i < MaxRewardsDrain && rs is not null && rs.Available.Count > 0; i++)
        {
            var pick = rs.Available[0];
            if (pick.Kind == RewardKind.Card)
            {
                // Pick the highest DraftScore option, OR skip if every
                // option is at-or-below neutral and skipping is allowed.
                // Forced (non-skippable) card rewards still take the best
                // we can.
                var ranked = pick.Cards?
                    .Select(c => (idx: c.Index, score: CardEffects.Get(c.Id).DraftScore, id: c.Id))
                    .OrderByDescending(t => t.score)
                    .ToList();
                var best = ranked is { Count: > 0 } ? ranked[0] : (idx: 0, score: 0, id: "");
                if (pick.CanSkip && best.score <= 0)
                {
                    var r = await host.SendAsync<RunSkipRewardResult>(
                        "run/skip_reward", new RunSkipRewardParams(RewardIndex: pick.Index));
                    rs = r.RewardsState;
                }
                else
                {
                    var r = await host.SendAsync<RunSelectRewardResult>(
                        "run/select_reward",
                        new RunSelectRewardParams(RewardIndex: pick.Index, CardIndex: best.idx));
                    rs = r.RewardsState;
                }
            }
            else
            {
                // Gold/relic/potion — claim everything, can't skip anyway.
                var r = await host.SendAsync<RunSelectRewardResult>(
                    "run/select_reward",
                    new RunSelectRewardParams(RewardIndex: pick.Index, CardIndex: null));
                rs = r.RewardsState;
            }
        }
        return await host.SendAsync<RunStateResult>("run/state");
    }
}
