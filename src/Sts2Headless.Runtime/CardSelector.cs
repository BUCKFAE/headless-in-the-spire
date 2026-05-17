using System.Collections;
using System.Reflection;

namespace Sts2Headless.Runtime;

// Headless implementation of MegaCrit.Sts2.Core.TestSupport.ICardSelector.
//
// sts2's CardSelectCmd.From* factories normally call into Godot UI
// (NSimpleCardSelectScreen.Create, NDeckUpgradeSelectScreen.ShowScreen)
// to surface a card-pick prompt. In headless those scenes can't load,
// the body NREs, and any card that needs a card-from-X choice — Headbutt
// (move a discarded card to top of draw), Armaments (upgrade a card in
// hand), Burning Pact (discard 1, draw 2), and the event "pick a card"
// screens — takes the host down.
//
// CardSelectCmd has a static UseSelector(ICardSelector) hook for exactly
// this case. When a selector is installed, the factories short-circuit
// the UI and route the choice through ICardSelector.GetSelectedCards.
// Installing one is therefore the correct fix, not a band-aid.
//
// AD-4: we never name sts2 types at compile time. ICardSelector and
// CardModel are resolved reflectively; the proxy that implements the
// interface is built by DispatchProxy.Create<T,TProxy>() with T as a
// runtime type argument via MakeGenericMethod.
//
// Selection policy:
//   * Default (no caller hint): pick the first `minSelect` options.
//     Deterministic and minimally invasive for cards whose effect
//     doesn't depend on which card is chosen (auto-resolve).
//   * Caller hint via QueueSelections(): the next GetSelectedCards
//     call dequeues an int[] and uses those indices. Out-of-range
//     entries are silently dropped; if fewer than `minSelect` valid
//     picks remain, the first-N fallback fills the rest. Excess
//     picks are truncated at `maxSelect`.
//
// The queue is FIFO, so a single play_card that triggers multiple
// nested selections (rare; mostly Armaments → maybe-upgrade chains)
// can supply one int[] per prompt.
public sealed class HeadlessCardSelector
{
    private readonly Type _cardModelType;
    private readonly Type _ienumerableCardModelType;
    private readonly MethodInfo _taskFromResultIenumCardModel;
    private readonly Queue<int[]> _pending = new();
    private int _lastOptionCount;

    private HeadlessCardSelector(Type cardModelType)
    {
        _cardModelType = cardModelType;
        _ienumerableCardModelType = typeof(IEnumerable<>).MakeGenericType(cardModelType);
        _taskFromResultIenumCardModel = typeof(Task)
            .GetMethod(nameof(Task.FromResult), BindingFlags.Public | BindingFlags.Static)!
            .MakeGenericMethod(_ienumerableCardModelType);
    }

    public Type CardModelType => _cardModelType;

    // Number of options offered by the most recent GetSelectedCards call.
    // Diagnostic-only — exposed so a wire handler can warn the caller
    // when they supplied indices for a prompt that never fired.
    public int LastOptionCount => _lastOptionCount;

    // Queue indices for the next selection prompt. Pops one int[] per
    // GetSelectedCards call; remaining entries persist across calls.
    // Caller is responsible for clearing leftovers (ClearPending) at
    // request boundaries so a stale queue doesn't bleed into the next
    // play_card.
    public void QueueSelection(IReadOnlyList<int> indices)
    {
        _pending.Enqueue(indices.ToArray());
    }

    public bool HasPending => _pending.Count > 0;

    public int PendingCount => _pending.Count;

    public void ClearPending() => _pending.Clear();

    // Pop the next pending hint and return the first valid index it
    // carries against an option-count of `optionCount`. Returns null if
    // the queue is empty or no valid index is present. Used by the
    // hand-side factory bridge to route per-card-overrides from the
    // wire's cardSelectIndices into Armaments/BurningPact picks; the
    // screen-based factories consume hints via the standard Dispatch
    // path inside the engine's await.
    public int? DequeueFirstValid(int optionCount)
    {
        if (_pending.Count == 0) return null;
        var hint = _pending.Dequeue();
        for (var i = 0; i < hint.Length; i++)
        {
            if (hint[i] >= 0 && hint[i] < optionCount) return hint[i];
        }
        return null;
    }

