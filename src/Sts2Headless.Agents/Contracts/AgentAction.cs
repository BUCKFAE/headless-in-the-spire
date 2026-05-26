using System.Text.Json.Serialization;

namespace Sts2Headless.Agents.Contracts;

// Closed union of every decision an agent can make. Each variant maps
// one-to-one onto a wire method; `AgentDriver.ApplyAsync` is the
// dispatch table that translates the decision into a wire call.
//
// Why data, not async wire calls: keeping agents as `state → action`
// pure functions makes them trivially testable without a host
// subprocess, decouples them from `ITransport`, and lets the driver
// own framework concerns (stall detection, step budget, cancellation)
// in one place. Mirrors the Python `Agent`/`Action` split.
//
// Adding a new wire method: add an AgentAction subtype here, add a
// matching branch in `AgentDriver.ApplyAsync`. The switch in
// `ApplyAsync` is exhaustively checked by the compiler so the
// dispatch never silently drifts from the union.
//
// JSON polymorphism: the eval harness's `agent/*` wire dialect carries
// AgentAction across a subprocess boundary, so we declare the closed
// set of derived types here with a stable `kind` discriminator. The
// in-repo dispatch in `AgentDriver.ApplyAsync` does not depend on JSON
// — it pattern-matches the C# types directly — so the attributes are
// inert until something serialises through `System.Text.Json`. Adding
// a variant means adding its line below *and* its branch in
// ApplyAsync; the compiler's exhaustiveness check catches the second
// one, the OpenRPC export's coverage test catches the first.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(PlayCard),              nameof(PlayCard))]
[JsonDerivedType(typeof(EndTurn),               nameof(EndTurn))]
[JsonDerivedType(typeof(UsePotion),             nameof(UsePotion))]
[JsonDerivedType(typeof(SelectMapNode),         nameof(SelectMapNode))]
[JsonDerivedType(typeof(SelectEventOption),     nameof(SelectEventOption))]
[JsonDerivedType(typeof(SelectRestSiteOption),  nameof(SelectRestSiteOption))]
[JsonDerivedType(typeof(TakeTreasure),          nameof(TakeTreasure))]
[JsonDerivedType(typeof(SkipTreasure),          nameof(SkipTreasure))]
[JsonDerivedType(typeof(BuyMerchantItem),       nameof(BuyMerchantItem))]
[JsonDerivedType(typeof(LeaveMerchantRoom),     nameof(LeaveMerchantRoom))]
[JsonDerivedType(typeof(EnterNextAct),          nameof(EnterNextAct))]
[JsonDerivedType(typeof(ProceedEvent),          nameof(ProceedEvent))]
[JsonDerivedType(typeof(SelectReward),          nameof(SelectReward))]
[JsonDerivedType(typeof(SkipReward),            nameof(SkipReward))]
[JsonDerivedType(typeof(StopRun),               nameof(StopRun))]
public abstract record AgentAction;

// Combat actions.
public sealed record PlayCard(
    [property: JsonPropertyName("cardIndex")]   int  CardIndex,
    [property: JsonPropertyName("targetIndex")] int? TargetIndex = null) : AgentAction;
public sealed record EndTurn : AgentAction;
public sealed record UsePotion(
    [property: JsonPropertyName("potionIndex")] int  PotionIndex,
    [property: JsonPropertyName("targetIndex")] int? TargetIndex = null) : AgentAction;

// Map / event / room transitions.
public sealed record SelectMapNode(
    [property: JsonPropertyName("col")] int Col,
    [property: JsonPropertyName("row")] int Row) : AgentAction;
public sealed record SelectEventOption(
    [property: JsonPropertyName("optionIndex")] int OptionIndex) : AgentAction;
// CardSelectIndices is the same hint-array shape PlayCard uses; it
// reaches the engine via the host's ICardSelector queue. SMITH is the
// canonical consumer (one prompt over the deck's upgradable subset);
// pass [[0]] to upgrade the first upgradable card. Omit for HEAL and
// other options that don't prompt for cards.
public sealed record SelectRestSiteOption(
    [property: JsonPropertyName("optionIndex")]       int                                          OptionIndex,
    [property: JsonPropertyName("cardSelectIndices")] IReadOnlyList<IReadOnlyList<int>>?           CardSelectIndices = null) : AgentAction;
public sealed record TakeTreasure : AgentAction;
public sealed record SkipTreasure : AgentAction;
public sealed record BuyMerchantItem(
    [property: JsonPropertyName("itemIndex")] int ItemIndex) : AgentAction;
public sealed record LeaveMerchantRoom : AgentAction;
public sealed record EnterNextAct : AgentAction;
public sealed record ProceedEvent : AgentAction;

// Post-combat reward decisions.
public sealed record SelectReward(
    [property: JsonPropertyName("rewardIndex")] int  RewardIndex,
    [property: JsonPropertyName("cardIndex")]   int? CardIndex = null) : AgentAction;
public sealed record SkipReward(
    [property: JsonPropertyName("rewardIndex")] int RewardIndex) : AgentAction;

// Agent-side sentinel: the agent has decided the run can't make
// forward progress (or has met its own success condition) and the
// driver should exit. The reason string lands in the RunOutcome for
// diagnostics. No wire call is dispatched.
public sealed record StopRun(
    [property: JsonPropertyName("reason")] string Reason) : AgentAction;
