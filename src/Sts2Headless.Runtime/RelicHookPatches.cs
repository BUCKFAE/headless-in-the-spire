using System.Reflection;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Runtime;

// Thin front door over ModelHookPatcher for the relic kind. See
// ModelHookPatcher.cs for the implementation rationale.
//
// Bootstrap step calls Apply(sts2); the patcher walks every concrete
// RelicModel subtype's AbstractModel hook overrides and installs a
// shared postfix that records (Relic, relicId, hookName) to TriggerLog.
public static class RelicHookPatches
{
    public static ModelHookPatcher.PatchOutcome Apply(Assembly sts2) =>
        ModelHookPatcher.ApplyForBase(sts2, "MegaCrit.Sts2.Core.Models.RelicModel", TriggerKind.Relic);
}
