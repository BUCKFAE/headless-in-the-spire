namespace Sts2Headless.BattleAgent.Core;

// UCT-based Monte Carlo Tree Search planner. Single-turn search
// (one player turn), heuristic-evaluated leaves — no random rollouts.
// Per the STS bot research, random rollouts are too noisy in card
// games: a 5-card hand can be played dozens of ways, most of which
// converge on the same end-state, but the few "great" lines (Bash →
// Strike → Strike for Vulnerable-amplified damage) only matter if
// the rollout policy actually plays them. Substituting the evaluator
// for the rollout gives us the same backup target as
// ExhaustivePlanner uses, with UCT exploration on top.
//
// Compared to ExhaustivePlanner:
//   * Same scoring substrate (the IEvaluator), so head-to-head
//     comparisons isolate the search behaviour.
//   * UCB1 selection prefers visit-and-value imbalanced children,
//     which can find good lines faster on hands with extreme
//     branching that exhaustive DFS chews through linearly.
//   * Less complete: a budget too small means the tree never
//     reaches some children. ExhaustivePlanner under-budget at
//     least tries every child once via ordering.
//
// Intentionally simple: this is the comparison baseline, not a
// publish-quality MCTS. Tuning knobs (exploration constant, expansion
// strategy, prior-policy biases) can grow once the comparison harness
// gives us a fair benchmark.
public sealed class MctsPlanner : ICombatPlanner
{
    // UCB1 exploration constant. sqrt(2) is the textbook default for
    // [0,1]-normalised rewards; we keep it but normalise scores per
    // planner invocation so a single bad-score outlier doesn't poison
    // the selection.
    private static readonly double UcbExplorationC = Math.Sqrt(2);

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

        // The root node represents "we're about to act with this hand".
        // Children = each legal action.
        var root = new Node(rootState, parent: null, incomingAction: null);
        var nodesExpanded = 0;
        var bestLethalActions = (IReadOnlyList<SimAction>?)null;
        SimState bestLethalState = rootState;

        // Seed with the "end turn here" baseline. Scored via the
        // evaluator on the EndPlayerTurn-projected state.
        var seedEnd = model.EndPlayerTurn(rootState);
        var seedScore = evaluator.Score(seedEnd);
        IReadOnlyList<SimAction> bestSeenActions = new SimAction[] { new SimEndTurn() };
        var bestSeenState = seedEnd;
        var bestSeenScore = seedScore;

        for (var iter = 0; iter < budget.MaxNodes; iter++)
        {
            if (cancellationToken.IsCancellationRequested) break;
            if (deadline is { } d && DateTime.UtcNow > d) break;

            // 1. Selection: walk down using UCB1.
            var node = Select(root);

            // 2. Expansion: if not terminal and not fully expanded,
            // pop one new child.
            if (!IsTerminal(node, model))
            {
                var expanded = Expand(node, model);
                if (expanded is not null)
                {
                    node = expanded;
                    nodesExpanded++;
                }
            }

            // 3. Evaluation: score the leaf. Lethal short-circuit if
            // the leaf has all enemies dead before EOT.
            double score;
            SimState scored;
            IReadOnlyList<SimAction> actions = MaterialisePath(node);
            if (model.AllEnemiesDead(node.State))
            {
                score = double.PositiveInfinity;
                scored = node.State;
                if (bestLethalActions is null)
                {
                    bestLethalActions = actions;
                    bestLethalState = scored;
                }
            }
            else
            {
                scored = model.EndPlayerTurn(node.State);
                score = evaluator.Score(scored);
            }

            // Track the best plan we've seen.
            if (score > bestSeenScore)
            {
                bestSeenActions = actions;
                bestSeenState = scored;
                bestSeenScore = score;
            }

            // 4. Backprop.
            Backprop(node, score);

            // Early exit if we found lethal.
            if (bestLethalActions is not null) break;
        }

