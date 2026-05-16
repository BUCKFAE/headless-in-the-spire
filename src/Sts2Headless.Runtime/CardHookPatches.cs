using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Runtime;

// Coverage instrumentation for cards. Most card behavior runs through
// the play-action path (CardPlayCmd / OnPlay), already captured by the
// CardsPlayed axis (action-side, in CoverageRecorder). The AbstractModel
// hook overrides cards declare are the *passive* side — e.g. cards that
// listen for AfterTurnEnd / BeforeCombatStart / etc.
//
// So the Triggered axis here is strict-subset useful: it tells us which
// cards have passive listener code that actually fired in a run, on top
// of the existing "this card was played at least once" data.
public static class CardHookPatches
{
    public static ModelHookPatcher.PatchOutcome Apply(System.Reflection.Assembly sts2) =>
        ModelHookPatcher.ApplyForBase(sts2, "MegaCrit.Sts2.Core.Models.CardModel", TriggerKind.Card);
}
