namespace Sts2Headless.BattleAgent.Core;

// Exhaustive intra-turn DFS (same as ExhaustivePlanner) but the
// end-of-turn leaf is projected forward N additional enemy turns
// before scoring. Captures "will this state survive the upcoming
// enemy damage that the one-turn planner can't see"?
//
// The one-turn planner is blind to Buff-ramping enemies: it sees
// "monster intends Attack 4 next turn" and underestimates the threat
// because next-next turn the monster will attack for 4+STR=11.
// MultiTurnExhaustivePlanner doesn't model the STR growth (we don't
// replicate the engine's enemy-AI move-picker) but does keep applying
// the same Buff intent for N projection turns, which conservatively
// gives "if this thing stays alive, more damage stacks on me."
//
// Cost: each leaf evaluation calls EndPlayerTurn N more times.
// EndPlayerTurn is cheap (pure record mutations), so the multiplier
// is small in practice — empirically 1.5–2x the single-turn budget.
//
// Constraints:
//   - We don't know the next hand, so the projected player turns are
//     pure "do nothing, end turn". Block clears, energy refills,
//     player gets hit again. Realistic floor for "what if I just
//     stand here?".
//   - Intents are held constant across projection turns. The real
//     engine re-rolls intent each turn; this is a conservative
//     approximation (a Buff enemy keeps Buffing in our model
//     instead of switching to Attack with the stacked STR).
public sealed class MultiTurnExhaustivePlanner : ICombatPlanner
{
    public int LookaheadTurns { get; }

    public MultiTurnExhaustivePlanner(int lookaheadTurns = 2)
    {
        if (lookaheadTurns < 1)
            throw new ArgumentOutOfRangeException(nameof(lookaheadTurns),
                "LookaheadTurns must be at least 1 (== ExhaustivePlanner behaviour)");
        LookaheadTurns = lookaheadTurns;
    }

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
            LookaheadTurns = LookaheadTurns,
            BestActions = new SimAction[] { new SimEndTurn() },
            BestState = rootState,
            BestScore = double.NegativeInfinity,
            BestIsLethal = false,
        };

        // Seed with "end turn here" projected forward.
        var seedProjected = ProjectForward(rootState, model, LookaheadTurns);
        search.BestActions = new SimAction[] { new SimEndTurn() };
        search.BestState = seedProjected;
        search.BestScore = evaluator.Score(seedProjected);
        search.BestIsLethal = model.AllEnemiesDead(seedProjected);

        var path = new List<SimAction>(8);
        DepthFirst(rootState, path, search);

        return new TurnPlan(
            Actions: search.BestActions,
            ProjectedEndOfTurnState: search.BestState,
            Score: search.BestScore,
            IsLethal: search.BestIsLethal,
            NodesExplored: search.Nodes);
    }

    // Project N additional enemy turns from `state`. Caps when combat
    // ends. Used both as the leaf-scoring projection and as the seed
    // baseline.
    //
    // Phantom-turn injection: between each EndPlayerTurn pair we apply
    // a synthetic "average player turn" — gain some block, deal some
    // damage to the highest-HP enemy. Without this the projection lets
    // the enemy hit the player for free for N turns, which makes the
    // planner over-defensive (it sees "stand still" as -N enemy hits
    // worth of HP loss and prefers blocking forever).
    //
    // Numbers chosen to approximate ~70% of a nominal 3-energy Ironclad
    // turn: 7 block, 9 damage. Damage scales with Strength so post-
    // Inflame projections correctly value killing faster.
    private static SimState ProjectForward(SimState state, ICombatModel model, int turns)
    {
        var s = state;
        for (var t = 0; t < turns; t++)
        {
            if (model.IsCombatOver(s)) break;
            // First projected player turn already has the real EOT
            // applied by the planner's caller; subsequent ones get
            // injected phantom plays.
            if (t > 0)
            {
                s = ApplyPhantomPlayerTurn(s);
                if (model.IsCombatOver(s)) break;
            }
            s = model.EndPlayerTurn(s);
        }
        return s;
    }

    private static SimState ApplyPhantomPlayerTurn(SimState s)
    {
        // Block gain — approximates one Defend played.
        var blockGain = 7 + s.Status.Dexterity;
        if (s.Status.Frail > 0) blockGain = (int)Math.Floor(blockGain * 0.75);
        s = s with { Block = s.Block + Math.Max(0, blockGain) };

        // Damage to the highest-HP living enemy — approximates one
        // attack card. Strength scales but we don't double-count Vuln
        // amp (the engine applies that via DealSingleTargetDamage).
        var perHit = Math.Max(0, 9 + s.Status.Strength);
        if (s.Status.Weak > 0) perHit = (int)Math.Floor(perHit * 0.75);
        if (perHit > 0)
        {
            var targetIdx = -1;
            var bestHp = -1;
            for (var i = 0; i < s.Enemies.Count; i++)
            {
                if (s.Enemies[i].IsDead) continue;
                if (s.Enemies[i].Hp > bestHp) { bestHp = s.Enemies[i].Hp; targetIdx = i; }
            }
            if (targetIdx >= 0)
                s = CombatModel.DealSingleTargetDamage(s, targetIdx, perHit, hits: 1).state;
        }
        return s;
    }

    private static void DepthFirst(SimState state, List<SimAction> path, SearchState search)
    {
        if (search.CancellationToken.IsCancellationRequested) return;
        if (search.LethalFound) return;
        if (search.Nodes >= search.Budget.MaxNodes) return;
        if (search.Deadline is { } d && DateTime.UtcNow > d) return;
        search.Nodes++;

        if (state.IsInvalid) return;

        // Option 1: end the turn here, projected forward.
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
                plays.Add((p, ActionPriority(card)));
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
        // Lethal check before projection — if everything's dead now,
        // we don't need to look forward.
        if (search.Model.AllEnemiesDead(state))
        {
            search.BestActions = MaterialiseWithEndTurn(path);
            search.BestState = state;
            search.BestScore = double.PositiveInfinity;
            search.BestIsLethal = true;
            search.LethalFound = true;
            return;
        }

        var projected = ProjectForward(state, search.Model, search.LookaheadTurns);
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

    // Same ordering rules as ExhaustivePlanner: powers / debuffs /
    // draw before damage; bad cards last.
    private static int ActionPriority(SimCard card)
    {
        var effect = IroncladCardCatalog.Instance.GetEffect(card.Id, card.Upgraded);
        if (effect is null) return 90;
        if (effect.IsHeadlessUnsafe) return 1000;
        if (effect.IsStatus || effect.IsCurse) return 200;
        if (effect.IsPower) return 0;
        if (effect.VulnerableApply > 0 || effect.WeakApply > 0) return 10;
        if (effect.DrawCards > 0) return 20;
        if (effect.EnergyGain > 0) return 5;
        if (effect.Block > 0 && effect.Damage == 0) return 30;
        if (effect.BlockToDamage) return 40;
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
        public int LookaheadTurns;
        public int Nodes;
        public IReadOnlyList<SimAction> BestActions = Array.Empty<SimAction>();
        public SimState BestState = null!;
        public double BestScore;
        public bool BestIsLethal;
        public bool LethalFound;
    }
}