    internal object? Dispatch(MethodInfo method, object?[] args)
    {
        switch (method.Name)
        {
            case "GetSelectedCards":
                return GetSelectedCards(args);
            case "GetSelectedCardReward":
                return GetSelectedCardReward(args);
            default:
                // Unknown selector hook — let the engine NRE rather than
                // silently swallow it, so the gap surfaces as a runtime
                // error attributable to the selector.
                throw new NotSupportedException(
                    $"HeadlessCardSelector has no handler for ICardSelector.{method.Name}; " +
                    "interface surface changed in sts2 — extend Dispatch.");
        }
    }

    // Signature: Task<IEnumerable<CardModel>> GetSelectedCards(
    //     IEnumerable<CardModel> options, int minSelect, int maxSelect)
    private object GetSelectedCards(object?[] args)
    {
        var options = (IEnumerable?)args[0] ?? Array.Empty<object>();
        var minSelect = args[1] is int min ? min : 0;
        var maxSelect = args[2] is int max ? max : int.MaxValue;

        var opts = new List<object?>();
        foreach (var o in options) opts.Add(o);
        _lastOptionCount = opts.Count;

        int[] picks;
        if (_pending.Count > 0)
        {
            picks = _pending.Dequeue()
                .Where(i => i >= 0 && i < opts.Count)
                .Distinct()
                .ToArray();
            if (picks.Length < minSelect)
            {
                var taken = new HashSet<int>(picks);
                for (var i = 0; i < opts.Count && taken.Count < minSelect; i++)
                {
                    taken.Add(i);
                }
                picks = taken.OrderBy(i => i).ToArray();
            }
            if (maxSelect >= 0 && picks.Length > maxSelect)
            {
                picks = picks.Take(maxSelect).ToArray();
            }
        }
        else
        {
            var n = Math.Clamp(minSelect, 0, opts.Count);
            picks = Enumerable.Range(0, n).ToArray();
        }

        // Build a typed CardModel[] for the Task<IEnumerable<CardModel>>
        // return. A plain object[] won't satisfy the generic constraint
        // when the engine casts the awaited result back to its concrete
        // collection type.
        var selected = Array.CreateInstance(_cardModelType, picks.Length);
        for (var i = 0; i < picks.Length; i++)
        {
            selected.SetValue(opts[picks[i]], i);
        }

        return _taskFromResultIenumCardModel.Invoke(null, new object?[] { selected })!;
    }

    // Signature: CardModel? GetSelectedCardReward(
    //     IReadOnlyList<CardCreationResult> options,
    //     IReadOnlyList<CardRewardAlternative> alternatives)
    //
    // Used by event handlers that hand the player a card-reward screen
    // (e.g. RoomFullOfCheese.Gorge). The default policy — take the first
    // offered card — keeps event resolution moving without UI; callers
    // that route around the event entirely (the agent's "prefer Decline"
    // policy) never hit this path.
    private object? GetSelectedCardReward(object?[] args)
    {
        if (args[0] is not IList options || options.Count == 0) return null;
        var first = options[0];
        if (first is null) return null;
        // CardCreationResult.Card → CardModel
        var cardProp = first.GetType().GetProperty("Card", BindingFlags.Public | BindingFlags.Instance);
        return cardProp?.GetValue(first);
    }
}

// DispatchProxy subclass that forwards every interface call to the
// HeadlessCardSelector instance set after construction. DispatchProxy
// requires a parameterless ctor and an UNSEALED TProxy (it generates a
// concrete subclass at runtime that derives from us). State is wired up
// post-Create.
public class CardSelectorProxy : DispatchProxy
{
    public HeadlessCardSelector State { get; set; } = null!;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod is null) return null;
        return State.Dispatch(targetMethod, args ?? Array.Empty<object?>());
    }
}

// Wires up the selector at bootstrap time:
//   1. Resolves ICardSelector + CardModel + CardSelectCmd from the loaded sts2.
//   2. Builds a DispatchProxy that implements ICardSelector and forwards to
//      a HeadlessCardSelector.
//   3. Calls CardSelectCmd.UseSelector(proxy) so the From* factories
//      route the choice through us instead of the Godot UI.
//
// Returns a result that mirrors the HangPatches/LocPatches shape so the
// `--probe-init` report can surface whether the install landed.
public static class CardSelectorInstaller
{
    public sealed record InstallOutcome(
        bool Installed,
        string? Detail,
        HeadlessCardSelector? Selector);

