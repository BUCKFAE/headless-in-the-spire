using System.Collections;
using System.Reflection;

namespace Sts2Headless.Runtime;

// Sync bridge between Harmony prefixes on CardSelectCmd's hand-side
// factories (FromHandForUpgrade, FromHandForDiscard) and our installed
// HeadlessCardSelector. The factories' bodies normally drive an in-game
// UI screen via NPlayerHand.Instance / NCombatRoom.Instance — both null
// in headless, both NRE before the body's `await Selector.GetSelectedCards`
// can fire. This file replaces those bodies with a minimal sync
// implementation that:
//   1. Gets player.PlayerCombatState.Hand.Cards via reflection.
//   2. Applies an optional CardModel predicate (Armaments wants "not yet
//      upgraded"; Burning Pact wants anything).
//   3. Hands the filtered list to the installed selector.
//   4. Returns Task.FromResult(picked CardModel) so the caller's await
//      gets the same shape it expects.
//
// AD-4: no compile-time sts2 types. Everything resolves at Apply() time
// from the loaded assembly and is cached statically per patched factory.
public static class HeadlessCardSelectorBridge
{
    private static readonly Dictionary<MethodBase, FromHandFactoryConfig> _configs = new();

    // Set by CardSelectorInstaller after a successful install. The bridge
    // consults the selector's pending-hint queue before falling back to
    // "first eligible" so a wire caller can drive Armaments/Burning Pact's
    // pick via RunPlayCardParams.CardSelectIndices, same as it can for
    // Headbutt (which goes through the selector path).
    public static HeadlessCardSelector? Selector { get; set; }

    // Lazy-bound references to the engine types we walk: Player ->
    // PlayerCombatState -> Hand -> Cards. We don't take a hard dependency
    // on Sts2Bindings (that's a higher layer) — just resolve them off the
    // first Player instance we see.
    private static PropertyInfo? _playerPlayerCombatState;
    private static PropertyInfo? _pcsHand;
    private static PropertyInfo? _handCards;
    private static PropertyInfo? _cardIsUpgraded;
    // The card-model type we need to type the IEnumerable<CardModel> arg
    // the selector expects. Captured the first time we see a CardModel.
    private static Type? _cardModelType;
    private static Type? _ienumCardModelType;
    private static MethodInfo? _taskFromResultCardModel;

    public sealed record FromHandFactoryConfig(
        Func<object, bool>? Filter,
        int PlayerArgIndex,
        int CallerFilterArgIndex);

    public static void RegisterFromHandFactory(MethodInfo method, Func<object, bool>? filter, int playerArgIndex, int callerFilterArgIndex)
    {
        _configs[method] = new FromHandFactoryConfig(filter, playerArgIndex, callerFilterArgIndex);
    }

    // Harmony prefix signature. __originalMethod gives us the factory we
    // were patched onto, __args gives the boxed call args (we read
    // [playerArgIndex] for the Player), __result is the Task<T> slot —
    // T is CardModel for single-pick factories (FromHandForUpgrade) and
    // IEnumerable<CardModel> for multi-pick ones (FromHand,
    // FromHandForDiscard). We inspect __originalMethod.ReturnType to pick
    // the right Task shape.
    public static bool FromHandFactoryPrefix(MethodBase __originalMethod, object[] __args, ref Task __result)
    {
        if (!_configs.TryGetValue(__originalMethod, out var config))
        {
            return true;
        }

        var player = config.PlayerArgIndex < __args.Length ? __args[config.PlayerArgIndex] : null;

        // If the factory's caller passes a Func<CardModel, bool> filter,
        // honour it: it carries card-effect-specific semantics (e.g.
        // BurningPact's "not this card itself" exclusion) that we can't
        // re-derive from CardModel.
        Func<object, bool>? callerFilter = null;
        if (config.CallerFilterArgIndex >= 0 && config.CallerFilterArgIndex < __args.Length)
        {
            var raw = __args[config.CallerFilterArgIndex];
            if (raw is Delegate del)
            {
                callerFilter = card =>
                {
                    try { return (bool)(del.DynamicInvoke(card) ?? false); }
                    catch { return false; }
                };
            }
        }

        var picked = player is null ? null : PickFromHand(player, config.Filter, callerFilter);

        var returnType = ((MethodInfo)__originalMethod).ReturnType;
        // Task<T>: examine T to decide between Task<CardModel> and
        // Task<IEnumerable<CardModel>>. The former wants the picked
        // CardModel directly; the latter wants a single-element list (or
        // empty when nothing eligible). All other shapes we don't handle —
        // skip the prefix and let the original run.
        if (!returnType.IsGenericType || returnType.GetGenericTypeDefinition() != typeof(Task<>))
        {
            return true;
        }
        var inner = returnType.GetGenericArguments()[0];
        if (inner.IsGenericType && inner.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            __result = MakeTaskOfEnumerableCardModel(picked, inner.GetGenericArguments()[0]);
        }
        else
        {
            __result = MakeTaskOfCardModel(picked);
        }
        return false;
    }

    private static object? PickFromHand(object player, Func<object, bool>? builtIn, Func<object, bool>? caller)
    {
        EnsureCardPath(player);

        if (_playerPlayerCombatState is null || _pcsHand is null || _handCards is null)
        {
            return null;
        }

