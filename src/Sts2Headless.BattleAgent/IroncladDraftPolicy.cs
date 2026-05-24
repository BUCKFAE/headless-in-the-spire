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
    private const int SkipBelow = (int)CardTier.C;

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

            var bestTier = CardTier.F;
            var bestIdx = 0;
            for (var i = 0; i < cards.Count; i++)
            {
                var t = TierOf(cards[i].Id);
                if ((int)t > (int)bestTier)
                {
                    bestTier = t;
                    bestIdx = i;
                }
            }

            if (head.CanSkip && (int)bestTier < SkipBelow)
                return new SkipReward(head.Index);

            return new SelectReward(head.Index, cards[bestIdx].Index);
        }

        // Non-card rewards (gold, relic, potion): claim.
        return new SelectReward(head.Index);
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
        // Hard "don't draft" — these cards either trigger headless-only
        // crashes or are status/curse fillers we never want in deck.
        // Whirlwind WAS in this list (legacy NRE on play); the engine
        // fix landed and the catalog now models it as 5-per-energy AoE,
        // so it's draftable. PerfectedStrike, PactsEnd, Rampage all
        // received proper modelling in the 2026-05-24 scaling pass.
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
            CardId.Rupture => CardTier.B,
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
            CardId.Brand => CardTier.B,        // +1 Str / -1 HP — Strength source

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
