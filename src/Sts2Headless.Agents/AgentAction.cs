namespace Sts2Headless.Agents;

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
public abstract record AgentAction;

// Combat actions.
public sealed record PlayCard(int CardIndex, int? TargetIndex = null) : AgentAction;
public sealed record EndTurn : AgentAction;
public sealed record UsePotion(int PotionIndex, int? TargetIndex = null) : AgentAction;

// Map / event / room transitions.
public sealed record SelectMapNode(int Col, int Row) : AgentAction;
public sealed record SelectEventOption(int OptionIndex) : AgentAction;
public sealed record SelectRestSiteOption(int OptionIndex) : AgentAction;
public sealed record LeaveTreasureRoom : AgentAction;
public sealed record BuyMerchantItem(int ItemIndex) : AgentAction;
public sealed record LeaveMerchantRoom : AgentAction;
public sealed record EnterNextAct : AgentAction;

// Post-combat reward decisions.
public sealed record SelectReward(int RewardIndex, int? CardIndex = null) : AgentAction;
public sealed record SkipReward(int RewardIndex) : AgentAction;

// Agent-side sentinel: the agent has decided the run can't make
// forward progress (or has met its own success condition) and the
// driver should exit. The reason string lands in the RunOutcome for
// diagnostics. No wire call is dispatched.
public sealed record StopRun(string Reason) : AgentAction;
