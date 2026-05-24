using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.BattleAgent;

// Ironclad archetypes the draft policy reasons about. Not mutually
// exclusive — many decks blend two or more. Each card has 0..N
// Enables archetypes (it brings the engine into play) and 0..N
// PayoffsFrom archetypes (it's stronger when enablers are present).
// Cards can be both (Corruption enables Exhaust AND pays off from
// already-Exhaust decks via Ashen Strike-like reinforcement).
//
// Synergy graph derived from
// documentation/agent-tuning/research-archetypes-synergies.md.
// Where the research has LOW CONFIDENCE on a card, the profile is
// intentionally conservative — drop the card from enabler counts
// when we don't know its actual behaviour.
public enum Archetype
{
    None = 0,
    Strength,         // Inflame, DemonForm, Brand → Heavy Blade / Whirlwind / multi-hit
    Vulnerable,       // Bash, Tremble, Taunt → Bully, Dismantle, Cruelty
    Block,            // Barricade, FeelNoPain, Juggernaut → Body Slam, Crimson Mantle
    SelfDamage,       // Rupture, Inferno, Bloodletting → Hemokinesis, Spite, Crimson Mantle
    Exhaust,          // Corruption, Dark Embrace, FeelNoPain → Fiend Fire, Pact's End, Ashen Strike
    BigXCost,         // Cascade, Whirlwind, energy gen → cheat out X-cost finishers
    StrikeCycle,      // Hellraiser + PommelStrike × N → auto-Strike loop
    Powers,           // Power-stack-heavy decks; durationMultiplier compounds
}

public static class CardArchetypes
{
    private static readonly Archetype[] Empty = Array.Empty<Archetype>();

    public sealed record Profile(
        Archetype[] Enables,
        Archetype[] PayoffsFrom,
        Archetype[] AntiSynergyWith);

    private static readonly Profile EmptyProfile = new(Empty, Empty, Empty);

    public static Profile Of(CardId id) => Map.TryGetValue(id, out var p) ? p : EmptyProfile;

    private static Profile P(
        Archetype[]? enables = null,
        Archetype[]? payoffs = null,
        Archetype[]? anti = null)
        => new(enables ?? Empty, payoffs ?? Empty, anti ?? Empty);

    private static Archetype[] A(params Archetype[] xs) => xs;