        var pcs = _playerPlayerCombatState.GetValue(player);
        if (pcs is null) return null;
        var hand = _pcsHand.GetValue(pcs);
        if (hand is null) return null;
        var raw = _handCards.GetValue(hand);
        if (raw is not IEnumerable cards) return null;

        var eligible = new List<object?>();
        foreach (var c in cards)
        {
            if (c is null) continue;
            if (builtIn is not null && !builtIn(c)) continue;
            if (caller is not null && !caller(c)) continue;
            eligible.Add(c);
        }
        if (eligible.Count == 0) return null;

        // If the caller pre-queued indices via the wire's cardSelectIndices,
        // honour the first one whose index lands in our eligible list. The
        // indices are user-supplied positions in the engine's hand order;
        // we accept the first valid one and ignore the rest of that hint
        // (FromHand-style factories pick exactly one card per prompt).
        if (Selector is not null && Selector.HasPending)
        {
            var hint = Selector.DequeueFirstValid(eligible.Count);
            if (hint is int idx)
            {
                return eligible[idx];
            }
        }

        // Capture CardModel type from the first card.
        if (_cardModelType is null && eligible[0] is { } first)
        {
            _cardModelType = first.GetType();
            // Walk to the type's declared base until we hit `CardModel`
            // (the engine's polymorphic root) so the IEnumerable<T> we
            // build matches the awaited type, not the concrete subclass.
            for (var t = _cardModelType; t is not null; t = t.BaseType)
            {
                if (t.Name == "CardModel")
                {
                    _cardModelType = t;
                    break;
                }
            }
            _ienumCardModelType = typeof(IEnumerable<>).MakeGenericType(_cardModelType);
            _taskFromResultCardModel = typeof(Task)
                .GetMethod(nameof(Task.FromResult), BindingFlags.Public | BindingFlags.Static)!
                .MakeGenericMethod(_cardModelType);
        }

        // Just pick the first eligible card. The selector's pre-queued
        // hints would be the right place to support an agent-chosen
        // index for these factories, but the on-the-wire surface for
        // those (RunPlayCardParams.CardSelectIndices) is already covered
        // by the screen-based factories Headbutt uses; the FromHand path
        // is harder to thread because the engine fires it inside the
        // OnPlay state machine, not before, and the prefix runs without
        // visibility into the caller's stack. First-pick is good enough
        // for the unit-level tests; per-card override is a follow-up.
        return eligible[0];
    }

    private static void EnsureCardPath(object player)
    {
        if (_playerPlayerCombatState is not null) return;
        var playerType = player.GetType();
        _playerPlayerCombatState = WalkProperty(playerType, "PlayerCombatState");
        if (_playerPlayerCombatState is null) return;
        var pcsType = _playerPlayerCombatState.PropertyType;
        _pcsHand = WalkProperty(pcsType, "Hand");
        if (_pcsHand is null) return;
        var handType = _pcsHand.PropertyType;
        _handCards = WalkProperty(handType, "Cards");
    }

    private static PropertyInfo? WalkProperty(Type type, string name)
    {
        for (var t = type; t is not null; t = t.BaseType)
        {
            var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (p is not null) return p;
        }
        return null;
    }

    private static Task MakeTaskOfCardModel(object? picked)
    {
        // First-call init falls back to Task.FromResult<object>(null) when
        // we haven't seen a CardModel yet (empty hand on the very first
        // hand-side factory call) so the engine gets a typed Task back
        // either way. Subsequent calls use the cached typed FromResult.
        if (_taskFromResultCardModel is null || _cardModelType is null)
        {
            return Task.FromResult<object?>(picked);
        }
        return (Task)_taskFromResultCardModel.Invoke(null, new[] { picked })!;
    }

    private static Task MakeTaskOfNullCardModel() => MakeTaskOfCardModel(null);

    // For factories that return Task<IEnumerable<CardModel>> (FromHand,
    // FromHandForDiscard). Builds a typed single-element list (or empty)
    // and wraps it in the closed Task<IEnumerable<T>> the awaiter expects.
    private static Task MakeTaskOfEnumerableCardModel(object? picked, Type cardModelType)
    {
        var listType = typeof(List<>).MakeGenericType(cardModelType);
        var list = (IList)Activator.CreateInstance(listType)!;
        if (picked is not null) list.Add(picked);

        var ienumerableType = typeof(IEnumerable<>).MakeGenericType(cardModelType);
        var fromResult = typeof(Task)
            .GetMethod(nameof(Task.FromResult), BindingFlags.Public | BindingFlags.Static)!
            .MakeGenericMethod(ienumerableType);
        return (Task)fromResult.Invoke(null, new object?[] { list })!;
    }

    // CardModel.IsUpgraded returns true once the card is at max upgrade
    // level; the FromHandForUpgrade filter wants the inverse — "still
    // upgradeable". Cached after the first lookup so we don't walk
    // metadata on the hot path.
    public static bool IsNotUpgraded(object card)
    {
        if (_cardIsUpgraded is null)
        {
            _cardIsUpgraded = WalkProperty(card.GetType(), "IsUpgraded");
            if (_cardIsUpgraded is null)
            {
                // Property missing — be permissive so Armaments still
                // gets *some* card and either upgrades it or no-ops on
                // its own.
                return true;
            }
        }
        var v = _cardIsUpgraded.GetValue(card);
        return v is bool b && !b;
    }
}
