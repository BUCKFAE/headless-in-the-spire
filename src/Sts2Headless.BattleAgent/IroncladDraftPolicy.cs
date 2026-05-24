using Sts2Headless.Agents.Contracts;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.BattleAgent;

// Tier-list-driven card rewards. The point isn't to be smart — it's to
// avoid getting WORSE: every rated card is something the planner knows
// how to play, every unrated card is skipped so we don't bloat the deck
// with cards the combat sim treats as no-ops.
//
// Tiers come from STS1 Ironclad consensus where the cards translate
// directly. STS2-original cards (Bully, Tremble, BloodWall, …) are
// placed conservatively until we have stat verification.
//
// Gold, relics, potions: always claimed. Card rewards: pick the
// highest-tier offered if it beats the skip threshold, otherwise skip.
public sealed class IroncladDraftPolicy : IDraftPolicy
{
    private readonly RunDeckTracker? _tracker;

    public IroncladDraftPolicy(RunDeckTracker? tracker = null) { _tracker = tracker; }

    public AgentAction Choose(RunStateResult state)
    {
        var rewards = state.RewardsState
            ?? throw new InvalidOperationException("IroncladDraftPolicy: no rewardsState");
        if (rewards.Available.Count == 0)
            throw new InvalidOperationException("IroncladDraftPolicy: empty rewards");

        // Always handle the first available reward — the engine yields
        // rewards in order and won't let us cross-skip.
        var head = rewards.Available[0];

        if (head.Kind == RewardKind.Card)
        {
            var cards = head.Cards ?? Array.Empty<CardRewardOption>();
            if (cards.Count == 0)
            {
                if (head.CanSkip) return new SkipReward(head.Index);
                return new SelectReward(head.Index);
            }

            // Compute the deck's archetype affinity (how committed it is
            // to each archetype) from the run-deck tracker.
            var affinity = ComputeDeckAffinity();

            // Highest-tier wins. If the deck has a *committed*
            // archetype (any single archetype with >= 3 enablers in the
            // run deck), use synergy as the tie-break within the top
            // tier. Otherwise stay first-found (synergy tie-break on an
            // undecided deck regressed 11/50 → 8/50; the gate avoids
            // biasing the early-act picks before the run has a
            // direction).
            // Threshold of 3 enablers before the synergy tie-break
            // engages. Lower (= 2) regressed 11/50 → 10/50 because the
            // bias kicked in before the deck had a real direction.
            // Higher (= 4) is equivalent to dormant — almost no
            // 50-seed run reaches 4 enablers of one archetype.
            var committedThreshold = 3;
            var committed = false;
            foreach (var v in affinity.Values)
                if (v >= committedThreshold) { committed = true; break; }

            CardTier bestTier = CardTier.F;
            for (var i = 0; i < cards.Count; i++)
            {
                var t = TierOf(cards[i].Id);
                if ((int)t > (int)bestTier) bestTier = t;
            }
            var bestIdx = 0;
            var bestSynergy = int.MinValue;
            for (var i = 0; i < cards.Count; i++)
            {
                if (TierOf(cards[i].Id) != bestTier) continue;
                var synergy = committed ? SynergyBonus(cards[i].Id, affinity) : 0;
                if (synergy > bestSynergy)
                {
                    bestSynergy = synergy;
                    bestIdx = i;
                }
            }
            // Re-evaluate for the skip path: always compute the synergy
            // of the picked card (regardless of `committed`) so the
            // override fires on emerging-but-not-yet-committed archetypes
            // too.
            bestSynergy = SynergyBonus(cards[bestIdx].Id, affinity);

            // Deck-size-aware skip threshold:
            //   - small deck (<= 12): take everything (greedy stage)
            //   - mid deck (13-18): take C-tier or better
            //   - large deck (>= 19): only take A-tier or better
            //     (extra cards dilute payoffs in a 3-act run)
            //
            // Synergy-aware override: a B-tier card that buffs an
            // archetype the deck has already seeded (>=2 enablers) is
            // worth taking even past the size cap. Captured by checking
            // if the synergy bonus alone meets a small floor.
            // 50-seed sweep verified the current thresholds. Tighter
            // skip (require B+ in mid-deck) regresses 11/50 → 8/50:
            // the planner currently extracts value from C-tier cards
            // that fill out short Act-1 hands (more options per turn
            // beats narrower-archetype focus given a 1-turn planner).
            var threshold = state.DeckSize switch
            {
                <= 12 => (int)CardTier.D,
                <= 18 => (int)CardTier.C,
                _     => (int)CardTier.A,
            };
            // Synergy-aware override: if the best card pays off heavily
            // from the deck (sum >= 20), take it even if below the
            // tier threshold. Captures "this B-tier closes an archetype
            // gap" cases that flat tier-only logic would skip.
            var synergyOverride = bestSynergy >= 20
                && (int)bestTier >= (int)CardTier.C;

            if (head.CanSkip && (int)bestTier < threshold && !synergyOverride)
                return new SkipReward(head.Index);

            return new SelectReward(head.Index, cards[bestIdx].Index);
        }

        // Non-card rewards (gold, relic, potion): claim.
        return new SelectReward(head.Index);
    }

