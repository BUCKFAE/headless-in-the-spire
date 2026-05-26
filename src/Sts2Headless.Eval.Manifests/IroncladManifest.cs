using Sts2Headless.Agents.Contracts;
using Sts2Headless.BattleAgent;

namespace Sts2Headless.Eval.Manifests;

// Wraps `IroncladAgent` (src/Sts2Headless.BattleAgent/). Composes
// SimAgent's combat-planning brain with the production per-phase
// policies (IroncladDraftPolicy, IroncladPathPolicy, IroncladRestPolicy,
// IroncladEventPolicy, IroncladMerchantPolicy). The canonical "actually
// trying to win" agent on Ironclad A0.
//
// `CreateAgent()` is hand-written; the constructor's five-policy + four-
// component shape resists a generic `new()` constraint. Sibling
// manifests can wrap the same `IroncladAgent` class with different
// policy stacks for side-by-side leaderboard rows.
public sealed class IroncladManifest : BundledAgent
{
    public override string Name        => "ironclad";
    public override string Version     => "0.5.1";
    public override string Description => "Production Ironclad agent: SimAgent combat planner + Ironclad-specific Draft/Path/Rest/Event/Merchant policies.";
    public override IAgent CreateAgent() => new IroncladAgent();
}
