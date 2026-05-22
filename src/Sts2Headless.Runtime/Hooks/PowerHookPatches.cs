using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Runtime.Hooks;

// Coverage instrumentation for powers — same shape as RelicHookPatches.
// Powers are AbstractModel-derived; many override AfterDamageGiven,
// AfterCardPlayed, etc. to react to gameplay events. Patching every
// override gives us the trigger axis "power X actually responded to
// event Y" alongside the existing "power seen on a creature" axis.
public static class PowerHookPatches
{
    public static ModelHookPatcher.PatchOutcome Apply(System.Reflection.Assembly sts2) =>
        ModelHookPatcher.ApplyForBase(sts2, "MegaCrit.Sts2.Core.Models.PowerModel", TriggerKind.Power);
}
