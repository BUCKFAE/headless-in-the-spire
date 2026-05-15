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
//   * On rewards: pick cards by the agent's own `DraftScore` table
//     (below) rather than skipping. Skip only when every offering is
//     at-or-below neutral. Headless-unsafe cards (HeadButt, ARMAMENTS —
//     anything `CardMechanics.IsHeadlessUnsafe`) are filtered out
//     before scoring, regardless of how attractive the card would be
//     in a normal client.
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

    // Seed-42 + Ironclad + agent-specific desirability scores for the
    // card pool the recon surfaces. Lives next to the agent (not in
    // CardMechanics) because the score depends on *this* agent's
    // strategy — the defensive-stance bias, the SLIPPERY-drain plan,
    // the boss target — and would mean something different to a
    // different agent. Scale is roughly -3..+5, neutral = 0.
    //
    // CardMechanics.IsHeadlessUnsafe cards (Headbutt, Burning Pact,
    // Armaments) get a strong negative score here so the reward picker
    // skips them when possible. The engine-compat fact lives on the
    // card; the "how strongly do I want to avoid this" weighting is an
    // agent decision.
    private static readonly Dictionary<CardId, int> SeedFourtyTwoDraftScores = new()
    {
        // Starter — neutral; we already own them.
        [CardId.StrikeIronclad] = 0,
        [CardId.DefendIronclad] = 0,
        [CardId.Bash]           = 0,

        // F2 picks. Body Slam synergises with the defensive stance the
        // agent leans on (Phrog floor-8 → 4-wriggler turn-cycle); a
        // "3 defends → Body Slam ~16" turn out-damages SWORD_BOOMERANG
        // on a healthy block deck. Sword's per-hit 3 damage is mid-range
        // but it's a useful SLIPPERY drain (3 stacks per cost-1).
        [CardId.BodySlam]       = 4,
        [CardId.SwordBoomerang] = 3,
        [CardId.Tremble]        = -2, // sts2 loss-of-control status

        // F4 picks. Expect-A-Fight is a power card — keep neutral until
        // the wire surfaces power dynamics.
        [CardId.ExpectAFight]   = 1,

        // F5 picks. Bludgeon is the boss-killing card on this path:
        // 32 single-target damage, cost 3, SLIPPERY-5 still leaves
        // 27 landing per swing. Highest priority pick.
        [CardId.Bludgeon]       = 5,
        [CardId.Thunderclap]    = 2,
        [CardId.Bully]          = 0,

        // F8 elite picks — neutral; we're short Act 1.
        [CardId.Dismantle]      = 0,
        [CardId.Cascade]        = 0,

        // F9 picks. Uppercut cleanly pierces SLIPPERY (single-hit) and
        // stacks Vuln + Weak — excellent vs bosses. Stone Armor is a
        // mild power; we keep it positive but low.
        [CardId.Uppercut]       = 4,
        [CardId.StoneArmor]     = 2,

        // F12 picks. True Grit is solid block on the defensive stance.
        // Second Wind is exhaust-dependent — neutral until we model it.
        [CardId.TrueGrit]       = 3,
        [CardId.SecondWind]     = 1,

        // F15 picks. Blood Wall is good defensive offence.
        [CardId.BloodWall]      = 3,
        [CardId.Taunt]          = 0,
    };

    // Strongly negative score for IsHeadlessUnsafe cards. The exact
    // number matters: it must be below every legitimate "skip" threshold
    // (the picker skips when every score ≤ 0) and below any conceivable
    // neutral fallback, so that on a forced (CanSkip=false) pick we
    // still take the *least* bad of multiple unsafe cards rather than a
    // random index. -100 leaves headroom for sub-flag refinement later.
    private const int HeadlessUnsafePenalty = -100;

    private static int DraftScore(CardId cardId)
    {
        var penalty = CardMechanics.Get(cardId).IsHeadlessUnsafe ? HeadlessUnsafePenalty : 0;
        return penalty + (SeedFourtyTwoDraftScores.TryGetValue(cardId, out var s) ? s : 0);
    }

    public async Task<RunStateResult> DriveUntilAsync(
        ITransport host,
        Func<RunStateResult, bool> stopWhen,
        CancellationToken ct = default)
    {
        var state = await host.SendAsync<RunStateResult>("run/state");
        var stall = new StallDetector();
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
            stall.Observe(state);
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
        // Post-boss MapRoom with no nodes: the engine has flipped the
        // room past the boss but the map hasn't been regenerated for
        // the next act yet. Drive run/enter_next_act so the engine bumps
        // CurrentActIndex and regenerates the map. The host's guard
        // enforces "post-boss only" so calling here is safe.
        if (s.AvailableMapNodes.Count == 0)
        {
            await host.SendAsync<RunEnterNextActResult>("run/enter_next_act");
            return await host.SendAsync<RunStateResult>("run/state");
        }

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

        // Step P: emergency potion drink. If we'd take a fatal hit next
        // turn, drink a BlockPotion (or any high-block utility) before
        // playing any card. Defensive potions are "free" — they don't
        // consume energy — so they always beat spending energy on Defend
        // when available.
        var potionPick = ChoosePotion(combat, s, s.Hp, s.MaxHp);
        if (potionPick is not null)
        {
            var pr = await host.SendAsync<RunUsePotionResult>(
                "run/use_potion",
                new RunUsePotionParams(PotionIndex: potionPick.Value.potionIndex, TargetIndex: potionPick.Value.targetIndex));
            return await DrainRewardsAsync(host, pr.RewardsState);
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

    // Decide whether to use a potion this tick. Returns the (potionIndex,
    // targetIndex) to drink, or null to keep them in the bag.
    //
    // Trigger gates per potion id:
    //   * BlockPotion: incoming damage would land >= 8 unblocked HP and HP
    //     is at-or-below 50% maxHp. Saved for "this turn would otherwise
    //     hurt a lot" — at full HP we'd rather hoard the cushion.
    //   * EnergyPotion: hand has cost-2+ playable cards (e.g. BLUDGEON,
    //     UPPERCUT) we can't afford this turn, AND a high-priority target
    //     exists (low-HP enemy or boss). Burning energy when the deck
    //     can't capitalise is a waste.
    //   * Strength / Dexterity / Flex potions: round 1 only (long fight
    //     ahead amortises the buff).
    //   * EntropicBrew / utility: skip — too situational, agent doesn't
    //     understand the random outputs.
    private static (int potionIndex, int? targetIndex)? ChoosePotion(
        CombatState combat, RunStateResult s, int hp, int maxHp)
    {
        if (s.OwnedPotions.Count == 0) return null;
        var enemies = combat.Enemies;
        var primaryTarget = enemies.OrderBy(e => e.Hp).First();
        var incoming = CombatHelpers.IncomingDamage(combat);
        var unblocked = Math.Max(0, incoming - combat.PlayerBlock);

        foreach (var potion in s.OwnedPotions)
        {
            if (!potion.CanUse) continue;
            var (shouldUse, targetIndex) = ShouldUsePotion(potion, combat, hp, maxHp, unblocked, primaryTarget);
            if (shouldUse) return (potion.Index, targetIndex);
        }
        return null;
    }

    private static (bool, int?) ShouldUsePotion(
        OwnedPotion potion, CombatState combat, int hp, int maxHp, int unblocked, Enemy primaryTarget)
    {
        // The wire TargetType currently parses Unknown for potions (the
        // engine's enum strings don't match the wire's; a separate slice).
        // Until that's fixed we hard-code targeting per known potion id:
        // self-target potions pass null; damage potions pass primaryTarget.
        var selfPotions = new[] { "BlockPotion", "EnergyPotion", "StrengthPotion",
            "DexterityPotion", "FlexPotion", "FocusPotion", "RegenPotion",
            "EntropicBrew", "ColorlessPotion", "SpeedPotion", "BloodPotion" };
        var enemyPotions = new[] { "FirePotion", "PoisonPotion", "AttackPotion",
            "WeakPotion", "VulnerablePotion" };
        int? target = enemyPotions.Contains(potion.Id) ? primaryTarget.Index : (int?)null;

        switch (potion.Id)
        {
            case "BlockPotion":
                return (unblocked >= 8 && hp <= maxHp / 2, target);

            case "EnergyPotion":
                // Drink if there's an unaffordable high-cost attack OR we'd
                // play a card that obviously matters (we're below max HP).
                var hasUnaffordable = combat.Hand.Any(c => c.CanPlay && c.Cost > combat.Energy
                                                            && CardMechanics.Get(c.Id).Damage > 0);
                return (hasUnaffordable && hp > maxHp / 4, target);

            case "RegenPotion":
                // Regen restores HP over a few turns; drink early when wounded
                // (60% maxHp threshold) so the heal lands across the fight.
                return (hp <= maxHp * 6 / 10 && combat.Round <= 3, target);

            case "BloodPotion":
                // Heal 20% maxHp instantly — drink when low HP.
                return (hp < maxHp / 2, target);

            case "StrengthPotion":
            case "DexterityPotion":
            case "FlexPotion":
            case "FocusPotion":
                // Round 1 only — buffs amortise over the fight.
                return (combat.Round == 1, target);

            case "FirePotion":
            case "PoisonPotion":
            case "AttackPotion":
                // Damage potions: use on the lowest-HP enemy (likely kill).
                return (primaryTarget.Hp <= 25, target);

            case "WeakPotion":
            case "VulnerablePotion":
                // Debuff potions: cast on the primary threat at round 1.
                return (combat.Round == 1, target);

            default:
                // EntropicBrew, unknown utility — hold.
                return (false, null);
        }
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

        var incoming = CombatHelpers.IncomingDamage(combat);
        var unblocked = Math.Max(0, incoming - combat.PlayerBlock);
        var hpPct = maxHp > 0 ? (double)hp / maxHp : 1.0;

        // Step 0: play power cards (cost > 0, no damage, no block, no
        // BlockToDamage) on round 1 — they're "lay the buff, then fight".
        // Examples: EXPECT_A_FIGHT, STONE_ARMOR. Skipping this step on
        // later rounds avoids re-playing on a re-shuffle that puts a
        // power back into hand after exhaust.
        if (combat.Round == 1)
        {
            var power = hand
                .Where(c => c.CanPlay && c.Cost > 0 && c.Cost <= combat.Energy
                            && CardMechanics.Get(c.Id) is { Damage: 0, Block: 0, BlockToDamage: false }
                            && c.TargetType == TargetType.Self)
                .FirstOrDefault();
            if (power is not null)
                return (power.Index, null);
        }

        // Step 1: defend FIRST when survival is in question. If the next
        // enemy turn would land any unblocked damage AND we're below 70%
        // HP, stack defends ahead of offence. Reordered from the prior
        // "Bash → Defend → Attack" because applying Vulnerable doesn't
        // help if we game-over before the next attack lands.
        //   - threatenedByDamage: intent says incoming will land and
        //     would push HP below 70%.
        //   - threatenedByHpPct: belt-and-braces for intent gaps —
        //     low HP, low block, regardless of what the wire surfaces.
        var threatenedByDamage = unblocked > 0 && hp - unblocked < maxHp * 7 / 10;
        var threatenedByHpPct = combat.PlayerBlock < 10 && hpPct <= 0.6;
        if (threatenedByDamage || threatenedByHpPct)
        {
            var defendish = hand
                .Where(c => c.CanPlay && c.Cost <= combat.Energy
                            && CardMechanics.Get(c.Id).Block > 0)
                .OrderByDescending(c => CardMechanics.Get(c.Id).Block)
                .FirstOrDefault();
            if (defendish is not null)
                return (defendish.Index, defendish.TargetType == TargetType.AnyEnemy ? primaryTarget.Index : (int?)null);
        }

        // Step 2: Bash for Vulnerable when it actually pays off — i.e.
        // SLIPPERY isn't capping damage AND we're not in panic mode
        // (defending burns through hand faster; Vuln is for sustained
        // offence). Bash costs 2 — when HP is critical, spending 2
        // energy on a non-defensive card is a luxury.
        if (!primaryTargetVuln && !slipperyOnBoard && hpPct > 0.4)
        {
            var bash = hand.FirstOrDefault(c => c.CanPlay && c.Id == CardId.Bash && c.Cost <= combat.Energy);
            if (bash is not null)
                return (bash.Index, primaryTarget.Index);
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
                    var eff = CardMechanics.Get(c.Id);
                    return eff.Damage > 0 || eff.BlockToDamage;
                })
                // hits per energy first (multi-hit cheap is gold), then cost asc
                .OrderByDescending(c => CardMechanics.Get(c.Id).Hits)
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
            .Select(c => (card: c, dmg: CardMechanics.EstimateDamage(c, primaryTarget, combat)))
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
            .OrderByDescending(c => CardMechanics.Get(c.Id).Block)
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
                // Forced (non-skippable) card rewards still take the
                // best we can. The DraftScore helper pushes
                // CardMechanics.IsHeadlessUnsafe cards to a strongly
                // negative score so they skip when possible, and are
                // taken only as a last resort if every option is unsafe
                // (which can still crash on play — but that's an engine-
                // bug we want surfaced, not silently masked).
                var ranked = pick.Cards?
                    .Select(c => (idx: c.Index, score: DraftScore(c.Id), id: c.Id))
                    .OrderByDescending(t => t.score)
                    .ToList();
                var best = ranked is { Count: > 0 } ? ranked[0] : (idx: 0, score: 0, id: CardId.Unknown);
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
