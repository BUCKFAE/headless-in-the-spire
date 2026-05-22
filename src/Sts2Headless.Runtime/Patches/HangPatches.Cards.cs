using System.Reflection;
using HarmonyLib;
using Sts2Headless.Runtime.CardSelection;

namespace Sts2Headless.Runtime.Patches;

// Card-flow patches. TalkCmd.Play (speech-bubble VFX) and the CardSelectCmd
// .From* factories (Armaments / Burning Pact / Headbutt hand-pick screens).
// CardSelectCmd is wired to HeadlessCardSelectorBridge so the picked card
// returns through the engine's normal async contract.
public static partial class HangPatches
{
    // BygoneEffigy.WakeMove (and other intro monster moves) invokes
    // TalkCmd.Play(LocString, Creature, VfxColor, VfxDuration) to pop a speech
    // bubble over the speaker. Real Play returns NSpeechBubbleVfx (a Node-
    // derived UI object) and walks UI-only state to construct it; in headless
    // those nodes are absent, so the body NREs. The exception is swallowed by
    // TaskHelper.LogTaskExceptions inside the enemy-turn async chain, leaving
    // combat half-transitioned (EndingPlayerTurnPhaseTwo=True,
    // IsEnemyTurnStarted=True, IsPlayPhase=False) — the residual combat-stall
    // pattern after the GodotStubs gaps are filled.
    //
    // Patch shape: prefix that skips the original (returns false) and sets
    // __result to null. Caller code paths either null-check the returned VFX
    // or tween it; in headless the tween is patched to no-op separately.
    private static PatchOutcome PatchTalkCmdPlay(Harmony harmony, Assembly sts2)
    {
        const string label = "MegaCrit.Sts2.Core.Commands.TalkCmd.*";
        var talkCmdType = sts2.GetType("MegaCrit.Sts2.Core.Commands.TalkCmd");
        if (talkCmdType is null)
        {
            return new PatchOutcome(label, Patched: false, Detail: "type MegaCrit.Sts2.Core.Commands.TalkCmd not found");
        }

        var methods = talkCmdType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && !m.ReturnType.IsValueType && !typeof(System.Threading.Tasks.Task).IsAssignableFrom(m.ReturnType))
            .ToArray();
        if (methods.Length == 0)
        {
            return new PatchOutcome(label, Patched: false, Detail: "no reference-returning methods on TalkCmd to no-op");
        }

