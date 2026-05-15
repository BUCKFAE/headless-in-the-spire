using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Agents;

// Static card-mechanics database for the cards seen along seed-42's path
// (documentation/research/seed42-recon.md). Pure facts about each card —
// "does it deal damage?", "how much block?", "does it exhaust on play?" —
// independent of who's playing or which seed is in flight.
//
// This is NOT a full game database. It's a small fixture for the Ironclad
// starter deck plus everything offered along seed 42 so the Seed42Agent
// can reason about plays without round-tripping the engine. Unknown cards
// fall back to a neutral Mechanics() so callers can treat them as "no
// information" rather than "definitely bad."
//
// When sts2's card model eventually surfaces on the wire (a separate
// engine-binding decision), this file becomes a fallback rather than the
// primary source.
public static class CardMechanics
{
    // What the card does, mechanically. No agent preferences live here —
    // a Seed42-specific "should I draft this?" score lives next to the
    // agent that uses it. Things that *every* agent must respect (e.g.
    // "this card NREs in headless") get their own boolean rather than
    // being smuggled into a score field.
    public sealed record Mechanics(
        int Damage = 0,        // per-hit damage (multiply by Hits for total)
        int Hits = 1,
        int Block = 0,         // block applied to self
        int Vulnerable = 0,    // stacks applied to target
        int Weak = 0,          // stacks applied to target
        int Strength = 0,      // stacks applied to self
        bool Exhausts = false, // is the card exhausted on play?
        bool BlockToDamage = false, // body-slam-shape: damage = self block
        bool TargetsAllEnemies = false,
        // True when the card's side effects route through a screen / sub-
        // flow we haven't wired up in headless (today: any card-select
        // sub-flow — Headbutt, Burning Pact, Armaments). Playing or
        // drafting these throws inside sts2; every agent must avoid them.
        // When the missing sub-flow is implemented, flip this to false.
        bool IsHeadlessUnsafe = false);

    private static readonly Dictionary<CardId, Mechanics> Catalog = new()
    {
        // ── Starter ────────────────────────────────────────────────────────
        [CardId.StrikeIronclad]  = new Mechanics(Damage: 6),
        [CardId.DefendIronclad]  = new Mechanics(Block: 5),
        // Bash is the starter Vulnerable engine. Vulnerable=2 turns of "+50%
        // damage taken" on the target — pivotal vs single-target bosses
        // (VANTOM included).
        [CardId.Bash]            = new Mechanics(Damage: 8, Vulnerable: 2),

        // ── Cards offered on seed 42's path ────────────────────────────────
        // F2 rewards.
        // Body Slam: damage = current player block, cost 1.
        [CardId.BodySlam]        = new Mechanics(BlockToDamage: true),
        // Tremble is a sts2 status-shaped card (loss-of-control).
        [CardId.Tremble]         = new Mechanics(),
        // Sword Boomerang: 3 hits × 3 damage at random enemies.
        [CardId.SwordBoomerang]  = new Mechanics(Damage: 3, Hits: 3, TargetsAllEnemies: false),

        // F4 rewards.
        // Headbutt: 9 dmg + place a card from discard on top of draw pile.
        // The "place a card" side-effect routes through CardSelectCmd's
        // screen-create path, which NREs in headless. Marked unsafe until
        // the card-select screen stand-in lands.
        [CardId.Headbutt]        = new Mechanics(Damage: 9, IsHeadlessUnsafe: true),
        // Expect-A-Fight: a power card.
        [CardId.ExpectAFight]    = new Mechanics(),
        // Burning Pact: exhaust 1 card, draw 2. The "exhaust 1 card" leg
        // is a hand-selection step → CardSelectCmd → screen-create NRE in
        // headless. Marked unsafe until a screen stand-in lands.
        [CardId.BurningPact]     = new Mechanics(IsHeadlessUnsafe: true),

        // F5 rewards.
        [CardId.Bully]           = new Mechanics(Damage: 8),
        // Thunderclap: 4 dmg AOE + 1 vuln AOE.
        [CardId.Thunderclap]     = new Mechanics(Damage: 4, TargetsAllEnemies: true, Vulnerable: 1),
        // Bludgeon: 32 single-target damage, cost 3.
        [CardId.Bludgeon]        = new Mechanics(Damage: 32),

        // F8 (elite) rewards.
        [CardId.Dismantle]       = new Mechanics(),
        [CardId.Cascade]         = new Mechanics(),

        // F9 rewards.
        // Uppercut: 13 dmg + 1 Weak + 1 Vulnerable, cost 2.
        [CardId.Uppercut]        = new Mechanics(Damage: 13, Weak: 1, Vulnerable: 1),
        // Armaments: 5 block + upgrade a card in hand. The upgrade leg
        // routes through CardSelectCmd → headless screen-create NRE.
        // Marked unsafe until a screen stand-in lands.
        [CardId.Armaments]       = new Mechanics(Block: 5, IsHeadlessUnsafe: true),
        // Stone Armor: gain 1 plated armor (block at start of every turn).
        // Not yet modelled as block on the wire; the agent only cares that
        // it's a power-shaped card.
        [CardId.StoneArmor]      = new Mechanics(),

        // F12 rewards.
        [CardId.TrueGrit]        = new Mechanics(Block: 7, Exhausts: true),
        [CardId.SecondWind]      = new Mechanics(),

        // F15 rewards.
        [CardId.Taunt]           = new Mechanics(),
        // Blood Wall: 4 dmg + 6 block, cost 2.
        [CardId.BloodWall]       = new Mechanics(Damage: 4, Block: 6),

        // ── Statuses surfaced in combat that the agent should ignore ───────
        // Infection is the Phrog-Parasite-applied status with cost -1 /
        // canPlay=false — unplayable by construction; modelled here only
        // so the lookup doesn't return null.
        [CardId.Infection]       = new Mechanics(),
    };

    public static Mechanics Get(CardId cardId) =>
        Catalog.TryGetValue(cardId, out var e) ? e : new Mechanics();

    // Snapshot of every CardId for which Catalog has an explicit entry.
    // CardMechanicsCoverageTests uses this (combined with the
    // NotYetModelledCards set in the test) to assert nothing falls through
    // the cracks when sts2 ships a new card.
    public static IReadOnlyCollection<CardId> ModelledCardIds => Catalog.Keys;

    // Estimate damage dealt to the indexed target accounting for the
    // target's powers (Vulnerable on target, Weak on player, Strength on
    // player). Doesn't model SLIPPERY-style flat-reduction abilities —
    // the agent's strategy handles SLIPPERY separately via per-card
    // priority (cheapest multi-hit cards first).
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
}
