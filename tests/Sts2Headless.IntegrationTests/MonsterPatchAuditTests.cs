using Sts2Headless.IntegrationTests.Coverage;
using Xunit;
using Sts2Headless.Runtime.Loading;
using Sts2Headless.Utils;
using Sts2Headless.Runtime.Hooks;

namespace Sts2Headless.IntegrationTests;

// Doormaker-shape regression net. For every monster patched in
// HangPatches.Monsters.cs, walk the patched method's IL (following the
// async state machine when present) and flag any call to a gameplay
// mutator (CreatureCmd.SetMaxAndCurrentHp, DamageCmd.Attack,
// PowerCmd.Apply / Remove, …). The flagged set is what's currently
// being silently stripped by the "skip body, return CompletedTask"
// prefix — i.e. bosses that can't fight back.
//
// Doormaker was the loud failure because the engine's pre-init MaxHp
// (999999999) made the strip undeniable. Normal-HP bosses with the
// same shape are *silent* — combat still ends because the player kills
// them, but the boss never attacks and the headless win-rate / replay
// corpus / BattleAgent training set is calibrated against a punching
// bag instead of a real fight.
//
// The expected list below freezes the current understood set. The two
// failure modes:
//
//   1. NEW flag (unexpected). Either a new monster patch was added
//      whose move body has gameplay calls, or an existing patch was
//      widened. Either way, audit the entry — the right fix is usually
//      to patch the leaf UI helpers (Cmd.CustomScaledWait,
//      <Monster>.UpdateVisual, TalkCmd.Play, …) and drop the move
//      body from the patch set, as we did for Doormaker.
//   2. GONE flag (entry no longer present). Someone fixed one of the
//      tracked Doormaker-shape suspects (great!) but left the
//      expected-list entry behind. Remove it.
//
// Why this is integration-axis: needs the real sts2.dll loaded, real
// types, real IL via PatchProcessor.GetCurrentInstructions. A unit test
// against the registry alone can't see method bodies.
[Collection(InProcessSts2Collection.Name)]
public class MonsterPatchAuditTests
{
    // Snapshot of every patched monster method whose IL still contains
    // a gameplay-mutating call as of the audit's first introduction
    // (2026-05-21, post-Doormaker). Each entry is the candidate for a
    // Doormaker-shape fix: keep the leaf helpers patched, drop the move
    // body from the monster's patch set.
    //
    // Format: "TypeFqn.MethodName: SortedCommaList of detected gameplay calls".
    //
    // When fixing one: remove the entry below, drop the method from the
    // matching `_xEntry` in HangPatches.Monsters.cs, add a leaf-helper
    // patch as needed, and add a focused regression test (see
    // DoormakerPhaseTransitionTests.cs for the template).
    private static readonly string[] s_expectedDoormakerShape =
    [
        // Baseline captured 2026-05-21 (61 entries). Every patched
        // monster method here strips at least one gameplay mutator —
        // the boss/enemy never deals the damage / applies the power /
        // removes the sleep state the engine intended. Doormaker was
        // graduated out the same day by patching its leaf UI helpers
        // (Cmd.CustomScaledWait + Doormaker.UpdateVisual) and dropping
        // every *Move method from the patch set. Each entry below is a
        // candidate for the same treatment.
        //
        // Fix recipe (per monster):
        //   1. `just probe-method-body <Monster> <Move>` to see what UI
        //      helpers the body actually invokes.
        //   2. Add the leaf helpers to HangPatches.Async.cs /
        //      .Monsters.cs as needed (often Cmd.* already covers them;
        //      sometimes a Godot stub is missing).
        //   3. Drop the method from the monster's _xEntry MethodNames set.
        //   4. Add a focused regression test along the lines of
        //      DoormakerPhaseTransitionTests.cs.
        //   5. Remove the corresponding line from this list.
        //
        // Sorted alphabetically for diff readability.
        "MegaCrit.Sts2.Core.Models.Monsters.CeremonialBeast.BeastCryMove: MegaCrit.Sts2.Core.Commands.PowerCmd.Apply",
        "MegaCrit.Sts2.Core.Models.Monsters.CeremonialBeast.CrushMove: MegaCrit.Sts2.Core.Commands.DamageCmd.Attack,MegaCrit.Sts2.Core.Commands.PowerCmd.Apply",
        "MegaCrit.Sts2.Core.Models.Monsters.CeremonialBeast.PlowMove: MegaCrit.Sts2.Core.Commands.DamageCmd.Attack,MegaCrit.Sts2.Core.Commands.PowerCmd.Apply",
        "MegaCrit.Sts2.Core.Models.Monsters.CeremonialBeast.StampMove: MegaCrit.Sts2.Core.Commands.PowerCmd.Apply",
        "MegaCrit.Sts2.Core.Models.Monsters.CeremonialBeast.StompMove: MegaCrit.Sts2.Core.Commands.DamageCmd.Attack",
        "MegaCrit.Sts2.Core.Models.Monsters.CorpseSlug.GlompMove: MegaCrit.Sts2.Core.Commands.DamageCmd.Attack",
        "MegaCrit.Sts2.Core.Models.Monsters.CorpseSlug.GoopMove: MegaCrit.Sts2.Core.Commands.PowerCmd.Apply",
        "MegaCrit.Sts2.Core.Models.Monsters.CorpseSlug.WhipSlapMove: MegaCrit.Sts2.Core.Commands.DamageCmd.Attack",
        "MegaCrit.Sts2.Core.Models.Monsters.Crusher.AdaptMove: MegaCrit.Sts2.Core.Commands.PowerCmd.Apply",
        "MegaCrit.Sts2.Core.Models.Monsters.Crusher.AfterAddedToRoom: MegaCrit.Sts2.Core.Commands.PowerCmd.Apply",
        "MegaCrit.Sts2.Core.Models.Monsters.Crusher.BugStingMove: MegaCrit.Sts2.Core.Commands.DamageCmd.Attack,MegaCrit.Sts2.Core.Commands.PowerCmd.Apply",
        "MegaCrit.Sts2.Core.Models.Monsters.Crusher.EnlargingStrikeMove: MegaCrit.Sts2.Core.Commands.DamageCmd.Attack",
        "MegaCrit.Sts2.Core.Models.Monsters.Crusher.GuardedStrikeMove: MegaCrit.Sts2.Core.Commands.DamageCmd.Attack",
        "MegaCrit.Sts2.Core.Models.Monsters.Crusher.ThrashMove: MegaCrit.Sts2.Core.Commands.DamageCmd.Attack",
        "MegaCrit.Sts2.Core.Models.Monsters.DecimillipedeSegment.BulkMove: MegaCrit.Sts2.Core.Commands.DamageCmd.Attack,MegaCrit.Sts2.Core.Commands.PowerCmd.Apply",
        "MegaCrit.Sts2.Core.Models.Monsters.DecimillipedeSegment.ConstrictMove: MegaCrit.Sts2.Core.Commands.DamageCmd.Attack,MegaCrit.Sts2.Core.Commands.PowerCmd.Apply",
        "MegaCrit.Sts2.Core.Models.Monsters.DecimillipedeSegment.WritheMove: MegaCrit.Sts2.Core.Commands.DamageCmd.Attack",
        "MegaCrit.Sts2.Core.Models.Monsters.GremlinMerc.DoubleSmashMove: MegaCrit.Sts2.Core.Commands.DamageCmd.Attack,MegaCrit.Sts2.Core.Commands.PowerCmd.Apply",
        "MegaCrit.Sts2.Core.Models.Monsters.GremlinMerc.GimmeMove: MegaCrit.Sts2.Core.Commands.DamageCmd.Attack",
        "MegaCrit.Sts2.Core.Models.Monsters.GremlinMerc.HeheMove: MegaCrit.Sts2.Core.Commands.DamageCmd.Attack,MegaCrit.Sts2.Core.Commands.PowerCmd.Apply",
        "MegaCrit.Sts2.Core.Models.Monsters.LagavulinMatriarch.AfterAddedToRoom: MegaCrit.Sts2.Core.Commands.PowerCmd.Apply",
        "MegaCrit.Sts2.Core.Models.Monsters.LagavulinMatriarch.DisembowelMove: MegaCrit.Sts2.Core.Commands.DamageCmd.Attack",
        "MegaCrit.Sts2.Core.Models.Monsters.LagavulinMatriarch.Slash2Move: MegaCrit.Sts2.Core.Commands.DamageCmd.Attack",
        "MegaCrit.Sts2.Core.Models.Monsters.LagavulinMatriarch.SlashMove: MegaCrit.Sts2.Core.Commands.DamageCmd.Attack",
        "MegaCrit.Sts2.Core.Models.Monsters.LagavulinMatriarch.SoulSiphonMove: MegaCrit.Sts2.Core.Commands.PowerCmd.Apply",
        "MegaCrit.Sts2.Core.Models.Monsters.Rocket.AfterAddedToRoom: MegaCrit.Sts2.Core.Commands.PowerCmd.Apply",
        "MegaCrit.Sts2.Core.Models.Monsters.Rocket.ChargeUpMove: MegaCrit.Sts2.Core.Commands.PowerCmd.Apply",
        "MegaCrit.Sts2.Core.Models.Monsters.Rocket.LaserMove: MegaCrit.Sts2.Core.Commands.DamageCmd.Attack",
        "MegaCrit.Sts2.Core.Models.Monsters.Rocket.PrecisionBeamMove: MegaCrit.Sts2.Core.Commands.DamageCmd.Attack",
        "MegaCrit.Sts2.Core.Models.Monsters.Rocket.TargetingReticleMove: MegaCrit.Sts2.Core.Commands.DamageCmd.Attack",
        "MegaCrit.Sts2.Core.Models.Monsters.SlumberingBeetle.AfterAddedToRoom: MegaCrit.Sts2.Core.Commands.PowerCmd.Apply",
        "MegaCrit.Sts2.Core.Models.Monsters.SlumberingBeetle.RolloutMove: MegaCrit.Sts2.Core.Commands.DamageCmd.Attack,MegaCrit.Sts2.Core.Commands.PowerCmd.Apply",
        "MegaCrit.Sts2.Core.Models.Monsters.SlumberingBeetle.WakeUpMove: MegaCrit.Sts2.Core.Commands.PowerCmd.Remove",
        "MegaCrit.Sts2.Core.Models.Monsters.SoulNexus.DrainLifeMove: MegaCrit.Sts2.Core.Commands.DamageCmd.Attack,MegaCrit.Sts2.Core.Commands.PowerCmd.Apply",
        "MegaCrit.Sts2.Core.Models.Monsters.SoulNexus.MaelstromMove: MegaCrit.Sts2.Core.Commands.DamageCmd.Attack",
        "MegaCrit.Sts2.Core.Models.Monsters.SoulNexus.SoulBurnMove: MegaCrit.Sts2.Core.Commands.DamageCmd.Attack",
        "MegaCrit.Sts2.Core.Models.Monsters.TerrorEel.CrashMove: MegaCrit.Sts2.Core.Commands.DamageCmd.Attack",
        "MegaCrit.Sts2.Core.Models.Monsters.TerrorEel.TerrorMove: MegaCrit.Sts2.Core.Commands.PowerCmd.Apply",
        "MegaCrit.Sts2.Core.Models.Monsters.TerrorEel.ThrashMove: MegaCrit.Sts2.Core.Commands.DamageCmd.Attack,MegaCrit.Sts2.Core.Commands.PowerCmd.Apply",
        "MegaCrit.Sts2.Core.Models.Monsters.TestSubject.AfterAddedToRoom: MegaCrit.Sts2.Core.Commands.PowerCmd.Apply",
        "MegaCrit.Sts2.Core.Models.Monsters.TestSubject.BigPounceMove: MegaCrit.Sts2.Core.Commands.DamageCmd.Attack",
        "MegaCrit.Sts2.Core.Models.Monsters.TestSubject.BiteMove: MegaCrit.Sts2.Core.Commands.DamageCmd.Attack",
        "MegaCrit.Sts2.Core.Models.Monsters.TestSubject.BurningGrowlMove: MegaCrit.Sts2.Core.Commands.PowerCmd.Apply",
        "MegaCrit.Sts2.Core.Models.Monsters.TestSubject.MultiClawMove: MegaCrit.Sts2.Core.Commands.DamageCmd.Attack",
        "MegaCrit.Sts2.Core.Models.Monsters.TestSubject.Phase3LacerateMove: MegaCrit.Sts2.Core.Commands.DamageCmd.Attack",
        "MegaCrit.Sts2.Core.Models.Monsters.TestSubject.RespawnMove: MegaCrit.Sts2.Core.Commands.PowerCmd.Apply,MegaCrit.Sts2.Core.Commands.PowerCmd.Remove",
        "MegaCrit.Sts2.Core.Models.Monsters.TestSubject.Revive: MegaCrit.Sts2.Core.Commands.CreatureCmd.Heal",
        "MegaCrit.Sts2.Core.Models.Monsters.TestSubject.SkullBashMove: MegaCrit.Sts2.Core.Commands.DamageCmd.Attack,MegaCrit.Sts2.Core.Commands.PowerCmd.Apply",
        "MegaCrit.Sts2.Core.Models.Monsters.TheInsatiable.BiteMove: MegaCrit.Sts2.Core.Commands.DamageCmd.Attack",
        "MegaCrit.Sts2.Core.Models.Monsters.TheInsatiable.LiquifyMove: MegaCrit.Sts2.Core.Commands.PowerCmd.Apply",
        "MegaCrit.Sts2.Core.Models.Monsters.TheInsatiable.SalivateMove: MegaCrit.Sts2.Core.Commands.PowerCmd.Apply",
        "MegaCrit.Sts2.Core.Models.Monsters.TheInsatiable.ThrashMove: MegaCrit.Sts2.Core.Commands.DamageCmd.Attack",
        "MegaCrit.Sts2.Core.Models.Monsters.ThievingHopper.FlutterMove: MegaCrit.Sts2.Core.Commands.PowerCmd.Apply",
        "MegaCrit.Sts2.Core.Models.Monsters.ThievingHopper.HatTrickMove: MegaCrit.Sts2.Core.Commands.DamageCmd.Attack",
        "MegaCrit.Sts2.Core.Models.Monsters.ThievingHopper.NabMove: MegaCrit.Sts2.Core.Commands.DamageCmd.Attack",
        "MegaCrit.Sts2.Core.Models.Monsters.ThievingHopper.ThieveryMove: MegaCrit.Sts2.Core.Commands.DamageCmd.Attack,MegaCrit.Sts2.Core.Commands.PowerCmd.Apply",
        "MegaCrit.Sts2.Core.Models.Monsters.Tunneler.BelowMove: MegaCrit.Sts2.Core.Commands.DamageCmd.Attack",
        "MegaCrit.Sts2.Core.Models.Monsters.Tunneler.BiteMove: MegaCrit.Sts2.Core.Commands.DamageCmd.Attack",
        "MegaCrit.Sts2.Core.Models.Monsters.Tunneler.BurrowMove: MegaCrit.Sts2.Core.Commands.PowerCmd.Apply",
    ];

