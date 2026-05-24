using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.BattleAgent;

// Concrete card-pair synergies the draft policy can act on. The
// previous synergy logic was archetype-aggregate ("deck has 3+
// Vulnerable enablers → boost Vulnerable payoffs") and gated on a
// 3-enabler threshold that almost never fires in A0 sample decks.
//
// This table is *pair-level* from
// documentation/agent-tuning/research-archetypes-synergies.md §2.
// When card B is offered AND card A is already in the run-deck,
// add Score(A,B) to B's draft score. The bonus stacks across all
// pairs the offered card forms with existing deck cards.
//
// Magnitudes are calibrated for tier-equivalence:
//   S pair (35) ≈ one full tier step (a B-tier card with an S-pair
//                  partner already in deck plays like an A-tier).
//   A pair (20) ≈ half a tier.
//   B pair (10) ≈ within-tier tiebreak.
//
// Symmetric: Pair(A, B) implies Pair(B, A). The lookup tries both
// directions.
public static class CardPairSynergy
{
    public sealed record Pair(CardId A, CardId B, int Score, string Reason);

    private static readonly Pair[] Pairs = new[]
    {
        // ── Strength scaling ──────────────────────────────────────
        new Pair(CardId.Inflame, CardId.Whirlwind, 50, "every Whirlwind hit scales w/ Strength"),
        new Pair(CardId.Inflame, CardId.Dismantle, 35, "Vuln × Str compounds"),
        new Pair(CardId.Inflame, CardId.TwinStrike, 35, "2 hits × Str"),
        new Pair(CardId.Inflame, CardId.PerfectedStrike, 28, "Str adds per Strike"),
        new Pair(CardId.Inflame, CardId.PommelStrike, 28, "Str + draw"),
        new Pair(CardId.Inflame, CardId.Bludgeon, 28, "high-base single hit + Str"),
        new Pair(CardId.DemonForm, CardId.Whirlwind, 50, "every turn adds Str to every hit"),
        new Pair(CardId.DemonForm, CardId.Bludgeon, 30, "late-act snowball"),
        new Pair(CardId.Brand, CardId.Whirlwind, 35, "Brand-fed Str on multi-hit"),
        new Pair(CardId.Brand, CardId.PerfectedStrike, 28, "Str + Strike-count"),

        // ── Vulnerable ────────────────────────────────────────────
        new Pair(CardId.Tremble, CardId.Bully, 50, "3 Vuln → Bully hits ~10 for 0E"),
        new Pair(CardId.Tremble, CardId.Dismantle, 50, "Vuln → 16 dmg for 1E"),
        new Pair(CardId.Tremble, CardId.Cruelty, 35, "Vuln amp multiplier"),
        new Pair(CardId.Tremble, CardId.PactsEnd, 28, "Vuln × 17 AoE finisher"),
        new Pair(CardId.Bash, CardId.Bully, 28, "single-target Vuln payoff"),
        new Pair(CardId.Bash, CardId.Dismantle, 35, "1E setup → 1E payoff"),
        new Pair(CardId.Taunt, CardId.Bully, 28, "block + Vuln + payoff turn"),

        // ── Self-Damage / Rupture / Inferno ───────────────────────
        new Pair(CardId.Rupture, CardId.Brand, 50, "HP loss + Str + Exhaust"),
        new Pair(CardId.Rupture, CardId.CrimsonMantle, 50, "1 HP/turn → +Str passively"),
        new Pair(CardId.Rupture, CardId.Hemokinesis, 50, "15 dmg + auto-trigger Str"),
        new Pair(CardId.Rupture, CardId.Bloodletting, 35, "cheap energy + +Str per cast"),
        new Pair(CardId.Rupture, CardId.BloodWall, 35, "16 block + Str trigger"),
        new Pair(CardId.Rupture, CardId.Spite, 28, "HP loss triggers Spite + Str"),
        new Pair(CardId.Inferno, CardId.Brand, 50, "0E HP-loss → 6-9 AoE every turn"),
        new Pair(CardId.Inferno, CardId.CrimsonMantle, 50, "passive 6-9 AoE every turn"),
        new Pair(CardId.Inferno, CardId.Hemokinesis, 35, "single-target + AoE per HP-loss"),
        new Pair(CardId.Inferno, CardId.Bloodletting, 35, "cheap HP-loss trigger"),
        new Pair(CardId.Inferno, CardId.BloodWall, 35, "16 block + AoE trigger"),
        new Pair(CardId.CrimsonMantle, CardId.BloodWall, 28, "block stack + HP-loss"),

        // ── Exhaust ───────────────────────────────────────────────
        new Pair(CardId.Corruption, CardId.FeelNoPain, 50, "free skills + 3-4 block per skill"),
        new Pair(CardId.Corruption, CardId.DarkEmbrace, 50, "free skills + draw on each — pseudo-infinite"),
        new Pair(CardId.Corruption, CardId.AshenStrike, 35, "Skills exhaust → pile grows fast"),
        new Pair(CardId.Corruption, CardId.PactsEnd, 35, "Skills exhaust → 3-pile threshold by T2"),
        new Pair(CardId.DarkEmbrace, CardId.FeelNoPain, 28, "both fire on every Exhaust"),
        new Pair(CardId.DarkEmbrace, CardId.Brand, 28, "Brand exhausts a card → draw + Str"),
        new Pair(CardId.DarkEmbrace, CardId.Tremble, 20, "Tremble self-exhausts → +1 draw"),
        new Pair(CardId.FeelNoPain, CardId.Tremble, 20, "self-exhaust → block"),
        new Pair(CardId.FiendFire, CardId.Corruption, 28, "exhaust pile loaded fast"),
        new Pair(CardId.AshenStrike, CardId.Tremble, 20, "Tremble feeds pile, AshenStrike scales"),
        new Pair(CardId.PactsEnd, CardId.Tremble, 20, "Vuln + 17 AoE"),

        // ── Block / Barricade / Body Slam ─────────────────────────
        new Pair(CardId.Barricade, CardId.BodySlam, 50, "block doesn't decay → BodySlam scales"),
        new Pair(CardId.Barricade, CardId.CrimsonMantle, 50, "passive block + no decay"),
        new Pair(CardId.Barricade, CardId.Juggernaut, 35, "every block instance pings"),
        new Pair(CardId.Barricade, CardId.BloodWall, 35, "16 free permanent block"),
        new Pair(CardId.BodySlam, CardId.Juggernaut, 28, "both convert block to damage"),
        new Pair(CardId.BodySlam, CardId.Entrench, 28, "double block, then convert"),

        // ── Strike cycling (Hellraiser) ───────────────────────────
        new Pair(CardId.Hellraiser, CardId.PommelStrike, 50, "Strike → draw → auto-Strike"),
        new Pair(CardId.Hellraiser, CardId.PerfectedStrike, 35, "auto-played + scales w/ Strikes"),
        new Pair(CardId.Hellraiser, CardId.Anger, 35, "free auto-played Strikes that self-copy"),
        new Pair(CardId.Hellraiser, CardId.Inflame, 28, "every auto-Strike hits harder"),

        // ── X-cost / Cascade ──────────────────────────────────────
        new Pair(CardId.Cascade, CardId.Whirlwind, 50, "cheat out X-cost finisher"),
        new Pair(CardId.Cascade, CardId.Hemokinesis, 35, "auto-play 15-dmg combos"),
        new Pair(CardId.Cascade, CardId.Offering, 28, "energy + draw + cheat plays"),
        new Pair(CardId.Whirlwind, CardId.Offering, 35, "Offering's +2 energy = +2 Whirlwind hits"),
        new Pair(CardId.Whirlwind, CardId.Bloodletting, 28, "+2 energy → +2 Whirlwind hits"),

        // ── Other notable ─────────────────────────────────────────
        new Pair(CardId.SecondWind, CardId.Corruption, 35, "exhaust pile loader + skill spam"),
        new Pair(CardId.SecondWind, CardId.FeelNoPain, 28, "exhausting feeds block engine"),
        new Pair(CardId.BattleTrance, CardId.Corruption, 28, "burst draw → free skills"),
    };

