using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Agents;

// Best-effort card-effect database for the cards seen in the seed-42 recon
// (documentation/research/seed42-recon.md). Lets agents reason about
// "damage this card deals" / "block this card grants" / "vulnerable this
// card applies" without round-tripping the engine.
//
// This is NOT a full game database — it's a small fixture for the
// Ironclad starter deck plus the cards offered along seed 42's path. When
// the agent encounters an unknown card it falls back to a neutral
// estimate (Score=0) so the agent treats it as "no information," not
// "definitely bad."
//
// All numbers are unupgraded base values. Upgrades are noted in comments;
// the agent treats every card as unupgraded today (Armaments is a future
// slice). When sts2's card model is exposed on the wire (a separate
// engine-binding decision), this file becomes a fallback rather than the
// primary source.
public static class CardEffects
{
    public sealed record Effect(
        int Damage = 0,        // per-hit damage (multiply by Hits for total)
        int Hits = 1,
        int Block = 0,         // block applied to self
        int Vulnerable = 0,    // stacks applied to target
        int Weak = 0,          // stacks applied to target
        int Strength = 0,      // stacks applied to self
        bool Exhausts = false, // is the card exhausted on play?
        bool BlockToDamage = false, // body-slam-shape: damage = self block
        bool TargetsAllEnemies = false,
        // Approximate desirability for a generalist Ironclad deck. Used by
        // the reward picker to choose between card options when several
        // are offered. Scale is roughly -3..+5; 0 = neutral / unknown card.
        int DraftScore = 0);

    private static readonly Dictionary<string, Effect> Catalog = new(StringComparer.Ordinal)
    {
        // ── Starter ────────────────────────────────────────────────────────
        ["STRIKE_IRONCLAD"]  = new Effect(Damage: 6, DraftScore: 0),
        ["DEFEND_IRONCLAD"]  = new Effect(Block: 5, DraftScore: 0),
        // Bash is the starter Vulnerable engine. Vulnerable=2 turns of "+50%
        // damage taken" on the target — pivotal vs single-target bosses
        // (VANTOM included).
        ["BASH"]             = new Effect(Damage: 8, Vulnerable: 2, DraftScore: 0),

        // ── Cards offered on seed 42's path ────────────────────────────────
        // F2 rewards.
        // Body Slam: damage = current player block, cost 1. Synergises
        // with the heavy defensive stance the agent leans on for floor-8
        // Phrog → 4-wriggler survival — a turn of "3 defends → Body Slam
        // for ~16 damage" out-damages SWORD_BOOMERANG's spread on a
        // healthy block deck. Highest priority on F2.
        ["BODY_SLAM"]        = new Effect(BlockToDamage: true, DraftScore: 4),
        // Tremble is a sts2 status-shaped card (loss-of-control). Skip.
        ["TREMBLE"]          = new Effect(DraftScore: -2),
        // Sword Boomerang: 3 hits × 3 damage at random enemies. Strong
        // SLIPPERY drain (3 stacks for cost 1) and decent vs Phrog's
        // wriggler swarm. Slightly under Body Slam in the picker because
        // the agent leans defensive — Body Slam scales with that posture
        // while Sword's per-hit 3 damage is mid-range.
        ["SWORD_BOOMERANG"]  = new Effect(Damage: 3, Hits: 3, TargetsAllEnemies: false, DraftScore: 3),

        // F4 rewards.
        // Headbutt: 9 dmg + place a card from discard on top of draw pile.
        // The "place a card" side-effect routes through CardSelectCmd's
        // screen-create path, which NREs in headless (same shape as the
        // event-handler card-select crash documented in
        // agent-survival-gaps.md). Until that path has a screen stand-in,
        // Headbutt is unplayable — the agent must NEVER pick it. Negative
        // DraftScore guarantees the picker skips it.
        ["HEADBUTT"]         = new Effect(Damage: 9, DraftScore: -5),
        // Expect-A-Fight: a power card; not in the per-card model yet.
        // Neutral score so the picker has no opinion vs alternatives.
        ["EXPECT_A_FIGHT"]   = new Effect(DraftScore: 1),
        // Burning Pact: exhaust 1 card, draw 2. The "exhaust 1 card" leg
        // is a hand-selection step → CardSelectCmd → screen-create NRE in
        // headless (same shape as Headbutt). Avoid until a screen stand-in
        // lands.
        ["BURNING_PACT"]     = new Effect(DraftScore: -5),

        // F5 rewards.
        ["BULLY"]            = new Effect(Damage: 8, DraftScore: 0),
        // Thunderclap: 4 dmg AOE + 1 vuln AOE. Multi-target — irrelevant
        // for the boss but decent for mid-Act-1 multi-enemy fights.
        ["THUNDERCLAP"]      = new Effect(Damage: 4, TargetsAllEnemies: true, Vulnerable: 1, DraftScore: 2),
        // **Bludgeon: 32 single-target damage, cost 3.** SLIPPERY-5 still
        // leaves 27 damage landing per swing — the boss-killing card on
        // this path. Highest priority pick.
        ["BLUDGEON"]         = new Effect(Damage: 32, DraftScore: 5),

        // F8 (elite) rewards.
        // Dismantle: removes a card from your deck. Useful long-term but
        // we're playing a short Act 1; neutral.
        ["DISMANTLE"]        = new Effect(DraftScore: 0),
        // Cascade: speculative. Neutral.
        ["CASCADE"]          = new Effect(DraftScore: 0),

        // F9 rewards.
        // Uppercut: 13 dmg + 1 Weak + 1 Vulnerable, cost 2. Excellent vs
        // bosses — single-hit cleanly pierces SLIPPERY and adds vuln.
        ["UPPERCUT"]         = new Effect(Damage: 13, Weak: 1, Vulnerable: 1, DraftScore: 4),
        // Armaments: 5 block + upgrade a card in hand. The upgrade leg
        // routes through CardSelectCmd → headless screen-create NRE.
        // Avoid until a screen stand-in lands.
        ["ARMAMENTS"]        = new Effect(Block: 5, DraftScore: -5),
        // Stone Armor: gain 1 plated armor (block at start of every turn).
        ["STONE_ARMOR"]      = new Effect(Block: 0, DraftScore: 2),

        // F12 rewards.
        // True Grit: 7 block + exhaust a random card.
        ["TRUE_GRIT"]        = new Effect(Block: 7, Exhausts: true, DraftScore: 3),
        // Second Wind: exhaust all skills, gain 5 block per exhaust.
        ["SECOND_WIND"]      = new Effect(DraftScore: 1),

        // F15 rewards.
        // Taunt: force enemies to attack you next turn. Mostly irrelevant
        // since enemies attack the player by default in 1v1 and N-v-1.
        ["TAUNT"]            = new Effect(DraftScore: 0),
        // Blood Wall: 4 dmg + 6 block, cost 2. Good defensive attack.
        ["BLOOD_WALL"]       = new Effect(Damage: 4, Block: 6, DraftScore: 3),

        // ── Statuses surfaced in combat that the agent should ignore ───────
        // Infection is the Phrog-Parasite-applied status with cost -1 /
        // canPlay=false — unplayable by construction; modelled here only
        // so the lookup doesn't return null.
        ["INFECTION"]        = new Effect(DraftScore: -5),
    };