    private static readonly string[] CardSelectorInterfaceCandidates =
    [
        "MegaCrit.Sts2.Core.TestSupport.ICardSelector",
        "MegaCrit.Sts2.Core.CardSelection.ICardSelector",
    ];

    private static readonly string[] CardModelCandidates =
    [
        "MegaCrit.Sts2.Core.Entities.Cards.CardModel",
        "MegaCrit.Sts2.Core.Models.Cards.CardModel",
        "MegaCrit.Sts2.Core.Models.CardModel",
    ];

    public static InstallOutcome Install(Assembly sts2)
    {
        var iface = TryResolve(sts2, CardSelectorInterfaceCandidates, out var ifaceSource);
        if (iface is null)
        {
            return new InstallOutcome(false, $"ICardSelector not found ({ifaceSource})", null);
        }

        var cmdLookup = Sts2Reflection.FindType(sts2, "MegaCrit.Sts2.Core.Commands.CardSelectCmd");
        if (!cmdLookup.Found)
        {
            return new InstallOutcome(false, $"CardSelectCmd not found ({cmdLookup.Source})", null);
        }
        var useSelector = cmdLookup.Type!.GetMethod(
            "UseSelector",
            BindingFlags.Public | BindingFlags.Static);
        if (useSelector is null)
        {
            return new InstallOutcome(false, "CardSelectCmd.UseSelector(static) not found", null);
        }

        var cardModel = TryResolve(sts2, CardModelCandidates, out var cardSource);
        if (cardModel is null)
        {
            return new InstallOutcome(false, $"CardModel not found ({cardSource})", null);
        }

        HeadlessCardSelector state;
        object proxy;
        try
        {
            state = Construct(cardModel);
            proxy = BuildProxy(iface, state);
        }
        catch (Exception ex)
        {
            var inner = ex is TargetInvocationException tie ? tie.InnerException ?? ex : ex;
            return new InstallOutcome(false, $"proxy construction failed: {inner.GetType().Name}: {inner.Message}", null);
        }

        try
        {
            useSelector.Invoke(null, new object?[] { proxy });
        }
        catch (TargetInvocationException tie)
        {
            return new InstallOutcome(false, $"UseSelector threw: {tie.InnerException?.GetType().Name}: {tie.InnerException?.Message}", null);
        }

        // Hand-side factories (FromHandForUpgrade etc.) get their per-call
        // hints from the same selector queue via HeadlessCardSelectorBridge,
        // not the DispatchProxy path. Cross-wire here so the bridge can
        // consult `selector.DequeueFirstValid(optionCount)` when picking.
        HeadlessCardSelectorBridge.Selector = state;

        return new InstallOutcome(
            true,
            $"iface={iface.FullName}, cardModel={cardModel.FullName}",
            state);
    }

    private static Type? TryResolve(Assembly sts2, IReadOnlyList<string> candidates, out string source)
    {
        var attempts = new List<string>();
        foreach (var name in candidates)
        {
            var lookup = Sts2Reflection.FindType(sts2, name);
            if (lookup.Found)
            {
                source = lookup.Source;
                return lookup.Type;
            }
            attempts.Add(lookup.Source);
        }
        source = string.Join("; ", attempts);
        return null;
    }

    // Use HeadlessCardSelector's private ctor via reflection so callers
    // can't accidentally construct one without the binding context.
    private static HeadlessCardSelector Construct(Type cardModelType)
    {
        var ctor = typeof(HeadlessCardSelector).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(Type) },
            modifiers: null);
        if (ctor is null)
        {
            throw new InvalidOperationException("HeadlessCardSelector(Type) ctor not found");
        }
        return (HeadlessCardSelector)ctor.Invoke(new object?[] { cardModelType });
    }

    private static object BuildProxy(Type interfaceType, HeadlessCardSelector state)
    {
        var createOpen = typeof(DispatchProxy)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m =>
                m.Name == "Create"
                && m.IsGenericMethodDefinition
                && m.GetGenericArguments().Length == 2
                && m.GetParameters().Length == 0)
            ?? throw new InvalidOperationException("DispatchProxy.Create<T,TProxy>() not found");

        var createClosed = createOpen.MakeGenericMethod(interfaceType, typeof(CardSelectorProxy));
        var proxy = createClosed.Invoke(null, null)
            ?? throw new InvalidOperationException("DispatchProxy.Create returned null");
        ((CardSelectorProxy)proxy).State = state;
        return proxy;
    }
}
