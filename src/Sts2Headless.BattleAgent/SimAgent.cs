using Sts2Headless.Agents.Authoring;
using Sts2Headless.Agents.Contracts;
using Sts2Headless.BattleAgent.Core;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.BattleAgent;

// Combat-only IAgent. Builds a SimState from the wire CombatState each
// time the host asks for a decision, runs the configured ICombatPlanner,
// and returns the first SimAction of the resulting plan translated to an
// AgentAction.
//
// Re-planning every step (rather than caching the turn's full plan
// across DecideCombat() calls) is deliberate: if our simulator's
// damage prediction is slightly off, or if the engine surfaces state
// that drifts from our model, we always pick the best action under
// fresh-from-engine state. The planner is fast enough that this is
// not a perf concern for v1.
//
// Everything outside combat falls through to HeuristicAgent defaults.
// SimAgent is the smoke-harness agent that pairs with the framework
// alone; the production agent IroncladAgent (later in this project)
// composes SimAgent's combat brain with Draft / Path / Rest / Event
// policies.
public class SimAgent : HeuristicAgent
{
    private readonly ICombatModel _model;
    private readonly IEvaluator _evaluator;
    private readonly ICombatPlanner _planner;
    private readonly PlannerBudget _budget;

    public SimAgent(
        ICombatModel? model = null,
        IEvaluator? evaluator = null,
        ICombatPlanner? planner = null,
        PlannerBudget? budget = null)
    {
        _model = model ?? new CombatModel(IroncladCardCatalog.Instance);
        _evaluator = evaluator ?? new HeuristicEvaluator();
        // Default to the single-turn ExhaustivePlanner — it wins the
        // 10-seed head-to-head against MultiTurnExhaustivePlanner
        // (same 3/10 wins, better avg floor 10.3 vs 9.3) and against
        // MctsPlanner (3/10 vs 0/10). MultiTurn and MCTS stay
        // injectable for the comparison harness and for callers that
        // want different exploration profiles. See
        // tests/.End2EndTests/PlannerComparisonHarness.cs and
        // /tmp/planner-comparison.md for the measured data.
        _planner = planner ?? new ExhaustivePlanner();
        _budget = budget ?? PlannerBudget.Default;
    }

    public string LastPlanSummary { get; private set; } = "";

    protected override AgentAction DecideCombat(RunStateResult state)
    {
        var combat = state.CombatState
            ?? throw new InvalidOperationException(
                $"SimAgent: in {state.CurrentRoomType} but combatState is null.");

        // Potion pre-check: if we're hurt enough that the next big enemy
        // hit threatens lethal, drink any usable potion before planning
        // cards. Naive but high-impact — the previous agent never used
        // potions across a full 10-seed sweep.
        if (TryDecidePotion(state, combat) is { } potionAction)
            return potionAction;

        var sim = SimStateBuilder.FromWire(combat, state.Hp, state.MaxHp);
        var plan = _planner.PlanTurn(sim, _model, _evaluator, _budget, default);
        LastPlanSummary =
            $"plan: {plan.Actions.Count} steps, nodes={plan.NodesExplored}, "
            + $"score={plan.Score:F1}, lethal={plan.IsLethal}";

        if (plan.Actions.Count == 0) return new EndTurn();
        return Translate(plan.Actions[0], sim);
    }

    // Conservative potion-drinking: only fires when (a) we own at least
    // one usable potion AND (b) the player is in real danger — HP below
    // 40% of max OR incoming damage this turn exceeds remaining HP.
    // Picks the first usable potion regardless of effect; most potions
    // (block, attack, energy, draw, heal) are net positive in a combat
    // we're losing. Targeting: pass enemy 0 for AnyEnemy potions; null
    // otherwise. Anything subtler than that requires a typed potion
    // catalog we don't have yet.
    private static AgentAction? TryDecidePotion(RunStateResult state, CombatState combat)
    {
        if (state.OwnedPotions is null) return null;
        OwnedPotion? usable = null;
        foreach (var p in state.OwnedPotions)
        {
            if (p.CanUse) { usable = p; break; }
        }
        if (usable is null) return null;

        // Trigger when wounded or facing imminent lethal.
        var hpRatio = state.MaxHp <= 0 ? 0.0 : (double)state.Hp / state.MaxHp;
        var incoming = 0;
        foreach (var enemy in combat.Enemies)
        {
            foreach (var intent in enemy.Intents)
            {
                if (intent.Damage is int d) incoming += d * (intent.Hits ?? 1);
            }
        }
        var threatened = incoming - combat.PlayerBlock >= state.Hp;
        // Lower bar (0.50) than the rest-site policy because we want
        // to spend potions before they expire end-of-fight — not hoard
        // them until we're nearly dead.
        if (!threatened && hpRatio >= 0.50) return null;

        // Targeting: prefer the highest-HP living enemy. This is the
        // right call for almost every targeted potion class — damage
        // potions kill or scratch a tank; Vulnerable potions amplify
        // future damage against the biggest threat. (Was: target enemy
        // 0 unconditionally, which wasted a Vulnerable potion on a
        // 3-HP dying Shrinker in seed 1's trace.)
        int? target = null;
        if (usable.TargetType == TargetType.AnyEnemy)
        {
            var bestHp = -1;
            foreach (var e in combat.Enemies)
            {
                if (e.Hp > 0 && e.Hp > bestHp) { bestHp = e.Hp; target = e.Index; }
            }
        }
        return new UsePotion(usable.Index, target);
    }

    private static AgentAction Translate(SimAction action, SimState atPlanRoot) => action switch
    {
        SimEndTurn => new EndTurn(),
        SimPlayCard play => TranslatePlayCard(play, atPlanRoot),
        SimUsePotion potion => new UsePotion(potion.PotionIndex, potion.TargetEnemyIndex),
        _ => throw new ArgumentOutOfRangeException(nameof(action), action.GetType().Name),
    };

    // SimPlayCard's HandIndex refers to the *simulator's* hand position
    // (which matches the wire's snapshot at plan root, since SimAgent
    // re-plans from a fresh CombatState every call). Convert back via
    // the SimCard's preserved OriginalHandIndex.
    private static AgentAction TranslatePlayCard(SimPlayCard play, SimState root)
    {
        if (play.HandIndex < 0 || play.HandIndex >= root.Hand.Count)
            throw new InvalidOperationException(
                $"SimAgent: planner returned out-of-bounds hand index {play.HandIndex}");
        var card = root.Hand[play.HandIndex];
        var wireIndex = card.OriginalHandIndex
            ?? throw new InvalidOperationException(
                "SimAgent: planner returned a card without an OriginalHandIndex — "
                + "first-tick plans should only reference cards built from the wire.");
        return new PlayCard(wireIndex, play.TargetEnemyIndex);
    }
}
