using System.Reflection;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Runtime.Bindings;

// Read surface of Sts2Bindings. Snapshot the engine state into the wire
// RunSnapshot record and the supporting projections (combat / rewards /
// relics / potions). Lives in its own partial so the binding file isn't
// dominated by a single 380-line method tree.
//
// AD-4 still applies: every engine touch is reflective. The
// field/method-info handles ReadSnapshot uses are declared in
// Sts2Bindings.cs alongside the rest of the binding state.
public sealed partial class Sts2Bindings
{
    // Optional post-read transform applied to every snapshot the bindings
    // produce. The host injects a name-lookup-aware enricher
    // (SnapshotEnricher.WithDisplayNames) so inline DisplayName fields
    // come back filled. Kept as an opaque delegate so this layer stays
    // Content-agnostic — Sts2Headless.Content depends on Runtime, not
    // the other way around.
    private Func<RunSnapshot, RunSnapshot>? _snapshotPostProcessor;

    public void SetSnapshotPostProcessor(Func<RunSnapshot, RunSnapshot>? transform) =>
        _snapshotPostProcessor = transform;

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

        // Treasure room offering. GetTreasureOffering eagerly drives
        // TreasureRoom.DoNormalRewards on first call per-room, so callers
        // see the chest's relic before deciding whether to take or skip
        // via run/take_treasure / run/skip_treasure. Idempotent: subsequent snapshots
        // within the same room read the cached synchronizer state without
        // re-invoking DoNormalRewards.
        var availableTreasureRelics = roomType == RoomType.TreasureRoom
            ? GetTreasureOffering(handle)
            : Array.Empty<TreasureRelic>();

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

        var (bossWireId, secondBossWireId) = ReadActBosses(handle);
        EncounterId? bossId = bossWireId is not null ? EncounterIdNames.FromWire(bossWireId) : null;
        EncounterId? secondBossId = secondBossWireId is not null ? EncounterIdNames.FromWire(secondBossWireId) : null;
        var snapshot = new RunSnapshot(currentHp, maxHp, gold, deckSize, roomType, actFloor, currentActIndex, isGameOver, isVictory, isDead, availableNodes, availableEventOptions, availableRestSiteOptions, availableMerchantItems, availableTreasureRelics, combatState, rewardsState, relics, ownedPotions, bossId, secondBossId);
        return _snapshotPostProcessor is null ? snapshot : _snapshotPostProcessor(snapshot);
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

    private (RewardKind, bool, int?, PotionId?, RelicId?, IReadOnlyList<CardRewardOption>?) ProjectReward(object reward)
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
            var idWire = _potionRewardPotionId is not null
                ? ReadEntryId(_potionRewardPotionId, reward) ?? _potionRewardPotionId.GetValue(reward)?.ToString()
                : null;
            var id = idWire is not null ? PotionIdNames.FromWire(idWire) : (PotionId?)null;
            return (RewardKind.Potion, false, null, id, null, null);
        }
        if (_relicRewardType is not null && _relicRewardType.IsInstanceOfType(reward))
        {
            var idWire = _relicRewardRelicId is not null
                ? ReadEntryId(_relicRewardRelicId, reward) ?? _relicRewardRelicId.GetValue(reward)?.ToString()
                : null;
            var id = idWire is not null ? RelicIdNames.FromWire(idWire) : (RelicId?)null;
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
            var upgraded = _cardIsUpgraded is not null && (bool)(_cardIsUpgraded.GetValue(card) ?? false);
            result.Add(new Card(i, CardIdNames.FromWire(idWire), cost, canPlay, targetType, upgraded));
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
            var monsterIdWire = monster is not null ? ReadEntryId(_monsterId, monster) : null;
            var monsterId = monsterIdWire is not null
                ? MonsterIdNames.FromWire(monsterIdWire)
                : MonsterId.Unknown;
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
            var idWire = ReadEntryId(_powerId, power) ?? power.GetType().Name;
            var amount = _powerAmount is not null ? Convert.ToInt32(_powerAmount.GetValue(power)) : 0;
            result.Add(new Power(PowerIdNames.FromWire(idWire), amount));
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
            var idWire = ReadEntryId(_relicId, relic) ?? relic.GetType().Name;
            result.Add(new Relic(RelicIdNames.FromWire(idWire)));
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
            var idWire = (idProp is not null ? ReadEntryId(idProp, potion) : null)
                     ?? potion.GetType().Name;  // last-ditch: GetType().Name as before
            var target = _potionTargetType?.GetValue(potion) is { } tt
                ? ParseEnum<TargetType>(tt)
                : TargetType.Unknown;
            var canUse = _potionPassesUsabilityCheck?.GetValue(potion) is bool b ? b : true;
            result.Add(new OwnedPotion(idx, PotionIdNames.FromWire(idWire), target, canUse));
            idx++;
        }
        return result;
    }
}
