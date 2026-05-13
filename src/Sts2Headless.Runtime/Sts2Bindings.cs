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

// A "live run" is a triple: the Player aggregate, the RunState owned by the
// game, and the RunManager singleton instance that mutates them. Wire code
// treats it opaquely; the binding layer is the only thing that destructures.
public sealed record RunHandle(object Player, object RunState, object RunManager);

// Snapshot of the run for read-only wire surfacing. ExpandableRecord pattern:
// add fields as we bind more reads, never break existing JSON shape.
public sealed record RunSnapshot(
    int CurrentHp,
    int MaxHp,
    int Gold,
    int DeckSize,
    string CurrentRoomType,
    int ActFloor,
    bool IsGameOver);

public sealed class Sts2Bindings
{
    public Assembly Sts2 { get; }

    // ── Player creation ──────────────────────────────────────────────────
    private readonly Type _playerType;
    private readonly MethodInfo _createIroncladRun;
    private readonly object _unlockStateAll;

    // ── StartRun chain (sts2-cli RunSimulator.StartRun) ─────────────────
    private readonly InvocationPlan _runStateCreateForTest;
    private readonly PropertyInfo _runManagerInstance;
    private readonly Type _netServiceType;
    private readonly InvocationPlan _runManagerSetUpTest;
    private readonly PropertyInfo _runStateExtraFields;
    private readonly PropertyInfo _extraFieldsStartedWithNeow;
    private readonly MethodInfo _runManagerGenerateRooms;
    private readonly MethodInfo _runManagerLaunch;
    private readonly MethodInfo _runManagerFinalizeStartingRelics;
    private readonly InvocationPlan _runManagerEnterAct;

    // ── Mutations ────────────────────────────────────────────────────────
    private readonly InvocationPlan _runManagerEnterMapCoord;
    private readonly Type _mapCoordType;

    // ── Read surface ────────────────────────────────────────────────────
    private readonly PropertyInfo _playerGold;
    private readonly PropertyInfo _playerCreature;
    private readonly PropertyInfo _playerDeck;
    private readonly PropertyInfo _creatureCurrentHp;
    private readonly PropertyInfo _creatureMaxHp;
    private readonly PropertyInfo _deckCards;
    private readonly PropertyInfo _runStateCurrentRoom;
    private readonly PropertyInfo _runStateActFloor;
    private readonly PropertyInfo _runStateIsGameOver;

    private Sts2Bindings(Assembly sts2, BindingState s)
    {
        Sts2 = sts2;
        _playerType = s.PlayerType;
        _createIroncladRun = s.CreateIroncladRun;
        _unlockStateAll = s.UnlockStateAll;
        _runStateCreateForTest = s.RunStateCreateForTest;
        _runManagerInstance = s.RunManagerInstance;
        _netServiceType = s.NetServiceType;
        _runManagerSetUpTest = s.RunManagerSetUpTest;
        _runStateExtraFields = s.RunStateExtraFields;
        _extraFieldsStartedWithNeow = s.ExtraFieldsStartedWithNeow;
        _runManagerGenerateRooms = s.RunManagerGenerateRooms;
        _runManagerLaunch = s.RunManagerLaunch;
        _runManagerFinalizeStartingRelics = s.RunManagerFinalizeStartingRelics;
        _runManagerEnterAct = s.RunManagerEnterAct;
        _runManagerEnterMapCoord = s.RunManagerEnterMapCoord;
        _mapCoordType = s.MapCoordType;
        _playerGold = s.PlayerGold;
        _playerCreature = s.PlayerCreature;
        _playerDeck = s.PlayerDeck;
        _creatureCurrentHp = s.CreatureCurrentHp;
        _creatureMaxHp = s.CreatureMaxHp;
        _deckCards = s.DeckCards;
        _runStateCurrentRoom = s.RunStateCurrentRoom;
        _runStateActFloor = s.RunStateActFloor;
        _runStateIsGameOver = s.RunStateIsGameOver;
    }