    [Fact]
    public void EveryMonsterPatch_GameplayMutationFootprint_MatchesExpected()
    {
        var repoRoot = Paths.LocateRepoRoot();
        var vendorDir = Path.Combine(repoRoot, "vendor");
        Assert.True(Directory.Exists(vendorDir), $"vendor/ missing at {vendorDir} — run `just setup`.");

        VendorAssemblyResolver.Install(vendorDir);
        var preamble = RuntimeBootstrap.Run(vendorDir);
        Assert.Null(preamble.SetupError);
        Assert.NotNull(preamble.Sts2);

        var audit = MonsterPatchAuditor.Audit(preamble.Sts2!);

        // Flag = "this patched method strips at least one gameplay-mutator
        // call". Methods whose body is pure UI (the SAFE shape) have no
        // flagged calls and aren't surfaced.
        var actual = audit
            .Where(a => a.GameplayCalls.Count > 0)
            .Select(a => $"{a.TypeFqn}.{a.MethodName}: {string.Join(",", a.GameplayCalls)}")
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToArray();
        var expected = s_expectedDoormakerShape
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToArray();

        var unexpected = actual.Except(expected, StringComparer.Ordinal).ToList();
        var stale = expected.Except(actual, StringComparer.Ordinal).ToList();

        var failures = new List<string>();
        if (unexpected.Count > 0)
        {
            failures.Add(
                $"{unexpected.Count} NEW Doormaker-shape flag(s) — a patched move body still " +
                $"contains a gameplay mutator and is silently stripping it:\n" +
                string.Join("\n", unexpected.Select(s => $"  + {s}")) + "\n\n" +
                "Likely fix: patch the leaf UI helpers (Cmd.CustomScaledWait, " +
                "<Monster>.UpdateVisual, TalkCmd.Play, NRunMusicController) and drop " +
                "the listed method from the monster's patch entry in " +
                "HangPatches.Monsters.cs. See PatchDoormaker / DoormakerPhaseTransitionTests " +
                "for the worked example.\n\n" +
                "If the strip is intentional and acceptable for headless coverage, " +
                "add the line to s_expectedDoormakerShape with a // note explaining why.");
        }
        if (stale.Count > 0)
        {
            failures.Add(
                $"{stale.Count} GONE flag(s) — these methods no longer contain gameplay " +
                $"mutators, so the expected-list entry is stale:\n" +
                string.Join("\n", stale.Select(s => $"  - {s}")) + "\n\n" +
                "Remove the corresponding line from s_expectedDoormakerShape.");
        }
        if (failures.Count == 0) return;

        // Always echo the full current list at the bottom so a fresh
        // expected-list paste-in is one copy away.
        failures.Add(
            $"Full current flagged set ({actual.Length} entries) — paste between the " +
            $"`{{`/`}}` of s_expectedDoormakerShape:\n" +
            string.Join("\n", actual.Select(s => $"        \"{s}\",")));

        Assert.Fail(string.Join("\n\n", failures));
    }
}
