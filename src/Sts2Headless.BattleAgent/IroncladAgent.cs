using Sts2Headless.Agents.Contracts;
using Sts2Headless.BattleAgent.Core;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.BattleAgent;

// The production full-run agent. Inherits SimAgent's combat-planning
// brain and overrides every other phase with the corresponding
// IxxxPolicy.
//
// Phase routing comes from HeuristicAgent's PhaseDetector. Each
// override calls into the injected policy so the agent is a
// composition of pluggable parts — swap any policy without subclassing
// IroncladAgent.
public sealed class IroncladAgent : SimAgent
{
    public IDraftPolicy DraftPolicy { get; }
    public IPathPolicy PathPolicy { get; }
    public IRestPolicy RestPolicy { get; }
    public IEventPolicy EventPolicy { get; }
    public IMerchantPolicy MerchantPolicy { get; }
    public RunDeckTracker DeckTracker { get; } = new();

    public IroncladAgent(
        IDraftPolicy? draftPolicy = null,
        IPathPolicy? pathPolicy = null,
        IRestPolicy? restPolicy = null,
        IEventPolicy? eventPolicy = null,
        IMerchantPolicy? merchantPolicy = null,
        ICombatModel? model = null,
        IEvaluator? evaluator = null,
        ICombatPlanner? planner = null,
        PlannerBudget? budget = null)
        : base(model, evaluator, planner, budget)
    {
        // Pass the run-deck tracker into DraftPolicy so it can pick a
        // gap-filler when offered cards tie in tier. DeckTracker is a
        // field-initialised RunDeckTracker so it's already non-null
        // here.
        DraftPolicy    = draftPolicy ?? new IroncladDraftPolicy(tracker: DeckTracker);
        PathPolicy     = pathPolicy ?? new IroncladPathPolicy();
        RestPolicy     = restPolicy ?? new IroncladRestPolicy();
        EventPolicy    = eventPolicy ?? new IroncladEventPolicy();
        MerchantPolicy = merchantPolicy ?? new IroncladMerchantPolicy();
    }

    // Override SimAgent's tracker hook with the live RunDeckTracker so
    // PerfectedStrike's scaling formula reads the real Strike count.
    protected override int? GetStrikeCardsInDeck() => DeckTracker.CountStrikeNamed();

    protected override AgentAction DecideMap(RunStateResult state) =>
        PathPolicy.Choose(state);

    protected override AgentAction DecideRewards(RunStateResult state)
    {
        var action = DraftPolicy.Choose(state);
        if (action is SelectReward sel && sel.CardIndex is int cardIndex)
        {
            var head = state.RewardsState?.Available[sel.RewardIndex];
            if (head?.Kind == RewardKind.Card && head.Cards is { } cards)
            {
                var picked = cards.FirstOrDefault(c => c.Index == cardIndex);
                if (picked is not null) DeckTracker.AddCard(picked.Id);
            }
        }
        return action;
    }

    protected override AgentAction DecideRestSite(RunStateResult state) =>
        RestPolicy.Choose(state);

    protected override AgentAction DecideEvent(RunStateResult state) =>
        EventPolicy.Choose(state);

    protected override AgentAction DecideMerchant(RunStateResult state)
    {
        var action = MerchantPolicy.Choose(state);
        if (action is BuyMerchantItem buy)
        {
            var item = state.AvailableMerchantItems.FirstOrDefault(i => i.Index == buy.ItemIndex);
            if (item?.Kind == MerchantKind.Card && item.CardId is { } cardIdStr)
            {
                var cid = CardIdNames.FromWire(cardIdStr);
                if (cid != CardId.Unknown) DeckTracker.AddCard(cid);
            }
        }
        return action;
    }
}