        var finalActions = bestLethalActions ?? bestSeenActions;
        var finalState = bestLethalActions is not null ? bestLethalState : bestSeenState;
        return new TurnPlan(
            Actions: finalActions,
            ProjectedEndOfTurnState: finalState,
            Score: bestSeenScore,
            IsLethal: bestLethalActions is not null,
            NodesExplored: nodesExpanded);
    }

    private static Node Select(Node root)
    {
        var node = root;
        while (node.UntriedActions is { Count: 0 } && node.Children.Count > 0)
        {
            // All children expanded; descend via UCB1.
            node = SelectUcb(node);
        }
        return node;
    }

    private static Node SelectUcb(Node parent)
    {
        Node best = parent.Children[0];
        var bestValue = double.NegativeInfinity;
        var lnParentVisits = Math.Log(Math.Max(1, parent.Visits));
        foreach (var c in parent.Children)
        {
            var exploit = c.Visits > 0 ? c.TotalScore / c.Visits : 0;
            var explore = c.Visits > 0
                ? UcbExplorationC * Math.Sqrt(lnParentVisits / c.Visits)
                : double.PositiveInfinity;
            var ucb = exploit + explore;
            if (ucb > bestValue) { bestValue = ucb; best = c; }
        }
        return best;
    }

    private static Node? Expand(Node node, ICombatModel model)
    {
        node.UntriedActions ??= BuildUntried(node.State, model);
        if (node.UntriedActions.Count == 0) return null;
        // Pop one untried action.
        var action = node.UntriedActions[node.UntriedActions.Count - 1];
        node.UntriedActions.RemoveAt(node.UntriedActions.Count - 1);
        var next = model.Apply(node.State, action);
        if (next.IsInvalid) return null;
        var child = new Node(next, node, action);
        node.Children.Add(child);
        return child;
    }

    private static List<SimAction> BuildUntried(SimState state, ICombatModel model)
    {
        // "End the turn" is one of the legal actions; the planner picks
        // up other plays via LegalActions.
        var actions = new List<SimAction>(model.LegalActions(state));
        // Move SimEndTurn to the END of the untried list so it's the
        // LAST option we expand from a fresh node — we want to first
        // explore card-play subtrees before committing to ending the
        // turn. (Untried is popped from the back; back = first popped.)
        // Actually we want EndTurn explored ONCE per root context, so
        // keep it but de-prioritise.
        actions.Sort(CompareForExpansion);
        return actions;
    }

    private static int CompareForExpansion(SimAction a, SimAction b)
    {
        // SimPlayCard before SimEndTurn (end-turn ends the branch).
        var aIsEnd = a is SimEndTurn ? 1 : 0;
        var bIsEnd = b is SimEndTurn ? 1 : 0;
        return aIsEnd.CompareTo(bIsEnd);
    }

    private static bool IsTerminal(Node node, ICombatModel model)
    {
        if (model.IsCombatOver(node.State)) return true;
        // A node where the agent has already "ended turn" is terminal
        // for this single-turn search.
        if (node.IncomingAction is SimEndTurn) return true;
        return false;
    }

    private static IReadOnlyList<SimAction> MaterialisePath(Node leaf)
    {
        var path = new List<SimAction>();
        for (var n = leaf; n.Parent is not null; n = n.Parent)
        {
            if (n.IncomingAction is not null)
                path.Insert(0, n.IncomingAction);
        }
        // Ensure a terminating SimEndTurn — the planner contract says
        // every TurnPlan ends with one.
        if (path.Count == 0 || path[^1] is not SimEndTurn)
            path.Add(new SimEndTurn());
        return path;
    }

    private static void Backprop(Node leaf, double score)
    {
        for (var n = leaf; n is not null; n = n.Parent)
        {
            n.Visits++;
            n.TotalScore += score;
        }
    }

    private sealed class Node(SimState state, Node? parent, SimAction? incomingAction)
    {
        public SimState State { get; } = state;
        public Node? Parent { get; } = parent;
        public SimAction? IncomingAction { get; } = incomingAction;
        public List<Node> Children { get; } = new();
        public List<SimAction>? UntriedActions { get; set; } = null;
        public int Visits { get; set; }
        public double TotalScore { get; set; }
    }
}
