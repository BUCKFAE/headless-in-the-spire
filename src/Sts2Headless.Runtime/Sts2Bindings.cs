using System.Reflection;
using Sts2Headless.Protocol.Methods;

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
//
// Wire-facing types (RunHandle, RunSnapshot) live in RunHandle.cs; the
// reflection helper InvocationPlan lives in InvocationPlan.cs.

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

    // ── Map traversal (for available-node enumeration) ──────────────────
    private readonly PropertyInfo _runStateMap;
    private readonly PropertyInfo _runStateCurrentMapCoord;
    private readonly PropertyInfo _mapStartingMapPoint;
    private readonly MethodInfo _mapGetPoint;
    private readonly PropertyInfo _mapPointChildren;
    private readonly FieldInfo _mapPointCoord;
    private readonly PropertyInfo _mapPointPointType;
    private readonly FieldInfo _mapCoordColField;
    private readonly FieldInfo _mapCoordRowField;

    // ── Event surface ───────────────────────────────────────────────────
    private readonly PropertyInfo _runManagerEventSynchronizer;
    private readonly MethodInfo _eventSyncGetLocalEvent;
    private readonly PropertyInfo _eventIsFinished;
    private readonly PropertyInfo _eventCurrentOptions;
    private readonly PropertyInfo? _eventOptionTextKey;
    private readonly PropertyInfo _eventOptionIsLocked;
    private readonly MethodInfo _eventOptionChosen;
    private readonly MethodInfo? _runManagerProceedFromTerminalRewards;
    private readonly MethodInfo? _runManagerEnterRoom;
    private readonly Type? _mapRoomType;

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
        _runStateMap = s.RunStateMap;
        _runStateCurrentMapCoord = s.RunStateCurrentMapCoord;
        _mapStartingMapPoint = s.MapStartingMapPoint;
        _mapGetPoint = s.MapGetPoint;
        _mapPointChildren = s.MapPointChildren;
        _mapPointCoord = s.MapPointCoord;
        _mapPointPointType = s.MapPointPointType;
        _mapCoordColField = s.MapCoordColField;
        _mapCoordRowField = s.MapCoordRowField;
        _runManagerEventSynchronizer = s.RunManagerEventSynchronizer;
        _eventSyncGetLocalEvent = s.EventSyncGetLocalEvent;
        _eventIsFinished = s.EventIsFinished;
        _eventCurrentOptions = s.EventCurrentOptions;
        _eventOptionTextKey = s.EventOptionTextKey;
        _eventOptionIsLocked = s.EventOptionIsLocked;
        _eventOptionChosen = s.EventOptionChosen;
        _runManagerProceedFromTerminalRewards = s.RunManagerProceedFromTerminalRewards;
        _runManagerEnterRoom = s.RunManagerEnterRoom;
        _mapRoomType = s.MapRoomType;
    }

    // Full sts2-cli StartRun chain, condensed. Returns a triple the wire
    // layer can pass back in for subsequent calls. `withNeow` opts into the
    // Neow blessing event: lands CurrentRoom at EventRoom (the Neow node)
    // instead of MapRoom. Callers can then drive run/select_event_option
    // to dismiss the event; LocPatches + the Texture2D / StringName stubs
    // are what let the event populate options in the first place.
    public RunHandle StartIroncladRun(ulong seed, bool withNeow = false)
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
        _extraFieldsStartedWithNeow.SetValue(extra, withNeow);

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
        var roomTypeName = room?.GetType().Name;
        // Map sts2's PascalCase type name onto the Protocol enum. Anything
        // the enum doesn't catalogue surfaces as Unknown — see RoomType
        // for the curated list and add as we encounter new rooms.
        var roomType = roomTypeName is not null
                       && Enum.TryParse<RoomType>(roomTypeName, ignoreCase: false, out var parsed)
            ? parsed
            : RoomType.Unknown;
        var actFloor = (int)_runStateActFloor.GetValue(handle.RunState)!;
        var isGameOver = (bool)_runStateIsGameOver.GetValue(handle.RunState)!;

        // Only surface map choices when the player is actually at the map.
        // The underlying map graph is computable any time, but exposing it
        // mid-combat or mid-event would let callers issue run/select_map_node
        // in a state where the engine will reject it; better to be empty
        // and have the consumer wait for currentRoomType to flip back.
        var availableNodes = roomType == RoomType.MapRoom
            ? ReadAvailableMapNodes(handle.RunState)
            : Array.Empty<MapNode>();

        // Symmetric to availableNodes: only surface event picks when the
        // engine actually has an Event live. Outside EventRoom GetLocalEvent
        // can return null or stale state — gate to keep the wire honest.
        var availableEventOptions = roomType == RoomType.EventRoom
            ? ReadAvailableEventOptions(handle.RunManager)
            : Array.Empty<EventOption>();

        return new RunSnapshot(currentHp, maxHp, gold, deckSize, roomType, actFloor, isGameOver, availableNodes, availableEventOptions);
    }

    // Walk RunManager.EventSynchronizer → GetLocalEvent → CurrentOptions and
    // shape each option into the wire record. Mirrors sts2-cli's
    // EventChoiceState reduced to the data fields we surface — the loc-lookup
    // and dynamic-vars layers stay client-side, keeping the host loc-free.
    //
    // Returns [] when:
    // - EventSynchronizer is null (e.g. the engine cleared it between rooms);
    // - GetLocalEvent returns null (no event currently live);
    // - IsFinished is true (the event has resolved but the room hasn't flipped yet);
    // - CurrentOptions is null/empty.
    // Each "[]" case means a caller-visible event isn't actually pickable;
    // the room transition is the engine's responsibility to drive.
    private IReadOnlyList<EventOption> ReadAvailableEventOptions(object runManager)
    {
        var sync = _runManagerEventSynchronizer.GetValue(runManager);
        if (sync is null) return Array.Empty<EventOption>();

        var localEvent = _eventSyncGetLocalEvent.Invoke(sync, null);
        if (localEvent is null) return Array.Empty<EventOption>();

        if ((bool)_eventIsFinished.GetValue(localEvent)!) return Array.Empty<EventOption>();

        if (_eventCurrentOptions.GetValue(localEvent) is not System.Collections.IList options
            || options.Count == 0)
        {
            return Array.Empty<EventOption>();
        }

        var result = new List<EventOption>(options.Count);
        for (var i = 0; i < options.Count; i++)
        {
            var opt = options[i];
            if (opt is null) continue;
            var textKey = _eventOptionTextKey?.GetValue(opt) as string;
            var isLocked = (bool)_eventOptionIsLocked.GetValue(opt)!;
            result.Add(new EventOption(i, textKey, isLocked));
        }
        return result;
    }

    // Fire EventOption.Chosen() for the option at `optionIndex` on the
    // currently-live event. No-op if no event is live or the index is out of
    // range — the caller will see the unchanged AvailableEventOptions in the
    // next snapshot and can decide how to recover. Mirrors sts2-cli's
    // DoChooseOption(EventRoom) path, minus the card-reward / pending-bundles
    // detection (those gates don't yet exist on our wire).
    public void SelectEventOption(RunHandle handle, int optionIndex)
    {
        var sync = _runManagerEventSynchronizer.GetValue(handle.RunManager)
            ?? throw new InvalidOperationException("RunManager.EventSynchronizer was null");
        var localEvent = _eventSyncGetLocalEvent.Invoke(sync, null)
            ?? throw new InvalidOperationException("EventSynchronizer.GetLocalEvent returned null");
        if ((bool)_eventIsFinished.GetValue(localEvent)!)
        {
            throw new InvalidOperationException("event is already finished");
        }
        if (_eventCurrentOptions.GetValue(localEvent) is not System.Collections.IList options)
        {
            throw new InvalidOperationException("event has no CurrentOptions");
        }
        if (optionIndex < 0 || optionIndex >= options.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(optionIndex),
                $"optionIndex {optionIndex} out of range; event has {options.Count} options");
        }

        var option = options[optionIndex]
            ?? throw new InvalidOperationException($"event option at index {optionIndex} was null");

        // Chosen() is synchronous in the no-card-reward case (Neow's GAIN
        // OBOL etc.). For options that branch into card selection, the call
        // would block on GetSelectedCardReward — that path is not yet on the
        // wire, so callers picking such options will hang. Documented gap.
        var result = _eventOptionChosen.Invoke(option, null);
        if (result is Task t) t.GetAwaiter().GetResult();

        // sts2-cli pattern: after the event finishes, the game still leaves
        // CurrentRoom as the EventRoom until something nudges the next
        // transition. Without this, the wire reports CurrentRoomType=EventRoom
        // forever and the caller has no way to leave. Mirror sts2-cli's force-
        // transition (ProceedFromTerminalRewardsScreen → EnterRoom(MapRoom))
        // so a successful pick lands the player back on the map.
        var nowFinished = (bool)_eventIsFinished.GetValue(localEvent)!;
        if (nowFinished)
        {
            AutoAdvanceFinishedEvent(handle.RunManager, handle.RunState);
        }
    }

    private void AutoAdvanceFinishedEvent(object runManager, object runState)
    {
        if (_runManagerProceedFromTerminalRewards is not null)
        {
            try
            {
                var proceed = _runManagerProceedFromTerminalRewards.Invoke(runManager, null);
                if (proceed is Task pt) pt.GetAwaiter().GetResult();
            }
            catch { /* sts2-cli also swallows — terminal-rewards may not apply */ }
        }

        // Only force-enter the map if we're still in an EventRoom; for
        // events that resolve naturally (e.g. a curse adding a card and
        // leaving the player at a treasure room) we don't want to override
        // the engine's chosen next room.
        var stillEvent = _runStateCurrentRoom.GetValue(runState)?.GetType().Name == "EventRoom";
        if (stillEvent && _runManagerEnterRoom is not null && _mapRoomType is not null)
        {
            try
            {
                var mapRoom = Activator.CreateInstance(_mapRoomType)
                    ?? throw new InvalidOperationException($"{_mapRoomType.FullName} default ctor returned null");
                var enter = _runManagerEnterRoom.Invoke(runManager, new[] { mapRoom });
                if (enter is Task et) et.GetAwaiter().GetResult();
            }
            catch { /* sts2-cli also swallows — caller will see EventRoom remains and can decide */ }
        }
    }

    // Enumerate next-move candidates by mirroring sts2-cli's MapSelectState:
    // - currentCoord null → starting position; offer the start node plus its
    //   children (sts2 lets you pick any of the starting-row entries).
    // - currentCoord set + GetPoint returns a MapPoint → its Children.
    // - currentCoord set + GetPoint returns null → stale coord after a forced
    //   transition; fall back to StartingMapPoint.Children so the wire stays
    //   honest rather than silently empty.
    // Returns [] if the Map isn't available yet (pre-EnterAct, mid-event).
    private IReadOnlyList<MapNode> ReadAvailableMapNodes(object runState)
    {
        var map = _runStateMap.GetValue(runState);
        if (map is null) return Array.Empty<MapNode>();

        var currentCoord = _runStateCurrentMapCoord.GetValue(runState);

        if (currentCoord is null)
        {
            var startingPoint = _mapStartingMapPoint.GetValue(map);
            if (startingPoint is null) return Array.Empty<MapNode>();

            var result = new List<MapNode> { ToMapNode(startingPoint) };
            AppendChildren(startingPoint, result);
            return result;
        }

        var currentPoint = _mapGetPoint.Invoke(map, new[] { currentCoord });
        if (currentPoint is null)
        {
            var startingPoint = _mapStartingMapPoint.GetValue(map);
            if (startingPoint is null) return Array.Empty<MapNode>();
            var fallback = new List<MapNode>();
            AppendChildren(startingPoint, fallback);
            return fallback;
        }

        var children = new List<MapNode>();
        AppendChildren(currentPoint, children);
        return children;
    }

    private void AppendChildren(object mapPoint, List<MapNode> sink)
    {
        if (_mapPointChildren.GetValue(mapPoint) is not System.Collections.IEnumerable children) return;
        foreach (var child in children)
        {
            if (child is null) continue;
            sink.Add(ToMapNode(child));
        }
    }

    private MapNode ToMapNode(object mapPoint)
    {
        var coord = _mapPointCoord.GetValue(mapPoint)
            ?? throw new InvalidOperationException("MapPoint.coord was null");
        var col = Convert.ToInt32(_mapCoordColField.GetValue(coord));
        var row = Convert.ToInt32(_mapCoordRowField.GetValue(coord));

        var pointTypeValue = _mapPointPointType.GetValue(mapPoint);
        var pointTypeName = pointTypeValue?.ToString();
        // Same Unknown-fallback discipline as RoomType. If the test harness
        // reports an Unknown that isn't actually unknown, the fix is to add
        // the value to MapNodeType — not to widen the parse.
        var type = pointTypeName is not null
                   && Enum.TryParse<MapNodeType>(pointTypeName, ignoreCase: false, out var parsed)
            ? parsed
            : MapNodeType.Unknown;

        return new MapNode(col, row, type);
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

        // Map traversal handles. Walks: RunState.Map → StartingMapPoint or
        // GetPoint(currentMapCoord) → Children → coord.{col,row} + PointType.
        // MapPoint's `coord` is a public field on a struct (lowercase, same
        // as MapCoord proper); MapCoord's col/row are also public fields.
        var runStateMap = RequireProperty(runStateType, "Map");
        var runStateCurrentMapCoord = RequireProperty(runStateType, "CurrentMapCoord");
        var mapType = runStateMap.PropertyType;
        var mapStartingMapPoint = RequireProperty(mapType, "StartingMapPoint");
        var mapGetPoint = mapType.GetMethod("GetPoint", BindingFlags.Public | BindingFlags.Instance, new[] { mapCoordType })
            ?? mapType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "GetPoint" && m.GetParameters().Length == 1)
            ?? throw new InvalidOperationException($"{mapType.FullName}.GetPoint(MapCoord) not found");
        var mapPointType = mapStartingMapPoint.PropertyType;
        var mapPointChildren = RequireProperty(mapPointType, "Children");
        var mapPointCoord = mapPointType.GetField("coord", BindingFlags.Public | BindingFlags.Instance)
            ?? mapPointType.GetField("Coord", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"{mapPointType.FullName}.coord field not found");
        var mapPointPointType = RequireProperty(mapPointType, "PointType");
        var mapCoordColField = mapCoordType.GetField("col", BindingFlags.Public | BindingFlags.Instance)
            ?? mapCoordType.GetField("Col", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"{mapCoordType.FullName}.col field not found");
        var mapCoordRowField = mapCoordType.GetField("row", BindingFlags.Public | BindingFlags.Instance)
            ?? mapCoordType.GetField("Row", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"{mapCoordType.FullName}.row field not found");

        // Event surface. Walk: RunManager.EventSynchronizer →
        // GetLocalEvent() → CurrentOptions / IsFinished. Element type of
        // CurrentOptions tells us where to find TextKey / IsLocked / Chosen.
        // Types are discovered through reachability rather than FQN so a
        // namespace rename in MegaCrit's tree doesn't break the binding.
        var runManagerEventSync = RequireProperty(runManagerType, "EventSynchronizer");
        var eventSyncType = runManagerEventSync.PropertyType;
        var eventSyncGetLocalEvent = eventSyncType.GetMethod("GetLocalEvent", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes)
            ?? eventSyncType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "GetLocalEvent" && m.GetParameters().Length == 0)
            ?? throw new InvalidOperationException($"{eventSyncType.FullName}.GetLocalEvent() not found");
        var eventType = eventSyncGetLocalEvent.ReturnType;
        var eventIsFinished = RequireProperty(eventType, "IsFinished");
        var eventCurrentOptions = RequireProperty(eventType, "CurrentOptions");
        // CurrentOptions is an IList<EventOption> (or similar) — pull the
        // element type so we can bind TextKey / IsLocked / Chosen on it.
        var optionsListType = eventCurrentOptions.PropertyType;
        var eventOptionType = ExtractElementType(optionsListType)
            ?? throw new InvalidOperationException($"{optionsListType.FullName}: cannot infer element type for EventOption discovery");
        // TextKey is the only one of the three we can live without — a few
        // procedural options skip it. IsLocked and Chosen are load-bearing.
        var eventOptionTextKey = eventOptionType.GetProperty("TextKey", BindingFlags.Public | BindingFlags.Instance);
        var eventOptionIsLocked = RequireProperty(eventOptionType, "IsLocked");
        var eventOptionChosen = eventOptionType.GetMethod("Chosen", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes)
            ?? eventOptionType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "Chosen" && m.GetParameters().Length == 0)
            ?? throw new InvalidOperationException($"{eventOptionType.FullName}.Chosen() not found");

        // Soft-bound: auto-advance after a finished event tries to call
        // these to leave the EventRoom. Failures degrade to "caller sees
        // CurrentRoomType=EventRoom and is responsible for next steps".
        var proceedFromTerminalRewards = runManagerType.GetMethod("ProceedFromTerminalRewardsScreen", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes)
            ?? runManagerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "ProceedFromTerminalRewardsScreen" && m.GetParameters().Length == 0);
        var enterRoom = runManagerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == "EnterRoom" && m.GetParameters().Length == 1);
        var mapRoomLookup = Sts2Reflection.FindType(sts2, "MegaCrit.Sts2.Core.Rooms.MapRoom");
        var mapRoomType2 = mapRoomLookup.Found ? mapRoomLookup.Type : null;

        return new Sts2Bindings(sts2, new BindingState(
            playerType, createIroncladRun, unlockAll,
            new InvocationPlan(createForTest), runManagerInstance, netServiceType,
            setUpTest, extraFields, startedWithNeow,
            generateRooms, launch, finalize, enterAct, enterMapCoord, mapCoordType,
            playerGold, playerCreature, playerDeck,
            creatureCurrentHp, creatureMaxHp, deckCards,
            currentRoom, actFloor, isGameOver,
            runStateMap, runStateCurrentMapCoord, mapStartingMapPoint, mapGetPoint,
            mapPointChildren, mapPointCoord, mapPointPointType,
            mapCoordColField, mapCoordRowField,
            runManagerEventSync, eventSyncGetLocalEvent, eventIsFinished, eventCurrentOptions,
            eventOptionTextKey, eventOptionIsLocked, eventOptionChosen,
            proceedFromTerminalRewards, enterRoom, mapRoomType2));
    }

    // List-like CurrentOptions could be IList<T>, List<T>, or a custom Godot-
    // collection wrapper. Try the obvious shapes — generic-interface arg,
    // GenericTypeArguments, then "Item" indexer return — before giving up.
    private static Type? ExtractElementType(Type listType)
    {
        foreach (var iface in listType.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
                return iface.GenericTypeArguments[0];
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IList<>))
                return iface.GenericTypeArguments[0];
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return iface.GenericTypeArguments[0];
        }
        if (listType.IsGenericType && listType.GenericTypeArguments.Length == 1)
            return listType.GenericTypeArguments[0];
        var indexer = listType.GetProperty("Item", BindingFlags.Public | BindingFlags.Instance);
        return indexer?.PropertyType;
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
        PropertyInfo RunStateCurrentRoom, PropertyInfo RunStateActFloor, PropertyInfo RunStateIsGameOver,
        PropertyInfo RunStateMap, PropertyInfo RunStateCurrentMapCoord, PropertyInfo MapStartingMapPoint,
        MethodInfo MapGetPoint, PropertyInfo MapPointChildren, FieldInfo MapPointCoord,
        PropertyInfo MapPointPointType, FieldInfo MapCoordColField, FieldInfo MapCoordRowField,
        PropertyInfo RunManagerEventSynchronizer, MethodInfo EventSyncGetLocalEvent,
        PropertyInfo EventIsFinished, PropertyInfo EventCurrentOptions,
        PropertyInfo? EventOptionTextKey, PropertyInfo EventOptionIsLocked, MethodInfo EventOptionChosen,
        MethodInfo? RunManagerProceedFromTerminalRewards, MethodInfo? RunManagerEnterRoom, Type? MapRoomType);
}