    // Full sts2-cli StartRun chain, condensed. Returns a triple the wire
    // layer can pass back in for subsequent calls. We intentionally leave
    // StartedWithNeow=false: with it true, NEventRoom.Create silently zeroes
    // Player.Creature.CurrentHp via a GodotStubs gap (see probe-run-state
    // commit 7c9faa1 and the sts2-startrun-chain memory).
    public RunHandle StartIroncladRun(ulong seed)
    {
        var player = _createIroncladRun.Invoke(null, new object?[] { _unlockStateAll, seed })
            ?? throw new InvalidOperationException("Player.CreateForNewRun returned null");

        // CreateForTest takes IReadOnlyList<Player> — pass a strongly-typed
        // Player[] so the framework's parameter-binding sees a compatible
        // covariant cast rather than List<object>.
        var playerArray = Array.CreateInstance(_playerType, 1);
        playerArray.SetValue(player, 0);

        var runState = _runStateCreateForTest.Invoke(null, new Dictionary<string, object?>
        {
            ["players"] = playerArray,
            ["ascensionLevel"] = 0,
            ["seed"] = $"sts2headless-{seed}",
        }) ?? throw new InvalidOperationException("RunState.CreateForTest returned null");

        var runManager = _runManagerInstance.GetValue(null)
            ?? throw new InvalidOperationException("RunManager.Instance returned null");
        var netService = Activator.CreateInstance(_netServiceType)
            ?? throw new InvalidOperationException($"{_netServiceType.FullName} default ctor returned null");

        _runManagerSetUpTest.Invoke(runManager, new Dictionary<string, object?>
        {
            ["state"] = runState,
            ["gameService"] = netService,
        });

        var extra = _runStateExtraFields.GetValue(runState)
            ?? throw new InvalidOperationException("RunState.ExtraFields was null");
        _extraFieldsStartedWithNeow.SetValue(extra, false);

        _runManagerGenerateRooms.Invoke(runManager, null);
        _runManagerLaunch.Invoke(runManager, null);
        if (_runManagerFinalizeStartingRelics.Invoke(runManager, null) is Task finalize)
            finalize.GetAwaiter().GetResult();

        var enterActResult = _runManagerEnterAct.Invoke(runManager, new Dictionary<string, object?>
        {
            ["currentActIndex"] = 0,
            ["doTransition"] = false,
        });
        if (enterActResult is Task enterAct) enterAct.GetAwaiter().GetResult();

        return new RunHandle(player, runState, runManager);
    }

    public RunSnapshot ReadSnapshot(RunHandle handle)
    {
        var creature = _playerCreature.GetValue(handle.Player);
        var currentHp = creature is null ? 0 : (int)_creatureCurrentHp.GetValue(creature)!;
        var maxHp = creature is null ? 0 : (int)_creatureMaxHp.GetValue(creature)!;
        var gold = (int)_playerGold.GetValue(handle.Player)!;

        var deck = _playerDeck.GetValue(handle.Player);
        var deckSize = 0;
        if (deck is not null && _deckCards.GetValue(deck) is System.Collections.IEnumerable cards)
        {
            foreach (var card in cards)
            {
                if (card is not null) deckSize++;
            }
        }

        var room = _runStateCurrentRoom.GetValue(handle.RunState);
        var roomType = room is null ? "<none>" : room.GetType().Name;
        var actFloor = (int)_runStateActFloor.GetValue(handle.RunState)!;
        var isGameOver = (bool)_runStateIsGameOver.GetValue(handle.RunState)!;

        return new RunSnapshot(currentHp, maxHp, gold, deckSize, roomType, actFloor, isGameOver);
    }

    // Build a MapCoord from {col, row} and pump RunManager.EnterMapCoord.
    // MapCoord is a struct with public Col/Row fields (probed at Bind time);
    // construct via Activator.CreateInstance + field set rather than chasing
    // ctor overloads, since structs in sts2 favour init-set fields.
    public void EnterMapCoord(RunHandle handle, int col, int row)
    {
        var coord = Activator.CreateInstance(_mapCoordType)
            ?? throw new InvalidOperationException($"{_mapCoordType.FullName} default ctor returned null");
        SetCoordComponent(coord, "col", col);
        SetCoordComponent(coord, "row", row);

        var result = _runManagerEnterMapCoord.Invoke(handle.RunManager, new Dictionary<string, object?>
        {
            ["coord"] = coord,
        });
        if (result is Task t) t.GetAwaiter().GetResult();
    }