    private static readonly Dictionary<CardId, Profile> Map = new()
    {
        // ── Strength sources / payoffs ─────────────────────────────────
        // Inflame and Demon Form are the only standalone Strength sources
        // in STS2's verified catalog (Limit Break / Catalyst may have
        // been removed — see research-archetypes-synergies §0).
        [CardId.Inflame]   = P(enables: A(Archetype.Strength, Archetype.Powers)),
        [CardId.DemonForm] = P(enables: A(Archetype.Strength, Archetype.Powers)),

        // Strength payoffs.
        [CardId.TwinStrike]   = P(payoffs: A(Archetype.Strength, Archetype.Vulnerable)),
        [CardId.PommelStrike] = P(payoffs: A(Archetype.Strength), enables: A(Archetype.StrikeCycle)),
        [CardId.SwordBoomerang] = P(payoffs: A(Archetype.Strength, Archetype.Vulnerable)),
        [CardId.Whirlwind]    = P(enables: A(Archetype.BigXCost),
                                  payoffs: A(Archetype.Strength, Archetype.Vulnerable)),
        [CardId.PerfectedStrike] = P(payoffs: A(Archetype.Strength, Archetype.StrikeCycle)),
        [CardId.Bludgeon]     = P(payoffs: A(Archetype.Strength, Archetype.BigXCost)),

        // ── Vulnerable sources / payoffs ───────────────────────────────
        [CardId.Bash]        = P(enables: A(Archetype.Vulnerable)),
        [CardId.Tremble]     = P(enables: A(Archetype.Vulnerable, Archetype.Exhaust)),
        [CardId.Thunderclap] = P(enables: A(Archetype.Vulnerable)),
        [CardId.Shockwave]   = P(enables: A(Archetype.Vulnerable, Archetype.Exhaust)),
        [CardId.Uppercut]    = P(enables: A(Archetype.Vulnerable)),
        [CardId.Taunt]       = P(enables: A(Archetype.Vulnerable, Archetype.Block)),
        [CardId.Bully]       = P(payoffs: A(Archetype.Vulnerable)),
        [CardId.Dismantle]   = P(payoffs: A(Archetype.Vulnerable, Archetype.Strength)),
        [CardId.Cruelty]     = P(payoffs: A(Archetype.Vulnerable)),

        // ── Block / Barricade / Body Slam ──────────────────────────────
        // The Block archetype needs Barricade to be a real engine.
        // Without Barricade these are a survival kit.
        [CardId.ShrugItOff]   = P(enables: A(Archetype.Block)),
        [CardId.FlameBarrier] = P(enables: A(Archetype.Block)),
        [CardId.Entrench]     = P(payoffs: A(Archetype.Block)),
        [CardId.Impervious]   = P(enables: A(Archetype.Block, Archetype.Exhaust)),
        [CardId.BodySlam]     = P(payoffs: A(Archetype.Block)),
        [CardId.Barricade]    = P(enables: A(Archetype.Block, Archetype.Powers)),
        [CardId.StoneArmor]   = P(enables: A(Archetype.Block, Archetype.Powers)),
        [CardId.Juggernaut]   = P(enables: A(Archetype.Powers),
                                  payoffs: A(Archetype.Block)),
        [CardId.SecondWind]   = P(enables: A(Archetype.Exhaust),
                                  payoffs: A(Archetype.Block)),
        [CardId.BloodWall]    = P(enables: A(Archetype.Block, Archetype.SelfDamage)),

        // ── Self-damage / Rupture / Inferno engine ─────────────────────
        // The triad: Rupture turns HP loss into Strength; Inferno turns
        // HP loss into AoE; Brand is the cheapest HP-loss trigger.
        [CardId.Rupture]      = P(enables: A(Archetype.SelfDamage, Archetype.Powers, Archetype.Strength)),
        [CardId.Inferno]      = P(enables: A(Archetype.SelfDamage, Archetype.Powers)),
        [CardId.Hemokinesis]  = P(enables: A(Archetype.SelfDamage),
                                  payoffs: A(Archetype.Strength)),
        [CardId.Bloodletting] = P(enables: A(Archetype.SelfDamage, Archetype.BigXCost)),
        [CardId.Brand]        = P(enables: A(Archetype.Strength, Archetype.SelfDamage, Archetype.Exhaust)),
        [CardId.Spite]        = P(payoffs: A(Archetype.SelfDamage)),
        [CardId.CrimsonMantle] = P(enables: A(Archetype.Powers, Archetype.SelfDamage),
                                   payoffs: A(Archetype.Block)),
        [CardId.Offering]     = P(enables: A(Archetype.SelfDamage, Archetype.BigXCost)),
        [CardId.Feed]         = P(payoffs: A(Archetype.Strength)),

        // ── Exhaust engine ─────────────────────────────────────────────
        [CardId.Corruption]   = P(enables: A(Archetype.Exhaust, Archetype.Powers)),
        [CardId.FeelNoPain]   = P(enables: A(Archetype.Powers),
                                  payoffs: A(Archetype.Exhaust, Archetype.Block)),
        [CardId.DarkEmbrace]  = P(enables: A(Archetype.Powers),
                                  payoffs: A(Archetype.Exhaust)),
        [CardId.FiendFire]    = P(enables: A(Archetype.BigXCost),
                                  payoffs: A(Archetype.Strength, Archetype.Exhaust)),
        [CardId.PactsEnd]     = P(payoffs: A(Archetype.Exhaust, Archetype.BigXCost)),
        [CardId.AshenStrike]  = P(payoffs: A(Archetype.Exhaust)),
        [CardId.TrueGrit]     = P(enables: A(Archetype.Exhaust),
                                  payoffs: A(Archetype.Block)),

        // ── Big X-cost / Cascade ───────────────────────────────────────
        // Cascade cheats out the top draw-pile card. Whirlwind is the
        // canonical X-cost finisher. Anger is the canonical anti-synergy:
        // it floods the draw pile with low-EV junk that Cascade burns
        // its X on.
        [CardId.Cascade]      = P(enables: A(Archetype.BigXCost),
                                  payoffs: A(Archetype.BigXCost),
                                  anti: A(Archetype.StrikeCycle)),

        // ── Strike-cycling / Hellraiser ────────────────────────────────
        // Hellraiser auto-plays drawn Strikes; PommelStrike draws on
        // damage; the deck loops itself.
        [CardId.Hellraiser]   = P(enables: A(Archetype.StrikeCycle, Archetype.Powers)),
        [CardId.Anger]        = P(payoffs: A(Archetype.StrikeCycle),
                                  anti: A(Archetype.BigXCost)),
        // BattleTrance grants a big draw but applies No Draw — fatal for
        // any cycling archetype that depends on drawing more cards.
        [CardId.BattleTrance] = P(payoffs: A(Archetype.Exhaust),  // burst-draw → Corruption skills
                                  anti: A(Archetype.StrikeCycle)),
        [CardId.BurningPact]  = P(enables: A(Archetype.Exhaust)),

        // ── Generic damage / utility ───────────────────────────────────
        [CardId.IronWave]     = P(payoffs: A(Archetype.Strength)),
        [CardId.Rampage]      = P(payoffs: A(Archetype.Strength)),
        [CardId.Rage]         = P(enables: A(Archetype.Powers)),
        [CardId.ExpectAFight] = P(payoffs: A(Archetype.BigXCost)),

        // ── Headbutt: a tool, not an enabler ───────────────────────────
        // Per the research deliverable (§4): Headbutt does NOT grant
        // block in STS2. It's a discard→draw cycler — a tool that
        // re-decks key cards. Pays off in archetypes that have a
        // specific re-deckable target (Body Slam under Block, finishers
        // under BigXCost, Pact's End / Fiend Fire under Exhaust).
        [CardId.Headbutt]     = P(payoffs: A(Archetype.Block, Archetype.BigXCost, Archetype.Exhaust)),
    };
}