    // Per-archetype count of "enabler" cards present in the run-true
    // deck. Used as the deck's commitment score per archetype — more
    // enablers = stronger pull toward that archetype.
    private Dictionary<Archetype, int> ComputeDeckAffinity()
    {
        var result = new Dictionary<Archetype, int>();
        if (_tracker is null) return result;
        foreach (var card in _tracker.Cards)
        {
            var profile = CardArchetypes.Of(card);
            foreach (var a in profile.Enables)
                result[a] = result.GetValueOrDefault(a) + 1;
        }
        return result;
    }

    // Synergy bonus — used only as a tie-breaker within the
    // highest-tier offers. Sum payoff weight × deck commitment for
    // each archetype the card pays off from, minus anti-synergy
    // penalty when the deck is committed to an archetype the card
    // breaks. Caller path ensures this never overrides a tier gap.
    private static int SynergyBonus(CardId id, Dictionary<Archetype, int> affinity)
    {
        var profile = CardArchetypes.Of(id);
        var bonus = 0;
        foreach (var a in profile.PayoffsFrom)
        {
            var commitment = affinity.GetValueOrDefault(a);
            if (commitment >= 1) bonus += 5 + 4 * commitment;
        }
        foreach (var a in profile.AntiSynergyWith)
        {
            var commitment = affinity.GetValueOrDefault(a);
            if (commitment >= 2) bonus -= 8 * commitment;
        }
        return bonus;
    }


    private enum CardTier
    {
        F = 0,  // headless-unsafe / catalog-untracked → don't draft
        D = 1,  // niche or risky
        C = 2,  // playable but redundant with starter (Strike/Defend)
        B = 3,  // useful workhorses
        A = 4,  // strong picks
        S = 5,  // run-defining
    }

