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

public sealed partial class Sts2Bindings
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
    // Multiplayer-aware lookups (LocalContext.GetMe, RunHistory.GetPlayerStats,
    // CardReward.OnSelectWrapper) walk LocalContext.NetId to find the local
    // player. Without it they throw "Local player not found"; with it the
    // natural async chains in EndTurn / reward selection complete cleanly.
    // sts2-cli mirrors this at RunSimulator.cs:253-255.
    private readonly MemberInfo _localContextNetIdMember;
    private readonly InvocationPlan _runManagerSetUpTest;
    // Reset surface for run reuse. SetUpTest throws "State is already set."
    // when called twice, so a second run/new on the same host must tear down
    // the prior run first. Mirrors sts2-cli RunSimulator.CleanUp:3573.
    private readonly PropertyInfo _runManagerIsInProgress;
    private readonly MethodInfo _runManagerCleanUp;
    private readonly PropertyInfo _runStateExtraFields;
    private readonly PropertyInfo _extraFieldsStartedWithNeow;
    private readonly MethodInfo _runManagerGenerateRooms;
    private readonly MethodInfo _runManagerLaunch;
    private readonly MethodInfo _runManagerFinalizeStartingRelics;
    private readonly InvocationPlan _runManagerEnterAct;
    // Bridge for boss → next-act transition. After defeating an act boss
    // and draining rewards, the engine leaves the player in an empty
    // MapRoom (CurrentMapCoord no longer points at a real node) until
    // EnterNextAct() bumps CurrentActIndex, regenerates the map, and
    // re-enters at the new act's start. Returns a Task — same async
    // posture as EnterAct. Sts2-cli mirrors this at RunSimulator.cs:2221.
    private readonly MethodInfo _runManagerEnterNextAct;

    // ── Mutations ────────────────────────────────────────────────────────
    private readonly InvocationPlan _runManagerEnterMapCoord;
    private readonly Type _mapCoordType;

    // ── Read surface ────────────────────────────────────────────────────
    private readonly PropertyInfo _playerGold;
    private readonly PropertyInfo _playerCreature;
    private readonly PropertyInfo _playerDeck;
    // Player.Relics — the run-scoped relic bag (starter relic + everything
    // obtained mid-run). Soft-bound: a missing property surfaces an empty
    // list on snapshots rather than failing bootstrap. The relic element's
    // Id property is discovered at bind time so ReadRelics can avoid per-
    // entry GetProperty calls.
    private readonly PropertyInfo? _playerRelics;
    private readonly PropertyInfo? _relicId;
    // Potion bag: read PotionSlots (IReadOnlyList<PotionModel>, with nulls
    // for empty slots) to surface OwnedPotions; EnqueueManualUse + the
    // PotionModel.TargetType drive run/use_potion. Player.Creature (used
    // for self-target potions) is already bound non-nullably above.
    private readonly PropertyInfo? _playerPotionSlots;
    private readonly PropertyInfo? _potionTargetType;
    private readonly PropertyInfo? _potionPassesUsabilityCheck;
    private readonly MethodInfo? _potionEnqueueManualUse;
    // Player.NetId — anchor for LocalContext alignment. Multiplayer lookups
    // ride on the contract that LocalContext.NetId == Player.NetId for the
    // local player.
    private readonly PropertyInfo _playerNetId;
    private readonly PropertyInfo _creatureCurrentHp;
    private readonly PropertyInfo _creatureMaxHp;
    // Backing fields for CurrentHp / MaxHp. The properties are read-only on
    // the engine side (state mutation goes through damage / heal commands),
    // so debug/set_hp writes to the underlying fields directly — same
    // posture sts2-cli's SetPlayer takes. Soft-bound: a renamed backing
    // field disables debug/set_hp via WireException at call time rather
    // than failing host startup.
    private readonly FieldInfo? _creatureCurrentHpField;
    private readonly FieldInfo? _creatureMaxHpField;
    private readonly PropertyInfo _deckCards;
    private readonly PropertyInfo _runStateCurrentRoom;
    private readonly PropertyInfo _runStateActFloor;
    private readonly PropertyInfo _runStateCurrentActIndex;
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

    // ── Rest-site surface ────────────────────────────────────────────────
    // All soft-bound: if the engine moves the rest-site shape, the host still
    // boots and the wire just reports an empty AvailableRestSiteOptions on
    // every rest-site snapshot. SelectRestSiteOption throws against a null
    // binding so callers see the gap rather than a silent no-op.
    private readonly PropertyInfo? _runManagerRestSiteSynchronizer;
    private readonly MethodInfo? _restSiteSyncChooseLocalOption;
    private readonly PropertyInfo? _restSiteRoomOptions;
    private readonly PropertyInfo? _restSiteOptionOptionId;
    private readonly PropertyInfo? _restSiteOptionIsEnabled;

    // ── Merchant-room surface ────────────────────────────────────────────
    // Soft-bound: a missing piece degrades the merchant wire surface to
    // "empty inventory list" on snapshots and "throws at call time" on the
    // mutating paths (BuyMerchantItem, LeaveMerchantRoom) — the host still
    // boots. The flow we drive:
    //   MerchantRoom.Inventory (MerchantInventory)
    //     .AllEntries (IEnumerable<MerchantEntry>) — stable iteration order
    //       (CharacterCards → ColorlessCards → Relics → Potions → CardRemoval)
    //   Per entry: Cost, EnoughGold (= IsAffordable), IsStocked.
    //   Per kind: CardEntry.CreationResult.Card.Id, RelicEntry.Model.Id,
    //             PotionEntry.Model.Id, CardRemovalEntry (no id).
    //   Buy: MerchantEntry.OnTryPurchaseWrapper(inventory, ignoreCost=false).
    //   Leave: RunManager.EnterRoom(new MapRoom()) — same pattern rest-site
    //          and treasure use; the merchant has no auto-advance.
    private readonly Type? _merchantRoomType;
    private readonly PropertyInfo? _merchantRoomInventory;
    private readonly PropertyInfo? _merchantInventoryAllEntries;
    private readonly PropertyInfo? _merchantEntryCost;
    private readonly PropertyInfo? _merchantEntryEnoughGold;
    private readonly PropertyInfo? _merchantEntryIsStocked;
    private readonly MethodInfo? _merchantEntryOnTryPurchaseWrapper;
    private readonly Type? _merchantCardEntryType;
    private readonly PropertyInfo? _merchantCardEntryCreationResult;
    private readonly PropertyInfo? _cardCreationResultCard;
    private readonly PropertyInfo? _cardModelId;
    private readonly Type? _merchantRelicEntryType;
    private readonly PropertyInfo? _merchantRelicEntryModel;
    private readonly PropertyInfo? _relicModelId;
    private readonly Type? _merchantPotionEntryType;
    private readonly PropertyInfo? _merchantPotionEntryModel;
    private readonly PropertyInfo? _potionModelId;
    private readonly Type? _merchantCardRemovalEntryType;

    // ── Treasure-room surface ────────────────────────────────────────────
    // Soft-bound: each piece's absence degrades LeaveTreasureRoom to a
    // typed runtime error rather than failing host bootstrap. The flow is:
    //   1. TreasureRoom.DoNormalRewards() (Task<int>) populates
    //      RunManager.TreasureRoomRelicSynchronizer.CurrentRelics with the
    //      chest's offering — typically one relic.
    //   2. The synchronizer is the grant channel: PickRelicLocally(index)
    //      claims a relic into Player.Relics. CompleteWithNoRelics() is the
    //      skip path (used by future SilverCrucible-style "empty chest"
    //      modifiers).
    //   3. TreasureRoom.DoExtraRewardsIfNeeded() covers act-3 / ascension
    //      extras (typically a no-op for Act 1).
    //   4. ForceToMap exits via EnterRoom(MapRoom).
    private readonly Type? _treasureRoomType;
    private readonly MethodInfo? _treasureRoomDoNormalRewards;
    private readonly MethodInfo? _treasureRoomDoExtraRewards;
    private readonly PropertyInfo? _runManagerTreasureRoomRelicSync;
    private readonly PropertyInfo? _treasureSyncCurrentRelics;
    private readonly MethodInfo? _treasureSyncCompleteWithNoRelics;

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
    private readonly MethodInfo? _combatManagerCheckWinCondition;
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
    // AttackIntent surface: damage is computed at read-time via the
    // engine's modifier-aware DamageCalc func; repeats is the hit count.
    // Both come from sts2's AttackIntent base — SingleAttackIntent and
    // MultiAttackIntent (and DeathBlowIntent) inherit the props.
    private readonly Type? _attackIntentType;
    private readonly PropertyInfo? _attackIntentDamageCalc;
    private readonly PropertyInfo? _attackIntentRepeats;
    private readonly MethodInfo? _playerCmdEndTurn;
    private readonly ConstructorInfo? _playCardActionCtor;
    private readonly PropertyInfo? _runManagerActionQueueSet;
    private readonly MethodInfo? _actionQueueSetEnqueueWithoutSynchronizing;
    private readonly PropertyInfo? _runManagerActionExecutor;
    private readonly PropertyInfo? _actionExecutorIsRunning;
    private readonly MethodInfo? _actionExecutorFinishedExecutingActions;

    // ── Reward surface ──────────────────────────────────────────────────
    // All soft-bound: if a reward type or member doesn't resolve, the host
    // still boots — the wire just surfaces an empty/Unknown reward. The
    // mutating paths throw if their handles are null so callers see the gap.
    private readonly ConstructorInfo? _rewardsSetCtor;
    private readonly MethodInfo? _rewardsSetWithRewardsFromRoom;
    private readonly MethodInfo? _rewardsSetGenerateWithoutOffering;
    private readonly MethodInfo? _rewardOnSelectWrapper;
    private readonly Type? _cardRewardType;
    private readonly PropertyInfo? _cardRewardCards;
    private readonly PropertyInfo? _cardRewardCanSkip;
    private readonly MethodInfo? _cardRewardOnSkipped;
    private readonly Type? _goldRewardType;
    private readonly PropertyInfo? _goldRewardAmount;
    private readonly Type? _potionRewardType;
    private readonly PropertyInfo? _potionRewardPotionId;
    private readonly Type? _relicRewardType;
    private readonly PropertyInfo? _relicRewardRelicId;
    private readonly PropertyInfo? _runManagerRewardSynchronizer;
    private readonly MethodInfo? _rewardSyncSyncLocalObtainedCard;
    // CardPileCmd.Add(card, PileType.Deck) — engine path for adding a chosen
    // card-reward card to the deck. Routes through the listener pipeline so
    // on-card-obtain relics (LuckyFysh, Ceramic Fish, …) fire; a direct
    // deck.Add would skip the hook. RelicListenerTests pins this behaviour.
    private readonly MethodInfo? _cardPileCmdAdd;
    private readonly object? _pileTypeDeckValue;

    // RelicCmd.Obtain(model, player) + ModelDb.GetById<RelicModel>(ModelId)
    // — engine path for granting a relic mid-run. Used only by the
    // debug/give_relic test affordance; soft-bound so the host still boots
    // without it.
    private readonly MethodInfo? _relicCmdObtain;
    private readonly MethodInfo? _modelDbGetByIdRelic;
    private readonly ConstructorInfo? _modelIdCtor;
    private readonly MethodInfo? _relicModelToMutable;

    // Mutable post-combat reward state. Generated lazily once combat ends
    // (see TryGeneratePendingRewards); consumed by SelectReward / SkipReward.
    // Single-slot to match the single-active-run host model. Cleared on
    // run/new and once the last reward is claimed.
    private List<object>? _pendingRewards;

    // Optional handle to the inline sync context installed by RuntimeBootstrap.
    // EndTurn pumps it to drive sts2's fire-and-forget enemy-turn chain through
    // completion. If absent (older boot path that doesn't capture it), we fall
    // back to manually triggering SwitchFromPlayerToEnemySide.
    private readonly InlineSynchronizationContext? _syncCtx;

    private Sts2Bindings(Assembly sts2, BindingState s, InlineSynchronizationContext? syncCtx)
    {
        Sts2 = sts2;
        _syncCtx = syncCtx;
        _playerType = s.PlayerType;
        _createIroncladRun = s.CreateIroncladRun;
        _unlockStateAll = s.UnlockStateAll;
        _runStateCreateForTest = s.RunStateCreateForTest;
        _runManagerInstance = s.RunManagerInstance;
        _netServiceType = s.NetServiceType;
        _localContextNetIdMember = s.LocalContextNetIdMember;
        _runManagerSetUpTest = s.RunManagerSetUpTest;
        _runManagerIsInProgress = s.RunManagerIsInProgress;
        _runManagerCleanUp = s.RunManagerCleanUp;
        _runStateExtraFields = s.RunStateExtraFields;
        _extraFieldsStartedWithNeow = s.ExtraFieldsStartedWithNeow;
        _runManagerGenerateRooms = s.RunManagerGenerateRooms;
        _runManagerLaunch = s.RunManagerLaunch;
        _runManagerFinalizeStartingRelics = s.RunManagerFinalizeStartingRelics;
        _runManagerEnterAct = s.RunManagerEnterAct;
        _runManagerEnterNextAct = s.RunManagerEnterNextAct;
        _runManagerEnterMapCoord = s.RunManagerEnterMapCoord;
        _mapCoordType = s.MapCoordType;
        _playerGold = s.PlayerGold;
        _playerCreature = s.PlayerCreature;
        _playerDeck = s.PlayerDeck;
        _playerRelics = s.PlayerRelics;
        _relicId = s.RelicId;
        _playerPotionSlots = s.PlayerPotionSlots;
        _potionTargetType = s.PotionTargetType;
        _potionPassesUsabilityCheck = s.PotionPassesUsabilityCheck;
        _potionEnqueueManualUse = s.PotionEnqueueManualUse;
        _playerNetId = s.PlayerNetId;
        _creatureCurrentHp = s.CreatureCurrentHp;
        _creatureMaxHp = s.CreatureMaxHp;
        _creatureCurrentHpField = s.CreatureCurrentHpField;
        _creatureMaxHpField = s.CreatureMaxHpField;
        _deckCards = s.DeckCards;
        _runStateCurrentRoom = s.RunStateCurrentRoom;
        _runStateActFloor = s.RunStateActFloor;
        _runStateCurrentActIndex = s.RunStateCurrentActIndex;
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
        _runManagerRestSiteSynchronizer = s.RunManagerRestSiteSynchronizer;
        _restSiteSyncChooseLocalOption = s.RestSiteSyncChooseLocalOption;
        _restSiteRoomOptions = s.RestSiteRoomOptions;
        _restSiteOptionOptionId = s.RestSiteOptionOptionId;
        _restSiteOptionIsEnabled = s.RestSiteOptionIsEnabled;
        _treasureRoomType = s.TreasureRoomType;
        _treasureRoomDoNormalRewards = s.TreasureRoomDoNormalRewards;
        _treasureRoomDoExtraRewards = s.TreasureRoomDoExtraRewards;
        _runManagerTreasureRoomRelicSync = s.RunManagerTreasureRoomRelicSync;
        _treasureSyncCurrentRelics = s.TreasureSyncCurrentRelics;
        _treasureSyncCompleteWithNoRelics = s.TreasureSyncCompleteWithNoRelics;
        _merchantRoomType = s.MerchantRoomType;
        _merchantRoomInventory = s.MerchantRoomInventory;
        _merchantInventoryAllEntries = s.MerchantInventoryAllEntries;
        _merchantEntryCost = s.MerchantEntryCost;
        _merchantEntryEnoughGold = s.MerchantEntryEnoughGold;
        _merchantEntryIsStocked = s.MerchantEntryIsStocked;
        _merchantEntryOnTryPurchaseWrapper = s.MerchantEntryOnTryPurchaseWrapper;
        _merchantCardEntryType = s.MerchantCardEntryType;
        _merchantCardEntryCreationResult = s.MerchantCardEntryCreationResult;
        _cardCreationResultCard = s.CardCreationResultCard;
        _cardModelId = s.CardModelId;
        _merchantRelicEntryType = s.MerchantRelicEntryType;
        _merchantRelicEntryModel = s.MerchantRelicEntryModel;
        _relicModelId = s.RelicModelId;
        _merchantPotionEntryType = s.MerchantPotionEntryType;
        _merchantPotionEntryModel = s.MerchantPotionEntryModel;
        _potionModelId = s.PotionModelId;
        _merchantCardRemovalEntryType = s.MerchantCardRemovalEntryType;
        var c = s.Combat;
        _combatManagerInstance = c.CombatManagerInstance;
        _combatManagerIsInProgress = c.CombatManagerIsInProgress;
        _combatManagerIsPlayPhase = c.CombatManagerIsPlayPhase;
        _combatManagerDebugOnlyGetState = c.CombatManagerDebugOnlyGetState;
        _combatManagerCheckWinCondition = c.CombatManagerCheckWinCondition;
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
        _attackIntentType = c.AttackIntentType;
        _attackIntentDamageCalc = c.AttackIntentDamageCalc;
        _attackIntentRepeats = c.AttackIntentRepeats;
        _playerCmdEndTurn = c.PlayerCmdEndTurn;
        _playCardActionCtor = c.PlayCardActionCtor;
        _runManagerActionQueueSet = c.RunManagerActionQueueSet;
        _actionQueueSetEnqueueWithoutSynchronizing = c.ActionQueueSetEnqueueWithoutSynchronizing;
        _runManagerActionExecutor = c.RunManagerActionExecutor;
        _actionExecutorIsRunning = c.ActionExecutorIsRunning;
        _actionExecutorFinishedExecutingActions = c.ActionExecutorFinishedExecutingActions;
        var r = s.Rewards;
        _rewardsSetCtor = r.RewardsSetCtor;
        _rewardsSetWithRewardsFromRoom = r.RewardsSetWithRewardsFromRoom;
        _rewardsSetGenerateWithoutOffering = r.RewardsSetGenerateWithoutOffering;
        _rewardOnSelectWrapper = r.RewardOnSelectWrapper;
        _cardRewardType = r.CardRewardType;
        _cardRewardCards = r.CardRewardCards;
        _cardRewardCanSkip = r.CardRewardCanSkip;
        _cardRewardOnSkipped = r.CardRewardOnSkipped;
        _goldRewardType = r.GoldRewardType;
        _goldRewardAmount = r.GoldRewardAmount;
        _potionRewardType = r.PotionRewardType;
        _potionRewardPotionId = r.PotionRewardPotionId;
        _relicRewardType = r.RelicRewardType;
        _relicRewardRelicId = r.RelicRewardRelicId;
        _runManagerRewardSynchronizer = r.RunManagerRewardSynchronizer;
        _rewardSyncSyncLocalObtainedCard = r.RewardSyncSyncLocalObtainedCard;
        _cardPileCmdAdd = r.CardPileCmdAdd;
        _pileTypeDeckValue = r.PileTypeDeckValue;
        _relicCmdObtain = r.RelicCmdObtain;
        _modelDbGetByIdRelic = r.ModelDbGetByIdRelic;
        _modelIdCtor = r.ModelIdCtor;
        _relicModelToMutable = r.RelicModelToMutable;
    }

    // Full sts2-cli StartRun chain, condensed. Returns a triple the wire
    // layer can pass back in for subsequent calls. `withNeow` opts into the
    // Neow blessing event: lands CurrentRoom at EventRoom (the Neow node)
    // instead of MapRoom. Callers can then drive run/select_event_option
    // to dismiss the event; LocPatches + the Texture2D / StringName stubs
    // are what let the event populate options in the first place.
    public RunHandle StartIroncladRun(ulong seed, bool withNeow = false)
    {
        // A new run cannot inherit pending rewards from a previous one — the
        // reward-set objects belong to the prior RunManager state and become
        // invalid after the second run/new wipes that state.
        _pendingRewards = null;

        // Reset RunManager if a previous run is still installed; SetUpTest
        // throws "State is already set." otherwise. sts2-cli does the same
        // thing at RunSimulator.CleanUp:3573.
        var existingManager = _runManagerInstance.GetValue(null);
        if (existingManager is not null
            && _runManagerIsInProgress.GetValue(existingManager) is true)
        {
            _runManagerCleanUp.Invoke(existingManager, new object?[] { /* graceful: */ true });
        }

        // Player.CreateForNewRun's second ulong is the player's NetId, not the
        // run seed (the seed lives on RunState — see CreateForTest below).
        // We pass 1uL — sts2-cli's "everything is player 1" contract.
        // NetSingleplayerGameService.NetId is a baked 1uL (read-only) and
        // keys the engine's multiplayer-aware paths (ActionQueueSet, Reward-
        // Synchronizer, RunHistory). With Player.NetId = LocalContext.NetId
        // = netService.NetId = 1uL, the natural enemy-turn / reward chains
        // run end-to-end without our manual fallbacks intervening.
        // (probe-natural-chain proved this; see natural-chain-gaps.md.)
        const ulong playerNetId = 1uL;
        var player = _createIroncladRun.Invoke(null, new object?[] { _unlockStateAll, playerNetId })
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

        // Mirror sts2-cli's RunSimulator.cs:255: align LocalContext.NetId to
        // the local player's NetId. With Player.NetId = NetSingleplayerGame-
        // Service.NetId = 1uL above, the engine's multiplayer-aware lookups
        // (LocalContext.GetMe, RunHistory.GetPlayerStats, CardReward.OnSelect-
        // Wrapper) all resolve to this single player.
        var resolvedNetId = _playerNetId.GetValue(player)
            ?? throw new InvalidOperationException("Player.NetId returned null");
        WriteLocalContextNetId(resolvedNetId);

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

        // sts2 has no dedicated BossRoom type — the act boss is a normal
        // CombatRoom whose monster happens to be the act boss. We surface
        // BossRoom anyway by checking whether the player's current map coord
        // points at a Boss-kind MapPoint; without this flip, callers using
        // `currentRoomType == BossRoom` as a stop signal never trigger.
        if (roomType == RoomType.CombatRoom && IsCurrentMapPointBoss(handle.RunState))
            roomType = RoomType.BossRoom;
        var actFloor = (int)_runStateActFloor.GetValue(handle.RunState)!;
        var currentActIndex = (int)_runStateCurrentActIndex.GetValue(handle.RunState)!;
        var engineIsGameOver = (bool)_runStateIsGameOver.GetValue(handle.RunState)!;
        // Disambiguate victory vs death. The engine's IsGameOver flag flips
        // for both, so we split it the same way sts2-cli does
        // (RunSimulator.cs:1682 + 1801): Creature.IsDead is death; IsGameOver
        // without death is victory. The combined IsGameOver field is kept on
        // the snapshot so callers wanting "stop on any termination" don't
        // need to OR both flags.
        var isDead = creature is not null && _creatureIsDead is not null
            && Convert.ToBoolean(_creatureIsDead.GetValue(creature));
        var isVictory = engineIsGameOver && !isDead;
        var isGameOver = isVictory || isDead;

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

        // Same room-scoped gating for rest-site options. Outside a
        // RestSiteRoom the engine's options list is either null or stale
        // from a previous rest; surface only when we're actually there.
        var availableRestSiteOptions = roomType == RoomType.RestSiteRoom
            ? ReadAvailableRestSiteOptions(handle.RunState)
            : Array.Empty<RestSiteOption>();

        // Same room-scoped gating for merchant items: only surface when
        // standing in a merchant room. The MerchantInventory persists on
        // the room instance after a buy (so re-reads pick up the now-
        // unstocked entry), but it's still gated by RoomType to keep the
        // wire honest — between rooms there's no inventory.
        var availableMerchantItems = roomType == RoomType.MerchantRoom
            ? ReadAvailableMerchantItems(handle.RunState)
            : Array.Empty<MerchantItem>();

        // Same gating discipline for combat: only read when sts2 has a live
        // combat (room == CombatRoom). Outside, CombatManager.Instance may be
        // null or carry stale state and PlayerCombatState is undefined.
        // BossRoom is the wire-level synthetic for CombatRoom-on-a-Boss-point
        // (see the flip above); the engine's actual room is still CombatRoom
        // with a live combat, so we must read combat state for it too — without
        // this branch, callers entering the act-boss fight see combatState=null
        // and any combat agent crashes on first read.
        var combatState = (roomType == RoomType.CombatRoom || roomType == RoomType.BossRoom)
            ? ReadCombatState(handle)
            : null;

        // Rewards persist across the room flip — the engine generates them
        // while still in CombatRoom (combat ended, isInProgress=false) and we
        // hold them through the wire turn(s) until the caller consumes them.
        // Surface whenever non-empty regardless of room.
        var rewardsState = ReadRewardsState();

        // Relics are run-scoped, not room-scoped: surface on every snapshot.
        var relics = ReadRelics(handle);
        // Potion belt is run-scoped too; empty slots are filtered out so
        // callers index against a dense list.
        var ownedPotions = ReadOwnedPotions(handle);

        return new RunSnapshot(currentHp, maxHp, gold, deckSize, roomType, actFloor, currentActIndex, isGameOver, isVictory, isDead, availableNodes, availableEventOptions, availableRestSiteOptions, availableMerchantItems, combatState, rewardsState, relics, ownedPotions);
    }

    // Project the stashed list of pending rewards into the wire DTO. Returns
    // null when nothing is pending — the convention RewardsState carries
    // (no decision required). Indexes are reassigned 0..N-1 every call so a
    // fresh snapshot is always self-consistent for the next select_reward.
    private RewardsState? ReadRewardsState()
    {
        if (_pendingRewards is null || _pendingRewards.Count == 0) return null;

        var options = new List<RewardOption>(_pendingRewards.Count);
        for (var i = 0; i < _pendingRewards.Count; i++)
        {
            var reward = _pendingRewards[i];
            var (kind, canSkip, gold, potion, relic, cards) = ProjectReward(reward);
            options.Add(new RewardOption(i, kind, canSkip, gold, potion, relic, cards));
        }
        return new RewardsState(options);
    }

    private (RewardKind, bool, int?, string?, string?, IReadOnlyList<CardRewardOption>?) ProjectReward(object reward)
    {
        if (_cardRewardType is not null && _cardRewardType.IsInstanceOfType(reward))
        {
            var canSkip = _cardRewardCanSkip is not null
                && (bool)(_cardRewardCanSkip.GetValue(reward) ?? false);
            var cards = ReadCardRewardCards(reward);
            return (RewardKind.Card, canSkip, null, null, null, cards);
        }
        if (_goldRewardType is not null && _goldRewardType.IsInstanceOfType(reward))
        {
            var amount = _goldRewardAmount is not null
                ? Convert.ToInt32(_goldRewardAmount.GetValue(reward) ?? 0)
                : 0;
            return (RewardKind.Gold, false, amount, null, null, null);
        }
        if (_potionRewardType is not null && _potionRewardType.IsInstanceOfType(reward))
        {
            var id = _potionRewardPotionId is not null
                ? ReadEntryId(_potionRewardPotionId, reward) ?? _potionRewardPotionId.GetValue(reward)?.ToString()
                : null;
            return (RewardKind.Potion, false, null, id, null, null);
        }
        if (_relicRewardType is not null && _relicRewardType.IsInstanceOfType(reward))
        {
            var id = _relicRewardRelicId is not null
                ? ReadEntryId(_relicRewardRelicId, reward) ?? _relicRewardRelicId.GetValue(reward)?.ToString()
                : null;
            return (RewardKind.Relic, false, null, null, id, null);
        }
        return (RewardKind.Unknown, false, null, null, null, null);
    }

    private IReadOnlyList<CardRewardOption> ReadCardRewardCards(object cardReward)
    {
        if (_cardRewardCards?.GetValue(cardReward) is not System.Collections.IEnumerable raw)
            return Array.Empty<CardRewardOption>();
        var result = new List<CardRewardOption>();
        var idx = 0;
        foreach (var card in raw)
        {
            if (card is null) continue;
            var idWire = ReadEntryId(_cardId, card) ?? card.GetType().Name;
            var cost = _cardEnergyCost is not null && _energyCostGetResolved is not null
                ? Convert.ToInt32(_energyCostGetResolved.Invoke(_cardEnergyCost.GetValue(card), null) ?? 0)
                : 0;
            result.Add(new CardRewardOption(idx++, CardIdNames.FromWire(idWire), cost));
        }
        return result;
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
            var idWire = ReadEntryId(_cardId, card) ?? card.GetType().Name;
            var cost = _cardEnergyCost is not null && _energyCostGetResolved is not null
                ? Convert.ToInt32(_energyCostGetResolved.Invoke(_cardEnergyCost.GetValue(card), null) ?? 0)
                : 0;
            var canPlay = _cardCanPlay is not null && (bool)(_cardCanPlay.Invoke(card, null) ?? false);
            var targetType = ParseEnum<TargetType>(_cardTargetType?.GetValue(card));
            result.Add(new Card(i, CardIdNames.FromWire(idWire), cost, canPlay, targetType));
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
            // AttackIntent (and subclasses SingleAttackIntent /
            // MultiAttackIntent / DeathBlowIntent) carries a `DamageCalc:
            // Func<int>` that produces the engine-side modifier-aware
            // damage value (monster strength, player vulnerable etc.
            // baked in). Repeats is the hit count. For non-attack intents
            // these props don't exist — fall through with null.
            int? damage = null;
            int? hits = null;
            if (_attackIntentType is not null && _attackIntentType.IsInstanceOfType(intent))
            {
                if (_attackIntentDamageCalc?.GetValue(intent) is Delegate calc)
                {
                    // sts2's DamageCalc returns Decimal (engine uses Decimal
                    // for damage so percentage modifiers like Vulnerable's
                    // +50% stay exact). Convert.ToInt32 rounds half-to-even,
                    // matching sts2's own display path.
                    try { damage = Convert.ToInt32(calc.DynamicInvoke()); }
                    catch { damage = null; } // tolerate engine-side calc failures rather than aborting the snapshot
                }
                if (_attackIntentRepeats?.GetValue(intent) is int r) hits = r;
            }
            // DefendIntent's block value isn't surfaced on the engine
            // type's public props (only IntentType). Leaving null is the
            // honest read; agents that want defend amounts will need
            // either a separate binding or a parsed-tip estimator.
            result.Add(new Intent(kind, Damage: damage, Hits: hits, Block: null));
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

    // Walk Player.Relics into the wire shape. Mirrors ReadPowers's discipline:
    // a missing element falls back to the runtime class name so a relic with
    // no readable Id still surfaces (with a useful identifier in tests). The
    // walk is tolerant of a null collection — relics are run-scoped state and
    // the snapshot path runs even before the engine has populated Player.
    private IReadOnlyList<Relic> ReadRelics(RunHandle handle)
    {
        if (_playerRelics is null) return Array.Empty<Relic>();
        var relicsObj = _playerRelics.GetValue(handle.Player);
        if (relicsObj is not System.Collections.IEnumerable enumerable) return Array.Empty<Relic>();

        var result = new List<Relic>();
        foreach (var relic in enumerable)
        {
            if (relic is null) continue;
            var id = ReadEntryId(_relicId, relic) ?? relic.GetType().Name;
            result.Add(new Relic(id));
        }
        return result;
    }

    // Read Player.PotionSlots into the wire DTO. Empty slots (null entries
    // in the engine list) are skipped — the wire indices are dense, so a
    // caller's `potionIndex` always lands on a real potion. The potion id
    // we surface is the model's canonical wire form via Id.Entry (e.g.
    // "BLOCK_POTION"), matching cards/relics/merchant entries. PotionModel
    // is AbstractModel-derived so its Id property is inherited; we look it
    // up reflectively per type (cached implicitly by the runtime — there
    // are ~10 distinct PotionModel subclasses per run, all in cs.Hand-style
    // hot paths).
    private IReadOnlyList<OwnedPotion> ReadOwnedPotions(RunHandle handle)
    {
        if (_playerPotionSlots is null) return Array.Empty<OwnedPotion>();
        var slotsObj = _playerPotionSlots.GetValue(handle.Player);
        if (slotsObj is not System.Collections.IEnumerable slots) return Array.Empty<OwnedPotion>();

        var result = new List<OwnedPotion>();
        var idx = 0;
        foreach (var potion in slots)
        {
            if (potion is null) { idx++; continue; }
            var idProp = potion.GetType().GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
            var id = (idProp is not null ? ReadEntryId(idProp, potion) : null)
                     ?? potion.GetType().Name;  // last-ditch: GetType().Name as before
            var target = _potionTargetType?.GetValue(potion) is { } tt
                ? ParseEnum<TargetType>(tt)
                : TargetType.Unknown;
            var canUse = _potionPassesUsabilityCheck?.GetValue(potion) is bool b ? b : true;
            result.Add(new OwnedPotion(idx, id, target, canUse));
            idx++;
        }
        return result;
    }

    // Use a potion via the engine's manual-use path. Mirrors play_card's
    // shape: potionIndex is the wire index into ReadOwnedPotions (which
    // skips empty slots — *not* the underlying PotionSlots index), and
    // targetIndex is required when the potion's TargetType is AnyEnemy.
    // For self / non-targeted potions, the player's own Creature is
    // passed as the target (the engine ignores it for those usages).
    public void UsePotion(RunHandle handle, int potionIndex, int? targetIndex)
    {
        if (_playerPotionSlots is null || _potionEnqueueManualUse is null || _potionTargetType is null)
            throw new InvalidOperationException(
                "Sts2Bindings: potion surface not bound — Player.PotionSlots / EnqueueManualUse missing on this dll");

        var slotsObj = _playerPotionSlots.GetValue(handle.Player)
            ?? throw new InvalidOperationException("Sts2Bindings: Player.PotionSlots returned null");
        if (slotsObj is not System.Collections.IEnumerable slots)
            throw new InvalidOperationException("Sts2Bindings: Player.PotionSlots is not enumerable");

        object? potion = null;
        var idx = 0;
        foreach (var p in slots)
        {
            if (p is null) { idx++; continue; }
            if (idx == potionIndex) { potion = p; break; }
            idx++;
        }
        if (potion is null)
            throw new ArgumentOutOfRangeException(nameof(potionIndex),
                $"no potion at wire index {potionIndex} (bag is dense after skipping empty slots)");

        // Pick the target Creature. AnyEnemy → indexed enemy (using the
        // same resolver play_card uses, so the targetIndex semantics are
        // identical). Self / other → the player's own creature (engine
        // ignores the target field for those usages).
        var target = ParseEnum<TargetType>(_potionTargetType.GetValue(potion));
        object targetCreature;
        if (target == TargetType.AnyEnemy)
        {
            targetCreature = ResolveAnyEnemyTarget(targetIndex)
                ?? throw new InvalidOperationException(
                    targetIndex is null
                        ? "potion targets AnyEnemy but no targetIndex was supplied"
                        : $"targetIndex {targetIndex} is not a live enemy");
        }
        else
        {
            targetCreature = _playerCreature.GetValue(handle.Player)
                ?? throw new InvalidOperationException("Player.Creature is null");
        }

        _potionEnqueueManualUse.Invoke(potion, new[] { targetCreature });
        DrainActionExecutor(handle);
        AutoAdvancePostCombat(handle);
    }

    // Read the .Entry string off an Id-shaped object (sts2 wraps stable ids in
    // a struct/record with a public .Entry member). Returns null when the
    // owner is null or .Entry can't be located — caller substitutes a fallback.
    //
    // sts2 surfaces three shapes through "id"-looking properties:
    //   1. Strongly-typed: CardId, RelicId, MonsterId, PowerId — structs
    //      whose Entry is a PROPERTY. Bound up-front via _idEntry.
    //   2. Generic ModelId: a class with Entry as a property (registry key
    //      shape; used by merchant entries).
    //   3. AbstractModel reference: PotionReward.Potion returns a PotionModel
    //      directly (not a ModelId). The model carries .Id which is the
    //      strongly-typed shape from #1.
    //
    // We try (1) fast-path, then look for Entry directly on the value
    // (handles #2), then unwrap a .Id step (handles #3). No compile-time
    // sts2 type names per AD-4 — all reflection.
    private string? ReadEntryId(PropertyInfo? idProp, object owner)
    {
        if (idProp is null) return null;
        var idValue = idProp.GetValue(owner);
        if (idValue is null) return null;
        if (_idEntry is not null && idProp.PropertyType == _idEntry.DeclaringType)
        {
            return _idEntry.GetValue(idValue) as string;
        }
        // Direct: value itself exposes .Entry.
        if (TryReadEntryMember(idValue) is string direct) return direct;
        // Indirect: value is a model whose .Id is the strongly-typed Id
        // (PotionModel.Id, MonsterModel.Id, RelicModel.Id — the AbstractModel
        // contract). Follow one step and re-check.
        var nestedIdProp = idValue.GetType().GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
        if (nestedIdProp?.GetValue(idValue) is object nested && TryReadEntryMember(nested) is string indirect)
            return indirect;
        return null;
    }

    // Locate an "Entry" string on @value, trying property then field. Used
    // by ReadEntryId for both the direct-value and unwrap-one-step paths.
    private static string? TryReadEntryMember(object value)
    {
        var t = value.GetType();
        var entryProp = t.GetProperty("Entry", BindingFlags.Public | BindingFlags.Instance);
        if (entryProp?.GetValue(value) is string sProp) return sProp;
        var entryField = t.GetField("Entry", BindingFlags.Public | BindingFlags.Instance);
        if (entryField?.GetValue(value) is string sField) return sField;
        return null;
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

    // True when the engine considers the current event resolved but the
    // room hasn't transitioned back to MapRoom yet. The wire surface
    // signals this state by reporting CurrentRoomType=EventRoom with an
    // empty AvailableEventOptions list; callers can confirm via this
    // helper (used by ProceedEvent's caller-guard).
    private bool IsLocalEventFinished(object runManager)
    {
        var sync = _runManagerEventSynchronizer.GetValue(runManager);
        if (sync is null) return false;
        var localEvent = _eventSyncGetLocalEvent.Invoke(sync, null);
        if (localEvent is null) return false;
        return (bool)_eventIsFinished.GetValue(localEvent)!;
    }

    // Walk RunState.CurrentRoom (cast to RestSiteRoom) → Options and shape
    // each option into the wire record. Mirrors sts2-cli's RestSiteState
    // reduced to the data fields we surface.
    //
    // Returns [] when the binding never resolved, the current room isn't a
    // RestSiteRoom (defence in depth — callers gate by RoomType already), or
    // the engine cleared Options after a pick. An empty list while standing
    // on a RestSiteRoom is the engine's "decision already made, advance to
    // map" signal — handled by sts2-cli via ForceToMap; we leave the wire
    // honest about the state and let the next caller drive the transition.
    private IReadOnlyList<RestSiteOption> ReadAvailableRestSiteOptions(object runState)
    {
        if (_restSiteRoomOptions is null) return Array.Empty<RestSiteOption>();
        var room = _runStateCurrentRoom.GetValue(runState);
        if (room is null) return Array.Empty<RestSiteOption>();
        if (_restSiteRoomOptions.DeclaringType is null
            || !_restSiteRoomOptions.DeclaringType.IsInstanceOfType(room))
        {
            return Array.Empty<RestSiteOption>();
        }
        if (_restSiteRoomOptions.GetValue(room) is not System.Collections.IList options
            || options.Count == 0)
        {
            return Array.Empty<RestSiteOption>();
        }
        var result = new List<RestSiteOption>(options.Count);
        for (var i = 0; i < options.Count; i++)
        {
            var opt = options[i];
            if (opt is null) continue;
            var optionId = _restSiteOptionOptionId?.GetValue(opt) as string ?? opt.GetType().Name;
            var isEnabled = _restSiteOptionIsEnabled is not null
                && (bool)(_restSiteOptionIsEnabled.GetValue(opt) ?? false);
            result.Add(new RestSiteOption(i, optionId, isEnabled));
        }
        return result;
    }

    // Fire RunManager.RestSiteSynchronizer.ChooseLocalOption(optionIndex)
    // and pump the sync context so synchronous follow-ups complete inside
    // the call. SMITH branches into card-selection which we have no wire
    // for yet — that call hangs on the engine's GetSelectedCardReward
    // future and the caller will see CurrentRoomType stuck at RestSiteRoom
    // on the next snapshot.
    //
    // After non-SMITH picks (HEAL, future DIG, …), sts2 clears the room's
    // Options list but doesn't auto-transition to MapRoom — sts2-cli's
    // ForceToMap pattern covers this. Mirror it: if Options is empty after
    // the synchronous call, drive the engine through
    // ProceedFromTerminalRewardsScreen → EnterRoom(MapRoom) so the next
    // wire snapshot reports MapRoom, the post-rest contract callers rely on.
    public void SelectRestSiteOption(RunHandle handle, int optionIndex)
    {
        if (_runManagerRestSiteSynchronizer is null || _restSiteSyncChooseLocalOption is null)
        {
            throw new InvalidOperationException(
                "rest-site binding not resolved at boot — RestSiteSynchronizer.ChooseLocalOption is missing. " +
                "Either the engine moved the type or the bootstrap walk did not surface it.");
        }
        var sync = _runManagerRestSiteSynchronizer.GetValue(handle.RunManager)
            ?? throw new InvalidOperationException("RunManager.RestSiteSynchronizer was null");
        var result = _restSiteSyncChooseLocalOption.Invoke(sync, new object?[] { optionIndex });
        if (result is Task t) t.GetAwaiter().GetResult();
        _syncCtx?.Pump();

        // Auto-advance after HEAL / DIG / etc. — when Options is empty the
        // engine has accepted the pick but left CurrentRoom on RestSite.
        // SMITH leaves the room pending a card-select; we leave that alone
        // so a future card-select wire can resume it.
        if (_restSiteRoomOptions is not null && _runManagerEnterRoom is not null && _mapRoomType is not null)
        {
            var room = _runStateCurrentRoom.GetValue(handle.RunState);
            if (room is not null
                && _restSiteRoomOptions.DeclaringType is not null
                && _restSiteRoomOptions.DeclaringType.IsInstanceOfType(room))
            {
                var options = _restSiteRoomOptions.GetValue(room) as System.Collections.IList;
                if (options is null || options.Count == 0)
                {
                    try
                    {
                        var mapRoom = Activator.CreateInstance(_mapRoomType)
                            ?? throw new InvalidOperationException($"{_mapRoomType.FullName} default ctor returned null");
                        var enter = _runManagerEnterRoom.Invoke(handle.RunManager, new[] { mapRoom });
                        if (enter is Task et) et.GetAwaiter().GetResult();
                        _syncCtx?.Pump();
                    }
                    catch { /* sts2-cli also swallows — caller will see RestSiteRoom remains and can recover */ }
                }
            }
        }
    }

    // Open the chest in the current treasure room and exit to MapRoom.
    // The engine flow we drive (discovered by reflection probe):
    //   1. TreasureRoom.DoNormalRewards() (Task<int>) populates
    //      RunManager.TreasureRoomRelicSynchronizer.CurrentRelics with the
    //      chest's offering — typically one chest-tier relic.
    //   2. We can't drive the synchronizer's TCS-based picking flow in
    //      headless — the engine awaits an animation-completion signal
    //      raised by the UI screen normally. Instead we read the first
    //      relic from CurrentRelics and grant it via RelicCmd.Obtain (the
    //      same engine path RelicReward.OnSelectWrapper uses, so
    //      Player.Relics + on-obtain listener hooks stay aligned), then
    //      call SyncCompleteWithNoRelics to release the synchronizer
    //      session. Skipping the release would trip the next treasure
    //      room's "session already occurring" guard (sts2-cli's BUG-013).
    //   3. TreasureRoom.DoExtraRewardsIfNeeded() covers act-3 / ascension
    //      extras (typically a no-op for Act 1).
    //   4. EnterRoom(MapRoom) flips the room — the engine does not flip
    //      it on its own after the chest is empty.
    //
    // Empty chests (CurrentRelics.Count == 0) close out via
    // CompleteWithNoRelics so the synchronizer state doesn't linger and
    // a future treasure room can BeginRelicPicking cleanly.
    //
    // No params on the wire because there's no real player decision — a
    // future slice can split this into a previewable pick/skip if a
    // SilverCrucible-style "first chest is empty" relic ever ships.
    public void LeaveTreasureRoom(RunHandle handle)
    {
        if (_treasureRoomType is null)
        {
            throw new InvalidOperationException(
                "treasure-room binding not resolved at boot — TreasureRoom type missing. " +
                "Either the engine renamed it or the bootstrap walk did not surface it.");
        }
        var room = _runStateCurrentRoom.GetValue(handle.RunState)
            ?? throw new InvalidOperationException("RunState.CurrentRoom was null");
        if (!_treasureRoomType.IsInstanceOfType(room))
        {
            throw new InvalidOperationException(
                $"run/leave_treasure_room called but current room is {room.GetType().Name}, not TreasureRoom");
        }

        if (_treasureRoomDoNormalRewards is null
            || _treasureRoomDoExtraRewards is null
            || _runManagerTreasureRoomRelicSync is null
            || _treasureSyncCompleteWithNoRelics is null)
        {
            throw new InvalidOperationException(
                "treasure-room binding not fully resolved at boot — one of " +
                "DoNormalRewards / DoExtraRewardsIfNeeded / TreasureRoomRelicSynchronizer / " +
                "CompleteWithNoRelics is missing. The engine likely renamed a member.");
        }

        // BUG-013 (sts2-cli): drain any pending relic-picking session from a
        // prior room before invoking the chest reward chain.
        DrainActionExecutor(handle);
        _syncCtx?.Pump();

        // 1. Set up the chest offering. The returned Task<int> resolves to
        //    the count of relics offered; we don't need the value — the
        //    authoritative source is the synchronizer's CurrentRelics.
        var doNormalResult = _treasureRoomDoNormalRewards.Invoke(room, null);
        if (doNormalResult is Task t1) t1.GetAwaiter().GetResult();
        _syncCtx?.Pump();
        DrainActionExecutor(handle);

        // 2. Greedy pick: claim the first relic the chest offered. There's
        //    no real player decision here — chests offer a single relic and
        //    the only alternative is to walk past, which a greedy run won't
        //    do. A future slice can split this into a previewable pick if
        //    a SilverCrucible-style "first chest is empty" relic ever ships.
        //    If the chest had no relics (empty offering), close out via
        //    CompleteWithNoRelics so the synchronizer state doesn't linger.
        var sync = _runManagerTreasureRoomRelicSync.GetValue(handle.RunManager)
            ?? throw new InvalidOperationException(
                "RunManager.TreasureRoomRelicSynchronizer was null after DoNormalRewards");

        var hasRelics = false;
        if (_treasureSyncCurrentRelics?.GetValue(sync) is System.Collections.IEnumerable currentRelics)
        {
            foreach (var _ in currentRelics) { hasRelics = true; break; }
        }

        if (hasRelics)
        {
            // Pick the first relic from the offering. We can't reliably
            // drive the synchronizer's TCS-based picking flow in headless
            // (the engine awaits an animation-completion signal normally
            // raised by the UI screen), so we:
            //   1. Pull the first RelicModel from CurrentRelics
            //   2. Grant it via RelicCmd.Obtain — the same engine path that
            //      RelicReward.OnSelectWrapper uses, so Player.Relics + the
            //      on-obtain listener pipeline stay aligned
            //   3. Call SyncCompleteWithNoRelics to release the synchronizer
            //      session — without this, the next treasure room trips
            //      "session already occurring" (BUG-013).
            //
            // If anything in this chain fails, fall through to
            // CompleteWithNoRelics so the run isn't stuck on a dangling
            // synchronizer state — the caller will see 0 relics granted and
            // the next snapshot reports MapRoom either way.
            if (_relicCmdObtain is not null && _treasureSyncCurrentRelics is not null)
            {
                object? pickedRelic = null;
                if (_treasureSyncCurrentRelics.GetValue(sync) is System.Collections.IEnumerable cr)
                {
                    foreach (var r in cr) { pickedRelic = r; break; }
                }
                if (pickedRelic is not null)
                {
                    // The model from CurrentRelics is already a mutable
                    // per-run instance (probe showed IsMutable=False on the
                    // canonical RELIC.GORGET, but the synchronizer cache
                    // gives us the runtime-instance variant; ToMutable
                    // produces a fresh mutable if it isn't already).
                    var modelToGrant = _relicModelToMutable is not null
                        ? (_relicModelToMutable.Invoke(pickedRelic, null) ?? pickedRelic)
                        : pickedRelic;
                    var paramCount = _relicCmdObtain.GetParameters().Length;
                    var args = new object?[paramCount];
                    args[0] = modelToGrant;
                    args[1] = handle.Player;
                    for (var i = 2; i < paramCount; i++) args[i] = Type.Missing;
                    try
                    {
                        var obtainResult = _relicCmdObtain.Invoke(null, args);
                        if (obtainResult is Task ot) ot.GetAwaiter().GetResult();
                        _syncCtx?.Pump();
                        DrainActionExecutor(handle);
                    }
                    catch { /* fall through; CompleteWithNoRelics below at least closes the session */ }
                }
            }
        }
        // Always close out the synchronizer — either because the chest was
        // empty, or because we already granted the relic directly above and
        // the session would otherwise linger.
        _treasureSyncCompleteWithNoRelics.Invoke(sync, null);
        _syncCtx?.Pump();
        DrainActionExecutor(handle);

        // 3. Run any extra-reward path (act-3 / ascension chests). Typically
        //    a no-op for Act 1.
        var doExtraResult = _treasureRoomDoExtraRewards.Invoke(room, null);
        if (doExtraResult is Task t2) t2.GetAwaiter().GetResult();
        _syncCtx?.Pump();
        DrainActionExecutor(handle);

        // 4. ForceToMap. Without this, CurrentRoom stays TreasureRoom even
        //    though every reward has been granted.
        if (_runManagerProceedFromTerminalRewards is not null)
        {
            try
            {
                var proceed = _runManagerProceedFromTerminalRewards.Invoke(handle.RunManager, null);
                if (proceed is Task pt) pt.GetAwaiter().GetResult();
                _syncCtx?.Pump();
            }
            catch { /* sts2-cli also swallows */ }
        }
        if (_runManagerEnterRoom is not null && _mapRoomType is not null)
        {
            try
            {
                var mapRoom = Activator.CreateInstance(_mapRoomType)
                    ?? throw new InvalidOperationException($"{_mapRoomType.FullName} default ctor returned null");
                var enter = _runManagerEnterRoom.Invoke(handle.RunManager, new[] { mapRoom });
                if (enter is Task et) et.GetAwaiter().GetResult();
                _syncCtx?.Pump();
            }
            catch { /* caller will see TreasureRoom remains and can decide */ }
        }
        DrainActionExecutor(handle);
    }

    // Boss → next act transition. After an act boss is defeated and its
    // rewards drained, the engine leaves the player in a stale MapRoom
    // whose CurrentMapCoord no longer points at a real node — the snapshot
    // surfaces an empty AvailableMapNodes. EnterNextAct bumps
    // RunState.CurrentActIndex, regenerates the next act's map, and
    // re-enters at the start node. Sts2-cli mirrors this at
    // RunSimulator.cs:2221 — only safe to call once the wire reports
    // CurrentRoomType == BossRoom with combat ended and the boss reward
    // chain consumed.
    //
    // Caller guard: we require the engine-reported `CurrentRoom` to be a
    // CombatRoom whose current map point is a Boss point (i.e. what the
    // snapshot surfaces as BossRoom). Calling from anywhere else throws.
    public void EnterNextAct(RunHandle handle)
    {
        var room = _runStateCurrentRoom.GetValue(handle.RunState)
            ?? throw new InvalidOperationException("RunState.CurrentRoom was null");
        var roomTypeName = room.GetType().Name;

        // Two legal entry conditions, both meaning "post-boss, pre-next-act":
        //   1. CurrentRoom is the boss CombatRoom and combat has ended —
        //      sts2-cli's pattern (RunSimulator.cs:1655). Rare for us in
        //      practice because our reward-drain flow tends to advance
        //      past the room before the caller invokes enter_next_act.
        //   2. CurrentRoom is MapRoom but AvailableMapNodes is empty —
        //      the engine has flipped the room post-rewards but the
        //      current map coord no longer points at a real node, so
        //      forward map traversal is blocked. This is the state
        //      DiagnoseAct2WalkTests observed on seed 42 floor 17.
        // Anything else (mid-combat, mid-event, non-boss map node with
        // valid children) is a caller mistake — reject so a stray call
        // never silently corrupts run state by skipping an act.
        var onBossPoint = roomTypeName == "CombatRoom" && IsCurrentMapPointBoss(handle.RunState);
        var stuckPostBossMap = roomTypeName == "MapRoom"
            && ReadAvailableMapNodes(handle.RunState).Count == 0;
        if (!onBossPoint && !stuckPostBossMap)
        {
            throw new InvalidOperationException(
                $"run/enter_next_act called but current room is {roomTypeName} with " +
                $"{ReadAvailableMapNodes(handle.RunState).Count} available map nodes. " +
                $"Only legal after defeating an act boss and draining rewards.");
        }

        _syncCtx?.Pump();
        DrainActionExecutor(handle);

        var result = _runManagerEnterNextAct.Invoke(handle.RunManager, null);
        if (result is Task task) task.GetAwaiter().GetResult();
        _syncCtx?.Pump();
        DrainActionExecutor(handle);
    }

    // Finished-event auto-advance. When an event resolves (sts2 sets
    // IsFinished=true on the local event), the engine sometimes does
    // NOT flip CurrentRoom back to MapRoom on its own — observed on
    // seed 42 Act 3 floor 10 (FakeMerchant). The wire surface signals
    // this state by reporting CurrentRoomType=EventRoom with an empty
    // AvailableEventOptions list; this method drives the transition
    // explicitly, mirroring sts2-cli's `Leave` pattern at
    // RunSimulator.cs:1626-1646:
    //   1. RunManager.ProceedFromTerminalRewardsScreen() — graceful
    //      exit through the engine's natural reward-screen handler.
    //   2. If still in EventRoom afterward, force EnterRoom(MapRoom) —
    //      the engine occasionally leaves the room half-transitioned
    //      and the explicit EnterRoom snaps it back to the map.
    //
    // Caller guard: rejects unless CurrentRoom is EventRoom AND the
    // local event reports IsFinished=true. Anything else is a caller
    // mistake — events with active options should be advanced via
    // run/select_event_option, not this method.
    public void ProceedEvent(RunHandle handle)
    {
        var room = _runStateCurrentRoom.GetValue(handle.RunState)
            ?? throw new InvalidOperationException("RunState.CurrentRoom was null");
        var roomTypeName = room.GetType().Name;
        if (roomTypeName != "EventRoom")
        {
            throw new InvalidOperationException(
                $"run/proceed_event called but current room is {roomTypeName}, not EventRoom. " +
                $"Only legal when an event has finished and the room hasn't auto-transitioned.");
        }
        if (!IsLocalEventFinished(handle.RunManager))
        {
            throw new InvalidOperationException(
                "run/proceed_event called but the local event is not finished. " +
                "Advance the event via run/select_event_option first.");
        }

        if (_runManagerProceedFromTerminalRewards is not null)
        {
            try
            {
                var proceed = _runManagerProceedFromTerminalRewards.Invoke(handle.RunManager, null);
                if (proceed is Task pt) pt.GetAwaiter().GetResult();
                _syncCtx?.Pump();
            }
            catch { /* sts2-cli also swallows; the EnterRoom fallback below covers it */ }
        }

        // Re-read CurrentRoom — if Proceed succeeded the room has flipped
        // to MapRoom and we're done. Otherwise force the transition.
        var roomAfter = _runStateCurrentRoom.GetValue(handle.RunState);
        var stillEvent = roomAfter is not null && roomAfter.GetType().Name == "EventRoom";
        if (stillEvent && _runManagerEnterRoom is not null && _mapRoomType is not null)
        {
            try
            {
                var mapRoom = Activator.CreateInstance(_mapRoomType)
                    ?? throw new InvalidOperationException($"{_mapRoomType.FullName} default ctor returned null");
                var enter = _runManagerEnterRoom.Invoke(handle.RunManager, new[] { mapRoom });
                if (enter is Task et) et.GetAwaiter().GetResult();
                _syncCtx?.Pump();
            }
            catch { /* caller sees CurrentRoomType=EventRoom on the next snapshot and can decide */ }
        }
        DrainActionExecutor(handle);
    }

    // Walk MerchantRoom.Inventory.AllEntries and shape each entry into a
    // wire MerchantItem. Returns [] when the merchant binding never
    // resolved, the current room isn't a MerchantRoom (defence in depth),
    // or AllEntries is null/empty. AllEntries is the engine's stable roll-
    // up (CharacterCards → ColorlessCards → Relics → Potions → CardRemoval),
    // so the wire's Index matches what the caller will see next read.
    //
    // An entry we can't classify into a known kind still surfaces as
    // Unknown — the wire never silently hides an item even if the engine
    // adds a new entry subtype.
    private IReadOnlyList<MerchantItem> ReadAvailableMerchantItems(object runState)
    {
        if (_merchantRoomType is null || _merchantRoomInventory is null
            || _merchantInventoryAllEntries is null
            || _merchantEntryCost is null
            || _merchantEntryEnoughGold is null
            || _merchantEntryIsStocked is null)
        {
            return Array.Empty<MerchantItem>();
        }
        var room = _runStateCurrentRoom.GetValue(runState);
        if (room is null || !_merchantRoomType.IsInstanceOfType(room))
        {
            return Array.Empty<MerchantItem>();
        }
        var inventory = _merchantRoomInventory.GetValue(room);
        if (inventory is null) return Array.Empty<MerchantItem>();
        if (_merchantInventoryAllEntries.GetValue(inventory) is not System.Collections.IEnumerable entries)
        {
            return Array.Empty<MerchantItem>();
        }

        var result = new List<MerchantItem>();
        var index = 0;
        foreach (var entry in entries)
        {
            if (entry is null) { index++; continue; }
            var cost = (int)(_merchantEntryCost.GetValue(entry) ?? 0);
            var enoughGold = (bool)(_merchantEntryEnoughGold.GetValue(entry) ?? false);
            var isStocked = (bool)(_merchantEntryIsStocked.GetValue(entry) ?? false);
            var (kind, cardId, relicId, potionId) = ClassifyMerchantEntry(entry);
            result.Add(new MerchantItem(
                Index: index,
                Kind: kind,
                Cost: cost,
                IsStocked: isStocked,
                IsAffordable: enoughGold,
                CardId: cardId,
                RelicId: relicId,
                PotionId: potionId));
            index++;
        }
        return result;
    }

    // Classify a MerchantEntry into its wire kind + extract the id. Each
    // kind branch is gated on the subtype's binding being present; a kind
    // we couldn't bind still returns Unknown so the entry stays selectable
    // (the engine's purchase path doesn't care about our wire shape) but
    // the caller can't pretend it knows what it is.
    private (MerchantKind Kind, string? CardId, string? RelicId, string? PotionId)
        ClassifyMerchantEntry(object entry)
    {
        if (_merchantCardEntryType is not null && _merchantCardEntryType.IsInstanceOfType(entry))
        {
            string? cardId = null;
            if (_merchantCardEntryCreationResult is not null && _cardCreationResultCard is not null)
            {
                var creation = _merchantCardEntryCreationResult.GetValue(entry);
                var cardModel = creation is null ? null : _cardCreationResultCard.GetValue(creation);
                if (cardModel is not null) cardId = ReadEntryId(_cardModelId, cardModel);
            }
            return (MerchantKind.Card, cardId, null, null);
        }
        if (_merchantRelicEntryType is not null && _merchantRelicEntryType.IsInstanceOfType(entry))
        {
            string? relicId = null;
            if (_merchantRelicEntryModel is not null)
            {
                var model = _merchantRelicEntryModel.GetValue(entry);
                if (model is not null) relicId = ReadEntryId(_relicModelId, model);
            }
            return (MerchantKind.Relic, null, relicId, null);
        }
        if (_merchantPotionEntryType is not null && _merchantPotionEntryType.IsInstanceOfType(entry))
        {
            string? potionId = null;
            if (_merchantPotionEntryModel is not null)
            {
                var model = _merchantPotionEntryModel.GetValue(entry);
                if (model is not null) potionId = ReadEntryId(_potionModelId, model);
            }
            return (MerchantKind.Potion, null, null, potionId);
        }
        if (_merchantCardRemovalEntryType is not null && _merchantCardRemovalEntryType.IsInstanceOfType(entry))
        {
            // No item id — card removal is a service, identified solely by
            // its kind. CardRemoval slots typically have IsStocked=false
            // once consumed, which the wire surfaces via IsStocked.
            return (MerchantKind.CardRemoval, null, null, null);
        }
        return (MerchantKind.Unknown, null, null, null);
    }

    // Drive a merchant purchase. Resolves the entry at `itemIndex` against
    // MerchantInventory.AllEntries, then calls the entry's OnTryPurchase-
    // Wrapper(inventory, ignoreCost=false). The wrapper is the engine's
    // canonical purchase path; it gates affordability, marks the slot
    // unstocked, deducts gold via the standard cmd, and fires on-obtain
    // listeners for relics / potions / cards.
    //
    // The base method has 2 params; MerchantCardRemovalEntry overrides
    // with a 3-arg form (adding `cancelable`). Reflection's virtual
    // dispatch picks up the override against the runtime entry, so the
    // base-arity MethodInfo we bound at boot routes correctly for either
    // shape — but if the engine adds an entry kind with a different arity
    // override, the failure mode is a TargetParameterCountException
    // surfaced as InvalidOperationException to the caller. Validation
    // failures (sold out, insufficient gold) surface as the wrapper's
    // returned Task<bool> == false; the host handler converts that to
    // WireException(InvalidParams) so generated clients see the right code.
    public bool BuyMerchantItem(RunHandle handle, int itemIndex)
    {
        if (_merchantRoomType is null || _merchantRoomInventory is null
            || _merchantInventoryAllEntries is null
            || _merchantEntryOnTryPurchaseWrapper is null)
        {
            throw new InvalidOperationException(
                "merchant binding not resolved at boot — MerchantRoom / Inventory / OnTryPurchaseWrapper missing. " +
                "Either the engine moved a type or the bootstrap walk did not surface it.");
        }
        var room = _runStateCurrentRoom.GetValue(handle.RunState)
            ?? throw new InvalidOperationException("RunState.CurrentRoom was null");
        if (!_merchantRoomType.IsInstanceOfType(room))
        {
            throw new InvalidOperationException(
                $"run/buy_merchant_item called but current room is {room.GetType().Name}, not MerchantRoom");
        }
        var inventory = _merchantRoomInventory.GetValue(room)
            ?? throw new InvalidOperationException("MerchantRoom.Inventory was null");

        if (_merchantInventoryAllEntries.GetValue(inventory) is not System.Collections.IEnumerable entries)
        {
            throw new InvalidOperationException("MerchantInventory.AllEntries was null or not enumerable");
        }

        object? picked = null;
        var i = 0;
        foreach (var e in entries)
        {
            if (i == itemIndex) { picked = e; break; }
            i++;
        }
        if (picked is null)
        {
            throw new ArgumentOutOfRangeException(nameof(itemIndex),
                $"itemIndex {itemIndex} not in MerchantInventory.AllEntries (count={i + 1}+)");
        }

        // Look the method up on the entry's runtime type so a subtype
        // override (e.g. MerchantCardRemovalEntry's 3-arg form) is picked
        // up. Fall back to the base if the lookup doesn't find a 2-arg form
        // (covers an engine change that drops the base overload).
        var purchaseFn = picked.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                            .FirstOrDefault(m => m.Name == "OnTryPurchaseWrapper" && m.GetParameters().Length == 2)
                        ?? _merchantEntryOnTryPurchaseWrapper;

        var result = purchaseFn.Invoke(picked, new object?[] { inventory, /* ignoreCost */ false });
        if (result is Task<bool> tb)
        {
            var purchased = tb.GetAwaiter().GetResult();
            _syncCtx?.Pump();
            DrainActionExecutor(handle);
            return purchased;
        }
        if (result is Task t)
        {
            t.GetAwaiter().GetResult();
            _syncCtx?.Pump();
            DrainActionExecutor(handle);
            // Non-generic Task — assume success and let the caller verify
            // via the post-buy snapshot's IsStocked flip.
            return true;
        }
        _syncCtx?.Pump();
        DrainActionExecutor(handle);
        return true;
    }

    // Exit the current merchant room to MapRoom. Same pattern rest-site and
    // treasure use: there's no engine auto-advance from a merchant, so we
    // drive RunManager.EnterRoom(new MapRoom()) directly. A future
    // purchasable that locks the merchant (none ship today) could add a
    // pre-leave gate here; for now any in-progress buy completes
    // synchronously and the room flips cleanly.
    public void LeaveMerchantRoom(RunHandle handle)
    {
        if (_merchantRoomType is null)
        {
            throw new InvalidOperationException(
                "merchant binding not resolved at boot — MerchantRoom type missing. " +
                "Either the engine renamed it or the bootstrap walk did not surface it.");
        }
        var room = _runStateCurrentRoom.GetValue(handle.RunState)
            ?? throw new InvalidOperationException("RunState.CurrentRoom was null");
        if (!_merchantRoomType.IsInstanceOfType(room))
        {
            throw new InvalidOperationException(
                $"run/leave_merchant_room called but current room is {room.GetType().Name}, not MerchantRoom");
        }
        if (_runManagerEnterRoom is null || _mapRoomType is null)
        {
            throw new InvalidOperationException(
                "merchant exit path not resolved at boot — EnterRoom or MapRoom type missing.");
        }

        // Drain any pending engine work (a buy whose Task hadn't returned
        // by the time the caller issues leave) before swapping rooms.
        _syncCtx?.Pump();
        DrainActionExecutor(handle);

        var mapRoom = Activator.CreateInstance(_mapRoomType)
            ?? throw new InvalidOperationException($"{_mapRoomType.FullName} default ctor returned null");
        var enter = _runManagerEnterRoom.Invoke(handle.RunManager, new[] { mapRoom });
        if (enter is Task et) et.GetAwaiter().GetResult();
        _syncCtx?.Pump();
        DrainActionExecutor(handle);
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
        // OBOL etc.). Options that branch into card selection
        // (CardSelectCmd.From*, NSimpleCardSelectScreen.Create) load .tscn
        // assets that aren't present in headless — Create returns null,
        // the event-model body NREs (or throws ArgumentNullException via
        // LINQ on a null collection downstream). HangPatches turns the
        // factory NRE into `await null`, but the model-layer dereference
        // still throws. Rather than let one bad option kill the host,
        // catch the exception and force-advance the room: AutoAdvance's
        // EnterRoom(MapRoom) fallback flips the room type even when
        // IsFinished is still false. The player keeps their pre-event HP/
        // gold (the event's gameplay effect is partially or wholly
        // skipped), the agent continues. This is identical-shape recovery
        // to what AutoAdvance already does post-successful-pick; the
        // try-catch just extends it to "engine threw mid-Chosen."
        Exception? chosenError = null;
        try
        {
            var result = _eventOptionChosen.Invoke(option, null);
            if (result is Task t) t.GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            chosenError = ex.InnerException ?? ex;
        }

        // sts2-cli pattern: after the event finishes, the game still leaves
        // CurrentRoom as the EventRoom until something nudges the next
        // transition. Without this, the wire reports CurrentRoomType=EventRoom
        // forever and the caller has no way to leave. Mirror sts2-cli's force-
        // transition (ProceedFromTerminalRewardsScreen → EnterRoom(MapRoom))
        // so a successful pick — or a recovered failure — lands the player
        // back on the map.
        var nowFinished = (bool)_eventIsFinished.GetValue(localEvent)!;
        if (nowFinished || chosenError is not null)
        {
            AutoAdvanceFinishedEvent(handle.RunManager, handle.RunState);
        }

        if (chosenError is not null)
        {
            // Verify the recovery actually moved us off EventRoom; if not
            // the engine is in a state we can't reason about and surfacing
            // the original exception is the honest answer.
            var currentName = _runStateCurrentRoom.GetValue(handle.RunState)?.GetType().Name;
            if (currentName == "EventRoom")
            {
                throw new InvalidOperationException(
                    $"EventOption.Chosen() threw and AutoAdvance failed to leave EventRoom. " +
                    $"Original error: {chosenError.GetType().Name}: {chosenError.Message}",
                    chosenError);
            }
        }
    }

    // Fire PlayerCmd.EndTurn(player, canBackOut: false) and pump the engine's
    // async chain to completion. PlayerCmd.EndTurn → SetReadyToEndTurn →
    // (fire-and-forget) AfterAllPlayersReadyToEndTurn → enqueue
    // ReadyToBeginEnemyTurnAction → SetReadyToBeginEnemyTurn →
    // AfterAllPlayersReadyToBeginEnemyTurn → SwitchFromPlayerToEnemySide →
    // SwitchSides → StartTurn(enemy) → ExecuteEnemyTurn (monsters attack)
    // → EndEnemyTurn → SwitchSides → StartTurn(player). The chain hops the
    // ActionExecutor twice and posts continuations to the sync context; we
    // drive it to the next player turn by alternating Pump (drains posted
    // continuations) with DrainActionExecutor (awaits FinishedExecutingActions).
    //
    // With Player.NetId = LocalContext.NetId = 1uL (sts2-cli's contract) and
    // the GodotStubs gaps catalogued by Phase 1 patched, the natural chain
    // runs end-to-end. probe-natural-chain converges to next-player-turn in
    // a single pump iteration and reports zero gaps — the manual side-switch
    // fallback that used to live here is no longer needed.
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

        var roundBefore = ReadRound(cm);

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

        // Pump the engine until a terminal condition is reached. Cap the
        // deadline so a stuck chain surfaces as a debug-logged timeout rather
        // than hanging the host. The cap is generous (vs. probe's 1-iteration
        // happy path) to absorb future scenarios — multi-enemy boss fights,
        // multi-hit attacks, etc. — without re-tightening every time.
        var converged = PumpUntilTerminal(handle, cm, roundBefore, deadlineIterations: 500);

        if (!converged && Environment.GetEnvironmentVariable("STS2_HEADLESS_DEBUG") is not null)
        {
            var ipp = (bool)_combatManagerIsPlayPhase!.GetValue(cm)!;
            var inp = (bool)_combatManagerIsInProgress!.GetValue(cm)!;
            Console.Error.WriteLine($"[end_turn] did not converge — roundBefore={roundBefore}, roundNow={ReadRound(cm)}, IsPlayPhase={ipp}, IsInProgress={inp}");
        }

        AutoAdvancePostCombat(handle);
    }


    // Returns true if a terminal condition was reached (next player turn,
    // combat ended, or player dead). Returns false on timeout.
    private bool PumpUntilTerminal(RunHandle handle, object cm, int roundBefore, int deadlineIterations)
    {
        if (_combatManagerIsInProgress is null || _combatManagerIsPlayPhase is null) return true;

        for (var i = 0; i < deadlineIterations; i++)
        {
            _syncCtx?.Pump();
            DrainActionExecutor(handle);

            if (!(bool)_combatManagerIsInProgress.GetValue(cm)!) return true;
            if (_creatureIsDead is not null)
            {
                var creature = _playerCreature.GetValue(handle.Player);
                if (creature is not null && (bool)_creatureIsDead.GetValue(creature)!) return true;
            }
            if ((bool)_combatManagerIsPlayPhase.GetValue(cm)!)
            {
                var roundNow = ReadRound(cm);
                // Real progress: a new round started. Round-stable + play-phase
                // means we never actually left (e.g. EndTurn was a no-op).
                if (roundNow > roundBefore) return true;
            }
            Thread.Sleep(2);
        }
        return false;
    }

    private int ReadRound(object cm)
    {
        if (_combatManagerDebugOnlyGetState is null || _combatStateRoundNumber is null) return 0;
        var state = _combatManagerDebugOnlyGetState.Invoke(cm, Array.Empty<object?>());
        if (state is null) return 0;
        var v = _combatStateRoundNumber.GetValue(state);
        return v is null ? 0 : Convert.ToInt32(v);
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

    // After a mutating combat action, decide what to do next:
    //   - combat still running → no-op
    //   - combat ended, no rewards bound → legacy path (proceed + EnterRoom),
    //     skipping every reward the engine offered (matches the pre-rewards
    //     behaviour for setups where we couldn't bind RewardsSet)
    //   - combat ended, rewards bound → generate the reward set and hold it
    //     in _pendingRewards. Do NOT advance — the wire surfaces the pending
    //     decisions via RewardsState; the caller drives select_reward / skip
    //     until the list empties, at which point AdvanceAfterRewardsConsumed
    //     fires the legacy proceed-and-enter path to reach MapRoom.
    private void AutoAdvancePostCombat(RunHandle handle)
    {
        if (_combatManagerInstance is null) return;
        var cm = _combatManagerInstance.GetValue(null);
        if (cm is null) return;
        var inProgress = _combatManagerIsInProgress is not null && (bool)_combatManagerIsInProgress.GetValue(cm)!;
        if (inProgress) return;

        var roomName = _runStateCurrentRoom.GetValue(handle.RunState)?.GetType().Name;
        if (roomName != "CombatRoom") return;

        // Already-pending rewards mean the caller previously consumed at least
        // one reward but more remain; don't regenerate.
        if (_pendingRewards is not null && _pendingRewards.Count > 0) return;

        if (TryGeneratePendingRewards(handle))
        {
            // Surface them to the wire; caller will drive consumption.
            return;
        }

        // No reward bindings — fall through to the original behaviour so the
        // host still escapes the CombatRoom on its own.
        AutoAdvanceFinishedEvent(handle.RunManager, handle.RunState);
    }

    // Generates the post-combat RewardsSet and stashes everything it produced
    // into _pendingRewards. Returns true iff at least one reward landed in the
    // pending list; false signals the caller should fall back to the legacy
    // proceed-and-skip path. Failures (missing bindings, generation throws)
    // also return false rather than booby-trap the wire with an empty list.
    private bool TryGeneratePendingRewards(RunHandle handle)
    {
        if (_rewardsSetCtor is null
            || _rewardsSetWithRewardsFromRoom is null
            || _rewardsSetGenerateWithoutOffering is null)
        {
            return false;
        }

        var room = _runStateCurrentRoom.GetValue(handle.RunState);
        if (room is null) return false;

        try
        {
            var rewardsSet = _rewardsSetCtor.Invoke(new[] { handle.Player });
            // WithRewardsFromRoom returns the same RewardsSet (fluent); accept
            // either-or so a future signature change doesn't crash us.
            var withRoom = _rewardsSetWithRewardsFromRoom.Invoke(rewardsSet, new[] { room }) ?? rewardsSet;
            var task = _rewardsSetGenerateWithoutOffering.Invoke(withRoom, Array.Empty<object?>());
            object? generated = null;
            if (task is Task t)
            {
                t.GetAwaiter().GetResult();
                // Task<IEnumerable<Reward>>.Result via reflection — the runtime
                // type is the closed generic, so .GetType().GetProperty works.
                var resultProp = t.GetType().GetProperty("Result", BindingFlags.Public | BindingFlags.Instance);
                generated = resultProp?.GetValue(t);
            }
            else
            {
                generated = task; // Synchronous override (unlikely but defensive).
            }

            if (generated is not System.Collections.IEnumerable seq) return false;

            var collected = new List<object>();
            foreach (var reward in seq)
            {
                if (reward is null) continue;
                collected.Add(reward);
            }
            if (collected.Count == 0) return false;

            _pendingRewards = collected;
            return true;
        }
        catch
        {
            return false;
        }
    }

    // Claim the reward at `rewardIndex` in the latest snapshot. Card-kind
    // rewards take `cardIndex` and route through CardPileCmd.Add (which fans
    // out to obtain-listeners — relics like LuckyFysh observe it). Non-card
    // rewards run the engine's OnSelectWrapper (gold credit, potion grant,
    // relic obtain). Both paths propagate exceptions: --probe-rewards-natural-
    // chain confirmed the chain runs gap-free with NetIds aligned, so safety
    // nets here would only mask future regressions.
    public void SelectReward(RunHandle handle, int rewardIndex, int? cardIndex)
    {
        if (_pendingRewards is null || _pendingRewards.Count == 0)
            throw new InvalidOperationException("no pending rewards to select");
        if (rewardIndex < 0 || rewardIndex >= _pendingRewards.Count)
            throw new ArgumentOutOfRangeException(nameof(rewardIndex),
                $"rewardIndex {rewardIndex} out of range; {_pendingRewards.Count} reward(s) pending");

        var reward = _pendingRewards[rewardIndex];

        if (_cardRewardType is not null && _cardRewardType.IsInstanceOfType(reward))
        {
            if (cardIndex is null)
                throw new ArgumentException("card-kind reward requires cardIndex");
            ClaimCardReward(handle, reward, cardIndex.Value);
        }
        else
        {
            InvokeOnSelectWrapper(reward);
            _syncCtx?.Pump();
        }

        _pendingRewards.RemoveAt(rewardIndex);
        DrainActionExecutor(handle);
        AdvanceAfterRewardsConsumed(handle);
    }

    // Skip the reward at `rewardIndex`. Only legal for skippable card rewards;
    // the wire's CanSkip flag tells callers in advance, but the host still
    // re-checks here so a stale snapshot can't drift state. Non-card rewards
    // and locked card rewards both throw rather than silently no-op.
    public void SkipReward(RunHandle handle, int rewardIndex)
    {
        if (_pendingRewards is null || _pendingRewards.Count == 0)
            throw new InvalidOperationException("no pending rewards to skip");
        if (rewardIndex < 0 || rewardIndex >= _pendingRewards.Count)
            throw new ArgumentOutOfRangeException(nameof(rewardIndex),
                $"rewardIndex {rewardIndex} out of range; {_pendingRewards.Count} reward(s) pending");

        var reward = _pendingRewards[rewardIndex];
        if (_cardRewardType is null || !_cardRewardType.IsInstanceOfType(reward))
            throw new InvalidOperationException("only card rewards are skippable");
        if (_cardRewardCanSkip is not null && !(bool)(_cardRewardCanSkip.GetValue(reward) ?? false))
            throw new InvalidOperationException("this card reward is not skippable (CanSkip=false)");

        if (_cardRewardOnSkipped is not null)
        {
            var result = _cardRewardOnSkipped.Invoke(reward, Array.Empty<object?>());
            if (result is Task t) t.GetAwaiter().GetResult();
            _syncCtx?.Pump();
        }

        _pendingRewards.RemoveAt(rewardIndex);
        DrainActionExecutor(handle);
        AdvanceAfterRewardsConsumed(handle);
    }

    private void ClaimCardReward(RunHandle handle, object cardReward, int cardIndex)
    {
        if (_cardRewardCards?.GetValue(cardReward) is not System.Collections.IEnumerable cardsEnumerable)
            throw new InvalidOperationException("CardReward.Cards was null or not enumerable");
        var cards = new List<object>();
        foreach (var c in cardsEnumerable) if (c is not null) cards.Add(c);
        if (cardIndex < 0 || cardIndex >= cards.Count)
            throw new ArgumentOutOfRangeException(nameof(cardIndex),
                $"cardIndex {cardIndex} out of range; {cards.Count} card(s) on offer");

        var picked = cards[cardIndex];

        // Engine path: CardPileCmd.Add(card, PileType.Deck). Routes through
        // the listener pipeline so relic on-card-obtain hooks fire; a direct
        // deck.Add would bypass listeners. RelicListenerTests pins this.
        if (_cardPileCmdAdd is null || _pileTypeDeckValue is null)
            throw new InvalidOperationException("CardPileCmd.Add or PileType.Deck not bound — cannot route card-obtain through engine");
        var paramCount = _cardPileCmdAdd.GetParameters().Length;
        var args = new object?[paramCount];
        args[0] = picked;
        args[1] = _pileTypeDeckValue;
        for (var i = 2; i < paramCount; i++) args[i] = Type.Missing;
        var addResult = _cardPileCmdAdd.Invoke(null, args);
        if (addResult is Task addTask) addTask.GetAwaiter().GetResult();
        _syncCtx?.Pump();

        if (_runManagerRewardSynchronizer is not null && _rewardSyncSyncLocalObtainedCard is not null)
        {
            var sync = _runManagerRewardSynchronizer.GetValue(handle.RunManager);
            if (sync is not null) _rewardSyncSyncLocalObtainedCard.Invoke(sync, new[] { picked });
        }

        // Engine bookkeeping the bypass skipped: stamp CardChoices on
        // the current map-point's player_stats with one entry per
        // offered card (was_picked=true for the picked one, false for
        // the rest). The engine's `CardReward.OnSelectWrapper` does
        // this around line 44197 of the v0.103.2 decompile; we don't
        // call OnSelectWrapper because it depends on the NCardReward
        // UI screen, which is null in our headless context. Without
        // this stamping, `run.json` only carries `cards_gained` and
        // the viewer can't tell the user what options were offered
        // (only what was picked). Best-effort: any reflection miss
        // surfaces on stderr but doesn't break the pick.
        StampCardChoices(handle, picked, cards);
    }

    private void StampCardChoices(RunHandle handle, object pickedCard, IReadOnlyList<object> offeredCards)
    {
        try
        {
            var runManagerType = handle.RunManager.GetType();
            var stateProp = runManagerType.GetProperty("State", BindingFlags.NonPublic | BindingFlags.Instance);
            var state = stateProp?.GetValue(handle.RunManager);
            if (state is null) return;

            var currentEntryProp = state.GetType().GetProperty("CurrentMapPointHistoryEntry", BindingFlags.Public | BindingFlags.Instance);
            var currentEntry = currentEntryProp?.GetValue(state);
            if (currentEntry is null) return;

            var getEntry = currentEntry.GetType().GetMethod("GetEntry", BindingFlags.Public | BindingFlags.Instance, [typeof(ulong)]);
            if (getEntry is null) return;

            // For single-player our local NetId is 1. The recorder
            // already uses LocalContext.NetId; here we read the same
            // value off RunState.Players[0].NetId to avoid pulling
            // LocalContext into the runtime layer.
            var playersProp = state.GetType().GetProperty("Players", BindingFlags.Public | BindingFlags.Instance);
            var players = playersProp?.GetValue(state) as System.Collections.IEnumerable;
            var first = players?.Cast<object?>().FirstOrDefault();
            if (first is null) return;
            var netId = (ulong)(_playerNetId.GetValue(first) ?? 0uL);

            var playerEntry = getEntry.Invoke(currentEntry, [netId]);
            if (playerEntry is null) return;
            var cardChoicesProp = playerEntry.GetType().GetProperty("CardChoices", BindingFlags.Public | BindingFlags.Instance);
            var cardChoices = cardChoicesProp?.GetValue(playerEntry);
            if (cardChoices is null) return;

            var entryType = handle.RunManager.GetType().Assembly.GetType("MegaCrit.Sts2.Core.Runs.History.CardChoiceHistoryEntry");
            if (entryType is null) return;
            // The constructor is `(CardModel card, bool wasPicked)`.
            // The CardModel parameter type matches whatever `picked`
            // is at runtime — resolve dynamically rather than naming
            // CardModel at compile time (AD-4).
            var entryCtor = entryType.GetConstructors().FirstOrDefault(c => c.GetParameters().Length == 2);
            if (entryCtor is null) return;

            var listAdd = cardChoices.GetType().GetMethod("Add", BindingFlags.Public | BindingFlags.Instance);
            if (listAdd is null) return;

            // Picked card first (mirrors engine ordering).
            var pickedEntry = entryCtor.Invoke([pickedCard, true]);
            listAdd.Invoke(cardChoices, [pickedEntry]);
            foreach (var c in offeredCards)
            {
                if (ReferenceEquals(c, pickedCard)) continue;
                var unpickedEntry = entryCtor.Invoke([c, false]);
                listAdd.Invoke(cardChoices, [unpickedEntry]);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Sts2Bindings.StampCardChoices: {ex}");
        }
    }

    // Best-effort OnSelectWrapper invocation for non-card rewards. Goes
    // through the engine's standard reward-claim path (which credits gold,
    // grants the relic/potion, etc.). May throw on multiplayer-aware
    // lookups under our setup; the caller treats this as "best effort —
    // we already removed the reward from the pending list".
    private void InvokeOnSelectWrapper(object reward)
    {
        if (_rewardOnSelectWrapper is null) return;
        var mi = _rewardOnSelectWrapper.DeclaringType?.IsInstanceOfType(reward) == true
            ? _rewardOnSelectWrapper
            : reward.GetType().GetMethod("OnSelectWrapper", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes)
              ?? _rewardOnSelectWrapper;
        var result = mi.Invoke(reward, Array.Empty<object?>());
        if (result is Task t) t.GetAwaiter().GetResult();
    }

    // Test affordance: grant `relicId` to the player via the engine path
    // (RelicCmd.Obtain). Mirrors what RelicReward.OnSelectWrapper does
    // when the player picks a relic from a treasure room — the relic ends
    // up in Player.Relics with proper Owner / subscription wiring, so
    // AfterCardChangedPiles hooks fire on subsequent card-obtains. The
    // wire layer exposes this as `debug/give_relic` for regression tests
    // (see RelicListenerTests.SelectCardReward_FiresLuckyFyshOnObtain).
    public void GiveRelic(RunHandle handle, string relicId)
    {
        if (_relicCmdObtain is null || _modelDbGetByIdRelic is null || _modelIdCtor is null)
            throw new InvalidOperationException("RelicCmd.Obtain / ModelDb.GetById<RelicModel> / ModelId(string,string) not bound — debug/give_relic unavailable");

        var modelId = _modelIdCtor.Invoke(new object?[] { "RELIC", relicId });
        var canonical = _modelDbGetByIdRelic.Invoke(null, new[] { modelId })
            ?? throw new InvalidOperationException($"ModelDb.GetById<RelicModel>(\"{relicId}\") returned null — unknown relic id");
        // ModelDb hands back an immutable canonical model; RelicCmd.Obtain
        // requires a per-run mutable copy or it throws CanonicalModelException.
        var relicModel = _relicModelToMutable is not null
            ? (_relicModelToMutable.Invoke(canonical, null) ?? canonical)
            : canonical;

        var paramCount = _relicCmdObtain.GetParameters().Length;
        var args = new object?[paramCount];
        args[0] = relicModel;
        args[1] = handle.Player;
        for (var i = 2; i < paramCount; i++) args[i] = Type.Missing;
        var obtainResult = _relicCmdObtain.Invoke(null, args);
        if (obtainResult is Task t) t.GetAwaiter().GetResult();
        _syncCtx?.Pump();
    }

    // Set the player's CurrentHp (and optionally MaxHp) by writing the
    // engine's backing fields directly. Bypasses the damage-event pipeline
    // and any on-hit relic listeners; the wire-level Methods doc records
    // the contract.
    //
    // Caller is responsible for validation — Methods.DebugSetHpParams +
    // HostMethods.DebugSetHp enforce the rules (hp >= 0, maxHp >= 1, hp <=
    // maxHp). This helper trusts its inputs and reports the post-write
    // (HP, MaxHp) tuple for the caller to use in the wire result.
    //
    // Returns the new (CurrentHp, MaxHp) read back through the public
    // properties — useful as a defence-in-depth check that the write took.
    public (int Hp, int MaxHp) SetPlayerHp(RunHandle handle, int hp, int? maxHp)
    {
        if (_creatureCurrentHpField is null)
        {
            throw new InvalidOperationException(
                "debug/set_hp: backing field for Creature.CurrentHp was not located at bootstrap. " +
                "Either the engine renamed the field or BindingFlags need updating.");
        }
        var creature = _playerCreature.GetValue(handle.Player)
            ?? throw new InvalidOperationException("Player.Creature was null — no live player to mutate");

        // MaxHp first: if the caller is raising MaxHp, we want CurrentHp to
        // be writable up to the new max. If the caller is lowering it, the
        // hp clamp at the validation layer is already in place.
        if (maxHp is not null)
        {
            if (_creatureMaxHpField is null)
            {
                throw new InvalidOperationException(
                    "debug/set_hp: maxHp was requested but the backing field for Creature.MaxHp was not located.");
            }
            _creatureMaxHpField.SetValue(creature, maxHp.Value);
        }

        _creatureCurrentHpField.SetValue(creature, hp);

        var newHp = (int)_creatureCurrentHp.GetValue(creature)!;
        var newMaxHp = (int)_creatureMaxHp.GetValue(creature)!;
        return (newHp, newMaxHp);
    }

    // Test affordance: drop every alive enemy in the current combat to 0 HP
    // by writing the same Creature._currentHp backing field that SetPlayerHp
    // uses (Enemy : Creature in sts2's hierarchy — confirmed by sts2-cli's
    // `Creature? target = state.Enemies.FirstOrDefault(...)` pattern). After
    // the writes the helper drains the action executor and calls
    // AutoAdvancePostCombat so the engine notices the empty combat and
    // emits rewards through the normal path — same surface UsePotion /
    // PlayCard land on after they mutate enemy state.
    //
    // Bypasses on-kill listeners (the same way SetPlayerHp bypasses on-hit
    // relics). Returns (enemiesKilledNow, combatEnded) for the wire result;
    // a zero kill count just means combat wasn't in progress when the
    // caller fired the cheat.
    public (int Killed, bool CombatEnded) KillAllEnemies(RunHandle handle)
    {
        if (_creatureCurrentHpField is null)
        {
            throw new InvalidOperationException(
                "debug/kill_all_enemies: backing field for Creature.CurrentHp was not located at bootstrap. " +
                "Either the engine renamed the field or BindingFlags need updating.");
        }
        if (_combatManagerInstance is null || _combatManagerDebugOnlyGetState is null
            || _combatStateEnemies is null || _enemyIsAlive is null)
        {
            // No combat surface bound → nothing to kill; treat as no-op so the
            // wire still returns a structured success (matches the spirit of
            // SetPlayerHp, which doesn't refuse outside of combat).
            return (0, false);
        }

        var cm = _combatManagerInstance.GetValue(null);
        if (cm is null) return (0, false);
        var state = _combatManagerDebugOnlyGetState.Invoke(cm, null);
        if (state is null) return (0, false);
        if (_combatStateEnemies.GetValue(state) is not System.Collections.IEnumerable enemies) return (0, false);

        var killed = 0;
        foreach (var enemy in enemies)
        {
            if (enemy is null) continue;
            // Only touch alive ones — re-zeroing a dead enemy is harmless
            // but inflates the "killed" count and muddies the wire signal.
            if (!(bool)_enemyIsAlive.GetValue(enemy)!) continue;
            _creatureCurrentHpField.SetValue(enemy, 0);
            killed++;
        }

        // The HP writes alone don't end combat — the engine only re-evaluates
        // "all enemies dead" through CombatManager.CheckWinCondition, which
        // is what a real damage action triggers as a follow-up. Without this
        // call, IsInProgress stays true even though the alive enumeration
        // is empty. CheckWinCondition is async; mirror the GiveRelic /
        // RelicCmd.Obtain pattern (Task → GetAwaiter().GetResult() →
        // _syncCtx.Pump()) so the engine's EndCombatInternal completes
        // synchronously from the caller's perspective.
        if (_combatManagerCheckWinCondition is not null && killed > 0)
        {
            var paramCount = _combatManagerCheckWinCondition.GetParameters().Length;
            var args = new object?[paramCount];
            for (var i = 0; i < paramCount; i++) args[i] = Type.Missing;
            var checkResult = _combatManagerCheckWinCondition.Invoke(cm, args);
            if (checkResult is Task t) t.GetAwaiter().GetResult();
            _syncCtx?.Pump();
        }

        DrainActionExecutor(handle);
        AutoAdvancePostCombat(handle);

        // combatEnded is "the cheat just transitioned combat to !InProgress",
        // not "combat is currently inactive". When the caller fires the cheat
        // outside combat (or back-to-back, where the first call already
        // ended combat), we report combatEnded=false — that signal stays
        // honest for the full-run driver's "did this tick actually clear
        // something?" check.
        var inProgress = _combatManagerIsInProgress is not null && (bool)_combatManagerIsInProgress.GetValue(cm)!;
        return (killed, killed > 0 && !inProgress);
    }

    // Test affordance: replace the player's deck with a curated list of
    // (CardId, UpgradeLevel) pairs. Routes through the engine's "create a
    // card mid-run" pipeline that sts2-cli's RunSimulator.SetPlayer uses
    // (see external-tools/sts2-cli/src/Sts2Headless/RunSimulator.cs:340-358),
    // so the new cards are properly registered with RunState (Owner +
    // tracking) and the resulting deck is what `run/state` surfaces.
    //
    // Pipeline per card:
    //   1. ModelDb.GetById<CardModel>(new ModelId("CARD", id)) → canonical
    //   2. RunState.CreateCard(canonical, player)              → Card with Owner
    //   3. If upgradeLevel > 0: call Card.Upgrade() upgradeLevel times.
    //   4. player.Deck.AddInternal(card, silent: true)         → into deck
    //
    // Why canonical, not mutable: sts2's engine guards CreateCard with a
    // MutableModelException when the model is mutable. Cards keep their
    // upgrade level on the per-instance Card, not on its model — so we
    // create with the canonical and apply upgrades to the resulting Card.
    //
    // Before the loop: every existing card is removed via RunState.RemoveCard
    // and the deck is cleared (silent so the no-listener path matches the
    // sts2-cli reference). This is a hard write — no events fire — so
    // relic listeners that react to deck changes won't see anything. That
    // matches the spirit of every other debug/ helper: bypass the event
    // pipeline so the test sets up state without unrelated side effects.
    //
    // Reflection is done inline (no fields cached in BindingState) — the
    // call is rare, and locating the members on demand keeps the bootstrap
    // path unchanged for the common case where deck replacement isn't used.
    public IReadOnlyList<string> ReplaceDeck(RunHandle handle, IReadOnlyList<(string CardId, int UpgradeLevel)> cards)
    {
        if (_modelIdCtor is null)
            throw new InvalidOperationException("debug/replace_deck: ModelId(string,string) ctor not bound — bootstrap likely failed; check probe-bootstrap output");

        var cardModelType = Sts2.GetType("MegaCrit.Sts2.Core.Models.CardModel")
            ?? throw new InvalidOperationException("debug/replace_deck: CardModel type not found in sts2 assembly");
        var modelDbType = Sts2.GetType("MegaCrit.Sts2.Core.Models.ModelDb")
            ?? throw new InvalidOperationException("debug/replace_deck: ModelDb type not found");

        var getByIdGeneric = modelDbType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "GetById" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1)
            ?? throw new InvalidOperationException("debug/replace_deck: ModelDb.GetById<T>(ModelId) not found");
        var getByIdCard = getByIdGeneric.MakeGenericMethod(cardModelType);

        var runStateType = handle.RunState.GetType();
        var createCard = runStateType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == "CreateCard"
                && m.GetParameters().Length == 2
                && m.GetParameters()[0].ParameterType == cardModelType
                && m.GetParameters()[1].ParameterType == _playerType)
            ?? throw new InvalidOperationException("debug/replace_deck: RunState.CreateCard(CardModel, Player) not found");
        var removeCard = runStateType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == "RemoveCard" && m.GetParameters().Length == 1);

        // Cards in sts2 *are* CardModel subclasses (the value returned by
        // CreateCard is a mutable CardModel instance — there's no separate
        // Card class). Upgrades are applied by calling UpgradeInternal +
        // FinalizeUpgradeInternal on the per-instance mutable model. The
        // pair mirrors sts2-cli's GetUpgradedInfo preview loop.
        var upgradeInternal = cardModelType.GetMethod("UpgradeInternal", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
        var finalizeUpgradeInternal = cardModelType.GetMethod("FinalizeUpgradeInternal", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);

        var deck = _playerDeck.GetValue(handle.Player)
            ?? throw new InvalidOperationException("debug/replace_deck: Player.Deck was null");
        var deckType = deck.GetType();
        var clear = deckType.GetMethod("Clear", BindingFlags.Public | BindingFlags.Instance, new[] { typeof(bool) })
            ?? throw new InvalidOperationException("debug/replace_deck: Deck.Clear(bool) not found");
        // AddInternal's exact signature varies (sts2-cli's reference uses the
        // named-arg form `AddInternal(card, silent: true)`, implying at
        // least two parameters with optional ones). We resolve the `silent`
        // parameter index by name and fill the rest with Type.Missing.
        var addInternal = deckType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == "AddInternal" && m.GetParameters().Length >= 2)
            .FirstOrDefault(m => m.GetParameters().Any(p => p.Name == "silent" && p.ParameterType == typeof(bool)))
            ?? throw new InvalidOperationException("debug/replace_deck: Deck.AddInternal(..., bool silent, ...) not found");
        var addInternalSilentIdx = addInternal.GetParameters()
            .Select((p, i) => (p, i)).First(t => t.p.Name == "silent" && t.p.ParameterType == typeof(bool)).i;
        var addInternalCardIdx = 0; // first parameter is the card by convention

        // 1. Untrack & clear. Snapshot the list first since RemoveCard may
        // mutate the underlying collection.
        if (_deckCards.GetValue(deck) is System.Collections.IEnumerable existingCards)
        {
            var existing = new List<object>();
            foreach (var c in existingCards) if (c is not null) existing.Add(c);
            if (removeCard is not null)
            {
                foreach (var c in existing) removeCard.Invoke(handle.RunState, new[] { c });
            }
        }
        clear.Invoke(deck, new object[] { /* silent: */ true });

        // 2. Add the requested cards. Unknown ids surface as a clean error
        // (we unwrap TargetInvocationException so the caller sees the
        // engine's original exception message, not the reflection wrapper).
        var added = new List<string>(cards.Count);
        var addInternalParamCount = addInternal.GetParameters().Length;
        foreach (var (id, upgradeLevel) in cards)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new InvalidOperationException("debug/replace_deck: cardId must be non-empty");
            if (upgradeLevel < 0)
                throw new InvalidOperationException($"debug/replace_deck: upgradeLevel must be >= 0 (got {upgradeLevel} for {id})");

            var modelId = _modelIdCtor.Invoke(new object?[] { "CARD", id });
            object? canonical;
            try
            {
                canonical = getByIdCard.Invoke(null, new[] { modelId });
            }
            catch (TargetInvocationException tie) when (tie.InnerException is not null)
            {
                throw new InvalidOperationException(
                    $"debug/replace_deck: unknown or invalid card id \"{id}\" — {tie.InnerException.Message}");
            }
            if (canonical is null)
                throw new InvalidOperationException($"debug/replace_deck: unknown card id \"{id}\"");

            object card;
            try
            {
                card = createCard.Invoke(handle.RunState, new[] { canonical, handle.Player })
                    ?? throw new InvalidOperationException($"debug/replace_deck: RunState.CreateCard returned null for \"{id}\"");
            }
            catch (TargetInvocationException tie) when (tie.InnerException is not null)
            {
                throw new InvalidOperationException(
                    $"debug/replace_deck: RunState.CreateCard failed for \"{id}\" — {tie.InnerException.Message}");
            }

            if (upgradeLevel > 0)
            {
                if (upgradeInternal is null || finalizeUpgradeInternal is null)
                    throw new InvalidOperationException(
                        $"debug/replace_deck: CardModel.UpgradeInternal / FinalizeUpgradeInternal not bound; can't apply upgradeLevel={upgradeLevel} to \"{id}\"");
                for (var i = 0; i < upgradeLevel; i++)
                {
                    upgradeInternal.Invoke(card, null);
                    finalizeUpgradeInternal.Invoke(card, null);
                }
            }

            var addArgs = new object?[addInternalParamCount];
            for (var i = 0; i < addInternalParamCount; i++) addArgs[i] = Type.Missing;
            addArgs[addInternalCardIdx] = card;
            addArgs[addInternalSilentIdx] = /* silent: */ true;
            addInternal.Invoke(deck, addArgs);
            added.Add(id);
        }
        _syncCtx?.Pump();
        return added;
    }

    private void AdvanceAfterRewardsConsumed(RunHandle handle)
    {
        if (_pendingRewards is null || _pendingRewards.Count > 0) return;
        _pendingRewards = null;
        // Now the legacy escape path is correct: combat is over, no decisions
        // left, push us back to MapRoom (or whatever the engine flips to).
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

    // Read the current map point's PointType, or null if we're not standing
    // on a map point (pre-EnterAct, between rooms, stale coord). Used by
    // BuildSnapshot to flip CombatRoom → BossRoom for the act-boss combat,
    // which the engine itself reports as a plain CombatRoom.
    private string? ReadCurrentMapPointTypeName(object runState)
    {
        var map = _runStateMap.GetValue(runState);
        if (map is null) return null;
        var currentCoord = _runStateCurrentMapCoord.GetValue(runState);
        if (currentCoord is null) return null;
        var currentPoint = _mapGetPoint.Invoke(map, new[] { currentCoord });
        if (currentPoint is null) return null;
        return _mapPointPointType.GetValue(currentPoint)?.ToString();
    }

    private bool IsCurrentMapPointBoss(object runState)
        => string.Equals(ReadCurrentMapPointTypeName(runState), "Boss", StringComparison.Ordinal);

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

    // LocalContext.NetId is exposed as either a static property or a static
    // field depending on the game version. The Bind layer captures whichever
    // it found; this helper hides the discriminator at call sites.
    private void WriteLocalContextNetId(object netId)
    {
        switch (_localContextNetIdMember)
        {
            case PropertyInfo p:
                p.SetValue(null, netId);
                break;
            case FieldInfo f:
                f.SetValue(null, netId);
                break;
            default:
                throw new InvalidOperationException(
                    $"LocalContext.NetId binding is neither PropertyInfo nor FieldInfo (got {_localContextNetIdMember.GetType().Name})");
        }
    }

    // Diagnostic shortcut: create a Player without booting a full run. Used
    // by --probe-run-state. Wire callers should use StartIroncladRun instead.
    public object CreateIroncladRun(ulong seed) =>
        _createIroncladRun.Invoke(null, new object?[] { _unlockStateAll, seed })
            ?? throw new InvalidOperationException("Player.CreateForNewRun returned null");

    public static Sts2Bindings Bind(Assembly sts2, InlineSynchronizationContext? syncCtx = null)
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

        // LocalContext is a static type; its NetId is settable through either
        // a property or a public field. Prefer property + setter; fall back to
        // a public static field. Type-mismatch (NetId vs ulong vs custom NetId
        // struct) is OK — we just hand back the value read off netService.NetId,
        // which by construction has the right runtime type.
        var localContextType = Require(sts2, "MegaCrit.Sts2.Core.Multiplayer.LocalContext");
        MemberInfo localContextNetIdMember =
            (MemberInfo?)localContextType.GetProperty("NetId", BindingFlags.Public | BindingFlags.Static)
            ?? localContextType.GetField("NetId", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException($"{localContextType.FullName}.NetId (static property or field) not found");

        var setUpTest = SoleOverload(runManagerType, "SetUpTest");
        var isInProgress = RequireProperty(runManagerType, "IsInProgress");
        var cleanUp = runManagerType.GetMethod("CleanUp",
                BindingFlags.Public | BindingFlags.Instance, new[] { typeof(bool) })
            ?? throw new InvalidOperationException("RunManager.CleanUp(bool) not found");
        var generateRooms = NoArgInstance(runManagerType, "GenerateRooms");
        var launch = NoArgInstance(runManagerType, "Launch");
        var finalize = NoArgInstance(runManagerType, "FinalizeStartingRelics");
        var enterAct = SoleOverload(runManagerType, "EnterAct");
        var enterNextAct = NoArgInstance(runManagerType, "EnterNextAct");
        var enterMapCoord = SoleOverload(runManagerType, "EnterMapCoord");

        var extraFields = RequireProperty(runStateType, "ExtraFields");
        var extraFieldsType = extraFields.PropertyType;
        var startedWithNeow = RequireProperty(extraFieldsType, "StartedWithNeow");

        var playerGold = RequireProperty(playerType, "Gold");
        var playerCreature = RequireProperty(playerType, "Creature");
        var playerDeck = RequireProperty(playerType, "Deck");
        var playerNetId = RequireProperty(playerType, "NetId");

        // Player.Relics is soft-bound: element type and its Id property are
        // discovered through reachability. Missing pieces degrade to an
        // empty Relics list on snapshots rather than failing bootstrap.
        var playerRelics = playerType.GetProperty("Relics", BindingFlags.Public | BindingFlags.Instance);
        PropertyInfo? relicIdProp = null;
        if (playerRelics is not null)
        {
            var relicElementType = ExtractElementType(playerRelics.PropertyType);
            if (relicElementType is not null)
            {
                relicIdProp = relicElementType.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
            }
        }

        // Player.PotionSlots is also soft-bound: missing → empty OwnedPotions
        // and run/use_potion throws a typed WireException at call time. The
        // PotionModel element type carries TargetType (reuses the same enum
        // as cards) and EnqueueManualUse(Creature) — the engine path
        // RunPlayer.OnSelectActiveItem uses for in-combat potion drinks.
        var playerPotionSlots = playerType.GetProperty("PotionSlots", BindingFlags.Public | BindingFlags.Instance);
        PropertyInfo? potionTargetType = null;
        PropertyInfo? potionPassesUsabilityCheck = null;
        MethodInfo? potionEnqueueManualUse = null;
        if (playerPotionSlots is not null)
        {
            var potionElementType = ExtractElementType(playerPotionSlots.PropertyType);
            if (potionElementType is not null)
            {
                potionTargetType = potionElementType.GetProperty("TargetType", BindingFlags.Public | BindingFlags.Instance);
                potionPassesUsabilityCheck = potionElementType.GetProperty("PassesCustomUsabilityCheck", BindingFlags.Public | BindingFlags.Instance);
                potionEnqueueManualUse = potionElementType.GetMethod("EnqueueManualUse", BindingFlags.Public | BindingFlags.Instance);
            }
        }
        var creatureCurrentHp = RequireProperty(playerCreature.PropertyType, "CurrentHp");
        var creatureMaxHp = RequireProperty(playerCreature.PropertyType, "MaxHp");
        // Backing fields for direct write (debug/set_hp). sts2-cli's
        // SetPlayer uses `_currentHp` and `_maxHp`; mirror that and
        // tolerate either casing for resilience. Soft-bound — debug/set_hp
        // throws a typed WireException at call time if either is absent.
        var creatureCurrentHpField = playerCreature.PropertyType.GetField("_currentHp", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? playerCreature.PropertyType.GetField("currentHp", BindingFlags.NonPublic | BindingFlags.Instance);
        var creatureMaxHpField = playerCreature.PropertyType.GetField("_maxHp", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? playerCreature.PropertyType.GetField("maxHp", BindingFlags.NonPublic | BindingFlags.Instance);
        var deckCards = RequireProperty(playerDeck.PropertyType, "Cards");
        var currentRoom = RequireProperty(runStateType, "CurrentRoom");
        var actFloor = RequireProperty(runStateType, "ActFloor");
        var currentActIndex = RequireProperty(runStateType, "CurrentActIndex");
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
        var rewards = BindRewards(sts2, runManagerType, playerType);

        // Rest-site surface. Each piece is soft-bound: the host still boots
        // even if RestSiteSynchronizer or RestSiteRoom moves, the wire just
        // surfaces an empty AvailableRestSiteOptions list and SelectRestSite-
        // Option throws when called against a null binding. Walks:
        //   RunManager.RestSiteSynchronizer.ChooseLocalOption(optionIndex)
        //   RestSiteRoom.Options → element type → OptionId / IsEnabled
        var runManagerRestSiteSync = runManagerType.GetProperty("RestSiteSynchronizer", BindingFlags.Public | BindingFlags.Instance);
        MethodInfo? restSiteSyncChooseLocalOption = null;
        if (runManagerRestSiteSync is not null)
        {
            var restSyncType = runManagerRestSiteSync.PropertyType;
            restSiteSyncChooseLocalOption = restSyncType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "ChooseLocalOption"
                    && m.GetParameters().Length == 1
                    && m.GetParameters()[0].ParameterType == typeof(int));
        }
        PropertyInfo? restSiteRoomOptions = null;
        PropertyInfo? restSiteOptionOptionId = null;
        PropertyInfo? restSiteOptionIsEnabled = null;
        var restSiteRoomLookup = Sts2Reflection.FindType(sts2, "MegaCrit.Sts2.Core.Rooms.RestSiteRoom");
        if (restSiteRoomLookup.Found && restSiteRoomLookup.Type is not null)
        {
            restSiteRoomOptions = restSiteRoomLookup.Type.GetProperty("Options", BindingFlags.Public | BindingFlags.Instance);
            if (restSiteRoomOptions is not null)
            {
                var optionElementType = ExtractElementType(restSiteRoomOptions.PropertyType);
                if (optionElementType is not null)
                {
                    restSiteOptionOptionId = optionElementType.GetProperty("OptionId", BindingFlags.Public | BindingFlags.Instance);
                    restSiteOptionIsEnabled = optionElementType.GetProperty("IsEnabled", BindingFlags.Public | BindingFlags.Instance);
                }
            }
        }

        // Treasure-room surface. Soft-bound — a missing piece degrades the
        // wire to "leave_treasure_room throws" rather than failing
        // bootstrap. See the field-block comment above LeaveTreasureRoom
        // for the discovered flow.
        Type? treasureRoomType = null;
        MethodInfo? treasureRoomDoNormalRewards = null;
        MethodInfo? treasureRoomDoExtraRewards = null;
        var treasureRoomLookup = Sts2Reflection.FindType(sts2, "MegaCrit.Sts2.Core.Rooms.TreasureRoom");
        if (treasureRoomLookup.Found && treasureRoomLookup.Type is not null)
        {
            treasureRoomType = treasureRoomLookup.Type;
            treasureRoomDoNormalRewards = treasureRoomType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "DoNormalRewards" && m.GetParameters().Length == 0);
            treasureRoomDoExtraRewards = treasureRoomType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "DoExtraRewardsIfNeeded" && m.GetParameters().Length == 0);
        }
        var runManagerTreasureRoomRelicSync = runManagerType.GetProperty(
            "TreasureRoomRelicSynchronizer", BindingFlags.Public | BindingFlags.Instance);
        PropertyInfo? treasureSyncCurrentRelics = null;
        MethodInfo? treasureSyncCompleteWithNoRelics = null;
        if (runManagerTreasureRoomRelicSync is not null)
        {
            var syncType = runManagerTreasureRoomRelicSync.PropertyType;
            treasureSyncCurrentRelics = syncType.GetProperty("CurrentRelics", BindingFlags.Public | BindingFlags.Instance);
            treasureSyncCompleteWithNoRelics = syncType.GetMethod("CompleteWithNoRelics",
                BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
        }

        // Merchant-room surface. Soft-bound — a missing piece degrades the
        // wire to "empty merchant items" / "buy/leave throw" rather than
        // failing bootstrap. See the field-block comment above the
        // _merchant* declarations for the discovered flow.
        Type? merchantRoomType = null;
        PropertyInfo? merchantRoomInventory = null;
        var merchantRoomLookup = Sts2Reflection.FindType(sts2, "MegaCrit.Sts2.Core.Rooms.MerchantRoom");
        if (merchantRoomLookup.Found && merchantRoomLookup.Type is not null)
        {
            merchantRoomType = merchantRoomLookup.Type;
            merchantRoomInventory = merchantRoomType.GetProperty("Inventory", BindingFlags.Public | BindingFlags.Instance);
        }

        PropertyInfo? merchantInventoryAllEntries = null;
        Type? merchantEntryType = null;
        PropertyInfo? merchantEntryCost = null;
        PropertyInfo? merchantEntryEnoughGold = null;
        PropertyInfo? merchantEntryIsStocked = null;
        MethodInfo? merchantEntryOnTryPurchaseWrapper = null;
        if (merchantRoomInventory is not null)
        {
            var inventoryType = merchantRoomInventory.PropertyType;
            merchantInventoryAllEntries = inventoryType.GetProperty("AllEntries", BindingFlags.Public | BindingFlags.Instance);
            // The element type of AllEntries (IEnumerable<MerchantEntry>) is
            // the base MerchantEntry. Use it to bind the shared shape; the
            // per-kind subtype lookups below cast against the entries the
            // engine actually yields.
            if (merchantInventoryAllEntries is not null)
            {
                var elementType = ExtractElementType(merchantInventoryAllEntries.PropertyType);
                if (elementType is not null)
                {
                    merchantEntryType = elementType;
                    merchantEntryCost = elementType.GetProperty("Cost", BindingFlags.Public | BindingFlags.Instance);
                    merchantEntryEnoughGold = elementType.GetProperty("EnoughGold", BindingFlags.Public | BindingFlags.Instance);
                    merchantEntryIsStocked = elementType.GetProperty("IsStocked", BindingFlags.Public | BindingFlags.Instance);
                    // OnTryPurchaseWrapper is overloaded on the base (2-arg:
                    // inventory, ignoreCost) and on MerchantCardRemovalEntry
                    // (3-arg: inventory, ignoreCost, cancelable). Bind the
                    // 2-arg form here; the buy path uses MethodBase.Invoke
                    // against the entry's own type so a 3-arg override is
                    // picked up via virtual dispatch on the runtime entry.
                    merchantEntryOnTryPurchaseWrapper = elementType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                        .FirstOrDefault(m => m.Name == "OnTryPurchaseWrapper" && m.GetParameters().Length == 2);
                }
            }
        }

        Type? merchantCardEntryType = null;
        PropertyInfo? merchantCardEntryCreationResult = null;
        PropertyInfo? cardCreationResultCard = null;
        PropertyInfo? cardModelId = null;
        var cardEntryLookup = Sts2Reflection.FindType(sts2, "MegaCrit.Sts2.Core.Entities.Merchant.MerchantCardEntry");
        if (cardEntryLookup.Found && cardEntryLookup.Type is not null)
        {
            merchantCardEntryType = cardEntryLookup.Type;
            merchantCardEntryCreationResult = merchantCardEntryType.GetProperty("CreationResult", BindingFlags.Public | BindingFlags.Instance);
            if (merchantCardEntryCreationResult is not null)
            {
                var creationType = merchantCardEntryCreationResult.PropertyType;
                cardCreationResultCard = creationType.GetProperty("Card", BindingFlags.Public | BindingFlags.Instance);
                if (cardCreationResultCard is not null)
                {
                    cardModelId = cardCreationResultCard.PropertyType.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
                }
            }
        }

        Type? merchantRelicEntryType = null;
        PropertyInfo? merchantRelicEntryModel = null;
        PropertyInfo? relicModelId = null;
        var relicEntryLookup = Sts2Reflection.FindType(sts2, "MegaCrit.Sts2.Core.Entities.Merchant.MerchantRelicEntry");
        if (relicEntryLookup.Found && relicEntryLookup.Type is not null)
        {
            merchantRelicEntryType = relicEntryLookup.Type;
            merchantRelicEntryModel = merchantRelicEntryType.GetProperty("Model", BindingFlags.Public | BindingFlags.Instance);
            if (merchantRelicEntryModel is not null)
            {
                relicModelId = merchantRelicEntryModel.PropertyType.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
            }
        }

        Type? merchantPotionEntryType = null;
        PropertyInfo? merchantPotionEntryModel = null;
        PropertyInfo? potionModelId = null;
        var potionEntryLookup = Sts2Reflection.FindType(sts2, "MegaCrit.Sts2.Core.Entities.Merchant.MerchantPotionEntry");
        if (potionEntryLookup.Found && potionEntryLookup.Type is not null)
        {
            merchantPotionEntryType = potionEntryLookup.Type;
            merchantPotionEntryModel = merchantPotionEntryType.GetProperty("Model", BindingFlags.Public | BindingFlags.Instance);
            if (merchantPotionEntryModel is not null)
            {
                potionModelId = merchantPotionEntryModel.PropertyType.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
            }
        }

        Type? merchantCardRemovalEntryType = null;
        var cardRemovalEntryLookup = Sts2Reflection.FindType(sts2, "MegaCrit.Sts2.Core.Entities.Merchant.MerchantCardRemovalEntry");
        if (cardRemovalEntryLookup.Found && cardRemovalEntryLookup.Type is not null)
        {
            merchantCardRemovalEntryType = cardRemovalEntryLookup.Type;
        }

        return new Sts2Bindings(sts2, new BindingState(
            playerType, createIroncladRun, unlockAll,
            new InvocationPlan(createForTest), runManagerInstance, netServiceType,
            localContextNetIdMember,
            setUpTest, isInProgress, cleanUp, extraFields, startedWithNeow,
            generateRooms, launch, finalize, enterAct, enterNextAct, enterMapCoord, mapCoordType,
            playerGold, playerCreature, playerDeck, playerNetId,
            playerRelics, relicIdProp,
            playerPotionSlots, potionTargetType,
            potionPassesUsabilityCheck, potionEnqueueManualUse,
            creatureCurrentHp, creatureMaxHp,
            creatureCurrentHpField, creatureMaxHpField,
            deckCards,
            currentRoom, actFloor, currentActIndex, isGameOver,
            runStateMap, runStateCurrentMapCoord, mapStartingMapPoint, mapGetPoint,
            mapPointChildren, mapPointCoord, mapPointPointType,
            mapCoordColField, mapCoordRowField,
            runManagerEventSync, eventSyncGetLocalEvent, eventIsFinished, eventCurrentOptions,
            eventOptionTextKey, eventOptionIsLocked, eventOptionChosen,
            proceedFromTerminalRewards, enterRoom, mapRoomType2,
            runManagerRestSiteSync, restSiteSyncChooseLocalOption,
            restSiteRoomOptions, restSiteOptionOptionId, restSiteOptionIsEnabled,
            treasureRoomType,
            treasureRoomDoNormalRewards, treasureRoomDoExtraRewards,
            runManagerTreasureRoomRelicSync, treasureSyncCurrentRelics,
            treasureSyncCompleteWithNoRelics,
            merchantRoomType, merchantRoomInventory,
            merchantInventoryAllEntries,
            merchantEntryCost, merchantEntryEnoughGold,
            merchantEntryIsStocked, merchantEntryOnTryPurchaseWrapper,
            merchantCardEntryType, merchantCardEntryCreationResult,
            cardCreationResultCard, cardModelId,
            merchantRelicEntryType, merchantRelicEntryModel, relicModelId,
            merchantPotionEntryType, merchantPotionEntryModel, potionModelId,
            merchantCardRemovalEntryType,
            combat, rewards), syncCtx);
    }

    // Reward surface discovery. Tries the well-known FQNs first (matching
    // sts2-cli's RunSimulator imports); falls back to simple-name scans via
    // Sts2Reflection.FindType. Each piece is independently nullable so a
    // moved type degrades the wire to "no rewards visible" rather than
    // failing bootstrap.
    private static RewardBindings BindRewards(Assembly sts2, Type runManagerType, Type playerType)
    {
        Type? rewardsSetType = null;
        ConstructorInfo? rewardsSetCtor = null;
        MethodInfo? withRewardsFromRoom = null;
        MethodInfo? generateWithoutOffering = null;
        MethodInfo? rewardOnSelectWrapper = null;
        var rewardsSetLookup = Sts2Reflection.FindType(sts2, "MegaCrit.Sts2.Core.Rewards.RewardsSet");
        if (rewardsSetLookup.Found)
        {
            rewardsSetType = rewardsSetLookup.Type!;
            rewardsSetCtor = rewardsSetType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(c => c.GetParameters().Length == 1 && c.GetParameters()[0].ParameterType == playerType);
            withRewardsFromRoom = rewardsSetType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "WithRewardsFromRoom" && m.GetParameters().Length == 1);
            generateWithoutOffering = rewardsSetType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "GenerateWithoutOffering" && m.GetParameters().Length == 0);
        }

        Type? cardRewardType = null;
        PropertyInfo? cardRewardCards = null, cardRewardCanSkip = null;
        MethodInfo? cardRewardOnSkipped = null;
        var cardRewardLookup = Sts2Reflection.FindType(sts2, "MegaCrit.Sts2.Core.Rewards.CardReward");
        if (cardRewardLookup.Found)
        {
            cardRewardType = cardRewardLookup.Type!;
            cardRewardCards = cardRewardType.GetProperty("Cards", BindingFlags.Public | BindingFlags.Instance);
            cardRewardCanSkip = cardRewardType.GetProperty("CanSkip", BindingFlags.Public | BindingFlags.Instance);
            cardRewardOnSkipped = cardRewardType.GetMethod("OnSkipped",
                BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
            // OnSelectWrapper lives on the Reward base; pull from CardReward since
            // we know it inherits from Reward (and CardReward is the most likely
            // entry-point for the lookup to succeed).
            rewardOnSelectWrapper = cardRewardType.GetMethod("OnSelectWrapper",
                BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
        }

        Type? goldRewardType = null;
        PropertyInfo? goldRewardAmount = null;
        var goldLookup = Sts2Reflection.FindType(sts2, "MegaCrit.Sts2.Core.Rewards.GoldReward");
        if (goldLookup.Found)
        {
            goldRewardType = goldLookup.Type!;
            // Amount is the most likely property name; try common variants.
            goldRewardAmount = goldRewardType.GetProperty("Amount", BindingFlags.Public | BindingFlags.Instance)
                ?? goldRewardType.GetProperty("Gold", BindingFlags.Public | BindingFlags.Instance)
                ?? goldRewardType.GetProperty("GoldAmount", BindingFlags.Public | BindingFlags.Instance);
        }

        Type? potionRewardType = null;
        PropertyInfo? potionRewardPotionId = null;
        var potionLookup = Sts2Reflection.FindType(sts2, "MegaCrit.Sts2.Core.Rewards.PotionReward");
        if (potionLookup.Found)
        {
            potionRewardType = potionLookup.Type!;
            potionRewardPotionId = potionRewardType.GetProperty("PotionId", BindingFlags.Public | BindingFlags.Instance)
                ?? potionRewardType.GetProperty("Potion", BindingFlags.Public | BindingFlags.Instance)
                ?? potionRewardType.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
        }

        Type? relicRewardType = null;
        PropertyInfo? relicRewardRelicId = null;
        var relicLookup = Sts2Reflection.FindType(sts2, "MegaCrit.Sts2.Core.Rewards.RelicReward");
        if (relicLookup.Found)
        {
            relicRewardType = relicLookup.Type!;
            relicRewardRelicId = relicRewardType.GetProperty("RelicId", BindingFlags.Public | BindingFlags.Instance)
                ?? relicRewardType.GetProperty("Relic", BindingFlags.Public | BindingFlags.Instance)
                ?? relicRewardType.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
        }

        var rewardSync = runManagerType.GetProperty("RewardSynchronizer", BindingFlags.Public | BindingFlags.Instance);
        MethodInfo? syncLocalObtainedCard = null;
        if (rewardSync is not null)
        {
            syncLocalObtainedCard = rewardSync.PropertyType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "SyncLocalObtainedCard" && m.GetParameters().Length == 1);
        }

        // CardPileCmd.Add(card, PileType.Deck) — sts2-cli's RunSimulator uses
        // this static helper to route a reward card through the engine's
        // listener pipeline (relics that trigger on card-obtain, multiplayer
        // sync, etc.). Soft-bind: a missing CardPileCmd or PileType still
        // boots; only the catalog/probe-rewards path requires it.
        MethodInfo? cardPileCmdAdd = null;
        object? pileTypeDeckValue = null;
        var cardPileCmdLookup = Sts2Reflection.FindType(sts2, "MegaCrit.Sts2.Core.Commands.CardPileCmd");
        var pileTypeLookup = Sts2Reflection.FindType(sts2, "MegaCrit.Sts2.Core.Entities.Cards.PileType");
        if (cardPileCmdLookup.Found && pileTypeLookup.Found)
        {
            cardPileCmdAdd = cardPileCmdLookup.Type!
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m =>
                {
                    if (m.Name != "Add") return false;
                    var ps = m.GetParameters();
                    return ps.Length >= 2 && ps[1].ParameterType == pileTypeLookup.Type;
                });
            try { pileTypeDeckValue = Enum.Parse(pileTypeLookup.Type!, "Deck"); }
            catch { /* enum value renamed — leave null, catalog will report it */ }
        }

        // RelicCmd.Obtain(relicModel, player) + ModelDb.GetById<RelicModel>(ModelId)
        // — engine path for granting a relic to the player after the run has
        // started. Used by debug/give_relic to inject regression-test fixtures
        // (e.g. on-card-obtain relics like LuckyFysh) without simulating a
        // treasure room. Soft-bound: the host still boots if any piece is
        // missing, but debug/give_relic surfaces an error.
        MethodInfo? relicCmdObtain = null;
        MethodInfo? modelDbGetByIdRelic = null;
        ConstructorInfo? modelIdCtor = null;
        MethodInfo? relicModelToMutable = null;
        var relicModelLookup = Sts2Reflection.FindType(sts2, "MegaCrit.Sts2.Core.Models.RelicModel");
        var modelDbLookup = Sts2Reflection.FindType(sts2, "MegaCrit.Sts2.Core.Models.ModelDb");
        var modelIdLookup = Sts2Reflection.FindType(sts2, "MegaCrit.Sts2.Core.Models.ModelId");
        var relicCmdLookup = Sts2Reflection.FindType(sts2, "MegaCrit.Sts2.Core.Commands.RelicCmd");
        if (relicModelLookup.Found && modelDbLookup.Found && modelIdLookup.Found && relicCmdLookup.Found)
        {
            modelIdCtor = modelIdLookup.Type!.GetConstructors()
                .FirstOrDefault(c =>
                {
                    var ps = c.GetParameters();
                    return ps.Length == 2 && ps[0].ParameterType == typeof(string) && ps[1].ParameterType == typeof(string);
                });
            var getByIdGeneric = modelDbLookup.Type!.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "GetById" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1);
            if (getByIdGeneric is not null)
                modelDbGetByIdRelic = getByIdGeneric.MakeGenericMethod(relicModelLookup.Type!);
            relicCmdObtain = relicCmdLookup.Type!.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m =>
                {
                    if (m.Name != "Obtain" || m.IsGenericMethod) return false;
                    var ps = m.GetParameters();
                    return ps.Length >= 2 && ps[0].ParameterType == relicModelLookup.Type && ps[1].ParameterType == playerType;
                });
            // Canonical models from ModelDb are immutable; ToMutable() returns
            // a per-run instance that's safe to pass into RelicCmd.Obtain.
            // Without it the engine throws CanonicalModelException.
            relicModelToMutable = relicModelLookup.Type!.GetMethod("ToMutable", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
        }

        return new RewardBindings(
            rewardsSetCtor, withRewardsFromRoom, generateWithoutOffering,
            rewardOnSelectWrapper,
            cardRewardType, cardRewardCards, cardRewardCanSkip, cardRewardOnSkipped,
            goldRewardType, goldRewardAmount,
            potionRewardType, potionRewardPotionId,
            relicRewardType, relicRewardRelicId,
            rewardSync, syncLocalObtainedCard,
            cardPileCmdAdd, pileTypeDeckValue,
            relicCmdObtain, modelDbGetByIdRelic, modelIdCtor,
            relicModelToMutable);
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
        MethodInfo? cmCheckWinCondition = null;
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
            // CheckWinCondition is the engine's "look at the live state, decide
            // if combat should end" entry point — its state machine surfaces in
            // sts2.dll as `<CheckWinCondition>d__113`. debug/kill_all_enemies
            // writes HP=0 on enemies (which removes them from the alive
            // enumeration) and then calls this so the engine notices and
            // routes through EndCombatInternal. Resolved by name only;
            // overloads, if any, are filtered down at the call site.
            cmCheckWinCondition = cmType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name == "CheckWinCondition");
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
        // sts2's Intent class hierarchy: AbstractIntent → {AttackIntent
        // (abstract, has DamageCalc:Func<int> + Repeats:int), DefendIntent,
        // BuffIntent, …}. AttackIntent's two concrete subclasses are
        // SingleAttackIntent and MultiAttackIntent. DamageCalc is the
        // engine-side, modifier-aware damage computation — invoking it
        // yields the current expected damage including the monster's
        // Strength and any player Vulnerable. Repeats is the hit count
        // (1 for SingleAttackIntent, N for MultiAttackIntent). We resolve
        // these on AttackIntent so both subclasses inherit the lookup.
        Type? attackIntentType = null;
        PropertyInfo? attackIntentDamageCalc = null;
        PropertyInfo? attackIntentRepeats = null;
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

            attackIntentType = Sts2Reflection.FindType(sts2, "MegaCrit.Sts2.Core.MonsterMoves.Intents.AttackIntent").Type;
            if (attackIntentType is not null)
            {
                attackIntentDamageCalc = attackIntentType.GetProperty("DamageCalc", BindingFlags.Public | BindingFlags.Instance);
                attackIntentRepeats = attackIntentType.GetProperty("Repeats", BindingFlags.Public | BindingFlags.Instance);
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
            attackIntentType, attackIntentDamageCalc, attackIntentRepeats,
            playerCmdEndTurn, playCardActionType, playCardActionCtor,
            actionQueueSet, enqueueWithoutSync,
            actionExecutor, actionExecutorIsRunning, actionExecutorFinished,
            cmCheckWinCondition);
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
        MemberInfo LocalContextNetIdMember,
        InvocationPlan RunManagerSetUpTest, PropertyInfo RunManagerIsInProgress, MethodInfo RunManagerCleanUp,
        PropertyInfo RunStateExtraFields, PropertyInfo ExtraFieldsStartedWithNeow,
        MethodInfo RunManagerGenerateRooms, MethodInfo RunManagerLaunch, MethodInfo RunManagerFinalizeStartingRelics,
        InvocationPlan RunManagerEnterAct, MethodInfo RunManagerEnterNextAct,
        InvocationPlan RunManagerEnterMapCoord, Type MapCoordType,
        PropertyInfo PlayerGold, PropertyInfo PlayerCreature, PropertyInfo PlayerDeck, PropertyInfo PlayerNetId,
        PropertyInfo? PlayerRelics, PropertyInfo? RelicId,
        PropertyInfo? PlayerPotionSlots, PropertyInfo? PotionTargetType,
        PropertyInfo? PotionPassesUsabilityCheck, MethodInfo? PotionEnqueueManualUse,
        PropertyInfo CreatureCurrentHp, PropertyInfo CreatureMaxHp,
        FieldInfo? CreatureCurrentHpField, FieldInfo? CreatureMaxHpField,
        PropertyInfo DeckCards,
        PropertyInfo RunStateCurrentRoom, PropertyInfo RunStateActFloor,
        PropertyInfo RunStateCurrentActIndex, PropertyInfo RunStateIsGameOver,
        PropertyInfo RunStateMap, PropertyInfo RunStateCurrentMapCoord, PropertyInfo MapStartingMapPoint,
        MethodInfo MapGetPoint, PropertyInfo MapPointChildren, FieldInfo MapPointCoord,
        PropertyInfo MapPointPointType, FieldInfo MapCoordColField, FieldInfo MapCoordRowField,
        PropertyInfo RunManagerEventSynchronizer, MethodInfo EventSyncGetLocalEvent,
        PropertyInfo EventIsFinished, PropertyInfo EventCurrentOptions,
        PropertyInfo? EventOptionTextKey, PropertyInfo EventOptionIsLocked, MethodInfo EventOptionChosen,
        MethodInfo? RunManagerProceedFromTerminalRewards, MethodInfo? RunManagerEnterRoom, Type? MapRoomType,
        PropertyInfo? RunManagerRestSiteSynchronizer, MethodInfo? RestSiteSyncChooseLocalOption,
        PropertyInfo? RestSiteRoomOptions, PropertyInfo? RestSiteOptionOptionId, PropertyInfo? RestSiteOptionIsEnabled,
        Type? TreasureRoomType,
        MethodInfo? TreasureRoomDoNormalRewards, MethodInfo? TreasureRoomDoExtraRewards,
        PropertyInfo? RunManagerTreasureRoomRelicSync, PropertyInfo? TreasureSyncCurrentRelics,
        MethodInfo? TreasureSyncCompleteWithNoRelics,
        Type? MerchantRoomType, PropertyInfo? MerchantRoomInventory,
        PropertyInfo? MerchantInventoryAllEntries,
        PropertyInfo? MerchantEntryCost, PropertyInfo? MerchantEntryEnoughGold,
        PropertyInfo? MerchantEntryIsStocked, MethodInfo? MerchantEntryOnTryPurchaseWrapper,
        Type? MerchantCardEntryType, PropertyInfo? MerchantCardEntryCreationResult,
        PropertyInfo? CardCreationResultCard, PropertyInfo? CardModelId,
        Type? MerchantRelicEntryType, PropertyInfo? MerchantRelicEntryModel, PropertyInfo? RelicModelId,
        Type? MerchantPotionEntryType, PropertyInfo? MerchantPotionEntryModel, PropertyInfo? PotionModelId,
        Type? MerchantCardRemovalEntryType,
        CombatBindings Combat, RewardBindings Rewards);

    // Post-combat reward surface. Soft-bound — if RewardsSet (or its members)
    // can't be located, ReadRewardsState returns null and the auto-advance
    // falls back to the legacy proceed-and-skip path. Each reward subtype is
    // optional too; rewards we can't classify still surface as Unknown so the
    // wire never silently drops a pending decision.
    private sealed record RewardBindings(
        ConstructorInfo? RewardsSetCtor,
        MethodInfo? RewardsSetWithRewardsFromRoom, MethodInfo? RewardsSetGenerateWithoutOffering,
        MethodInfo? RewardOnSelectWrapper,
        Type? CardRewardType, PropertyInfo? CardRewardCards, PropertyInfo? CardRewardCanSkip,
        MethodInfo? CardRewardOnSkipped,
        Type? GoldRewardType, PropertyInfo? GoldRewardAmount,
        Type? PotionRewardType, PropertyInfo? PotionRewardPotionId,
        Type? RelicRewardType, PropertyInfo? RelicRewardRelicId,
        PropertyInfo? RunManagerRewardSynchronizer,
        MethodInfo? RewardSyncSyncLocalObtainedCard,
        MethodInfo? CardPileCmdAdd, object? PileTypeDeckValue,
        MethodInfo? RelicCmdObtain, MethodInfo? ModelDbGetByIdRelic, ConstructorInfo? ModelIdCtor,
        MethodInfo? RelicModelToMutable);

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
        Type? AttackIntentType, PropertyInfo? AttackIntentDamageCalc, PropertyInfo? AttackIntentRepeats,
        MethodInfo? PlayerCmdEndTurn, Type? PlayCardActionType, ConstructorInfo? PlayCardActionCtor,
        PropertyInfo? RunManagerActionQueueSet, MethodInfo? ActionQueueSetEnqueueWithoutSynchronizing,
        PropertyInfo? RunManagerActionExecutor, PropertyInfo? ActionExecutorIsRunning,
        MethodInfo? ActionExecutorFinishedExecutingActions,
        MethodInfo? CombatManagerCheckWinCondition);
}
