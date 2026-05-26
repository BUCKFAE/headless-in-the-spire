namespace Sts2Headless.BattleAgent.Core;

// Declarative description of what a card does. Most fields are additive
// integer effects; the few cards that don't fit (Body Slam, X-cost
// Whirlwind, Rampage's cumulative buff) get a Custom delegate.
//
// CardEffect is per-card — upgrades are encoded as a separate "Upgraded"
// CardEffect that the catalog returns when SimCard.Upgraded is true.
//
// Card categories (IsAttack/IsSkill/IsPower) drive both ordering rules
// in the planner (powers first, then debuffs, then attacks) and a few
// side effects (Rage triggers off attacks, SecondWind exhausts
// non-attacks for block).
public sealed record CardEffect(
    // Categorisation
    bool IsAttack = false,
    bool IsSkill = false,
    bool IsPower = false,
    bool IsStatus = false,
    bool IsCurse = false,

    // Damage
    int Damage = 0,             // per-hit damage before status math
    int Hits = 1,
    bool TargetsAllEnemies = false,
    bool BlockToDamage = false, // damage = current player block (Body Slam)

    // Block + stats
    int Block = 0,
    int StrengthGain = 0,
    int DexterityGain = 0,
    bool LimitBreakDoubleStrength = false,

    // Debuffs applied to enemy target (or all if TargetsAllEnemies)
    int VulnerableApply = 0,
    int WeakApply = 0,
    int StrengthLossApply = 0,  // Strength reduction (-N) e.g. Bully? or Brand TBD

    // Self-affecting effects
    int Frail = 0,              // applied to self (rare)
    int SelfDamage = 0,         // pay HP (Bloodletting, Hemokinesis, Offering)
    int HealHp = 0,
    int EnergyGain = 0,
    int DrawCards = 0,
    int RemoveDebuffsFromSelf = 0, // Apotheosis? Self-cleanse (not Ironclad usually)

    // Persistent powers (when IsPower)
    int CombustGain = 0,
    int MetallicizeGain = 0,
    int PlatedArmorGain = 0,
    int FeelNoPainGain = 0,
    int DarkEmbraceGain = 0,
    int FireBreathingGain = 0,
    int RuptureGain = 0,
    int DemonFormGain = 0,
    int RageGain = 0,
    int JuggernautGain = 0,
    int BrutalityGain = 0,
    int EvolveGain = 0,
    int BerserkGain = 0,
    int BarricadeGain = 0,
    int CorruptionGain = 0,
    int HellraiserGain = 0,

    // Card-flow effects
    bool Exhausts = false,        // exhausts on play
    bool Ethereal = false,        // exhausts at end of turn if unplayed
    bool Innate = false,          // drawn in opening hand (informational)
    bool Retain = false,          // not discarded at end of turn (informational)
    int ExhaustRandomFromHand = 0,
    int DiscardRandom = 0,
    int DiscardForBlock = 0,      // SecondWind shape: per non-attack discard, gain N block

    // Escape hatch for cards that genuinely don't fit the above shape.
    // Receives an in-place mutable copy of the state and returns the
    // post-play state. Used for X-cost cards, Body Slam-style and any
    // truly bespoke logic.
    Func<CardEffectContext, SimState>? Custom = null);

// Context passed to Custom card handlers. Carries pre-play state, the
// SimCard played (so the handler can read Upgraded etc.), and the
// optional enemy target index.
public sealed record CardEffectContext(
    SimState State,
    SimCard Card,
    int? TargetIndex,
    ICardEffectCatalog Catalog);
