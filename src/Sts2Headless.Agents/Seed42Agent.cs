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
//   * On rewards: pick cards by the agent's own DraftScore table (below)
//     rather than skipping. Skip only when every offering is at-or-below
//     neutral. CardMechanics.IsHeadlessUnsafe cards (Headbutt, Burning
//     Pact, Armaments — they NRE on card-select sub-flows) carry a strong
//     negative score so the reward picker skips them when possible and
//     takes the least-bad option only when forced.
//   * On map / event / rest / treasure / merchant: the HeuristicAgent
//     defaults handle these. DecideMap is overridden with an HP-aware
//     priority bias toward rest sites when wounded.
//
// Why a separate class instead of refactoring GreedyAgent: GreedyAgent is
// a regression baseline — many tests assert its behaviour. Seed42Agent is
// an experimental forward-progress agent. They share the IAgent contract
// but diverge sharply in *what* they do at every decision; collapsing
// them later is a cleanup slice once the heuristic stabilises.
public sealed class Seed42Agent : HeuristicAgent
{
    // ── Combat: potion + card decision per snapshot ─────────────────────

    protected override AgentAction DecideCombat(RunStateResult state)
    {
        var combat = state.CombatState
            ?? throw new InvalidOperationException(
                $"Seed42Agent: {state.CurrentRoomType} with combatState=null.");

        if (!combat.IsPlayPhase || combat.Enemies.Count == 0)
        {
            // Out-of-phase: end the turn so the engine can flip phases.
            return new EndTurn();
        }

        // Step P: emergency potion drink. Defensive potions are "free" —
        // they don't consume energy — so they always beat spending energy
        // on Defend when available.
        var potionPick = ChoosePotion(combat, state, state.Hp, state.MaxHp);
        if (potionPick is not null)
        {
            return new UsePotion(potionPick.Value.potionIndex, potionPick.Value.targetIndex);
        }

        var pick = ChoosePlay(combat, state.Hp, state.MaxHp);
        if (pick is null)
        {
            return new EndTurn();
        }
        return new PlayCard(pick.Value.cardIndex, pick.Value.targetIndex);
    }

    // ── Map: HP-aware priority bias ─────────────────────────────────────

    protected override AgentAction DecideMap(RunStateResult state)
    {
        // PhaseDetector guarantees AvailableMapNodes is non-empty when
        // Phase.Map fires (empty maps route through Phase.MapEmpty →
        // DecideMapEmpty → EnterNextAct via the base class default).
        var hpPct = state.MaxHp > 0 ? (double)state.Hp / state.MaxHp : 1.0;
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
        var pick = state.AvailableMapNodes
            .OrderBy(n => Priority(n.Type))
            .ThenBy(n => n.Col)
            .First();
        return new SelectMapNode(pick.Col, pick.Row);
    }

    // ── Rewards: DraftScore-driven card pick ────────────────────────────

    protected override AgentAction DecideRewards(RunStateResult state)
    {
        // PhaseDetector guarantees Available is non-empty when we get here.
        var pick = state.RewardsState!.Available[0];
        if (pick.Kind != RewardKind.Card)
        {
            // Gold/relic/potion — claim everything, can't skip anyway.
            return new SelectReward(pick.Index, CardIndex: null);
        }

        // Pick the highest-DraftScore option. Skip if every option is
        // at-or-below neutral and skipping is allowed. Forced (non-
        // skippable) card rewards still take the best we can.
        var ranked = pick.Cards?
            .Select(c => (idx: c.Index, score: DraftScore(c.Id), id: c.Id))
            .OrderByDescending(t => t.score)
            .ToList();
        var best = ranked is { Count: > 0 } ? ranked[0] : (idx: 0, score: 0, id: CardId.Unknown);

        if (pick.CanSkip && best.score <= 0)
            return new SkipReward(pick.Index);

        return new SelectReward(pick.Index, best.idx);
    }

    // ── Per-card draft preferences (seed 42, Ironclad) ──────────────────
    //
    // Lives next to the agent (not in CardMechanics) because the score
    // depends on *this* agent's strategy — the defensive-stance bias, the
    // SLIPPERY-drain plan, the boss target. CardMechanics.IsHeadlessUnsafe
    // cards get a strong negative score here so the reward picker skips
    // them when possible.

    private static readonly Dictionary<CardId, int> SeedFourtyTwoDraftScores = new()
    {
        [CardId.StrikeIronclad] = 0,
        [CardId.DefendIronclad] = 0,
        [CardId.Bash]           = 0,
        [CardId.BodySlam]       = 4,
        [CardId.SwordBoomerang] = 3,
        [CardId.Tremble]        = -2,
        [CardId.ExpectAFight]   = 1,
        [CardId.Bludgeon]       = 5,
        [CardId.Thunderclap]    = 2,
        [CardId.Bully]          = 0,
        [CardId.Dismantle]      = 0,
        [CardId.Cascade]        = 0,
        [CardId.Uppercut]       = 4,
        [CardId.StoneArmor]     = 2,
        [CardId.TrueGrit]       = 3,
        [CardId.SecondWind]     = 1,
        [CardId.BloodWall]      = 3,
        [CardId.Taunt]          = 0,
    };

