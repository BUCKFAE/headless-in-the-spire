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
        // Doormaker-shape audit. Each entry below is a patched monster
        // method whose IL still contains a gameplay-mutating call —
        // patching the method strips that mutation. Doormaker (the
        // namesake) was graduated 2026-05-21 by patching its leaf UI
        // helpers (Cmd.CustomScaledWait + Doormaker.UpdateVisual) and
        // dropping every *Move method from the patch set.
        //
        // Cleanup wave 2026-05-23: with TestMode.IsOn=true set by
        // BootstrapSequence.SetTestMode, the per-monster move bodies
        // are safe to run unchanged for 11 monsters (Vantom,
        // BowlbugRock, CeremonialBeast, CorpseSlug, DecimillipedeSegment,
        // GremlinMerc, LagavulinMatriarch, TerrorEel, TestSubject,
        // TheInsatiable, ThievingHopper). The patches were removed
        // outright — their entries no longer appear here because the
        // auditor only walks PATCHED methods.
        //
        // Fix recipe (per monster):
        //   1. `dotnet run --project src/Sts2Headless/ -- --probe-method-body
        //      <Type.FullName> <MethodName>` to see what UI helpers the
        //      body actually invokes.
        //   2. Add the leaf helpers to HangPatches.Async.cs /
        //      .Monsters.cs as needed (often Cmd.* already covers them;
        //      sometimes a Godot stub is missing).
        //   3. Drop the method from the monster's _xEntry MethodNames set
        //      (or drop the entire entry if no methods remain).
        //   4. Add a focused regression test along the lines of
        //      DoormakerPhaseTransitionTests.cs.
        //   5. Remove the corresponding line from this list.
        //
        // Sorted alphabetically for diff readability.
        // LagavulinMatriarch / SlumberingBeetle AfterAddedToRoom — graduated
        // 2026-05-23 by switching from "skip the body" to body-replacement:
        // a Harmony prefix runs PlatingPower + the sleep power
        // (AsleepPower / SlumberPower) via PowerCmd.Apply and skips the
        // post-power UI VFX block. The MonsterPatchEntry MethodNames sets
        // are now empty (Crusher/Rocket shape), so the audit no longer
        // walks these methods. See PatchSleepingMonsterAfterAddedToRoom
        // in HangPatches.Monsters.cs.
        "MegaCrit.Sts2.Core.Models.Monsters.Vantom.DismemberMove: MegaCrit.Sts2.Core.Commands.DamageCmd.Attack",
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
