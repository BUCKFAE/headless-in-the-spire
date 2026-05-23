using System.Reflection;
using HarmonyLib;

namespace Sts2Headless.Runtime.Patches;

// Runtime Harmony patches that neutralise sts2.dll's async pumping. Without
// these, anything that hits a Godot frame-yield or a "wait for the animation
// queue to drain" call deadlocks immediately — the headless host has no
// frame loop and no animation queue.
//
// AD-4: we never name sts2 types in C#. The Cmd.Wait and WaitUntilQueue…
// methods are discovered by reflection from the loaded assembly. The Yield
// awaiter target is in the runtime, so we name it directly.
//
// Three foundational sts2-cli interventions (see
// documentation/research/04-sts2-cli-anatomy.md) live in HangPatches.Async.cs;
// CardSelectCmd.From* + TalkCmd patches live in HangPatches.Cards.cs;
// per-monster move/lifecycle patches in HangPatches.Monsters.cs;
// per-power hook patches in HangPatches.Powers.cs. This file owns Apply(),
// the PatchOutcome record, and the Harmony prefix helpers shared by every
// partial.
public static partial class HangPatches
{
    public sealed record PatchOutcome(string Target, bool Patched, string? Detail);

    private const string HarmonyId = "headless-in-the-spire.hang-patches";

    public static IReadOnlyList<PatchOutcome> Apply(Assembly sts2)
    {
        var harmony = new Harmony(HarmonyId);
        return
        [
            PatchYieldAwaiterIsCompleted(harmony),
            PatchCmdWait(harmony, sts2),
            PatchWaitUntilQueueIsEmpty(harmony, sts2),
            PatchTalkCmdPlay(harmony, sts2),
            // CardSelectCmd.From* factories used to be patched here to return
            // Task.FromResult(default) so events that opened a card-pick
            // screen (e.g. RoomFullOfCheese.Gorge) wouldn't take the host
            // down. That band-aid stopped the synchronous crash but left
            // every card that legitimately needs a card-pick (Headbutt,
            // Armaments, Burning Pact) awaiting a null CardSelectCmd. The
            // supported fix is to install a MegaCrit.Sts2.Core.TestSupport
            // .ICardSelector via CardSelectCmd.UseSelector — that runs in
            // CardSelectorInstaller during RuntimeBootstrap and covers
            // the screen-based factories (FromSimpleGrid, FromChooseACardScreen)
            // that Headbutt uses end-to-end.
            //
            // FromHandForUpgrade (Armaments) and FromHandForDiscard
            // (Burning Pact) need a different intervention: their bodies
            // unconditionally call NPlayerHand.Instance.CancelAllCardPlay
            // (NRE in headless — Instance is null) AND, on the
            // ShouldSelectLocalCard=false branch, PlayerChoiceSynchronizer
            // .WaitForRemoteChoice (throws "Cannot wait for remote choice
            // in singleplayer!" by design). Both branches fail. The fix
            // is to replace the body entirely with a prefix that runs the
            // engine's hand-filter logic, consults our selector, and
            // returns the picked CardModel via Task.FromResult — same
            // contract as the original async method, none of the
            // UI/choice-sync side effects that headless can't satisfy.
            PatchFromHandForUpgrade(harmony, sts2),
            PatchFromHandForDiscard(harmony, sts2),
            PatchFromHand(harmony, sts2),
            PatchEscapeArtistPowerAfterTurnEnd(harmony, sts2),
            PatchThievingHopperMoves(harmony, sts2),
            PatchBowlbugRockMoves(harmony, sts2),
            PatchImbalancedPowerAfterDamageGiven(harmony, sts2),
            PatchSoulNexus(harmony, sts2),
            PatchTestSubject(harmony, sts2),
            PatchCeremonialBeast(harmony, sts2),
            // Encounter-sweep wave (see documentation/coverage/every-encounter-ironclad.md):
            // every-encounter-ironclad smoke test surfaced 10 stalls / crashes
            // against Ironclad with [HELLRAISER, POMMEL_STRIKE×2] + 999/999 HP.
            // The seven monster hangs follow the same SoulNexus/CeremonialBeast
            // shape: Task-returning move/power bodies NRE on UI-only state and
            // the exception is swallowed by TaskHelper.LogTaskExceptions.
            PatchCorpseSlug(harmony, sts2),
            PatchDecimillipede(harmony, sts2),
            PatchDoormaker(harmony, sts2),
            PatchFatGremlin(harmony, sts2),
            PatchGremlinMerc(harmony, sts2),
            PatchTerrorEel(harmony, sts2),
            PatchTunneler(harmony, sts2),
            PatchTheInsatiable(harmony, sts2),
            PatchLagavulinMatriarch(harmony, sts2),
            PatchSlumberingBeetle(harmony, sts2),
            PatchRavenousPower(harmony, sts2),
            PatchReattachPower(harmony, sts2),
            PatchHungerPower(harmony, sts2),
            PatchVigorPower(harmony, sts2),
            PatchCrabRagePower(harmony, sts2),
            PatchCrusher(harmony, sts2),
            PatchRocket(harmony, sts2),
            // Godot-tree null-safety: GodotTreeExtensions.AddChildSafely
            // checks `child != null` but not `parent`. The attack-card
            // family (FlashOfSteel / Neutralize / Slice / Suppress /
            // Whirlwind) routes its VFX through
            // `((Node)(object)NCombatRoom.Instance?.CombatVfxContainer)
            //   .AddChildSafely(NThinSliceVfx.Create(...))`. In headless
            // NCombatRoom.Instance is null (no Godot scene tree), so the
            // null-conditional returns null and the inner `parent.AddChild`
            // NREs. Adding a parent null-check matches the method's
            // stated intent ("Safely") — surfaced by MechanicSweep on
            // 2026-05-22.
            PatchAddChildSafelyNullParent(harmony, sts2),
            // IRunState.CardMultiplayerConstraint defaults to
            // SingleplayerOnly when Players.Count <= 1, which removes
            // every MultiplayerOnly card from GetUnlockedCards's pool.
            // MASSIVE_SCROLL's AfterObtained then `where
            // c.MultiplayerConstraint == MultiplayerOnly` filter ends
            // up with an empty pool and the engine throws "couldn't
            // generate a valid rarity!". Override the property to
            // None so the filter is a no-op — headless never wants
            // the mode-specific exclusion anyway (no real multiplayer
            // session), and the engine treats None as "no filter".
            //
            // CardFactory.FilterForPlayerCount is a SECOND gatekeeper
            // that re-applies the same filter inside CreateForReward
            // (it doesn't consult CardMultiplayerConstraint — it
            // checks runState.Players.Count directly). Without the
            // second patch, the constraint-getter override has no
            // effect on MASSIVE_SCROLL's reward generation.
            PatchCardMultiplayerConstraintNone(harmony, sts2),
            PatchCardFactoryFilterForPlayerCount(harmony, sts2),
        ];
    }

