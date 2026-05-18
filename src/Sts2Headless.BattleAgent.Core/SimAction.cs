namespace Sts2Headless.BattleAgent.Core;

// Actions the simulator can apply to a SimState. Independent of
// AgentAction (the wire-facing action surface) so the combat framework
// stays portable across hosts. SimAgent translates SimAction →
// AgentAction at the boundary.
public abstract record SimAction;

// Play a card from hand. HandIndex is the position in SimState.Hand, NOT
// the wire's original index — Apply() must look up the SimCard by hand
// position because hand mutates across plays within a turn.
public sealed record SimPlayCard(int HandIndex, int? TargetEnemyIndex) : SimAction;

// End the player turn. The model will resolve end-of-turn effects
// (Combust damage, Metallicize block, debuff decay) then enemy intents
// then start-of-turn effects for the next player turn (DemonForm
// strength gain, Brutality damage+draw, energy refill, block clear).
public sealed record SimEndTurn : SimAction;

// Use a potion. Not exercised in v1's exhaustive search (potions are
// chosen by a separate policy), but defined here so future planners can
// reason about potion-in-combat.
public sealed record SimUsePotion(int PotionIndex, int? TargetEnemyIndex) : SimAction;
