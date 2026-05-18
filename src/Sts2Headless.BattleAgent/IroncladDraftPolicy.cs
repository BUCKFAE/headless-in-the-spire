using Sts2Headless.Agents;
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
        // Hard "don't draft" — these cards trip the headless host or
        // are unmodelled status fillers.
        switch (id)
        {
            case CardId.Headbutt:
            case CardId.BurningPact:
            case CardId.Armaments:
            case CardId.DualWield:
            case CardId.InfernalBlade:
            case CardId.Infection:
            // Whirlwind NREs in the headless engine — never draft.
            case CardId.Whirlwind:
                return CardTier.F;
        }

        return id switch
        {
            // S-tier — durable combat-defining buffs.
            CardId.DemonForm => CardTier.S,
            CardId.Barricade => CardTier.S,
            CardId.Bludgeon => CardTier.S,

            // A-tier — strong scalers / utility powers.
            CardId.Inflame => CardTier.A,
            CardId.FeelNoPain => CardTier.A,
            CardId.DarkEmbrace => CardTier.A,
            CardId.Corruption => CardTier.A,
            CardId.PommelStrike => CardTier.A,
            CardId.ShrugItOff => CardTier.A,
            CardId.Impervious => CardTier.A,
            CardId.Bash => CardTier.A, // starting copy is fine; duplicates still useful in big fights

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
            CardId.Juggernaut => CardTier.B,
            CardId.Rupture => CardTier.B,
            CardId.SecondWind => CardTier.B,
            CardId.FiendFire => CardTier.B,
            CardId.Feed => CardTier.B,
            CardId.Offering => CardTier.B,
            CardId.StoneArmor => CardTier.B, // PlatedArmor power

            // C-tier — redundant or situational.
            CardId.Anger => CardTier.C,
            CardId.PerfectedStrike => CardTier.C,
            CardId.TrueGrit => CardTier.C,
            CardId.Bully => CardTier.C,
            CardId.BloodWall => CardTier.C,
            CardId.Rampage => CardTier.C,
            CardId.AshenStrike => CardTier.C,
            CardId.Havoc => CardTier.C,

            // D-tier — playable but risky / unmodelled-effect.
            CardId.Clash => CardTier.D,
            CardId.Tremble => CardTier.D,
            CardId.Cascade => CardTier.D,
            CardId.Dismantle => CardTier.D,
            CardId.Taunt => CardTier.D,
            CardId.ExpectAFight => CardTier.D,
            CardId.CrimsonMantle => CardTier.D,
            CardId.Brand => CardTier.D,
            CardId.PactsEnd => CardTier.D,

            _ => CardTier.F,
        };
    }
}
