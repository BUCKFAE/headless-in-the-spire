using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Runtime.Hooks;

// Coverage instrumentation for monsters. Monster behavior is split
// between "moves" (per-turn AI: select an attack/defend/etc.) and
// AbstractModel hooks (reactions to gameplay events). This patcher
// covers the hook side — the move side is a separate axis future
// MonsterMoveCoverage work could surface, since moves are dispatched
// through a different call shape (e.g. MonsterModel.SelectMove).
//
// Coverage value here is moderate: hooked monster reactions are
// less central than relic/power reactions, but the manifest gap
// "120 monsters seen, 0 of which actually used their hooks" would
// still be a useful signal for fuzz-agent design.
public static class MonsterHookPatches
{
    public static ModelHookPatcher.PatchOutcome Apply(System.Reflection.Assembly sts2) =>
        ModelHookPatcher.ApplyForBase(sts2, "MegaCrit.Sts2.Core.Models.MonsterModel", TriggerKind.Monster);
}
