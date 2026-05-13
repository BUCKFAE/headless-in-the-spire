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

    // ── Combat surface ──────────────────────────────────────────────────
    // All members soft-bound (nullable): if the game version we pin against
    // ever moves combat to a different shape, the host still boots and just
    // surfaces CombatState as a stale/empty payload rather than crashing.
    // The mutating methods (EndTurn / PlayCard) throw if their handles are
    // null — that's the caller's signal that combat isn't available.
    private readonly PropertyInfo? _combatManagerInstance;
    private readonly PropertyInfo? _combatManagerIsInProgress;
    private readonly PropertyInfo? _combatManagerIsPlayPhase;
    private readonly MethodInfo? _combatManagerDebugOnlyGetState;
    private readonly PropertyInfo? _combatStateEnemies;
    private readonly PropertyInfo? _combatStateRoundNumber;
    private readonly PropertyInfo? _playerCombatState;
    private readonly PropertyInfo? _pcsHand;
    private readonly PropertyInfo? _pcsDrawPile;
    private readonly PropertyInfo? _pcsDiscardPile;
    private readonly PropertyInfo? _pcsEnergy;
    private readonly PropertyInfo? _pcsMaxEnergy;
    private readonly PropertyInfo? _handCards;
    private readonly PropertyInfo? _pileCards;
    private readonly PropertyInfo? _creaturePowers;
    private readonly PropertyInfo? _creatureBlock;
    private readonly PropertyInfo? _creatureIsDead;
    private readonly PropertyInfo? _powerId;
    private readonly PropertyInfo? _powerAmount;
    private readonly PropertyInfo? _idEntry;
    private readonly PropertyInfo? _cardId;
    private readonly PropertyInfo? _cardEnergyCost;
    private readonly MethodInfo? _energyCostGetResolved;
    private readonly MethodInfo? _cardCanPlay;
    private readonly PropertyInfo? _cardTargetType;
    private readonly PropertyInfo? _enemyMonster;
    private readonly PropertyInfo? _enemyCurrentHp;
    private readonly PropertyInfo? _enemyMaxHp;
    private readonly PropertyInfo? _enemyBlock;
    private readonly PropertyInfo? _enemyIsAlive;
    private readonly PropertyInfo? _enemyPowers;
    private readonly PropertyInfo? _monsterNextMove;
    private readonly PropertyInfo? _monsterId;
    private readonly PropertyInfo? _monsterIntendsToAttack;
    private readonly PropertyInfo? _nextMoveIntents;
    private readonly PropertyInfo? _intentIntentType;
    private readonly MethodInfo? _playerCmdEndTurn;
    private readonly Type? _playCardActionType;
    private readonly ConstructorInfo? _playCardActionCtor;
    private readonly PropertyInfo? _runManagerActionQueueSet;
    private readonly MethodInfo? _actionQueueSetEnqueueWithoutSynchronizing;
    private readonly PropertyInfo? _runManagerActionExecutor;
    private readonly PropertyInfo? _actionExecutorIsRunning;
    private readonly MethodInfo? _actionExecutorFinishedExecutingActions;

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
        var c = s.Combat;
        _combatManagerInstance = c.CombatManagerInstance;
        _combatManagerIsInProgress = c.CombatManagerIsInProgress;
        _combatManagerIsPlayPhase = c.CombatManagerIsPlayPhase;
        _combatManagerDebugOnlyGetState = c.CombatManagerDebugOnlyGetState;
        _combatStateEnemies = c.CombatStateEnemies;
        _combatStateRoundNumber = c.CombatStateRoundNumber;
        _playerCombatState = c.PlayerCombatState;
        _pcsHand = c.PcsHand;
        _pcsDrawPile = c.PcsDrawPile;
        _pcsDiscardPile = c.PcsDiscardPile;
        _pcsEnergy = c.PcsEnergy;
        _pcsMaxEnergy = c.PcsMaxEnergy;
        _handCards = c.HandCards;
        _pileCards = c.PileCards;
        _creaturePowers = c.CreaturePowers;
        _creatureBlock = c.CreatureBlock;
        _creatureIsDead = c.CreatureIsDead;
        _powerId = c.PowerId;
        _powerAmount = c.PowerAmount;
        _idEntry = c.IdEntry;
        _cardId = c.CardId;
        _cardEnergyCost = c.CardEnergyCost;
        _energyCostGetResolved = c.EnergyCostGetResolved;
        _cardCanPlay = c.CardCanPlay;
        _cardTargetType = c.CardTargetType;
        _enemyMonster = c.EnemyMonster;
        _enemyCurrentHp = c.EnemyCurrentHp;
        _enemyMaxHp = c.EnemyMaxHp;
        _enemyBlock = c.EnemyBlock;
        _enemyIsAlive = c.EnemyIsAlive;
        _enemyPowers = c.EnemyPowers;
        _monsterNextMove = c.MonsterNextMove;
        _monsterId = c.MonsterId;
        _monsterIntendsToAttack = c.MonsterIntendsToAttack;
        _nextMoveIntents = c.NextMoveIntents;
        _intentIntentType = c.IntentIntentType;
        _playerCmdEndTurn = c.PlayerCmdEndTurn;
        _playCardActionType = c.PlayCardActionType;
        _playCardActionCtor = c.PlayCardActionCtor;
        _runManagerActionQueueSet = c.RunManagerActionQueueSet;
        _actionQueueSetEnqueueWithoutSynchronizing = c.ActionQueueSetEnqueueWithoutSynchronizing;
        _runManagerActionExecutor = c.RunManagerActionExecutor;
        _actionExecutorIsRunning = c.ActionExecutorIsRunning;
        _actionExecutorFinishedExecutingActions = c.ActionExecutorFinishedExecutingActions;
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

        // Same gating discipline for combat: only read when sts2 has a live
        // combat (room == CombatRoom). Outside, CombatManager.Instance may be
        // null or carry stale state and PlayerCombatState is undefined.
        var combatState = roomType == RoomType.CombatRoom
            ? ReadCombatState(handle)
            : null;

        return new RunSnapshot(currentHp, maxHp, gold, deckSize, roomType, actFloor, isGameOver, availableNodes, availableEventOptions, combatState);
    }

    // CombatManager.Instance + Player.PlayerCombatState walk. Returns null if
    // either the binding didn't resolve or the engine reports no live combat
    // (CombatManager.Instance == null, or .DebugOnlyGetState() returned null).
    // Anything reachable is filled in; missing sub-trees stay at default
    // (e.g. empty Intents list if NextMove isn't populated yet).
    private CombatState? ReadCombatState(RunHandle handle)
    {
        if (_combatManagerInstance is null || _combatManagerDebugOnlyGetState is null) return null;
        var cm = _combatManagerInstance.GetValue(null);
        if (cm is null) return null;

        var isInProgress = _combatManagerIsInProgress is not null && (bool)_combatManagerIsInProgress.GetValue(cm)!;
        var isPlayPhase = _combatManagerIsPlayPhase is not null && (bool)_combatManagerIsPlayPhase.GetValue(cm)!;

        var rawState = _combatManagerDebugOnlyGetState.Invoke(cm, null);
        var round = rawState is not null && _combatStateRoundNumber is not null
            ? Convert.ToInt32(_combatStateRoundNumber.GetValue(rawState))
            : 0;

        var pcs = _playerCombatState?.GetValue(handle.Player);
        var energy = pcs is not null && _pcsEnergy is not null
            ? Convert.ToInt32(_pcsEnergy.GetValue(pcs)) : 0;
        var maxEnergy = pcs is not null && _pcsMaxEnergy is not null
            ? Convert.ToInt32(_pcsMaxEnergy.GetValue(pcs)) : 0;

        var creature = _playerCreature.GetValue(handle.Player);
        var playerBlock = creature is not null && _creatureBlock is not null
            ? Convert.ToInt32(_creatureBlock.GetValue(creature)) : 0;
        var playerPowers = creature is not null ? ReadPowers(_creaturePowers?.GetValue(creature)) : Array.Empty<Power>();

        var hand = pcs is not null && _pcsHand is not null && _handCards is not null
            ? ReadHand(_handCards.GetValue(_pcsHand.GetValue(pcs)!))
            : Array.Empty<Card>();
        var draw = pcs is not null && _pcsDrawPile is not null && _pileCards is not null
            ? CountCards(_pileCards.GetValue(_pcsDrawPile.GetValue(pcs)!)) : 0;
        var discard = pcs is not null && _pcsDiscardPile is not null && _pileCards is not null
            ? CountCards(_pileCards.GetValue(_pcsDiscardPile.GetValue(pcs)!)) : 0;

        var enemies = rawState is not null && _combatStateEnemies is not null
            ? ReadEnemies(_combatStateEnemies.GetValue(rawState))
            : Array.Empty<Enemy>();

        return new CombatState(round, energy, maxEnergy, playerBlock, isPlayPhase, isInProgress,
            draw, discard, hand, enemies, playerPowers);
    }

    private static int CountCards(object? cardsCollection)
    {
        if (cardsCollection is not System.Collections.IEnumerable enumerable) return 0;
        var count = 0;
        foreach (var c in enumerable) if (c is not null) count++;
        return count;
    }

    private IReadOnlyList<Card> ReadHand(object? handCardsObj)
    {
        if (handCardsObj is not System.Collections.IList list) return Array.Empty<Card>();
        var result = new List<Card>(list.Count);
        for (var i = 0; i < list.Count; i++)
        {
            var card = list[i];
            if (card is null) continue;
            var id = ReadEntryId(_cardId, card) ?? card.GetType().Name;
            var cost = _cardEnergyCost is not null && _energyCostGetResolved is not null
                ? Convert.ToInt32(_energyCostGetResolved.Invoke(_cardEnergyCost.GetValue(card), null) ?? 0)
                : 0;
            var canPlay = _cardCanPlay is not null && (bool)(_cardCanPlay.Invoke(card, null) ?? false);
            var targetType = ParseEnum<TargetType>(_cardTargetType?.GetValue(card));
            result.Add(new Card(i, id, cost, canPlay, targetType));
        }
        return result;
    }

    private IReadOnlyList<Enemy> ReadEnemies(object? enemiesObj)
    {
        if (enemiesObj is not System.Collections.IEnumerable enumerable) return Array.Empty<Enemy>();
        var result = new List<Enemy>();
        var idx = 0;
        foreach (var enemy in enumerable)
        {
            if (enemy is null) continue;
            var isAlive = _enemyIsAlive is not null && (bool)_enemyIsAlive.GetValue(enemy)!;
            if (!isAlive) continue;
            var monster = _enemyMonster?.GetValue(enemy);
            var monsterId = monster is not null ? ReadEntryId(_monsterId, monster) : null;
            var hp = _enemyCurrentHp is not null ? Convert.ToInt32(_enemyCurrentHp.GetValue(enemy)) : 0;
            var maxHp = _enemyMaxHp is not null ? Convert.ToInt32(_enemyMaxHp.GetValue(enemy)) : 0;
            var block = _enemyBlock is not null ? Convert.ToInt32(_enemyBlock.GetValue(enemy)) : 0;
            var intendsAttack = monster is not null && _monsterIntendsToAttack is not null
                && (bool)_monsterIntendsToAttack.GetValue(monster)!;
            var intents = monster is not null ? ReadIntents(monster) : Array.Empty<Intent>();
            var powers = ReadPowers(_enemyPowers?.GetValue(enemy));
            result.Add(new Enemy(idx++, monsterId, hp, maxHp, block, intendsAttack, intents, powers));
        }
        return result;
    }

    private IReadOnlyList<Intent> ReadIntents(object monster)
    {
        if (_monsterNextMove is null || _nextMoveIntents is null) return Array.Empty<Intent>();
        var nextMove = _monsterNextMove.GetValue(monster);
        if (nextMove is null) return Array.Empty<Intent>();
        if (_nextMoveIntents.GetValue(nextMove) is not System.Collections.IEnumerable intents) return Array.Empty<Intent>();

        var result = new List<Intent>();
        foreach (var intent in intents)
        {
            if (intent is null) continue;
            var kind = ParseEnum<IntentKind>(_intentIntentType?.GetValue(intent));
            // Damage / Hits / Block are not bound in this pass — sts2's AttackIntent
            // requires PlayerCreatures to call GetTotalDamage and that's a deeper
            // walk we haven't needed yet. Surface the kind only; numeric fields
            // stay null until a caller asks for them.
            result.Add(new Intent(kind, Damage: null, Hits: null, Block: null));
        }
        return result;
    }

    private IReadOnlyList<Power> ReadPowers(object? powersObj)
    {
        if (powersObj is not System.Collections.IEnumerable enumerable) return Array.Empty<Power>();
        var result = new List<Power>();
        foreach (var power in enumerable)
        {
            if (power is null) continue;
            var id = ReadEntryId(_powerId, power) ?? power.GetType().Name;
            var amount = _powerAmount is not null ? Convert.ToInt32(_powerAmount.GetValue(power)) : 0;
            result.Add(new Power(id, amount));
        }
        return result;
    }

    // Read the .Entry string off an Id-shaped object (sts2 wraps stable ids in
    // a struct/record with a public .Entry property). Returns null when the
    // owner is null or .Entry isn't there — caller substitutes a fallback.
    private string? ReadEntryId(PropertyInfo? idProp, object owner)
    {
        if (idProp is null) return null;
        var idValue = idProp.GetValue(owner);
        if (idValue is null) return null;
        if (_idEntry is not null && idProp.PropertyType == _idEntry.DeclaringType)
        {
            return _idEntry.GetValue(idValue) as string;
        }
        // Different Id types (Monster.Id, Power.Id) all expose .Entry but have
        // distinct declaring types; fall back to a direct lookup.
        var entryProp = idValue.GetType().GetProperty("Entry", BindingFlags.Public | BindingFlags.Instance);
        return entryProp?.GetValue(idValue) as string;
    }

    private static TEnum ParseEnum<TEnum>(object? value) where TEnum : struct, Enum
    {
        if (value is null) return default;
        var name = value.ToString();
        if (name is null) return default;
        return Enum.TryParse<TEnum>(name, ignoreCase: false, out var parsed) ? parsed : default;
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

    // Fire PlayerCmd.EndTurn(player, canBackOut: false). HangPatches already
    // forces Task.Yield() to complete inline (the equivalent of sts2-cli's
    // SuppressYield permanently on), so the call returns synchronously and
    // we don't need its multi-phase retry loop. After EndTurn, AutoAdvancePost-
    // Combat detects combat-end transitions and ushers the player back to
    // the map; the caller sees the room flip in the next snapshot.
    public void EndTurn(RunHandle handle)
    {
        if (_playerCmdEndTurn is null)
            throw new InvalidOperationException("PlayerCmd.EndTurn not bound");
        if (_combatManagerInstance is null)
            throw new InvalidOperationException("CombatManager.Instance not bound");
        var cm = _combatManagerInstance.GetValue(null)
            ?? throw new InvalidOperationException("CombatManager.Instance was null — not in combat");
        var inProgress = _combatManagerIsInProgress is not null && (bool)_combatManagerIsInProgress.GetValue(cm)!;
        if (!inProgress)
            throw new InvalidOperationException("combat is not in progress");

        // EndTurn(player, canBackOut, ...optional). Pass Type.Missing for any
        // trailing optional parameters so reflection picks up their default
        // values rather than null-ing a non-nullable parameter.
        var paramCount = _playerCmdEndTurn.GetParameters().Length;
        var args = new object?[paramCount];
        args[0] = handle.Player;
        args[1] = false;
        for (var i = 2; i < paramCount; i++) args[i] = Type.Missing;
        var result = _playerCmdEndTurn.Invoke(null, args);
        if (result is Task t) t.GetAwaiter().GetResult();
        DrainActionExecutor(handle);

        // After EndTurn, sts2 sits in "between phases" (enemy turn pending)
        // until something drives the enemy-side action queue. The next player
        // turn doesn't start until AfterAllPlayersReadyToEndTurn → SwitchSides
        // fires; under TestMode the engine queues this on the action executor
        // but needs nudging to drain when no Godot frame loop is calling in.
        // Spin-pump until IsPlayPhase flips back, combat ends, or we time out.
        AwaitNextPlayerTurnOrCombatEnd(handle);

        AutoAdvancePostCombat(handle);
    }

    private void AwaitNextPlayerTurnOrCombatEnd(RunHandle handle)
    {
        if (_combatManagerInstance is null || _combatManagerIsInProgress is null
            || _combatManagerIsPlayPhase is null) return;
        var cm = _combatManagerInstance.GetValue(null);
        if (cm is null) return;
        var cmType = cm.GetType();

        // After EndTurn, the engine sets the player's "ready" flag but waits
        // for an external trigger to actually fire the enemy turn (in real
        // gameplay, the multiplayer sync layer drives this). In TestMode we
        // need to push the same transition ourselves by calling the public
        // SwitchFromPlayerToEnemySide(Func<Task>) helper.
        var switchSides = cmType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == "SwitchFromPlayerToEnemySide" && m.GetParameters().Length == 1);

        for (var i = 0; i < 500; i++)
        {
            DrainActionExecutor(handle);
            var inProgress = (bool)_combatManagerIsInProgress.GetValue(cm)!;
            if (!inProgress) return;
            var playPhase = (bool)_combatManagerIsPlayPhase.GetValue(cm)!;
            if (playPhase) return;

            if (switchSides is not null)
            {
                try
                {
                    var task = switchSides.Invoke(cm, new object?[] { null });
                    if (task is Task st) st.GetAwaiter().GetResult();
                    DrainActionExecutor(handle);
                }
                catch
                {
                    // Engine refuses (not "all players ready" yet, or already
                    // switched). Give the sync context a chance to settle.
                }
            }
            Thread.Sleep(2);
        }
    }

    // Enqueue a PlayCardAction for hand[cardIndex]. When the card targets
    // AnyEnemy, targetIndex picks from the alive-enemy list (matching the
    // indices ReadCombatState surfaces). Other target types ignore the
    // index; the game resolves targeting internally.
    public void PlayCard(RunHandle handle, int cardIndex, int? targetIndex)
    {
        if (_playerCombatState is null || _pcsHand is null || _handCards is null)
            throw new InvalidOperationException("hand bindings missing — combat surface didn't resolve");
        if (_playCardActionCtor is null || _runManagerActionQueueSet is null
            || _actionQueueSetEnqueueWithoutSynchronizing is null)
            throw new InvalidOperationException("PlayCardAction or ActionQueueSet bindings missing");

        var pcs = _playerCombatState.GetValue(handle.Player)
            ?? throw new InvalidOperationException("Player.PlayerCombatState was null — not in combat");
        var hand = _pcsHand.GetValue(pcs);
        if (_handCards.GetValue(hand!) is not System.Collections.IList cards)
            throw new InvalidOperationException("Hand.Cards is not list-shaped");
        if (cardIndex < 0 || cardIndex >= cards.Count)
            throw new ArgumentOutOfRangeException(nameof(cardIndex),
                $"cardIndex {cardIndex} out of range; hand has {cards.Count} cards");
        var card = cards[cardIndex]
            ?? throw new InvalidOperationException($"card at index {cardIndex} was null");

        var targetType = ParseEnum<TargetType>(_cardTargetType?.GetValue(card));
        object? target = null;
        if (targetType == TargetType.AnyEnemy)
        {
            target = ResolveAnyEnemyTarget(targetIndex)
                ?? throw new InvalidOperationException(
                    targetIndex is null
                        ? "card targets AnyEnemy but no targetIndex was given"
                        : $"targetIndex {targetIndex} is not a live enemy");
        }

        // Pre-flight CanPlay so the wire returns a helpful error rather than
        // silently no-oping when energy/conditions don't allow the play.
        if (_cardCanPlay is not null)
        {
            var ok = (bool)(_cardCanPlay.Invoke(card, null) ?? false);
            if (!ok)
            {
                throw new InvalidOperationException("card cannot be played (CanPlay returned false)");
            }
        }

        var action = _playCardActionCtor.Invoke(new[] { card, target });
        var queue = _runManagerActionQueueSet.GetValue(handle.RunManager)
            ?? throw new InvalidOperationException("RunManager.ActionQueueSet was null");
        _actionQueueSetEnqueueWithoutSynchronizing.Invoke(queue, new[] { action });
        DrainActionExecutor(handle);

        AutoAdvancePostCombat(handle);
    }

    private object? ResolveAnyEnemyTarget(int? targetIndex)
    {
        if (_combatManagerInstance is null || _combatManagerDebugOnlyGetState is null
            || _combatStateEnemies is null || _enemyIsAlive is null) return null;
        var cm = _combatManagerInstance.GetValue(null);
        if (cm is null) return null;
        var state = _combatManagerDebugOnlyGetState.Invoke(cm, null);
        if (state is null) return null;
        if (_combatStateEnemies.GetValue(state) is not System.Collections.IEnumerable enemies) return null;

        var alive = new List<object>();
        foreach (var e in enemies)
        {
            if (e is null) continue;
            if ((bool)_enemyIsAlive.GetValue(e)!) alive.Add(e);
        }
        if (alive.Count == 0) return null;
        if (targetIndex is null) return alive[0];
        if (targetIndex < 0 || targetIndex >= alive.Count) return null;
        return alive[targetIndex.Value];
    }

    // After a mutating combat action, detect "combat ended, room hasn't moved"
    // and force the same transition sts2-cli's ForceToMap does: drain terminal
    // rewards (which generates the post-combat reward set), then EnterRoom(MapRoom)
    // if we're still parked at a CombatRoom. Card rewards are deferred — the
    // generated reward set is left in the engine's hands; only the room
    // transition is forced. Boss rooms aren't yet branched on (EnterNextAct);
    // tested seeds don't hit a boss until later passes wire that up.
    private void AutoAdvancePostCombat(RunHandle handle)
    {
        if (_combatManagerInstance is null) return;
        var cm = _combatManagerInstance.GetValue(null);
        if (cm is null) return;
        var inProgress = _combatManagerIsInProgress is not null && (bool)_combatManagerIsInProgress.GetValue(cm)!;
        if (inProgress) return;

        var roomName = _runStateCurrentRoom.GetValue(handle.RunState)?.GetType().Name;
        if (roomName != "CombatRoom") return;

        AutoAdvanceFinishedEvent(handle.RunManager, handle.RunState);
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

        // Only force-enter the map if we're still in a room that won't
        // resolve on its own: a finished Event whose room didn't flip, or a
        // CombatRoom whose combat ended but didn't auto-transition. For
        // rooms that resolve naturally (e.g. an event that leaves the
        // player at a treasure room) we don't override the engine.
        var currentName = _runStateCurrentRoom.GetValue(runState)?.GetType().Name;
        var stillStuck = currentName is "EventRoom" or "CombatRoom";
        if (stillStuck && _runManagerEnterRoom is not null && _mapRoomType is not null)
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

    // Spin until ActionExecutor.IsRunning flips to false. Each iteration also
    // pumps the synchronization context, draining any callbacks the executor
    // posted while running. Mirrors sts2-cli's WaitForActionExecutor; the
    // hard cap prevents an infinite loop if a deferred action wedges.
    // Wait for the action queue to drain. sts2's ActionExecutor.ExecuteActions
    // runs on a background-style task that's kicked off by ActionQueueChanged
    // when actions are enqueued; awaiting FinishedExecutingActions both
    // observes the drain and (in test mode) drives it forward under our
    // inline sync context. The IsRunning loop is the belt-and-suspenders
    // fallback for cases where the queue was already empty.
    public void DrainActionExecutor(RunHandle handle)
    {
        if (_runManagerActionExecutor is null) return;
        var executor = _runManagerActionExecutor.GetValue(handle.RunManager);
        if (executor is null) return;

        for (var i = 0; i < 5000; i++)
        {
            if (_actionExecutorFinishedExecutingActions is not null)
            {
                var task = _actionExecutorFinishedExecutingActions.Invoke(executor, Array.Empty<object?>());
                if (task is Task t) t.GetAwaiter().GetResult();
            }
            var running = _actionExecutorIsRunning is not null
                && (bool)_actionExecutorIsRunning.GetValue(executor)!;
            if (!running) return;
        }
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

        // EnterMapCoord returns once the room transition starts; combat-setup
        // actions (CombatRoom.EnterInternal → CombatManager.SetUpCombat →
        // StartCombatInternal) are queued on the ActionExecutor and need
        // draining before the snapshot will see IsInProgress=true.
        DrainActionExecutor(handle);
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

        var combat = BindCombat(sts2, runManagerType, playerType, playerCreature.PropertyType);

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
            proceedFromTerminalRewards, enterRoom, mapRoomType2,
            combat));
    }

    // Combat discovery. Every step is soft — if something doesn't resolve we
    // return a CombatBindings with nulls in those slots and the read/mutate
    // path degrades to "no combat data" rather than crashing bootstrap. The
    // chain follows sts2-cli's CombatPlayState / DoPlayCard / DoEndTurn
    // shapes, reduced to the minimal data the wire surfaces.
    //
    // Type discovery uses reachability (Player.PlayerCombatState → PlayerCombatState
    // → Hand → Hand.Cards → Card) where possible, with FQN fallback for the
    // few cross-tree types (CombatManager, PlayerCmd, PlayCardAction).
    private static CombatBindings BindCombat(Assembly sts2, Type runManagerType, Type playerType, Type creatureType)
    {
        // CombatManager singleton + state probes.
        PropertyInfo? cmInstance = null;
        PropertyInfo? cmIsInProgress = null;
        PropertyInfo? cmIsPlayPhase = null;
        MethodInfo? cmDebugOnly = null;
        PropertyInfo? csEnemies = null;
        PropertyInfo? csRound = null;
        var cmLookup = Sts2Reflection.FindType(sts2, "MegaCrit.Sts2.Core.Combat.CombatManager");
        if (cmLookup.Found)
        {
            var cmType = cmLookup.Type!;
            cmInstance = cmType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            cmIsInProgress = cmType.GetProperty("IsInProgress", BindingFlags.Public | BindingFlags.Instance);
            cmIsPlayPhase = cmType.GetProperty("IsPlayPhase", BindingFlags.Public | BindingFlags.Instance);
            cmDebugOnly = cmType.GetMethod("DebugOnlyGetState", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
            if (cmDebugOnly is not null)
            {
                var csType = cmDebugOnly.ReturnType;
                csEnemies = csType.GetProperty("Enemies", BindingFlags.Public | BindingFlags.Instance);
                csRound = csType.GetProperty("RoundNumber", BindingFlags.Public | BindingFlags.Instance);
            }
        }

        // PlayerCombatState → Hand / DrawPile / DiscardPile / Energy.
        var pcs = playerType.GetProperty("PlayerCombatState", BindingFlags.Public | BindingFlags.Instance);
        PropertyInfo? pcsHand = null, pcsDraw = null, pcsDiscard = null, pcsEnergy = null, pcsMaxEnergy = null;
        PropertyInfo? handCards = null, pileCards = null;
        PropertyInfo? cardId = null, cardEnergyCost = null, cardTargetType = null;
        MethodInfo? energyCostGetResolved = null, cardCanPlay = null;
        PropertyInfo? idEntry = null;
        if (pcs is not null)
        {
            var pcsType = pcs.PropertyType;
            pcsHand = pcsType.GetProperty("Hand", BindingFlags.Public | BindingFlags.Instance);
            pcsDraw = pcsType.GetProperty("DrawPile", BindingFlags.Public | BindingFlags.Instance);
            pcsDiscard = pcsType.GetProperty("DiscardPile", BindingFlags.Public | BindingFlags.Instance);
            pcsEnergy = pcsType.GetProperty("Energy", BindingFlags.Public | BindingFlags.Instance);
            pcsMaxEnergy = pcsType.GetProperty("MaxEnergy", BindingFlags.Public | BindingFlags.Instance);

            if (pcsHand is not null)
            {
                handCards = pcsHand.PropertyType.GetProperty("Cards", BindingFlags.Public | BindingFlags.Instance);
                if (handCards is not null)
                {
                    var cardType = ExtractElementType(handCards.PropertyType);
                    if (cardType is not null)
                    {
                        cardId = cardType.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
                        cardEnergyCost = cardType.GetProperty("EnergyCost", BindingFlags.Public | BindingFlags.Instance);
                        cardTargetType = cardType.GetProperty("TargetType", BindingFlags.Public | BindingFlags.Instance);
                        energyCostGetResolved = cardEnergyCost?.PropertyType.GetMethod("GetResolved",
                            BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
                        // Prefer the 0-arg CanPlay; the 2-out overload is also
                        // public but requires marshalling the out params and we
                        // don't surface a reason on the wire.
                        cardCanPlay = cardType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                            .FirstOrDefault(m => m.Name == "CanPlay" && m.ReturnType == typeof(bool)
                                              && m.GetParameters().Length == 0);
                        if (cardId is not null)
                        {
                            idEntry = cardId.PropertyType.GetProperty("Entry", BindingFlags.Public | BindingFlags.Instance);
                        }
                    }
                }
            }
            if (pcsDraw is not null)
            {
                pileCards = pcsDraw.PropertyType.GetProperty("Cards", BindingFlags.Public | BindingFlags.Instance);
            }
        }

        // Creature surface for Block / Powers / IsDead (lives on Creature itself,
        // not Player — player.Creature is the right entry point).
        var creatureBlock = creatureType.GetProperty("Block", BindingFlags.Public | BindingFlags.Instance);
        var creaturePowers = creatureType.GetProperty("Powers", BindingFlags.Public | BindingFlags.Instance);
        var creatureIsDead = creatureType.GetProperty("IsDead", BindingFlags.Public | BindingFlags.Instance);

        PropertyInfo? powerId = null, powerAmount = null;
        if (creaturePowers is not null)
        {
            var powerType = ExtractElementType(creaturePowers.PropertyType);
            if (powerType is not null)
            {
                powerId = powerType.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
                powerAmount = powerType.GetProperty("Amount", BindingFlags.Public | BindingFlags.Instance);
            }
        }

        // Enemy / Monster / Intent surface.
        PropertyInfo? enemyMonster = null, enemyHp = null, enemyMax = null, enemyBlock = null,
            enemyIsAlive = null, enemyPowers = null;
        PropertyInfo? monsterNextMove = null, monsterId = null, monsterIntendsToAttack = null;
        PropertyInfo? nextMoveIntents = null, intentIntentType = null;
        if (csEnemies is not null)
        {
            var enemyType = ExtractElementType(csEnemies.PropertyType);
            if (enemyType is not null)
            {
                enemyMonster = enemyType.GetProperty("Monster", BindingFlags.Public | BindingFlags.Instance);
                enemyHp = enemyType.GetProperty("CurrentHp", BindingFlags.Public | BindingFlags.Instance);
                enemyMax = enemyType.GetProperty("MaxHp", BindingFlags.Public | BindingFlags.Instance);
                enemyBlock = enemyType.GetProperty("Block", BindingFlags.Public | BindingFlags.Instance);
                enemyIsAlive = enemyType.GetProperty("IsAlive", BindingFlags.Public | BindingFlags.Instance);
                enemyPowers = enemyType.GetProperty("Powers", BindingFlags.Public | BindingFlags.Instance);
                if (enemyMonster is not null)
                {
                    var monsterType = enemyMonster.PropertyType;
                    monsterNextMove = monsterType.GetProperty("NextMove", BindingFlags.Public | BindingFlags.Instance);
                    monsterId = monsterType.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
                    monsterIntendsToAttack = monsterType.GetProperty("IntendsToAttack", BindingFlags.Public | BindingFlags.Instance);
                    if (monsterNextMove is not null)
                    {
                        nextMoveIntents = monsterNextMove.PropertyType.GetProperty("Intents", BindingFlags.Public | BindingFlags.Instance);
                        if (nextMoveIntents is not null)
                        {
                            var intentType = ExtractElementType(nextMoveIntents.PropertyType);
                            intentIntentType = intentType?.GetProperty("IntentType", BindingFlags.Public | BindingFlags.Instance);
                        }
                    }
                }
            }
        }

        // PlayerCmd.EndTurn static + PlayCardAction ctor + ActionQueueSet.Enqueue.
        // EndTurn's signature has grown over patches (currently 3 params, with
        // the trailing `Func<Task>` defaulting to null). Match by name only and
        // let the call site fill missing optional args with Type.Missing.
        MethodInfo? playerCmdEndTurn = null;
        var playerCmdLookup = Sts2Reflection.FindType(sts2, "MegaCrit.Sts2.Core.Commands.PlayerCmd");
        if (playerCmdLookup.Found)
        {
            playerCmdEndTurn = playerCmdLookup.Type!.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "EndTurn"
                                  && m.GetParameters().Length >= 2
                                  && m.GetParameters()[1].ParameterType == typeof(bool));
        }

        Type? playCardActionType = null;
        ConstructorInfo? playCardActionCtor = null;
        var pcaLookup = Sts2Reflection.FindType(sts2, "MegaCrit.Sts2.Core.GameActions.PlayCardAction");
        if (pcaLookup.Found)
        {
            playCardActionType = pcaLookup.Type!;
            playCardActionCtor = playCardActionType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(c => c.GetParameters().Length == 2);
        }

        var actionQueueSet = runManagerType.GetProperty("ActionQueueSet", BindingFlags.Public | BindingFlags.Instance);
        MethodInfo? enqueueWithoutSync = null;
        if (actionQueueSet is not null)
        {
            enqueueWithoutSync = actionQueueSet.PropertyType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "EnqueueWithoutSynchronizing" && m.GetParameters().Length == 1);
        }

        // ActionExecutor drives queued game actions (start combat, card plays,
        // monster turn). After a mutating wire call we wait for it to drain so
        // the snapshot reflects post-action state. IsRunning is the poll handle;
        // FinishedExecutingActions returns a Task that completes when the queue
        // empties — sts2-cli uses the spin-on-IsRunning pattern, which is
        // simpler and matches our inline sync-context posture.
        var actionExecutor = runManagerType.GetProperty("ActionExecutor", BindingFlags.Public | BindingFlags.Instance);
        PropertyInfo? actionExecutorIsRunning = null;
        MethodInfo? actionExecutorFinished = null;
        if (actionExecutor is not null)
        {
            actionExecutorIsRunning = actionExecutor.PropertyType.GetProperty("IsRunning", BindingFlags.Public | BindingFlags.Instance);
            actionExecutorFinished = actionExecutor.PropertyType.GetMethod("FinishedExecutingActions",
                BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
        }

        return new CombatBindings(
            cmInstance, cmIsInProgress, cmIsPlayPhase, cmDebugOnly,
            csEnemies, csRound,
            pcs, pcsHand, pcsDraw, pcsDiscard, pcsEnergy, pcsMaxEnergy,
            handCards, pileCards,
            creaturePowers, creatureBlock, creatureIsDead,
            powerId, powerAmount, idEntry,
            cardId, cardEnergyCost, energyCostGetResolved, cardCanPlay, cardTargetType,
            enemyMonster, enemyHp, enemyMax, enemyBlock, enemyIsAlive, enemyPowers,
            monsterNextMove, monsterId, monsterIntendsToAttack,
            nextMoveIntents, intentIntentType,
            playerCmdEndTurn, playCardActionType, playCardActionCtor,
            actionQueueSet, enqueueWithoutSync,
            actionExecutor, actionExecutorIsRunning, actionExecutorFinished);
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
        MethodInfo? RunManagerProceedFromTerminalRewards, MethodInfo? RunManagerEnterRoom, Type? MapRoomType,
        CombatBindings Combat);

    // Combat surface is grouped to keep BindingState's positional ctor scannable.
    // Every member is nullable: combat is opt-in at read time (snapshot returns
    // a null CombatState when reflection couldn't find a piece), and the mutating
    // methods throw with a clear message if they're called against a binding that
    // didn't resolve.
    private sealed record CombatBindings(
        PropertyInfo? CombatManagerInstance, PropertyInfo? CombatManagerIsInProgress,
        PropertyInfo? CombatManagerIsPlayPhase, MethodInfo? CombatManagerDebugOnlyGetState,
        PropertyInfo? CombatStateEnemies, PropertyInfo? CombatStateRoundNumber,
        PropertyInfo? PlayerCombatState, PropertyInfo? PcsHand, PropertyInfo? PcsDrawPile,
        PropertyInfo? PcsDiscardPile, PropertyInfo? PcsEnergy, PropertyInfo? PcsMaxEnergy,
        PropertyInfo? HandCards, PropertyInfo? PileCards,
        PropertyInfo? CreaturePowers, PropertyInfo? CreatureBlock, PropertyInfo? CreatureIsDead,
        PropertyInfo? PowerId, PropertyInfo? PowerAmount, PropertyInfo? IdEntry,
        PropertyInfo? CardId, PropertyInfo? CardEnergyCost, MethodInfo? EnergyCostGetResolved,
        MethodInfo? CardCanPlay, PropertyInfo? CardTargetType,
        PropertyInfo? EnemyMonster, PropertyInfo? EnemyCurrentHp, PropertyInfo? EnemyMaxHp,
        PropertyInfo? EnemyBlock, PropertyInfo? EnemyIsAlive, PropertyInfo? EnemyPowers,
        PropertyInfo? MonsterNextMove, PropertyInfo? MonsterId, PropertyInfo? MonsterIntendsToAttack,
        PropertyInfo? NextMoveIntents, PropertyInfo? IntentIntentType,
        MethodInfo? PlayerCmdEndTurn, Type? PlayCardActionType, ConstructorInfo? PlayCardActionCtor,
        PropertyInfo? RunManagerActionQueueSet, MethodInfo? ActionQueueSetEnqueueWithoutSynchronizing,
        PropertyInfo? RunManagerActionExecutor, PropertyInfo? ActionExecutorIsRunning,
        MethodInfo? ActionExecutorFinishedExecutingActions);
}
