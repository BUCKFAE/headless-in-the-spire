namespace Sts2Headless.BattleAgent.Core;

// Brute-force depth-first enumeration of card-play sequences within a
// single player turn. Mirrors scumthespire/bottled_ai/sts_lightspeed's
// dominant paradigm: STS combat search is exhaustive intra-turn,
// heuristic-evaluated at end-of-turn.
//
// Per-step the planner:
//   1. Considers "stop playing here, end turn" — scored via the
//      evaluator on the EndPlayerTurn-projected state.
//   2. Considers every legal SimPlayCard, ordered by a priority that
//      front-loads powers / debuffs / draws over damage. Ordering does
//      not affect correctness (every order is explored within budget)
//      but reaches good leaves faster, which matters under the node
//      cap.
//   3. Hard short-circuit on lethal — if any play kills all enemies,
//      return immediately without exploring sibling branches.
//   4. Budget-cap: stop after MaxNodes or when MaxTime elapses.
//
// No transposition table in v1. Adding one is straightforward (key on
// SimState canonicalised — energy, hp, block, statuses, hand multiset,
// enemy states) and is the natural next perf optimisation once a real
// benchmark surfaces.
public sealed class ExhaustivePlanner : ICombatPlanner
{
    public TurnPlan PlanTurn(
        SimState rootState,
        ICombatModel model,
        IEvaluator evaluator,
        PlannerBudget budget,
        CancellationToken cancellationToken)
    {
        var deadline = budget.MaxTime is { } maxTime
            ? DateTime.UtcNow + maxTime
            : (DateTime?)null;

        var search = new SearchState
        {
            Model = model,
            Evaluator = evaluator,
            Budget = budget,
            Deadline = deadline,
            CancellationToken = cancellationToken,
            BestActions = Array.Empty<SimAction>(),
            BestState = rootState,
            BestScore = double.NegativeInfinity,
            BestIsLethal = false,
        };

        // Seed with "do nothing, end turn" as the always-legal baseline.
        var endTurnState = model.EndPlayerTurn(rootState);
        var endTurnScore = evaluator.Score(endTurnState);
        search.BestActions = new SimAction[] { new SimEndTurn() };
        search.BestState = endTurnState;
        search.BestScore = endTurnScore;

        var path = new List<SimAction>(8);
        DepthFirst(rootState, path, search);

        return new TurnPlan(
            Actions: search.BestActions,
            ProjectedEndOfTurnState: search.BestState,
            Score: search.BestScore,
            IsLethal: search.BestIsLethal,
            NodesExplored: search.Nodes);
    }

    private static void DepthFirst(SimState state, List<SimAction> path, SearchState search)
    {
        if (search.CancellationToken.IsCancellationRequested) return;
        if (search.LethalFound) return;
        if (search.Nodes >= search.Budget.MaxNodes) return;
        if (search.Deadline is { } d && DateTime.UtcNow > d) return;
        search.Nodes++;

        if (state.IsInvalid) return;

        // Option 1: end the turn here.
        ConsiderEndTurn(state, path, search);
        if (search.LethalFound) return;

        // Option 2: play any legal card, in priority order.
        var actions = search.Model.LegalActions(state);
        var plays = new List<(SimPlayCard play, int priority)>(actions.Count);
        foreach (var a in actions)
        {
            if (a is SimPlayCard p)
            {
                var card = state.Hand[p.HandIndex];
                plays.Add((p, ActionPriority(card, search)));
            }
        }
        plays.Sort((a, b) => a.priority.CompareTo(b.priority));

        foreach (var (play, _) in plays)
        {
            if (search.LethalFound) return;
            var next = search.Model.Apply(state, play);
            if (next.IsInvalid) continue;
            path.Add(play);
            DepthFirst(next, path, search);
            path.RemoveAt(path.Count - 1);
        }
    }

    private static void ConsiderEndTurn(SimState state, List<SimAction> path, SearchState search)
    {
        // Lethal check first — if every enemy is already dead before we
        // even end the turn, claim it.
        if (search.Model.AllEnemiesDead(state))
        {
            search.BestActions = MaterialiseWithEndTurn(path);
            search.BestState = state;
            search.BestScore = double.PositiveInfinity;
            search.BestIsLethal = true;
            search.LethalFound = true;
            return;
        }

        var projected = search.Model.EndPlayerTurn(state);
        var score = search.Evaluator.Score(projected);
        if (score > search.BestScore)
        {
            search.BestActions = MaterialiseWithEndTurn(path);
            search.BestState = projected;
            search.BestScore = score;
            search.BestIsLethal = search.Model.AllEnemiesDead(projected);
        }
    }

    private static SimAction[] MaterialiseWithEndTurn(IReadOnlyList<SimAction> path)
    {
        var arr = new SimAction[path.Count + 1];
        for (var i = 0; i < path.Count; i++) arr[i] = path[i];
        arr[path.Count] = new SimEndTurn();
        return arr;
    }

    // Lower number = earlier in the play order. Powers and debuff-
    // applying cards rank highest so their multiplicative effects apply
    // before damage. Body-Slam-shape (BlockToDamage) ranks after block-
    // gaining cards so it sees the freshly-applied block.
    private static int ActionPriority(SimCard card, SearchState s)
    {
        var effect = s.Model is CombatModel cm && cm is not null
            ? IroncladCardCatalog.Instance.GetEffect(card.Id, card.Upgraded)
            : null;
        if (effect is null) return 90;

        if (effect.IsStatus || effect.IsCurse) return 200;
        if (effect.IsPower) return 0;
        if (effect.VulnerableApply > 0 || effect.WeakApply > 0) return 10;
        if (effect.DrawCards > 0) return 20;
        if (effect.EnergyGain > 0) return 5;            // SeeingRed/Bloodletting/Offering
        if (effect.Block > 0 && effect.Damage == 0) return 30;
        if (effect.BlockToDamage) return 40;           // after block-gaining
        if (effect.Damage > 0 || effect.Custom is not null) return 50;
        return 60;
    }

    private sealed class SearchState
    {
        public ICombatModel Model = null!;
        public IEvaluator Evaluator = null!;
        public PlannerBudget Budget = null!;
        public DateTime? Deadline;
        public CancellationToken CancellationToken;
        public int Nodes;
        public IReadOnlyList<SimAction> BestActions = Array.Empty<SimAction>();
        public SimState BestState = null!;
        public double BestScore;
        public bool BestIsLethal;
        public bool LethalFound;
    }
}