    private static CardTier TierOf(CardId id)
    {
        // Hard "don't draft" — these cards are status / curse fillers
        // we never want, or have hidden sub-flows our simulator can't
        // value at all. Headbutt used to be on this list but is now
        // draftable: even though the discard→draw cycle effect is
        // unmodelled, the 9 damage alone is fine and the cycle effect
        // is positive in any Block/Cycle deck the synergy bonus picks
        // up on. BurningPact / DualWield / InfernalBlade stay F because
        // their value is *entirely* in the unmodelled sub-flow.
        switch (id)
        {
            case CardId.Headbutt:
            case CardId.BurningPact:
            case CardId.Armaments:
            case CardId.DualWield:
            case CardId.InfernalBlade:
            case CardId.Infection:
                return CardTier.F;
        }

        return id switch
        {
            // S-tier — durable combat-defining buffs.
            CardId.DemonForm => CardTier.S,
            CardId.Barricade => CardTier.S,
            CardId.Bludgeon => CardTier.S,
            CardId.Corruption => CardTier.S, // 0-cost skill-spam + exhaust scaling: run-defining

            // A-tier — strong scalers / utility powers.
            CardId.Inflame => CardTier.A,
            CardId.FeelNoPain => CardTier.A,
            CardId.DarkEmbrace => CardTier.A,
            CardId.PommelStrike => CardTier.A,
            CardId.ShrugItOff => CardTier.A,
            CardId.Impervious => CardTier.A,
            CardId.Bash => CardTier.A,
            CardId.Whirlwind => CardTier.A, // X-cost AoE — 5/8 per energy to all enemies
            CardId.Offering => CardTier.A,  // burst draw + energy, even at 6 HP cost
            CardId.Juggernaut => CardTier.A,// per-block damage scales with Barricade/FeelNoPain decks
            CardId.PactsEnd => CardTier.A,  // 17 AoE once exhaust pile online

            // B-tier — workhorse damage / block.
            CardId.IronWave => CardTier.B,
            CardId.TwinStrike => CardTier.B,
            CardId.Uppercut => CardTier.B,
            CardId.BodySlam => CardTier.B,
            CardId.Thunderclap => CardTier.B,
            CardId.SwordBoomerang => CardTier.B,
            CardId.BattleTrance => CardTier.B,
            CardId.Bloodletting => CardTier.B,
            CardId.Hemokinesis => CardTier.B,
            CardId.Shockwave => CardTier.B,
            CardId.FlameBarrier => CardTier.B,
            CardId.Entrench => CardTier.B,
            CardId.Rage => CardTier.B,
            CardId.SecondWind => CardTier.B,
            CardId.FiendFire => CardTier.B,
            CardId.Feed => CardTier.B,
            CardId.StoneArmor => CardTier.B, // PlatedArmor power
            CardId.PerfectedStrike => CardTier.B, // 6 + 2 per Strike — fine pick in Strike decks
            CardId.Rampage => CardTier.B,         // 9 base, in-combat scaling (planner under-estimates but engine is right)

            // A/B-tier additions from STS2 Vulnerable archetype (per the
            // 2026-05 research deliverable):
            //   - Tremble: 3 Vulnerable to all enemies (exhaust). Common skill.
            //   - Bully: 4 + 2/Vuln. 0-cost uncommon attack.
            //   - Dismantle: 8 dmg, hits twice if Vuln.
            //   - Taunt: 7 block + 1 Vuln AoE.
            CardId.Tremble => CardTier.A,
            CardId.Bully => CardTier.A,
            CardId.Dismantle => CardTier.A,
            CardId.Taunt => CardTier.B,
            CardId.AshenStrike => CardTier.B, // 6+3/exhaust — scales fast
            CardId.Brand => CardTier.B,        // pivot card, but only A in decks committed to Self-Damage/Exhaust
            CardId.Rupture => CardTier.B,    // Self-Damage power core (modelled). Tier-A regressed 11/50 → 9/50.
            // Newly modelled (this commit): Spite as 6/2-hit attack,
            // Inferno as Combust-style 6/turn AoE — approximations
            // (see catalog comments for accuracy notes).
            // Tiers are conservative: Inferno-as-Combust is an
            // *overstatement* (real triggers per HP-loss event, model
            // triggers every turn) and a B keeps the agent from
            // chasing the false ramp.
            CardId.Spite => CardTier.C,
            CardId.Inferno => CardTier.B,
            // Cruelty / Hellraiser still NOT modelled — their effects
            // (Vulnerable damage multiplier, auto-Strike loop) can't
            // be captured in the declarative catalog and a Custom
            // handler for either would need new SimState fields.
            // Leave at F-tier so the planner doesn't draft dead cards.

            // C-tier — redundant or situational.
            CardId.Anger => CardTier.C,
            CardId.TrueGrit => CardTier.C,
            CardId.BloodWall => CardTier.C,
            CardId.Havoc => CardTier.C,

            // D-tier — playable but risky / unmodelled-effect.
            CardId.Clash => CardTier.D,
            CardId.Cascade => CardTier.D,
            CardId.ExpectAFight => CardTier.D,
            CardId.CrimsonMantle => CardTier.D,

            _ => CardTier.F,
        };
    }
}
