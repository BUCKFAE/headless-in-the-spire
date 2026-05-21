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
    private readonly PropertyInfo? _cardIsUpgraded;
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

    // The card selector wired into sts2's CardSelectCmd.UseSelector hook.
    // Exposed so HostMethods can push per-request selection hints (the
    // optional `cardSelectIndices` on run/play_card) before the action runs
    // and clear leftover hints after. Null when bootstrap couldn't resolve
    // ICardSelector — in that case card-selecting cards revert to the
    // crashing baseline and the integration test for them stays red.
    public HeadlessCardSelector? CardSelector { get; }

    private Sts2Bindings(Assembly sts2, BindingState s, InlineSynchronizationContext? syncCtx, HeadlessCardSelector? cardSelector)
    {
        Sts2 = sts2;
        _syncCtx = syncCtx;
        CardSelector = cardSelector;
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
        _cardIsUpgraded = c.CardIsUpgraded;
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

    // StartIroncladRun moved to Sts2Bindings.Run.cs (+ WriteLocalContextNetId).
    // UsePotion moved to Sts2Bindings.Potion.cs.

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

    // ReadAvailableEventOptions + IsLocalEventFinished moved to Sts2Bindings.Events.cs.

    // ReadAvailableRestSiteOptions + SelectRestSiteOption moved to Sts2Bindings.Rest.cs.
    // LeaveTreasureRoom moved to Sts2Bindings.Treasure.cs.
    // EnterNextAct moved to Sts2Bindings.Map.cs.
    // ProceedEvent moved to Sts2Bindings.Events.cs.

    // ReadAvailableMerchantItems + ClassifyMerchantEntry + BuyMerchantItem
    // + LeaveMerchantRoom moved to Sts2Bindings.Merchant.cs.

    // SelectEventOption + AutoAdvanceFinishedEvent moved to Sts2Bindings.Events.cs.
    // ReadCurrentMapPointTypeName + IsCurrentMapPointBoss + ReadAvailableMapNodes
    // + AppendChildren + ToMapNode moved to Sts2Bindings.Map.cs.

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

    // EnterMapCoord + SetCoordComponent moved to Sts2Bindings.Map.cs.
    // WriteLocalContextNetId moved to Sts2Bindings.Run.cs.

    // Diagnostic shortcut: create a Player without booting a full run. Used
    // by --probe-run-state. Wire callers should use StartIroncladRun instead.
    public object CreateIroncladRun(ulong seed) =>
        _createIroncladRun.Invoke(null, new object?[] { _unlockStateAll, seed })
            ?? throw new InvalidOperationException("Player.CreateForNewRun returned null");

    public static Sts2Bindings Bind(Assembly sts2, InlineSynchronizationContext? syncCtx = null, HeadlessCardSelector? cardSelector = null)
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
            combat, rewards), syncCtx, cardSelector);
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
        PropertyInfo? cardId = null, cardEnergyCost = null, cardTargetType = null, cardIsUpgraded = null;
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
                        cardIsUpgraded = cardType.GetProperty("IsUpgraded", BindingFlags.Public | BindingFlags.Instance);
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
            cardId, cardEnergyCost, energyCostGetResolved, cardCanPlay, cardTargetType, cardIsUpgraded,
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
        MethodInfo? CardCanPlay, PropertyInfo? CardTargetType, PropertyInfo? CardIsUpgraded,
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
