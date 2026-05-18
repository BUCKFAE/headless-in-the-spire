namespace Sts2Headless.BattleAgent.Core;

// Forward-simulation contract. Implementations describe how a SimState
// evolves under player actions and end-of-turn resolution.
//
// Pluggable so we can swap in:
//   - the reference CombatModel for production agents
//   - a test model with deterministic damage / no statuses for unit
//     tests that focus on planner behaviour
//   - a future "real-engine-fork" model that round-trips through the
//     game's CombatManager for ground-truth simulation
public interface ICombatModel
{
    // Actions the player can legally take in this state. Includes one
    // SimEndTurn plus one SimPlayCard per (legal hand index × legal
    // target). Excludes IsHeadlessUnsafe cards.
    IReadOnlyList<SimAction> LegalActions(SimState state);

    // Apply a single player action, returning the next state. The
    // returned state's IsInvalid is true for actions that can't legally
    // resolve (energy mismatch, unsafe card, dead-target attack);
    // planners treat those as dead ends.
    SimState Apply(SimState state, SimAction action);

    // Resolves the end of the player turn: end-of-turn powers (Combust,
    // Metallicize, PlatedArmor block, debuff countdown), then enemy
    // intents apply damage / debuffs, then start of the next player
    // turn (DemonForm strength, Brutality damage+draw, energy refill,
    // block clear unless Barricade). Does NOT draw new hand cards —
    // the simulator doesn't know deck composition, so the post-EOT
    // state has Hand=[].
    SimState EndPlayerTurn(SimState state);

    bool AllEnemiesDead(SimState state);
    bool IsPlayerDead(SimState state);
    bool IsCombatOver(SimState state);
}
