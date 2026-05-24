using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.BattleAgent.Core;

// Immutable snapshot of combat state used by the planner. Built from the
// wire's CombatState by SimStateBuilder; transformed by ICombatModel
// during search. Equality is value-based so transposition tables can key
// directly on SimState.
//
// Pile contents: Hand is always known (the wire exposes per-card detail).
// DrawPile/DiscardPile/ExhaustPile are tracked as counts in v1 — the
// engine ships piles as integers only. When a card draws, we know one
// card came out of the deck but not which one; the simulator advances
// the draw count without adding a card to Hand. Cards being a one-turn
// horizon, this loses some accuracy on draw-heavy plays (Pommel Strike,
// Battle Trance) but never simulates a card we don't actually hold.
public sealed record SimState(
    int Hp,
    int MaxHp,
    int Energy,
    int MaxEnergyPerTurn,
    int Block,
    int Turn,
    PlayerStatus Status,
    IReadOnlyList<SimCard> Hand,
    int DrawPileCount,
    int DiscardPileCount,
    int ExhaustPileCount,
    IReadOnlyList<SimEnemy> Enemies,
    // How many cards drawn so far during this player turn from in-turn
    // effects (Pommel Strike etc). Used to bound search and to credit
    // "drew a card" as a partial positive in the evaluator.
    int CardsDrawnThisTurn,
    // Set when Apply returns a state we should not transition out of —
    // e.g. headless-unsafe card or any other unrecoverable mid-turn
    // condition. Planners must treat this as a dead end.
    bool IsInvalid,
    // Count of "Strike"-named cards (StrikeIronclad, PerfectedStrike,
    // PommelStrike, TwinStrike, AshenStrike, …) anywhere in the deck —
    // draw pile + discard + exhaust + hand. PerfectedStrike's damage
    // formula reads this directly: 6 (+2 per Strike, +3 upgraded). The
    // wire doesn't expose deck composition during combat; SimStateBuilder
    // initialises this from the visible hand only and the agent can
    // override via SimStateBuilder.FromWire's optional parameter once a
    // run-level deck tracker is wired up. Underestimating is conservative
    // — PerfectedStrike still picks a sensible target, just deals less
    // than the engine actually rolls.
    int StrikeCardsInDeck = 0,
    // Wire-string ids of relics currently held by the player. Used by
    // CombatModel to apply known relic bonuses (Strike Dummy adds 3
    // damage to Strike-named cards, etc.). Defaults to empty so legacy
    // tests still work; SimStateBuilder.FromWire populates this from
    // the run state's relics list.
    IReadOnlyCollection<string>? Relics = null,
    // True until the first attack card has been played this combat.
    // Akabeko relic adds +8 damage on the first attack and only the
    // first; tracking the latch on SimState lets the planner correctly
    // value "open with Bash" vs "open with Defend".
    bool AkabekoAvailable = true);

// Player-side status effects. Includes both turn-decaying debuffs
// (Vulnerable, Weak, Frail) and persistent power-card effects (Combust,
// Metallicize, DemonForm, …). One unified bag keeps Apply() simple.
public sealed record PlayerStatus(
    // Stat modifiers
    int Strength = 0,
    int Dexterity = 0,
    // Standard debuffs — countdown each end-of-turn
    int Vulnerable = 0,
    int Weak = 0,
    int Frail = 0,
    // Power-card effects (persist for the combat). Amounts stack additively.
    int Combust = 0,            // deal N damage to ALL at end of turn (cost: 1 HP)
    // Hellraiser power (STS2 Ironclad rare 2E): whenever you DRAW a
    // Strike-named card, auto-play it on a random enemy. The
    // simulator doesn't track per-draw card identities (only
    // CardsDrawnThisTurn counter), so we approximate:
    //   end-of-turn damage to highest-HP enemy =
    //     stacks × (6 + Strength) × expectedStrikesDrawn
    //   where expectedStrikesDrawn ≈
    //     CardsDrawnThisTurn × (StrikeCardsInDeck / max(deck_size, 1))
    // For a starter Ironclad deck (5 Strikes / 10 cards) with 5 cards
    // drawn this turn the model expects ~2.5 strikes auto-played for
    // ~15 dmg/turn. A Pommel-Strike-loop deck multiplies this.
    int Hellraiser = 0,
    int Metallicize = 0,        // gain N block at end of turn
    int PlatedArmor = 0,        // gain N block at end of turn; decays 1 on attack damage taken
    int FeelNoPain = 0,         // gain N block when a card is exhausted
    int DarkEmbrace = 0,        // draw N when a card is exhausted
    int FireBreathing = 0,      // deal N damage to ALL when a status/curse is drawn or exhausted
    int Rupture = 0,            // gain N Strength when player loses HP from a card
    int DemonForm = 0,          // gain N Strength at start of every turn
    int Rage = 0,               // gain N block per attack played this turn (resets each turn)
    int Juggernaut = 0,         // deal N damage to a random enemy whenever block is gained
    int Brutality = 0,          // lose N HP and draw N at start of every turn
    int Evolve = 0,             // draw N whenever a status card is drawn
    int Berserk = 0,            // +N max energy at the cost of 2 Vulnerable
    int Barricade = 0,          // block persists across turns when > 0 (Barricade = 1)
    // Corruption power: while > 0, Skills cost 0 and exhaust on play.
    // Tracked as int (stacks additively like any wire power) but treated
    // as boolean in CombatModel — engine never grants > 1.
    int Corruption = 0)
{
    public static PlayerStatus Empty { get; } = new();
}

// One card in any pile. Hand cards carry the original wire hand index so
// SimAgent can translate SimPlayCard → run/play_card with the engine's
// index. Cards drawn during a turn carry index=null and are NOT modelled
// as in-hand in v1 (see SimState comment).
public sealed record SimCard(
    CardId Id,
    int Cost,
    bool Upgraded,
    TargetType TargetType,
    bool CanPlayFlag,
    int? OriginalHandIndex);

// One enemy. Block is single-turn (wiped at end of enemy turn). Intent
// damage on the wire already bakes in Strength + Vulnerable at the time
// the snapshot was taken, so the simulator uses intent.Damage directly
// when resolving EndPlayerTurn rather than re-applying status math.
public sealed record SimEnemy(
    int Index,
    string? MonsterId,
    int Hp,
    int MaxHp,
    int Block,
    int Strength,
    int Vulnerable,
    int Weak,
    EnemyIntent? Intent,
    // For powers we don't typed-model — kept around so the evaluator can
    // still notice "this enemy has a power".
    IReadOnlyList<OpaquePower>? OtherPowers = null)
{
    public bool IsDead => Hp <= 0;
}

public sealed record EnemyIntent(
    IntentKind Kind,
    int Damage,
    int Hits,
    int Block);

// An enemy power we don't have first-class support for. Kept by id so
// future model upgrades can promote it to a typed field without changing
// the catalog.
public sealed record OpaquePower(string Id, int Amount);
