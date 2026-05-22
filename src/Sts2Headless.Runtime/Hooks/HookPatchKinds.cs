using System.Reflection;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Runtime.Hooks;

// Single source of truth for which content kinds receive Harmony-postfix
// hook instrumentation at bootstrap. Each entry pins a (TriggerKind, base
// type FullName) pair; ModelHookPatcher walks every concrete subtype of
// the named base, finds its AbstractModel hook overrides (After* / Before*
// / On*), and installs a postfix that records (kind, source-id, hook) to
// TriggerLog. The wire surfaces those events on every run/state via
// RunStateResult.TriggeredSincePrev.
//
// Why a registry instead of one file per kind: keeping the list in one
// place makes InstrumentationKindParityTest a one-line comparison
// against GenerateContentIdsCommand.Kinds — drift in either direction is
// loud. New kinds: append one entry here + one TriggerKind value in
// Methods.cs, run `just regen`.
//
// What the patcher does per kind:
//   Card  — passive listener side (AfterTurnEnd / BeforeCombatStart etc.
//           on CardModel subtypes). The active side (CardPlayCmd / OnPlay)
//           goes through the play-action path and is NOT routed through
//           this patcher.
//   Relic — the main reason the patcher exists: per-relic Triggered isn't
//           observable from snapshot state alone (a relic's HP restore on
//           the player can happen with no visible state delta between
//           two consecutive run/state calls).
//   Monster — hook side only (reactions to gameplay events). The "moves"
//             side — per-turn AI like SelectMove — is a separate axis
//             dispatched through different call shapes; not wired here.
//   Potion — passive hook overrides only (active use goes through
//            PotionCmd / OnUsed). Few potions declare hooks; expect a
//            small patch budget here.
//   Power  — broad coverage: many powers override AfterDamageGiven /
//            AfterCardPlayed / etc. to react to gameplay events.
//   Affliction / Enchantment / Event / Modifier / Orb — added alongside
//            the InstrumentationKindParityTest sweep so the instrumented
//            set matches the enumerated set. Hook surface for these
//            kinds is narrow (1-6 overrides per namespace per the
//            listener-dispatch probe) but non-zero, so the wire's
//            TriggeredSincePrev is now meaningful for them.
//   Encounter — included for parity; EncounterModel subtypes have zero
//               AbstractModel hook overrides today, so the patcher reports
//               0 patched. Kept in the registry so a future encounter
//               that DOES override a hook gets instrumented automatically.
public static class HookPatchKinds
{
    public sealed record KindEntry(TriggerKind Kind, string BaseTypeFullName);

    // Alphabetical by kind for stable bootstrap-output ordering.
    public static readonly IReadOnlyList<KindEntry> All =
    [
        new(TriggerKind.Affliction,  "MegaCrit.Sts2.Core.Models.AfflictionModel"),
        new(TriggerKind.Card,        "MegaCrit.Sts2.Core.Models.CardModel"),
        new(TriggerKind.Enchantment, "MegaCrit.Sts2.Core.Models.EnchantmentModel"),
        new(TriggerKind.Encounter,   "MegaCrit.Sts2.Core.Models.EncounterModel"),
        new(TriggerKind.Event,       "MegaCrit.Sts2.Core.Models.EventModel"),
        new(TriggerKind.Modifier,    "MegaCrit.Sts2.Core.Models.ModifierModel"),
        new(TriggerKind.Monster,     "MegaCrit.Sts2.Core.Models.MonsterModel"),
        new(TriggerKind.Orb,         "MegaCrit.Sts2.Core.Models.OrbModel"),
        new(TriggerKind.Potion,      "MegaCrit.Sts2.Core.Models.PotionModel"),
        new(TriggerKind.Power,       "MegaCrit.Sts2.Core.Models.PowerModel"),
        new(TriggerKind.Relic,       "MegaCrit.Sts2.Core.Models.RelicModel"),
    ];

    public static IReadOnlyList<ModelHookPatcher.PatchOutcome> ApplyAll(Assembly sts2)
    {
        var outcomes = new List<ModelHookPatcher.PatchOutcome>(All.Count);
        foreach (var entry in All)
            outcomes.Add(ModelHookPatcher.ApplyForBase(sts2, entry.BaseTypeFullName, entry.Kind));
        return outcomes;
    }
}
