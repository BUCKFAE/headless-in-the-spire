using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.BattleAgent.Core;

// Like MultiTurnExhaustivePlanner but the per-projection-turn
// "phantom player" is replaced by a deck-sampled greedy rollout:
//   1. Sample 5 cards from SimState.DeckCardIds (the run-deck card
//      list, minus the current hand best-effort).
//   2. Build SimCards via IroncladCardCatalog so the standard
//      ApplyPlayCard path is reused.
//   3. Greedy play loop — score each playable card by a simple
//      heuristic (damage + block + power-card value) and play the
//      highest-scoring one until no energy left or no positive plays.
//   4. EndPlayerTurn (real enemy turn) resolves.
//
// Why not a full inner ExhaustivePlanner? Cost: outer DFS already
// explores ~50k nodes per turn; running a 5k-node inner plan at
// every leaf is 50k × 5k = 250M ops, ~50× slower than the budget.
// The greedy rollout is cheap (~15 ops per turn) and captures the
// signal we want — "if I let this enemy live another turn, my deck
// can plausibly handle it" vs the fixed-phantom-turn estimate's
// "+7 block / +9 damage" guess that breaks down at low HP.
//
// Falls back to MultiTurnExhaustivePlanner's fixed phantom when
// DeckCardIds is null (legacy agents or tests).
public sealed class RolloutMultiTurnPlanner : ICombatPlanner
{
    public int LookaheadTurns { get; }
    private readonly ICardEffectCatalog _catalog;
    private readonly MultiTurnExhaustivePlanner _fallback;

    public RolloutMultiTurnPlanner(int lookaheadTurns = 2, ICardEffectCatalog? catalog = null)
    {
        LookaheadTurns = lookaheadTurns;
        _catalog = catalog ?? IroncladCardCatalog.Instance;
        _fallback = new MultiTurnExhaustivePlanner(lookaheadTurns);
    }

