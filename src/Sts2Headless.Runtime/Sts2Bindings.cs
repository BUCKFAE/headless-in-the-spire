using System.Reflection;

namespace Sts2Headless.Runtime;

// Typed handles over the sts2 reflection surface. Bind once at startup, pay
// the reflection cost up-front, and let request handlers call typed methods
// without re-walking metadata per request.
//
// AD-4: still no compile-time sts2 reference — these are MethodInfo / Type
// handles cached behind named members. Adding a binding means: locate the
// target via Sts2Reflection.FindType, capture the MethodInfo/field/property,
// expose a thin wrapper here. Keep the wire-level concepts (e.g. character
// name strings) out of this class — that translation belongs in the method
// handler, not in the binding layer.
public sealed record PlayerState(int CurrentHp, int MaxHp, int Gold, int DeckSize);

public sealed class Sts2Bindings
{
    public Assembly Sts2 { get; }

    private readonly MethodInfo _createIroncladRun;
    private readonly object _unlockStateAll;
    private readonly PropertyInfo _playerGold;
    private readonly PropertyInfo _playerCreature;
    private readonly PropertyInfo _playerDeck;
    private readonly PropertyInfo _creatureCurrentHp;
    private readonly PropertyInfo _creatureMaxHp;
    private readonly PropertyInfo _deckCards;

    private Sts2Bindings(
        Assembly sts2,
        MethodInfo createIroncladRun,
        object unlockStateAll,
        PropertyInfo playerGold,
        PropertyInfo playerCreature,
        PropertyInfo playerDeck,
        PropertyInfo creatureCurrentHp,
        PropertyInfo creatureMaxHp,
        PropertyInfo deckCards)
    {
        Sts2 = sts2;
        _createIroncladRun = createIroncladRun;
        _unlockStateAll = unlockStateAll;
        _playerGold = playerGold;
        _playerCreature = playerCreature;
        _playerDeck = playerDeck;
        _creatureCurrentHp = creatureCurrentHp;
        _creatureMaxHp = creatureMaxHp;
        _deckCards = deckCards;
    }

    // Player.CreateForNewRun<Ironclad>(UnlockState.all, seed) → Player object
    // returned as `object`; the wire layer surfaces the concrete type name.
    public object CreateIroncladRun(ulong seed) =>
        _createIroncladRun.Invoke(null, new object?[] { _unlockStateAll, seed })
            ?? throw new InvalidOperationException("Player.CreateForNewRun returned null");

    // Snapshot of the live Player. All reads go through cached PropertyInfo
    // handles so request handlers don't re-walk metadata. Creature/Deck may
    // legitimately be null on a freshly-created Player (pre-combat, pre-load);
    // we surface zero rather than throwing so callers can distinguish "no
    // creature yet" from a hard binding failure.
    public PlayerState ReadPlayerState(object player)
    {
        var creature = _playerCreature.GetValue(player);
        var currentHp = creature is null ? 0 : (int)_creatureCurrentHp.GetValue(creature)!;
        var maxHp = creature is null ? 0 : (int)_creatureMaxHp.GetValue(creature)!;
        var gold = (int)_playerGold.GetValue(player)!;

        var deck = _playerDeck.GetValue(player);
        var deckSize = 0;
        if (deck is not null && _deckCards.GetValue(deck) is System.Collections.IEnumerable cards)
        {
            foreach (var card in cards)
            {
                if (card is not null) deckSize++;
            }
        }

        return new PlayerState(currentHp, maxHp, gold, deckSize);
    }

    public static Sts2Bindings Bind(Assembly sts2)
    {
        var playerType = Require(sts2, "MegaCrit.Sts2.Core.Entities.Players.Player");
        var ironcladType = Require(sts2, "MegaCrit.Sts2.Core.Models.Characters.Ironclad");
        var unlockStateType = Require(sts2, "MegaCrit.Sts2.Core.Unlocks.UnlockState");

        var createDef = playerType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "CreateForNewRun"
                              && m.IsGenericMethodDefinition
                              && m.GetGenericArguments().Length == 1
                              && m.GetParameters().Length == 2)
            ?? throw new InvalidOperationException("Player.CreateForNewRun<T>(?, ?) not found");

        var unlockAll = ReadStaticAll(unlockStateType);

        var playerGold = RequireProperty(playerType, "Gold");
        var playerCreature = RequireProperty(playerType, "Creature");
        var playerDeck = RequireProperty(playerType, "Deck");
        var creatureType = playerCreature.PropertyType;
        var deckType = playerDeck.PropertyType;
        var creatureCurrentHp = RequireProperty(creatureType, "CurrentHp");
        var creatureMaxHp = RequireProperty(creatureType, "MaxHp");
        var deckCards = RequireProperty(deckType, "Cards");

        return new Sts2Bindings(
            sts2,
            createDef.MakeGenericMethod(ironcladType),
            unlockAll,
            playerGold,
            playerCreature,
            playerDeck,
            creatureCurrentHp,
            creatureMaxHp,
            deckCards);
    }

    private static PropertyInfo RequireProperty(Type owner, string name) =>
        owner.GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"binding: {owner.FullName}.{name} property not found");

    private static Type Require(Assembly sts2, string fqn)
    {
        var lookup = Sts2Reflection.FindType(sts2, fqn);
        if (!lookup.Found) throw new InvalidOperationException($"binding: {fqn} not found ({lookup.Source})");
        return lookup.Type!;
    }

    private static object ReadStaticAll(Type type)
    {
        var field = type.GetField("all", BindingFlags.Public | BindingFlags.Static);
        var value = field is not null
            ? field.GetValue(null)
            : type.GetProperty("all", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        return value ?? throw new InvalidOperationException($"{type.FullName}.all returned null or not found");
    }
}