    // Strongly negative score for IsHeadlessUnsafe cards. -100 leaves
    // headroom for sub-flag refinement later and stays well below any
    // legitimate score in SeedFourtyTwoDraftScores.
    private const int HeadlessUnsafePenalty = -100;

    private static int DraftScore(CardId cardId)
    {
        var penalty = CardMechanics.Get(cardId).IsHeadlessUnsafe ? HeadlessUnsafePenalty : 0;
        return penalty + (SeedFourtyTwoDraftScores.TryGetValue(cardId, out var s) ? s : 0);
    }

    // ── Potion-use decision ─────────────────────────────────────────────
    //
    // Trigger gates per potion id:
    //   * BlockPotion: incoming damage would land >= 8 unblocked HP and HP
    //     is at-or-below 50% maxHp. Saved for "this turn would otherwise
    //     hurt a lot" — at full HP we'd rather hoard the cushion.
    //   * EnergyPotion: hand has cost-2+ playable cards (e.g. BLUDGEON,
    //     UPPERCUT) we can't afford this turn, AND a high-priority target
    //     exists (low-HP enemy or boss).
    //   * Strength / Dexterity / Flex potions: round 1 only (long fight
    //     ahead amortises the buff).
    //   * EntropicBrew / utility: skip — too situational.
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
        // The wire TargetType currently parses Unknown for potions, so
        // we hard-code targeting per known potion id: self-target potions
        // pass null; damage potions pass primaryTarget.
        var enemyPotions = new[] { "FirePotion", "PoisonPotion", "AttackPotion",
            "WeakPotion", "VulnerablePotion" };
        int? target = enemyPotions.Contains(potion.Id) ? primaryTarget.Index : (int?)null;

        switch (potion.Id)
        {
            case "BlockPotion":
                return (unblocked >= 8 && hp <= maxHp / 2, target);

            case "EnergyPotion":
                var hasUnaffordable = combat.Hand.Any(c => c.CanPlay && c.Cost > combat.Energy
                                                            && CardMechanics.Get(c.Id).Damage > 0);
                return (hasUnaffordable && hp > maxHp / 4, target);

            case "RegenPotion":
                return (hp <= maxHp * 6 / 10 && combat.Round <= 3, target);

            case "BloodPotion":
                return (hp < maxHp / 2, target);

            case "StrengthPotion":
            case "DexterityPotion":
            case "FlexPotion":
            case "FocusPotion":
                return (combat.Round == 1, target);

            case "FirePotion":
            case "PoisonPotion":
            case "AttackPotion":
                return (primaryTarget.Hp <= 25, target);

            case "WeakPotion":
            case "VulnerablePotion":
                return (combat.Round == 1, target);

            default:
                return (false, null);
        }
    }

    // ── Card-play decision ──────────────────────────────────────────────
    //
    // Strategy (top-priority first):
    //   0. Round-1 power cards (EXPECT_A_FIGHT, STONE_ARMOR).
    //   1. Defend FIRST when survival is in question.
    //   2. Bash for Vulnerable when it actually pays off.
    //   3. SLIPPERY-aware drain: cheapest multi-hit attacks.
    //   4. Highest-damage attack we can afford.
    //   5. Burn any playable card to cycle hand.
    private static (int cardIndex, int? targetIndex)? ChoosePlay(
        CombatState combat, int hp, int maxHp)
    {
        var hand = combat.Hand;
        var enemies = combat.Enemies;

        var primaryTarget = enemies.OrderBy(e => e.Hp).First();
        var primaryTargetVuln = primaryTarget.Powers.Any(p => p.Id == "VULNERABLE_POWER");

        var slipperyOnBoard = enemies.Any(e => e.Powers.Any(p => p.Id == "SLIPPERY_POWER"));

        var incoming = CombatHelpers.IncomingDamage(combat);
        var unblocked = Math.Max(0, incoming - combat.PlayerBlock);
        var hpPct = maxHp > 0 ? (double)hp / maxHp : 1.0;

        // Step 0: round-1 power cards.
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

        // Step 1: defend FIRST when survival is in question.
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

        // Step 2: Bash for Vulnerable when it actually pays off.
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
                .OrderByDescending(c => CardMechanics.Get(c.Id).Hits)
                .ThenBy(c => c.Cost)
                .FirstOrDefault();
            if (drain is not null)
            {
                int? target = drain.TargetType == TargetType.AnyEnemy ? slipperyTarget.Index : (int?)null;
                return (drain.Index, target);
            }
        }

        // Step 4: highest-damage affordable attack.
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
}
