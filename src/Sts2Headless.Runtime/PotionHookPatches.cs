using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Runtime;

// Coverage instrumentation for potions. Like cards, most potion behavior
// runs through the use-action path (already captured by PotionsUsed in
// CoverageRecorder). The hook-trigger axis here surfaces potions that
// have passive hook overrides — fairly rare in sts2's potion design,
// so expect a small patch budget and a small triggered set.
public static class PotionHookPatches
{
    public static ModelHookPatcher.PatchOutcome Apply(System.Reflection.Assembly sts2) =>
        ModelHookPatcher.ApplyForBase(sts2, "MegaCrit.Sts2.Core.Models.PotionModel", TriggerKind.Potion);
}