    // MapCoord's fields are lowercase in sts2 (`col`, `row`); fall back to
    // PascalCase if a future version renames them. FieldInfo.SetValue on a
    // boxed value-type writes directly into the box, which is what we want
    // since we pass `coord` through to EnterMapCoord unmodified.
    private static void SetCoordComponent(object coord, string lowerName, int value)
    {
        var t = coord.GetType();
        var upperName = char.ToUpperInvariant(lowerName[0]) + lowerName[1..];
        var field = t.GetField(lowerName, BindingFlags.Public | BindingFlags.Instance)
                    ?? t.GetField(upperName, BindingFlags.Public | BindingFlags.Instance);
        if (field is not null) { field.SetValue(coord, value); return; }
        var prop = t.GetProperty(lowerName, BindingFlags.Public | BindingFlags.Instance)
                   ?? t.GetProperty(upperName, BindingFlags.Public | BindingFlags.Instance)
                   ?? throw new InvalidOperationException($"MapCoord has no '{lowerName}' field or property");
        prop.SetValue(coord, value);
    }

    // Diagnostic shortcut: create a Player without booting a full run. Used
    // by --probe-run-state. Wire callers should use StartIroncladRun instead.
    public object CreateIroncladRun(ulong seed) =>
        _createIroncladRun.Invoke(null, new object?[] { _unlockStateAll, seed })
            ?? throw new InvalidOperationException("Player.CreateForNewRun returned null");

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
        var createIroncladRun = createDef.MakeGenericMethod(ironcladType);

        var unlockAll = ReadStaticAll(unlockStateType);