    // Indexed lookup. Pair (A, B) is queried both ways.
    private static readonly Dictionary<(CardId, CardId), int> Scores = BuildIndex();

    private static Dictionary<(CardId, CardId), int> BuildIndex()
    {
        var dict = new Dictionary<(CardId, CardId), int>();
        foreach (var p in Pairs)
        {
            dict[(p.A, p.B)] = p.Score;
            dict[(p.B, p.A)] = p.Score;
        }
        return dict;
    }

    public static int ScoreBetween(CardId a, CardId b)
        => Scores.TryGetValue((a, b), out var v) ? v : 0;

    // Total synergy of `candidate` against the cards already in
    // `deck`. Sum over each deck card (including duplicates — two
    // Bashes mean Bully gets the bonus twice). Capped so a deck
    // with 5 Strikes doesn't make every Strike-named offer look
    // S-tier on the cumulative count.
    public static int DeckSynergyOf(CardId candidate, IReadOnlyList<CardId> deck)
    {
        var sum = 0;
        var seenPartners = new Dictionary<CardId, int>();
        foreach (var inDeck in deck)
        {
            var s = ScoreBetween(candidate, inDeck);
            if (s <= 0) continue;
            // Diminishing returns on duplicate partners: full score
            // for the first instance, half for the second, quarter
            // for the third+. Avoids "I already have 4 Strikes" from
            // pumping a PerfectedStrike offer past A-tier.
            var prior = seenPartners.GetValueOrDefault(inDeck);
            var contribution = prior switch
            {
                0 => s,
                1 => s / 2,
                _ => s / 4,
            };
            sum += contribution;
            seenPartners[inDeck] = prior + 1;
        }
        return sum;
    }
}