    public TurnPlan PlanTurn(
        SimState rootState,
        ICombatModel model,
        IEvaluator evaluator,
        PlannerBudget budget,
        CancellationToken cancellationToken)
    {
        // No run-deck → no rollout signal; fall through to the
        // fixed-phantom MultiTurn.
        if (rootState.DeckCardIds is null) return _fallback.PlanTurn(rootState, model, evaluator, budget, cancellationToken);

        var deadline = budget.MaxTime is { } maxTime
            ? DateTime.UtcNow + maxTime
            : (DateTime?)null;

        var search = new SearchState
        {
            Model = model,
            Evaluator = evaluator,
            Catalog = _catalog,
            Budget = budget,
            Deadline = deadline,
            CancellationToken = cancellationToken,
            LookaheadTurns = LookaheadTurns,
            BestActions = new SimAction[] { new SimEndTurn() },
            BestState = rootState,
            BestScore = double.NegativeInfinity,
            BestIsLethal = false,
        };

        var seedProjected = ProjectForward(rootState, model, _catalog, LookaheadTurns);
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

    private static SimState ProjectForward(SimState state, ICombatModel model, ICardEffectCatalog catalog, int turns)
    {
        var s = state;
        for (var t = 0; t < turns; t++)
        {
            if (model.IsCombatOver(s)) break;
            if (t > 0)
            {
                s = ApplyRolloutPlayerTurn(s, model, catalog);
                if (model.IsCombatOver(s)) break;
            }
            s = model.EndPlayerTurn(s);
        }
        return s;
    }

    // Greedy single-turn rollout against a sampled hand. The output
    // is the post-player-turn SimState (energy spent, hand drained,
    // any damage / block applied).
    private static SimState ApplyRolloutPlayerTurn(SimState s, ICombatModel model, ICardEffectCatalog catalog)
    {
        var hand = SampleHand(s, catalog, count: 5);
        s = s with { Hand = hand };

        // Greedy play loop. Each iteration: score every playable card
        // by a cheap value heuristic, pick the best, apply. Stop when
        // no card scores above the "end turn" baseline.
        for (var step = 0; step < 6; step++)
        {
            if (model.IsCombatOver(s)) break;
            if (s.Energy <= 0) break;

            var bestIdx = -1;
            var bestScore = 0.0;
            for (var i = 0; i < s.Hand.Count; i++)
            {
                var c = s.Hand[i];
                if (!IsPlayable(c, s)) continue;
                var score = QuickValue(c, catalog, s);
                if (score > bestScore) { bestScore = score; bestIdx = i; }
            }
            if (bestIdx < 0) break;

            var target = ChooseTarget(s, s.Hand[bestIdx]);
            var play = new SimPlayCard(bestIdx, target);
            var next = model.Apply(s, play);
            if (next.IsInvalid) break;
            s = next;
        }
        return s;
    }

    // Sample `count` cards from DeckCardIds, treating the current
    // discard/draw piles as unknown contents. Deterministic for
    // search stability — uses a hash of (turn, enemy-hp) so the
    // sample shifts as the simulation advances but is the same for
    // every visit to the same node.
    private static IReadOnlyList<SimCard> SampleHand(SimState s, ICardEffectCatalog catalog, int count)
    {
        if (s.DeckCardIds is null || s.DeckCardIds.Count == 0) return Array.Empty<SimCard>();

        // Mix in some state to pseudo-randomise across projection
        // depths while staying deterministic for transposition keys.
        var seed = 17;
        seed = seed * 31 + s.Turn;
        seed = seed * 31 + s.Hp;
        seed = seed * 31 + s.Energy;

        var deck = s.DeckCardIds;
        var n = deck.Count;
        var picks = Math.Min(count, n);
        var hand = new SimCard[picks];
        for (var i = 0; i < picks; i++)
        {
            seed = seed * 1103515245 + 12345;
            var idx = (int)((uint)seed % (uint)n);
            var id = deck[idx];
            // Best-effort cost/upgrade: read catalog. Falls back to 1
            // for unmodelled cards (those will fail IsPlayable and
            // greedy will skip them — acceptable).
            var (cost, target) = DefaultCardCostAndTarget(id, catalog);
            hand[i] = new SimCard(id, cost, Upgraded: false, target, CanPlayFlag: true, OriginalHandIndex: null);
        }
        return hand;
    }

    private static (int cost, TargetType target) DefaultCardCostAndTarget(CardId id, ICardEffectCatalog catalog)
    {
        var effect = catalog.GetEffect(id, upgraded: false);
        if (effect is null) return (1, TargetType.None);
        // Most attacks are AnyEnemy; AoE attacks set TargetsAllEnemies.
        var t = effect.IsAttack
            ? (effect.TargetsAllEnemies ? TargetType.AllEnemies : TargetType.AnyEnemy)
            : TargetType.Self;
        return (DefaultCost(id), t);
    }

    private static int DefaultCost(CardId id) => id switch
    {
        CardId.StrikeIronclad or CardId.DefendIronclad => 1,
        CardId.Bash => 2,
        CardId.Inflame or CardId.PommelStrike or CardId.TwinStrike
            or CardId.IronWave or CardId.SwordBoomerang or CardId.Thunderclap
            or CardId.PerfectedStrike or CardId.AshenStrike or CardId.Tremble
            or CardId.Bully or CardId.Dismantle or CardId.Taunt
            or CardId.Headbutt or CardId.Anger => 1,
        CardId.Uppercut or CardId.Impervious or CardId.Whirlwind => 2,
        CardId.Bludgeon or CardId.DemonForm or CardId.Barricade
            or CardId.Corruption => 3,
        CardId.Bloodletting or CardId.Offering or CardId.PactsEnd
            or CardId.Brand or CardId.Cascade => 0,
        _ => 1,
    };

    private static bool IsPlayable(SimCard c, SimState s) =>
        c.CanPlayFlag && c.Cost >= 0 && c.Cost <= s.Energy;

    // Cheap value heuristic for a single rollout turn — score cards
    // by *this-turn contribution*, not run-defining payoff. Power
    // cards score low (their benefit applies on future turns the
    // rollout doesn't see); damage / block dominate. Cost penalty
    // discourages spending all energy on one card if cheaper cards
    // dominate per-energy.
    private static double QuickValue(SimCard c, ICardEffectCatalog catalog, SimState s)
    {
        var e = catalog.GetEffect(c.Id, c.Upgraded);
        if (e is null) return 0;
        var v = 0.0;
        v += e.Damage * Math.Max(1, e.Hits) * (e.TargetsAllEnemies ? 1.5 : 1.0);
        v += e.Block * 0.8;
        // Powers: small fixed value (they pay off later — we score
        // for the rollout *turn*, not the run). Inflame-shape strength
        // sources are slightly preferred because the rollout's later
        // card plays use the new Strength.
        if (e.IsPower) v += 4;
        if (e.StrengthGain > 0) v += 3 * e.StrengthGain;
        if (e.VulnerableApply > 0) v += 5 * e.VulnerableApply;
        if (e.WeakApply > 0)       v += 3 * e.WeakApply;
        if (e.DrawCards > 0)       v += 3 * e.DrawCards;
        if (e.EnergyGain > 0)      v += 6 * e.EnergyGain;
        if (e.SelfDamage > 0)      v -= 1.5 * e.SelfDamage;
        // Cost penalty — at equal raw value prefer cheaper cards.
        v -= 0.5 * Math.Max(0, c.Cost);
        return v;
    }

    private static int? ChooseTarget(SimState s, SimCard card)
    {
        // Pick highest-HP living enemy; mirror MainPlanner convention.
        var idx = -1;
        var bestHp = -1;
        for (var i = 0; i < s.Enemies.Count; i++)
        {
            if (s.Enemies[i].IsDead) continue;
            if (s.Enemies[i].Hp > bestHp) { bestHp = s.Enemies[i].Hp; idx = i; }
        }
        return idx >= 0 ? idx : null;
    }

    // Outer DFS — same shape as MultiTurnExhaustivePlanner.
    private static void DepthFirst(SimState state, List<SimAction> path, SearchState search)
    {
        if (search.CancellationToken.IsCancellationRequested) return;
        if (search.LethalFound) return;
        if (search.Nodes >= search.Budget.MaxNodes) return;
        if (search.Deadline is { } d && DateTime.UtcNow > d) return;
        search.Nodes++;

        if (state.IsInvalid) return;

        ConsiderEndTurn(state, path, search);
        if (search.LethalFound) return;

        var actions = search.Model.LegalActions(state);
        var plays = new List<(SimPlayCard play, int priority)>(actions.Count);
        foreach (var a in actions)
        {
            if (a is SimPlayCard p)
            {
                var card = state.Hand[p.HandIndex];
                plays.Add((p, ActionPriority(card, search.Catalog)));
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
        if (search.Model.AllEnemiesDead(state))
        {
            search.BestActions = MaterialiseWithEndTurn(path);
            search.BestState = state;
            search.BestScore = double.PositiveInfinity;
            search.BestIsLethal = true;
            search.LethalFound = true;
            return;
        }
        var projected = ProjectForward(state, search.Model, search.Catalog, search.LookaheadTurns);
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

    private static int ActionPriority(SimCard card, ICardEffectCatalog catalog)
    {
        var effect = catalog.GetEffect(card.Id, card.Upgraded);
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
        public ICardEffectCatalog Catalog = null!;
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
