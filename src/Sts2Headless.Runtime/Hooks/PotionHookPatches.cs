using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Runtime.Hooks;

// Hook instrumentation for potions. Most potion behavior runs through
// the use-action path (PotionCmd / OnUsed). The hook-trigger axis here
// surfaces potions that have passive hook overrides — fairly rare in
// sts2's potion design, so expect a small patch budget.
public static class PotionHookPatches
{
    public static ModelHookPatcher.PatchOutcome Apply(System.Reflection.Assembly sts2) =>
        ModelHookPatcher.ApplyForBase(sts2, "MegaCrit.Sts2.Core.Models.PotionModel", TriggerKind.Potion);
}