        var prefix = typeof(HangPatches).GetMethod(nameof(ReturnNullPrefix), BindingFlags.Static | BindingFlags.NonPublic);
        var sigs = new List<string>(methods.Length);
        foreach (var m in methods)
        {
            harmony.Patch(m, prefix: new HarmonyMethod(prefix));
            sigs.Add($"{m.Name}({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name))}) → {m.ReturnType.Name}");
        }
        return new PatchOutcome(label, Patched: true, Detail: string.Join(", ", sigs));
    }

    private static PatchOutcome PatchFromHandForUpgrade(Harmony harmony, Assembly sts2)
        => PatchFromHandFactory(
            harmony,
            sts2,
            methodName: "FromHandForUpgrade",
            // Cards in hand that aren't already upgraded. CardModel.IsUpgraded
            // returns true when the card is at max upgrade level (the engine
            // refuses to upgrade further); the filter scoping is intentionally
            // permissive so a hand with no upgradeable card just yields a
            // null pick, matching the engine's "no eligible options" case.
            filter: HeadlessCardSelectorBridge.IsNotUpgraded,
            // Method wire signature is (PlayerChoiceContext, Player,
            // AbstractModel) — no caller-supplied filter.
            playerArgIndex: 1,
            callerFilterArgIndex: -1);

    private static PatchOutcome PatchFromHandForDiscard(Harmony harmony, Assembly sts2)
        => PatchFromHandFactory(
            harmony,
            sts2,
            methodName: "FromHandForDiscard",
            // FromHandForDiscard's signature passes a caller-supplied
            // filter (Func<CardModel, bool>) at arg[3]; we apply it to
            // every hand card before picking.
            filter: null,
            playerArgIndex: 1,
            callerFilterArgIndex: 3);

    private static PatchOutcome PatchFromHand(Harmony harmony, Assembly sts2)
        => PatchFromHandFactory(
            harmony,
            sts2,
            methodName: "FromHand",
            // FromHand's signature passes a caller-supplied filter (Func<
            // CardModel, bool>) at arg[3]; BurningPact uses it to scope
            // the pickable set. We honour it so the picked card is
            // actually eligible for the caller's effect.
            filter: null,
            playerArgIndex: 1,
            callerFilterArgIndex: 3);

    // Prefix that short-circuits AddChildSafely when `parent` is null.
    // The engine's body unconditionally does `parent.AddChild(child, …)`
    // after its `child != null` guard, so a null parent NREs. The fix
    // mirrors the existing child-null guard — both args optional, both
    // can no-op when missing. Returning false suppresses the original
    // body so the no-op is clean (no half-executed RemoveChildSafely
    // sibling path either).
    private static bool AddChildSafelyParentGuardPrefix(object? __0)
    {
        if (__0 is null) return false;
        return true;
    }

    // Postfix that overrides IRunState.CardMultiplayerConstraint's
    // result to `None`. The original logic returns SingleplayerOnly
    // when Players.Count <= 1, which is fine for the single-player
    // mode the engine actually targets — but it kicks the
    // `MASSIVE_SCROLL` relic into an empty card pool because the
    // relic explicitly wants MultiplayerOnly cards (which the
    // SingleplayerOnly constraint strips). Returning None means
    // "no filter": both modes' cards stay in the pool, the relic's
    // own `c.MultiplayerConstraint == MultiplayerOnly` filter then
    // finds its 21 candidates and the reward path resolves.
    //
    // Side effect on other call sites: any other engine code that
    // reads CardMultiplayerConstraint to *exclude* the opposite
    // mode now sees None and includes both. Acceptable for headless
    // — we don't ship multiplayer-only cards into a single-player
    // run via any other surface (no shop with random colorless,
    // no event that grants a multiplayer card, etc.), so the only
    // path that's observably different is the deliberate
    // MASSIVE_SCROLL one.
    private static void CardMultiplayerConstraintNonePostfix(ref object __result)
    {
        // Enum value 0 == None for CardMultiplayerConstraint. Boxing
        // the int converts to the enum type the property returns.
        __result = Enum.ToObject(__result.GetType(), 0);
    }

    private static PatchOutcome PatchCardMultiplayerConstraintNone(Harmony harmony, Assembly sts2)
    {
        const string label = "MegaCrit.Sts2.Core.Runs.IRunState.get_CardMultiplayerConstraint";
        var iface = sts2.GetType("MegaCrit.Sts2.Core.Runs.IRunState");
        if (iface is null)
            return new PatchOutcome(label, Patched: false, Detail: "IRunState not found");
        var getter = iface.GetMethod("get_CardMultiplayerConstraint", BindingFlags.Public | BindingFlags.Instance);
        if (getter is null)
            return new PatchOutcome(label, Patched: false, Detail: "get_CardMultiplayerConstraint not found");
        var postfix = typeof(HangPatches).GetMethod(
            nameof(CardMultiplayerConstraintNonePostfix),
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("CardMultiplayerConstraintNonePostfix not found");
        harmony.Patch(getter, postfix: new HarmonyMethod(postfix));
        return new PatchOutcome(label, Patched: true, Detail: "→ None (skip the single/multiplayer card filter)");
    }

    // Postfix that turns CardFactory.FilterForPlayerCount into a
    // pass-through. The original strips MultiplayerOnly cards in
    // single-player and SingleplayerOnly cards in multi-player. In
    // headless we don't care about the mode-specific exclusion (no
    // shop / no random colorless / no event-card spawn that abuses
    // the off-mode set) — only MASSIVE_SCROLL's AfterObtained
    // explicitly wants the MultiplayerOnly subset, and the engine's
    // own filter inside CreateForReward strips it before the
    // relic's downstream filter can run. Pass-through is the
    // smallest blast radius: every other reward generation sees the
    // unfiltered pool and applies whatever rarity / character-pool
    // filters the engine already provides.
    private static void FilterForPlayerCountPassThroughPostfix(object __1, ref object __result)
    {
        // Original signature: FilterForPlayerCount(IRunState runState,
        // IEnumerable<CardModel> options) → IEnumerable<CardModel>.
        // __0 = runState (ignored), __1 = options, __result = filtered
        // output. We discard the filtered result and pass the input
        // options through unchanged — the engine downstream operates
        // on whatever sequence we return.
        __result = __1;
    }

    private static PatchOutcome PatchCardFactoryFilterForPlayerCount(Harmony harmony, Assembly sts2)
    {
        const string label = "MegaCrit.Sts2.Core.Factories.CardFactory.FilterForPlayerCount";
        var factoryType = sts2.GetType("MegaCrit.Sts2.Core.Factories.CardFactory");
        if (factoryType is null)
            return new PatchOutcome(label, Patched: false, Detail: "CardFactory type not found");
        // Static, non-public — the engine has it as `private static`.
        var method = factoryType.GetMethod(
            "FilterForPlayerCount",
            BindingFlags.NonPublic | BindingFlags.Static);
        if (method is null)
            return new PatchOutcome(label, Patched: false, Detail: "FilterForPlayerCount(static private) not found");
        var postfix = typeof(HangPatches).GetMethod(
            nameof(FilterForPlayerCountPassThroughPostfix),
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("FilterForPlayerCountPassThroughPostfix not found");
        harmony.Patch(method, postfix: new HarmonyMethod(postfix));
        return new PatchOutcome(label, Patched: true, Detail: "pass-through (skip single/multiplayer card exclusion)");
    }

    // Patch GodotTreeExtensions.AddChildSafely to handle a null parent
    // gracefully. Same posture as the existing patches: discover by
    // reflection (AD-4), prefix that returns false to suppress the
    // original when the new precondition fails.
    private static PatchOutcome PatchAddChildSafelyNullParent(Harmony harmony, Assembly sts2)
    {
        const string label = "MegaCrit.Sts2.Core.Helpers.GodotTreeExtensions.AddChildSafely";
        var extType = sts2.GetType("MegaCrit.Sts2.Core.Helpers.GodotTreeExtensions");
        if (extType is null)
            return new PatchOutcome(label, Patched: false, Detail: "GodotTreeExtensions type not found");
        var method = extType.GetMethod(
            "AddChildSafely",
            BindingFlags.Public | BindingFlags.Static);
        if (method is null)
            return new PatchOutcome(label, Patched: false, Detail: "AddChildSafely(static) not found");
        var prefix = typeof(HangPatches).GetMethod(
            nameof(AddChildSafelyParentGuardPrefix),
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("AddChildSafelyParentGuardPrefix not found");
        harmony.Patch(method, prefix: new HarmonyMethod(prefix));
        return new PatchOutcome(label, Patched: true, Detail: "null-parent → no-op");
    }

    private static PatchOutcome PatchFromHandFactory(
        Harmony harmony,
        Assembly sts2,
        string methodName,
        Func<object, bool>? filter,
        int playerArgIndex,
        int callerFilterArgIndex)
    {
        var label = $"MegaCrit.Sts2.Core.Commands.CardSelectCmd.{methodName}";
        var cmdType = sts2.GetType("MegaCrit.Sts2.Core.Commands.CardSelectCmd");
        if (cmdType is null)
        {
            return new PatchOutcome(label, Patched: false, Detail: "CardSelectCmd not found");
        }
        var method = cmdType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .FirstOrDefault(m => m.Name == methodName);
        if (method is null)
        {
            return new PatchOutcome(label, Patched: false, Detail: $"{methodName} not found on CardSelectCmd");
        }
        // Bind the bridge once per patched method so the harmony prefix
        // closes over the right filter+arg-indices.
        HeadlessCardSelectorBridge.RegisterFromHandFactory(method, filter, playerArgIndex, callerFilterArgIndex);
        var prefix = typeof(HeadlessCardSelectorBridge).GetMethod(
            nameof(HeadlessCardSelectorBridge.FromHandFactoryPrefix),
            BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("HeadlessCardSelectorBridge.FromHandFactoryPrefix not found");
        harmony.Patch(method, prefix: new HarmonyMethod(prefix));
        return new PatchOutcome(label, Patched: true, Detail: $"args=({string.Join(",", method.GetParameters().Select(p => p.ParameterType.Name))})");
    }
}