        var runStateType = Require(sts2, "MegaCrit.Sts2.Core.Runs.RunState");
        var createForTest = runStateType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "CreateForTest")
            ?? throw new InvalidOperationException("RunState.CreateForTest (public static) not found");

        var runManagerType = Require(sts2, "MegaCrit.Sts2.Core.Runs.RunManager");
        var runManagerInstance = runManagerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("RunManager.Instance (public static) not found");

        var netServiceType = Require(sts2, "MegaCrit.Sts2.Core.Multiplayer.NetSingleplayerGameService");

        var setUpTest = SoleOverload(runManagerType, "SetUpTest");
        var generateRooms = NoArgInstance(runManagerType, "GenerateRooms");
        var launch = NoArgInstance(runManagerType, "Launch");
        var finalize = NoArgInstance(runManagerType, "FinalizeStartingRelics");
        var enterAct = SoleOverload(runManagerType, "EnterAct");
        var enterMapCoord = SoleOverload(runManagerType, "EnterMapCoord");

        var extraFields = RequireProperty(runStateType, "ExtraFields");
        var extraFieldsType = extraFields.PropertyType;
        var startedWithNeow = RequireProperty(extraFieldsType, "StartedWithNeow");

        var playerGold = RequireProperty(playerType, "Gold");
        var playerCreature = RequireProperty(playerType, "Creature");
        var playerDeck = RequireProperty(playerType, "Deck");
        var creatureCurrentHp = RequireProperty(playerCreature.PropertyType, "CurrentHp");
        var creatureMaxHp = RequireProperty(playerCreature.PropertyType, "MaxHp");
        var deckCards = RequireProperty(playerDeck.PropertyType, "Cards");
        var currentRoom = RequireProperty(runStateType, "CurrentRoom");
        var actFloor = RequireProperty(runStateType, "ActFloor");
        var isGameOver = RequireProperty(runStateType, "IsGameOver");

        // MapCoord type comes off the EnterMapCoord signature so we don't
        // hard-code its FQN — if MegaCrit moves it, EnterMapCoord moves with it.
        var coordParam = enterMapCoord.Method.GetParameters().FirstOrDefault(p => p.Name == "coord")
            ?? enterMapCoord.Method.GetParameters().FirstOrDefault();
        var mapCoordType = coordParam?.ParameterType
            ?? throw new InvalidOperationException("RunManager.EnterMapCoord has no parameters; cannot infer MapCoord type");

        return new Sts2Bindings(sts2, new BindingState(
            playerType, createIroncladRun, unlockAll,
            new InvocationPlan(createForTest), runManagerInstance, netServiceType,
            setUpTest, extraFields, startedWithNeow,
            generateRooms, launch, finalize, enterAct, enterMapCoord, mapCoordType,
            playerGold, playerCreature, playerDeck,
            creatureCurrentHp, creatureMaxHp, deckCards,
            currentRoom, actFloor, isGameOver));
    }

    private static InvocationPlan SoleOverload(Type owner, string methodName)
    {
        var overloads = owner.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == methodName)
            .ToArray();
        if (overloads.Length == 0) throw new InvalidOperationException($"{owner.Name}.{methodName} (public instance) not found");
        if (overloads.Length > 1)
        {
            var sigs = string.Join(" | ", overloads.Select(m =>
                $"({string.Join(", ", m.GetParameters().Select(p => p.Name))})"));
            throw new InvalidOperationException($"{owner.Name}.{methodName} has {overloads.Length} overloads: {sigs}");
        }
        return new InvocationPlan(overloads[0]);
    }

    private static MethodInfo NoArgInstance(Type owner, string methodName) =>
        owner.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes)
            ?? owner.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == methodName && m.GetParameters().Length == 0)
            ?? throw new InvalidOperationException($"{owner.Name}.{methodName}() (no-arg) not found");

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

    // Capture-all bag for Bind's discovery output. Lets the ctor stay flat
    // and the field list grow without N more constructor params each pass.
    private sealed record BindingState(
        Type PlayerType, MethodInfo CreateIroncladRun, object UnlockStateAll,
        InvocationPlan RunStateCreateForTest, PropertyInfo RunManagerInstance, Type NetServiceType,
        InvocationPlan RunManagerSetUpTest, PropertyInfo RunStateExtraFields, PropertyInfo ExtraFieldsStartedWithNeow,
        MethodInfo RunManagerGenerateRooms, MethodInfo RunManagerLaunch, MethodInfo RunManagerFinalizeStartingRelics,
        InvocationPlan RunManagerEnterAct, InvocationPlan RunManagerEnterMapCoord, Type MapCoordType,
        PropertyInfo PlayerGold, PropertyInfo PlayerCreature, PropertyInfo PlayerDeck,
        PropertyInfo CreatureCurrentHp, PropertyInfo CreatureMaxHp, PropertyInfo DeckCards,
        PropertyInfo RunStateCurrentRoom, PropertyInfo RunStateActFloor, PropertyInfo RunStateIsGameOver);
}

// Bind-time-captured method + parameter shape. Invoke by supplying a name→
// value dict; anything the dict doesn't carry must have a default in the
// method signature, otherwise we throw a diagnostic naming the signature.
// Used wherever sts2's signatures have optional parameters we don't care to
// understand — version drift in those params then doesn't reach us.
internal sealed class InvocationPlan
{
    public MethodInfo Method { get; }
    public ParameterInfo[] Parameters { get; }

    public InvocationPlan(MethodInfo method)
    {
        Method = method;
        Parameters = method.GetParameters();
    }

    public object? Invoke(object? target, IReadOnlyDictionary<string, object?> known)
    {
        var args = new object?[Parameters.Length];
        for (var i = 0; i < Parameters.Length; i++)
        {
            var p = Parameters[i];
            if (known.TryGetValue(p.Name!, out var v))
            {
                args[i] = v;
            }
            else if (p.HasDefaultValue)
            {
                args[i] = p.DefaultValue;
            }
            else
            {
                throw new InvalidOperationException(
                    $"InvocationPlan({Method.DeclaringType?.Name}.{Method.Name}): " +
                    $"parameter '{p.Name}' has no provided value and no default. " +
                    $"signature: {Describe()}");
            }
        }
        return Method.Invoke(target, args);
    }

    public string Describe() =>
        $"({string.Join(", ", Parameters.Select(p => $"{p.Name}: {p.ParameterType.Name}{(p.HasDefaultValue ? "?" : "")}"))})";
}