    // Harmony prefix signatures: returning false skips the original method;
    // __result is the return slot the patched method will see.

    private static bool YieldIsCompletedPrefix(ref bool __result)
    {
        __result = true;
        return false;
    }

    private static bool ReturnCompletedTaskPrefix(ref System.Threading.Tasks.Task __result)
    {
        __result = System.Threading.Tasks.Task.CompletedTask;
        return false;
    }

    // Generic "skip original, return null" prefix for reference-returning
    // methods whose body NREs in headless because it walks UI-only state
    // (TalkCmd.Play and friends). Harmony copies the boxed-null into the
    // typed return slot, which JIT erases for plain `class` returns.
    private static bool ReturnNullPrefix(ref object? __result)
    {
        __result = null;
        return false;
    }

    // "Skip body entirely" prefix for void-returning methods (Vantom monster
    // moves). No __result slot — Harmony just suppresses the original.
    private static bool SkipVoidPrefix() => false;

    // Generic "skip original, return a completed Task with default result"
    // prefix for `async Task<T>` factories whose pre-first-await body NREs
    // in headless (CardSelectCmd.From*). For non-generic Task it returns
    // Task.CompletedTask; for Task<T> it returns Task.FromResult<T>(default).
    // The factories' callers `await` the result, so the synchronous NRE
    // becomes a normal `null` await. Harmony injects __originalMethod so
    // the prefix can introspect the actual return type per call site.
    private static bool ReturnDefaultTaskPrefix(ref System.Threading.Tasks.Task __result, MethodBase __originalMethod)
    {
        var rt = ((MethodInfo)__originalMethod).ReturnType;
        if (!rt.IsGenericType || rt.GetGenericTypeDefinition() != typeof(System.Threading.Tasks.Task<>))
        {
            __result = System.Threading.Tasks.Task.CompletedTask;
            return false;
        }
        var inner = rt.GetGenericArguments()[0];
        var fromResult = typeof(System.Threading.Tasks.Task)
            .GetMethod(nameof(System.Threading.Tasks.Task.FromResult), BindingFlags.Public | BindingFlags.Static)!
            .MakeGenericMethod(inner);
        var defaultValue = inner.IsValueType ? Activator.CreateInstance(inner) : null;
        __result = (System.Threading.Tasks.Task)fromResult.Invoke(null, [defaultValue])!;
        return false;
    }
}