    public static Effect Get(string cardId) =>
        Catalog.TryGetValue(cardId, out var e) ? e : new Effect();

    // Estimate damage dealt to the indexed target accounting for the
    // target's powers (Vulnerable on target, Weak on player, Strength on
    // player). Doesn't model SLIPPERY-style flat-reduction abilities — the
    // agent's strategy ranks cards by their *raw* output, and SLIPPERY's
    // effect is captured by the per-card DraftScore.
    public static int EstimateDamage(Card card, Enemy target, CombatState combat)
    {
        var eff = Get(card.Id);
        if (eff.Damage == 0 && !eff.BlockToDamage) return 0;

        var perHit = eff.BlockToDamage ? combat.PlayerBlock : eff.Damage;
        // Strength: +N damage per hit (player Strength only).
        var strength = combat.PlayerPowers.FirstOrDefault(p => p.Id == "STRENGTH_POWER")?.Amount ?? 0;
        perHit += strength;
        // Weak: player has -25% damage output (the convention is "x * 0.75
        // rounded down"). Headless treats Weak amount as boolean for now.
        var playerWeak = combat.PlayerPowers.Any(p => p.Id == "WEAK_POWER");
        if (playerWeak) perHit = (int)(perHit * 0.75);
        // Vulnerable on target: +50% damage taken.
        var targetVuln = target.Powers.Any(p => p.Id == "VULNERABLE_POWER");
        if (targetVuln) perHit = (int)(perHit * 1.5);
        return perHit * eff.Hits;
    }

    // Sum of incoming damage from every enemy that intends to attack
    // this turn. Trusts the wire-surfaced intent.Damage to already include
    // engine-side modifiers (sts2's DamageCalc is the source of this
    // number, and the engine computes it with Strength/Vulnerable already
    // baked in — adding them here would double-count). Block gained this
    // turn is the caller's responsibility to subtract.
    public static int IncomingDamage(CombatState combat)
    {
        var sum = 0;
        foreach (var e in combat.Enemies)
        {
            foreach (var intent in e.Intents)
            {
                if (intent.Damage is not int d) continue;
                var hits = intent.Hits ?? 1;
                sum += d * hits;
            }
        }
        return sum;
    }
}
