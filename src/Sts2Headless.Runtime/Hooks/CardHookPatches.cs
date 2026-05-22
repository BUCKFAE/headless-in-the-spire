using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Runtime.Hooks;

// Hook instrumentation for cards. The AbstractModel hook overrides cards
// declare are the *passive* side — cards that listen for AfterTurnEnd /
// BeforeCombatStart / etc. fire here. The active side (CardPlayCmd /
// OnPlay) goes through the play-action path and isn't routed through this
// patcher.
public static class CardHookPatches
{
    public static ModelHookPatcher.PatchOutcome Apply(System.Reflection.Assembly sts2) =>
        ModelHookPatcher.ApplyForBase(sts2, "MegaCrit.Sts2.Core.Models.CardModel", TriggerKind.Card);
}
