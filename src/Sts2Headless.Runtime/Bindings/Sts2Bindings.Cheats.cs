using System.Reflection;

namespace Sts2Headless.Runtime.Bindings;

// AD-7 debug surface. Each method here corresponds to one wire entry
// under `debug/*` (give_relic / set_hp / kill_all_enemies / read_deck /
// replace_deck), gated by --enable-debug at the host. The cheats bypass
// the normal event pipeline (on-hit relics, on-kill listeners, deck-
// change subscribers) so tests can stage state without unrelated side
// effects firing. Wire-level validation lives in CheatHostMethods; this
// layer trusts its inputs.
public sealed partial class Sts2Bindings
{
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

    // Test affordance: attach an affliction to a card in the player's
    // hand via the engine path (CardCmd.Afflict(AfflictionModel,
    // CardModel, Decimal)). Mirrors card / event side-effects that
    // afflict cards naturally — e.g. Hexed appearing on a played card.
    public string AfflictCard(RunHandle handle, string afflictionId, int handIndex, int amount) =>
        AttachToCard(
            handle: handle,
            kindWire: "AFFLICTION",
            modelTypeName: "MegaCrit.Sts2.Core.Models.AfflictionModel",
            attachMethodName: "Afflict",
            id: afflictionId,
            handIndex: handIndex,
            amount: amount,
            errorContext: "debug/afflict_card",
            kindShortName: "affliction");

    // Test affordance: attach an enchantment to a card in the player's
    // hand via CardCmd.Enchant(EnchantmentModel, CardModel, Decimal).
    // Same shape as AfflictCard.
    public string EnchantCard(RunHandle handle, string enchantmentId, int handIndex, int amount) =>
        AttachToCard(
            handle: handle,
            kindWire: "ENCHANTMENT",
            modelTypeName: "MegaCrit.Sts2.Core.Models.EnchantmentModel",
            attachMethodName: "Enchant",
            id: enchantmentId,
            handIndex: handIndex,
            amount: amount,
            errorContext: "debug/enchant_card",
            kindShortName: "enchantment");

    // Shared internals for affliction / enchantment attachment. Both
    // resolve a model by id via ModelDb.GetById<TModel>(ModelId), make
    // it mutable (via MutableClone since these AbstractModel subtypes
    // don't expose typed ToMutable like EncounterModel does), then call
    // the matching CardCmd static method which takes
    // (Model, CardModel, Decimal). Returns the wire id of the target
    // card so the caller can name what got hit.
    private string AttachToCard(
        RunHandle handle,
        string kindWire,
        string modelTypeName,
        string attachMethodName,
        string id,
        int handIndex,
        int amount,
        string errorContext,
        string kindShortName)
    {
        if (_modelIdCtor is null)
            throw new InvalidOperationException($"{errorContext}: ModelId(string,string) ctor not bound");

        var modelType = Sts2.GetType(modelTypeName)
            ?? throw new InvalidOperationException($"{errorContext}: {modelTypeName} type not found");
        var cardCmdType = Sts2.GetType("MegaCrit.Sts2.Core.Commands.CardCmd")
            ?? throw new InvalidOperationException($"{errorContext}: CardCmd type not found");
        var modelDbType = Sts2.GetType("MegaCrit.Sts2.Core.Models.ModelDb")
            ?? throw new InvalidOperationException($"{errorContext}: ModelDb type not found");
        var cardModelType = Sts2.GetType("MegaCrit.Sts2.Core.Models.CardModel")
            ?? throw new InvalidOperationException($"{errorContext}: CardModel type not found");

        // Find the CardCmd.Afflict / Enchant non-generic overload:
        // (TModel, CardModel, Decimal). The generic overloads need a
        // compile-time type — we resolve to a runtime model from a
        // string id, so non-generic is the right surface.
        var attach = cardCmdType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == attachMethodName
                && !m.IsGenericMethodDefinition
                && m.GetParameters().Length == 3
                && m.GetParameters()[0].ParameterType.IsAssignableFrom(modelType)
                && m.GetParameters()[1].ParameterType.IsAssignableFrom(cardModelType))
            ?? throw new InvalidOperationException(
                $"{errorContext}: CardCmd.{attachMethodName}({modelTypeName.Split('.').Last()}, CardModel, Decimal) not found");

        // Resolve the card under the player's HandIndex via the same
        // reflection chain the wire's snapshot path uses:
        //   Player.PlayerCombatState → Hand → Hand.Cards (IEnumerable).
        // The engine's "CombatState" object on CombatManager is a
        // different shape — that one doesn't expose Hand directly.
        if (_playerCombatState is null || _pcsHand is null || _handCards is null)
            throw new InvalidOperationException($"{errorContext}: no active combat (combat surface not bound)");
        var pcs = _playerCombatState.GetValue(handle.Player)
            ?? throw new InvalidOperationException($"{errorContext}: no active combat (Player.PlayerCombatState is null)");
        var hand = _pcsHand.GetValue(pcs)
            ?? throw new InvalidOperationException($"{errorContext}: no active combat (PlayerCombatState.Hand is null)");
        if (_handCards.GetValue(hand) is not System.Collections.IEnumerable handEnum)
            throw new InvalidOperationException($"{errorContext}: Hand.Cards is not enumerable");

        object? targetCard = null;
        var i = 0;
        foreach (var card in handEnum)
        {
            if (card is null) continue;
            if (i == handIndex) { targetCard = card; break; }
            i++;
        }
        if (targetCard is null)
            throw new InvalidOperationException($"{errorContext}: no card at hand index {handIndex}");

        // Read the card's wire id for the wire result.
        var cardIdProp = targetCard.GetType().GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
        var cardModelId = cardIdProp?.GetValue(targetCard);
        var cardEntryProp = cardModelId?.GetType().GetProperty("Entry", BindingFlags.Public | BindingFlags.Instance);
        var cardWireId = cardEntryProp?.GetValue(cardModelId) as string ?? "<unknown>";

        // Resolve the model by id.
        var getByIdGeneric = modelDbType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "GetById" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1)
            ?? throw new InvalidOperationException($"{errorContext}: ModelDb.GetById<T>(ModelId) not found");
        var getByIdSpec = getByIdGeneric.MakeGenericMethod(modelType);
        var modelIdObj = _modelIdCtor.Invoke(new object?[] { kindWire, id });
        object? canonical;
        try
        {
            canonical = getByIdSpec.Invoke(null, new[] { modelIdObj });
        }
        catch (TargetInvocationException tie) when (tie.InnerException is not null)
        {
            throw new InvalidOperationException(
                $"{errorContext}: unknown {kindShortName} id \"{id}\" — {tie.InnerException.Message}");
        }
        if (canonical is null)
            throw new InvalidOperationException($"{errorContext}: unknown {kindShortName} id \"{id}\"");

        // MutableClone the canonical (AfflictionModel / EnchantmentModel
        // require mutable; same reason as PowerCmd.Apply does).
        var mutableClone = canonical.GetType().GetMethod("MutableClone", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes)
            ?? throw new InvalidOperationException($"{errorContext}: AbstractModel.MutableClone() not found on canonical {kindShortName}");
        var model = mutableClone.Invoke(canonical, null)
            ?? throw new InvalidOperationException($"{errorContext}: MutableClone returned null for \"{id}\"");

        try
        {
            var result = attach.Invoke(null, new object?[] { model, targetCard, (decimal)amount });
            if (result is Task t) t.GetAwaiter().GetResult();
        }
        catch (TargetInvocationException tie) when (tie.InnerException is not null)
        {
            throw new InvalidOperationException(
                $"{errorContext}: CardCmd.{attachMethodName} failed for \"{id}\" on card \"{cardWireId}\" — {tie.InnerException.Message}");
        }
        _syncCtx?.Pump();
        return cardWireId;
    }

    // Test affordance: apply a power to the player or to an enemy via
    // the engine path (PowerCmd.Apply(model, target, amount, source,
    // cardSource: null, useFinalAmount: false)). Mirrors how the engine
    // dispatches power application from a card play.
    //
    // Target resolution:
    //   * enemyIndex null  → handle.Player.Creature (the player)
    //   * enemyIndex >= 0  → the i-th alive enemy in CombatManager.State
    //
    // Returns (appliedAmount, targetDescription). Reads the post-apply
    // power amount off the target's Powers collection by matching the
    // PowerModel's id; if the power is already present, this reports
    // the post-stack value, which is what the wire caller would see on
    // the next snapshot.
    //
    // Throws InvalidOperationException for the wire handler to translate
    // to InvalidParams on:
    //   * "unknown power id ..."
    //   * "no active combat"
    //   * "no enemy at index N"
    public (int AppliedAmount, string TargetDescription) ApplyPower(
        RunHandle handle, string powerId, int amount, int? enemyIndex)
    {
        if (_modelIdCtor is null)
            throw new InvalidOperationException("debug/apply_power: ModelId(string,string) ctor not bound");

        var powerModelType = Sts2.GetType("MegaCrit.Sts2.Core.Models.PowerModel")
            ?? throw new InvalidOperationException("debug/apply_power: PowerModel type not found");
        var powerCmdType = Sts2.GetType("MegaCrit.Sts2.Core.Commands.PowerCmd")
            ?? throw new InvalidOperationException("debug/apply_power: PowerCmd type not found");
        var modelDbType = Sts2.GetType("MegaCrit.Sts2.Core.Models.ModelDb")
            ?? throw new InvalidOperationException("debug/apply_power: ModelDb type not found");
        var creatureType = Sts2.GetType("MegaCrit.Sts2.Core.Entities.Creatures.Creature")
            ?? throw new InvalidOperationException("debug/apply_power: Creature type not found");

        // ModelDb.GetById<PowerModel>(ModelId) — same shape as every
        // other content kind.
        var getByIdGeneric = modelDbType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "GetById" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1)
            ?? throw new InvalidOperationException("debug/apply_power: ModelDb.GetById<T>(ModelId) not found");
        var getByIdPower = getByIdGeneric.MakeGenericMethod(powerModelType);

        // Pick the non-generic Apply: (PowerModel, Creature, Decimal,
        // Creature, CardModel, Boolean). The two generic Apply<T>
        // overloads need a compile-time type — we have a string id
        // and resolve to PowerModel at runtime, so non-generic is the
        // right surface.
        var apply = powerCmdType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "Apply"
                && !m.IsGenericMethodDefinition
                && m.GetParameters().Length == 6
                && m.GetParameters()[0].ParameterType.IsAssignableFrom(powerModelType))
            ?? throw new InvalidOperationException(
                "debug/apply_power: PowerCmd.Apply(PowerModel, Creature, Decimal, Creature, CardModel, Boolean) not found");

        var modelId = _modelIdCtor.Invoke(new object?[] { "POWER", powerId });
        object? canonical;
        try
        {
            canonical = getByIdPower.Invoke(null, new[] { modelId });
        }
        catch (TargetInvocationException tie) when (tie.InnerException is not null)
        {
            throw new InvalidOperationException(
                $"debug/apply_power: unknown power id \"{powerId}\" — {tie.InnerException.Message}");
        }
        if (canonical is null)
            throw new InvalidOperationException($"debug/apply_power: unknown power id \"{powerId}\"");

        // PowerCmd.Apply calls AssertMutable on the model first, so we
        // need a mutable copy of the canonical. PowerModel doesn't
        // expose the typed ToMutable() helper that EncounterModel /
        // RelicModel / PotionModel have; the universal path is
        // AbstractModel.MutableClone() which returns an AbstractModel
        // (cast back to PowerModel for the Apply signature). Caught by
        // DebugApplyPowerTests during initial implementation.
        var mutableClone = canonical.GetType().GetMethod("MutableClone", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes)
            ?? throw new InvalidOperationException("debug/apply_power: AbstractModel.MutableClone() not found on canonical PowerModel");
        var powerModel = mutableClone.Invoke(canonical, null)
            ?? throw new InvalidOperationException($"debug/apply_power: MutableClone returned null for \"{powerId}\"");

        // Resolve target.
        object targetCreature;
        string targetDesc;
        if (enemyIndex is null)
        {
            targetCreature = _playerCreature.GetValue(handle.Player)
                ?? throw new InvalidOperationException("debug/apply_power: Player.Creature was null");
            targetDesc = "Player";
        }
        else
        {
            if (_combatManagerInstance is null || _combatManagerDebugOnlyGetState is null
                || _combatStateEnemies is null || _enemyIsAlive is null)
                throw new InvalidOperationException("debug/apply_power: no active combat (combat surface not bound)");
            var cm = _combatManagerInstance.GetValue(null)
                ?? throw new InvalidOperationException("debug/apply_power: no active combat (CombatManager.Instance is null)");
            var state = _combatManagerDebugOnlyGetState.Invoke(cm, null)
                ?? throw new InvalidOperationException("debug/apply_power: no active combat");
            if (_combatStateEnemies.GetValue(state) is not System.Collections.IEnumerable enemies)
                throw new InvalidOperationException("debug/apply_power: combat state has no enemies");
            object? hit = null;
            var i = 0;
            foreach (var enemy in enemies)
            {
                if (enemy is null) continue;
                if (!(bool)_enemyIsAlive.GetValue(enemy)!) continue;
                if (i == enemyIndex.Value) { hit = enemy; break; }
                i++;
            }
            if (hit is null)
                throw new InvalidOperationException($"debug/apply_power: no enemy at index {enemyIndex.Value} (alive only)");
            targetCreature = hit;
            targetDesc = $"Enemy:{enemyIndex.Value}";
        }

        // The source defaults to the player. The engine's typical call
        // site uses the card-playing creature; for a free apply, player
        // is the safe default — the apply hooks see "player applied this
        // to target" which matches the natural game shape.
        var sourceCreature = _playerCreature.GetValue(handle.Player)
            ?? throw new InvalidOperationException("debug/apply_power: Player.Creature was null (source)");

        // Apply(model, target, amount, source, cardSource=null, useFinalAmount=false).
        // Decimal amount because PowerCmd takes Decimal across the board
        // (the engine fractional-power math). int → Decimal via conversion.
        try
        {
            var task = apply.Invoke(null, new object?[]
            {
                powerModel,
                targetCreature,
                (decimal)amount,
                sourceCreature,
                /* cardSource: */ null,
                /* useFinalAmount: */ false,
            });
            if (task is Task t) t.GetAwaiter().GetResult();
        }
        catch (TargetInvocationException tie) when (tie.InnerException is not null)
        {
            throw new InvalidOperationException(
                $"debug/apply_power: PowerCmd.Apply failed for \"{powerId}\" — {tie.InnerException.Message}");
        }
        _syncCtx?.Pump();

        // Read back the resulting amount by walking the target's Powers
        // collection and matching against powerId. Many powers stack,
        // so the post-apply amount may exceed the requested amount; that's
        // honest information for the caller.
        var resultingAmount = ReadPowerAmount(targetCreature, creatureType, powerId);
        return (resultingAmount, targetDesc);
    }

    private static int ReadPowerAmount(object creature, Type creatureType, string powerId)
    {
        var powersProp = creatureType.GetProperty("Powers", BindingFlags.Public | BindingFlags.Instance);
        if (powersProp?.GetValue(creature) is not System.Collections.IEnumerable powers) return 0;
        foreach (var power in powers)
        {
            if (power is null) continue;
            var idProp = power.GetType().GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
            var modelIdObj = idProp?.GetValue(power);
            var entryProp = modelIdObj?.GetType().GetProperty("Entry", BindingFlags.Public | BindingFlags.Instance);
            if (entryProp?.GetValue(modelIdObj) is string entry && string.Equals(entry, powerId, StringComparison.Ordinal))
            {
                var amountProp = power.GetType().GetProperty("Amount", BindingFlags.Public | BindingFlags.Instance);
                if (amountProp?.GetValue(power) is { } raw)
                {
                    // Amount is Decimal in the engine; cast to int for
                    // the wire result (whole-number amounts are the
                    // common case; non-integer powers will floor).
                    return Convert.ToInt32(raw);
                }
                return 0;
            }
        }
        return 0;
    }

    // Test affordance: force-start a specific event against the active
    // run. Mirrors StartCombat's shape but for EventRoom, which takes a
    // single EventModel ctor argument (verified via the potion-probe-
    // shaped inspection on the pinned game version):
    //   1. ModelDb.GetById<EventModel>(new ModelId("EVENT", id))
    //   2. eventModel.ToMutable()                      (the engine guards
    //      EventRoom ctor against canonical models for shared events)
    //   3. new EventRoom(mutableEvent)
    //   4. RunManager.Instance.EnterRoom(room) — await
    //   5. _syncCtx.Pump() + DrainActionExecutor    (same handshake the
    //      relic / kill-all-enemies / start-combat helpers use)
    //
    // Bypasses the map-progression path so callers can stage any event
    // from any room state. The engine doesn't validate Act / Character
    // compatibility, so off-act events work but may surface in an
    // unusual run state (e.g. an Act-3 event opened on Act-1 floor 1
    // still runs through its CurrentOptions).
    //
    // Returns (currentRoomType-as-string, optionsCount) so the wire
    // result tells the caller what landed without a follow-up state read.
    public (string CurrentRoomType, int OptionsCount) StartEvent(RunHandle handle, string eventId)
    {
        if (_modelIdCtor is null)
            throw new InvalidOperationException("debug/start_event: ModelId(string,string) ctor not bound");
        if (_runManagerEnterRoom is null)
            throw new InvalidOperationException("debug/start_event: RunManager.EnterRoom not bound");

        var eventModelType = Sts2.GetType("MegaCrit.Sts2.Core.Models.EventModel")
            ?? throw new InvalidOperationException("debug/start_event: EventModel type not found");
        var eventRoomType = Sts2.GetType("MegaCrit.Sts2.Core.Rooms.EventRoom")
            ?? throw new InvalidOperationException("debug/start_event: EventRoom type not found");
        var modelDbType = Sts2.GetType("MegaCrit.Sts2.Core.Models.ModelDb")
            ?? throw new InvalidOperationException("debug/start_event: ModelDb type not found");

        var getByIdGeneric = modelDbType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "GetById" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1)
            ?? throw new InvalidOperationException("debug/start_event: ModelDb.GetById<T>(ModelId) not found");
        var getByIdEvent = getByIdGeneric.MakeGenericMethod(eventModelType);

        var modelId = _modelIdCtor.Invoke(new object?[] { "EVENT", eventId });
        object? canonical;
        try
        {
            canonical = getByIdEvent.Invoke(null, new[] { modelId });
        }
        catch (TargetInvocationException tie) when (tie.InnerException is not null)
        {
            throw new InvalidOperationException(
                $"debug/start_event: unknown event id \"{eventId}\" — {tie.InnerException.Message}");
        }
        if (canonical is null)
            throw new InvalidOperationException($"debug/start_event: unknown event id \"{eventId}\"");

        // EventRoom takes the CANONICAL EventModel, not the mutable one —
        // opposite of CombatRoom's posture, verified empirically: passing
        // a mutable surfaces "Mutable model used in incorrect place." The
        // EventRoom ctor internally manages the canonical → LocalMutableEvent
        // bridge (visible on its CanonicalEvent + LocalMutableEvent
        // properties).
        var eventRoomCtor = eventRoomType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(c =>
            {
                var ps = c.GetParameters();
                return ps.Length == 1 && ps[0].ParameterType.IsAssignableFrom(eventModelType);
            })
            ?? throw new InvalidOperationException("debug/start_event: EventRoom(EventModel) ctor not found");

        object eventRoom;
        try
        {
            eventRoom = eventRoomCtor.Invoke(new[] { canonical })
                ?? throw new InvalidOperationException($"debug/start_event: EventRoom ctor returned null for \"{eventId}\"");
        }
        catch (TargetInvocationException tie) when (tie.InnerException is not null)
        {
            throw new InvalidOperationException(
                $"debug/start_event: EventRoom construction failed for \"{eventId}\" — {tie.InnerException.Message}");
        }

        try
        {
            var enterResult = _runManagerEnterRoom.Invoke(handle.RunManager, new[] { (object)eventRoom });
            if (enterResult is Task t) t.GetAwaiter().GetResult();
        }
        catch (TargetInvocationException tie) when (tie.InnerException is not null)
        {
            throw new InvalidOperationException(
                $"debug/start_event: RunManager.EnterRoom failed for \"{eventId}\" — {tie.InnerException.Message}");
        }
        _syncCtx?.Pump();
        DrainActionExecutor(handle);

        // Read-back: what room did we end up in, and how many options
        // are visible? The current-room name comes from RunState; the
        // options count goes through ReadAvailableEventOptions which
        // is the same path the snapshot uses.
        var currentRoom = _runStateCurrentRoom.GetValue(handle.RunState);
        var roomName = currentRoom?.GetType().Name ?? "<null>";
        var options = ReadAvailableEventOptions(handle.RunManager);
        return (roomName, options.Count);
    }

    // Test affordance: grant `potionId` to the player via the engine path
    // (PotionCmd.TryToProcure(PotionModel, Player, slot=-1)). Same shape as
    // GiveRelic but for potions. The engine picks the first empty slot
    // when the int argument is -1; the post-call PotionSlots walk locates
    // the landed slot so the wire can name it.
    //
    // Throws InvalidOperationException("unknown potion id ...") on a bad
    // id; the wire handler translates that to WireErrorCode.InvalidParams.
    // Returns (slotIndex, totalCount) for the caller to surface.
    public (int SlotIndex, int Count) GivePotion(RunHandle handle, string potionId)
    {
        if (_modelIdCtor is null)
            throw new InvalidOperationException("debug/give_potion: ModelId(string,string) ctor not bound — bootstrap likely failed");

        var potionModelType = Sts2.GetType("MegaCrit.Sts2.Core.Models.PotionModel")
            ?? throw new InvalidOperationException("debug/give_potion: PotionModel type not found");
        var potionCmdType = Sts2.GetType("MegaCrit.Sts2.Core.Commands.PotionCmd")
            ?? throw new InvalidOperationException("debug/give_potion: PotionCmd type not found");
        var modelDbType = Sts2.GetType("MegaCrit.Sts2.Core.Models.ModelDb")
            ?? throw new InvalidOperationException("debug/give_potion: ModelDb type not found");

        // ModelDb.GetById<PotionModel>(ModelId) — same shape as the relic /
        // encounter / card paths above.
        var getByIdGeneric = modelDbType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "GetById" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1)
            ?? throw new InvalidOperationException("debug/give_potion: ModelDb.GetById<T>(ModelId) not found");
        var getByIdPotion = getByIdGeneric.MakeGenericMethod(potionModelType);

        // Pick the 3-arg overload (PotionModel, Player, Int32). The 1-arg
        // overload (Player) picks a random potion — wrong shape for "give
        // me this specific id".
        var tryToProcure = potionCmdType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "TryToProcure"
                && m.GetParameters().Length == 3
                && m.GetParameters()[0].ParameterType.IsAssignableFrom(potionModelType)
                && m.GetParameters()[2].ParameterType == typeof(int))
            ?? throw new InvalidOperationException("debug/give_potion: PotionCmd.TryToProcure(PotionModel, Player, Int32) not found");

        var modelId = _modelIdCtor.Invoke(new object?[] { "POTION", potionId });
        object? canonical;
        try
        {
            canonical = getByIdPotion.Invoke(null, new[] { modelId });
        }
        catch (TargetInvocationException tie) when (tie.InnerException is not null)
        {
            throw new InvalidOperationException(
                $"debug/give_potion: unknown potion id \"{potionId}\" — {tie.InnerException.Message}");
        }
        if (canonical is null)
            throw new InvalidOperationException($"debug/give_potion: unknown potion id \"{potionId}\"");

        // PotionModel.ToMutable() bridges canonical → per-run-mutable. Same
        // CanonicalModelException posture as relics / encounters; if the
        // method isn't there we fall through with the canonical (engine may
        // accept it — defensive default).
        var toMutable = potionModelType.GetMethod("ToMutable", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
        var potionModel = toMutable is not null
            ? (toMutable.Invoke(canonical, null) ?? canonical)
            : canonical;

        // Invoke TryToProcure(potionModel, player, -1) and await. Slot=-1
        // is the engine's "first empty" sentinel; the result's `success`
        // field tells us whether a slot was found.
        object? procureResult;
        try
        {
            var task = tryToProcure.Invoke(null, new object?[] { potionModel, handle.Player, -1 });
            if (task is Task t)
            {
                t.GetAwaiter().GetResult();
                // Read Task<T>.Result reflectively — Task is non-generic in
                // our cast above. The wrapped value is PotionProcureResult.
                var resultProp = t.GetType().GetProperty("Result", BindingFlags.Public | BindingFlags.Instance);
                procureResult = resultProp?.GetValue(t);
            }
            else
            {
                procureResult = task;
            }
        }
        catch (TargetInvocationException tie) when (tie.InnerException is not null)
        {
            throw new InvalidOperationException(
                $"debug/give_potion: PotionCmd.TryToProcure failed for \"{potionId}\" — {tie.InnerException.Message}");
        }
        _syncCtx?.Pump();

        // Read success/failureReason fields so the wire surfaces honest
        // outcomes for "tried to procure a Necrobinder-only potion as
        // Ironclad" etc. PotionProcureResult is a class with public fields
        // (per the probe dump).
        if (procureResult is not null)
        {
            var successField = procureResult.GetType().GetField("success", BindingFlags.Public | BindingFlags.Instance);
            var success = successField?.GetValue(procureResult) as bool? ?? true;
            if (!success)
            {
                var reasonField = procureResult.GetType().GetField("failureReason", BindingFlags.Public | BindingFlags.Instance);
                var reason = reasonField?.GetValue(procureResult)?.ToString() ?? "<unknown>";
                throw new InvalidOperationException(
                    $"debug/give_potion: PotionCmd.TryToProcure returned success=false for \"{potionId}\" (reason: {reason})");
            }
        }

        // Locate the landed slot by walking PotionSlots and matching Id.Entry.
        // Empty slots are null; the granted potion lives at the first slot
        // whose model's Id.Entry equals potionId.
        var slotIndex = -1;
        var count = 0;
        if (_playerPotionSlots is not null && _playerPotionSlots.GetValue(handle.Player) is System.Collections.IEnumerable slots)
        {
            var i = 0;
            foreach (var slot in slots)
            {
                if (slot is not null)
                {
                    count++;
                    if (slotIndex < 0)
                    {
                        var idProp = slot.GetType().GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
                        var modelIdObj = idProp?.GetValue(slot);
                        var entryProp = modelIdObj?.GetType().GetProperty("Entry", BindingFlags.Public | BindingFlags.Instance);
                        if (entryProp?.GetValue(modelIdObj) is string entry && string.Equals(entry, potionId, StringComparison.Ordinal))
                        {
                            slotIndex = i;
                        }
                    }
                }
                i++;
            }
        }
        return (slotIndex, count);
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
    // Read the player's deck as (cardId, upgradeLevel) pairs, in
    // Deck.Cards insertion order. Mirrors ReplaceDeck's input shape so
    // a debug test can round-trip a replace → read assertion.
    //
    // CardId reads ModelId.Entry on the card's canonical model
    // (CardModel.Id.Entry returns the wire string id, e.g.
    // "POMMEL_STRIKE"). UpgradeLevel reads CurrentUpgradeLevel — 0 for
    // base, 1 for "+1", etc. — the same int CardModel.UpgradeInternal
    // increments. A null or unrecognised card surfaces a clean error
    // so a reflection drift doesn't quietly skew the test's assertion.
    public IReadOnlyList<(string CardId, int UpgradeLevel)> ReadDeck(RunHandle handle)
    {
        var deck = _playerDeck.GetValue(handle.Player)
            ?? throw new InvalidOperationException("debug/read_deck: Player.Deck was null");
        if (_deckCards.GetValue(deck) is not System.Collections.IEnumerable cards)
            return Array.Empty<(string, int)>();

        var result = new List<(string CardId, int UpgradeLevel)>();
        foreach (var card in cards)
        {
            if (card is null) continue;
            var cardType = card.GetType();
            var idProp = cardType.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"debug/read_deck: card type {cardType.FullName} has no Id property");
            var modelId = idProp.GetValue(card)
                ?? throw new InvalidOperationException($"debug/read_deck: card.Id was null on {cardType.FullName}");
            var entryProp = modelId.GetType().GetProperty("Entry", BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"debug/read_deck: ModelId type {modelId.GetType().FullName} has no Entry property");
            var cardId = entryProp.GetValue(modelId) as string
                ?? throw new InvalidOperationException($"debug/read_deck: ModelId.Entry returned non-string on {cardType.FullName}");

            var upgradeProp = cardType.GetProperty("CurrentUpgradeLevel", BindingFlags.Public | BindingFlags.Instance);
            var upgradeLevel = upgradeProp is null ? 0 : Convert.ToInt32(upgradeProp.GetValue(card) ?? 0);

            result.Add((cardId, upgradeLevel));
        }
        return result;
    }

    // Test affordance: force-start a specific combat against the chosen
    // `encounterId` (e.g. "SLIMES_NORMAL", "DOORMAKER_BOSS"). Mirrors the
    // engine path sts2-cli uses for its "/enter_room combat ..." command:
    //   1. ModelDb.GetById<EncounterModel>(new ModelId("ENCOUNTER", id))
    //   2. encounter.ToMutable()                       (engine guards
    //      CombatRoom ctor against canonical models)
    //   3. new CombatRoom(mutableEncounter, runState)
    //   4. RunManager.Instance.EnterRoom(room).GetAwaiter().GetResult()
    //   5. _syncCtx.Pump() + DrainActionExecutor       (same shape as the
    //      cheat-relic / kill-all-enemies post-mutation handshake).
    //
    // Bypasses the map-progression path so callers can stage any encounter
    // from any room state (MapRoom, RestRoom, even a previously-finished
    // CombatRoom). The engine does not validate Act/Character compatibility
    // at CombatRoom construction — starting Doormaker in Act 1 works, but
    // act-specific monster scenes that fail to bind via GodotStubs will
    // surface as a MissingMethodException, which is exactly the signal the
    // EveryEncounterSmokeTests sweep is built to catch.
    //
    // Reflection is inline (no BindingState changes) because the call is
    // test-only — same reasoning as ReplaceDeck's inline lookups.
    public (bool InProgress, int EnemyCount) StartCombat(RunHandle handle, string encounterId)
    {
        if (_modelIdCtor is null)
            throw new InvalidOperationException("debug/start_combat: ModelId(string,string) ctor not bound — bootstrap likely failed");
        if (_runManagerEnterRoom is null)
            throw new InvalidOperationException("debug/start_combat: RunManager.EnterRoom not bound — cannot drive room transition");

        var encounterModelType = Sts2.GetType("MegaCrit.Sts2.Core.Models.EncounterModel")
            ?? throw new InvalidOperationException("debug/start_combat: EncounterModel type not found in sts2 assembly");
        var combatRoomType = Sts2.GetType("MegaCrit.Sts2.Core.Rooms.CombatRoom")
            ?? throw new InvalidOperationException("debug/start_combat: CombatRoom type not found in sts2 assembly");
        var modelDbType = Sts2.GetType("MegaCrit.Sts2.Core.Models.ModelDb")
            ?? throw new InvalidOperationException("debug/start_combat: ModelDb type not found");

        var getByIdGeneric = modelDbType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "GetById" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1)
            ?? throw new InvalidOperationException("debug/start_combat: ModelDb.GetById<T>(ModelId) not found");
        var getByIdEncounter = getByIdGeneric.MakeGenericMethod(encounterModelType);

        var modelId = _modelIdCtor.Invoke(new object?[] { "ENCOUNTER", encounterId });
        object? canonical;
        try
        {
            canonical = getByIdEncounter.Invoke(null, new[] { modelId });
        }
        catch (TargetInvocationException tie) when (tie.InnerException is not null)
        {
            throw new InvalidOperationException(
                $"debug/start_combat: unknown encounter id \"{encounterId}\" — {tie.InnerException.Message}");
        }
        if (canonical is null)
            throw new InvalidOperationException($"debug/start_combat: unknown encounter id \"{encounterId}\"");

        // EncounterModel.ToMutable() is the engine's canonical→per-run-mutable
        // bridge; the CombatRoom ctor throws CanonicalModelException without it
        // (same shape as RelicCmd.Obtain in GiveRelic above).
        var toMutable = encounterModelType.GetMethod("ToMutable", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes)
            ?? throw new InvalidOperationException("debug/start_combat: EncounterModel.ToMutable() not found");
        var mutable = toMutable.Invoke(canonical, null)
            ?? throw new InvalidOperationException($"debug/start_combat: EncounterModel.ToMutable() returned null for \"{encounterId}\"");

        // CombatRoom(EncounterModel, RunState). The encounter parameter type
        // is the canonical EncounterModel (or any base); we accept any ctor
        // whose first parameter is assignable from EncounterModel and whose
        // second is the RunState type — defensive against signature jitter.
        var runStateType = handle.RunState.GetType();
        var combatRoomCtor = combatRoomType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(c =>
            {
                var ps = c.GetParameters();
                return ps.Length == 2
                    && ps[0].ParameterType.IsAssignableFrom(encounterModelType)
                    && ps[1].ParameterType.IsAssignableFrom(runStateType);
            })
            ?? throw new InvalidOperationException("debug/start_combat: CombatRoom(EncounterModel, RunState) ctor not found");

        object combatRoom;
        try
        {
            combatRoom = combatRoomCtor.Invoke(new[] { mutable, handle.RunState })
                ?? throw new InvalidOperationException($"debug/start_combat: CombatRoom ctor returned null for \"{encounterId}\"");
        }
        catch (TargetInvocationException tie) when (tie.InnerException is not null)
        {
            throw new InvalidOperationException(
                $"debug/start_combat: CombatRoom construction failed for \"{encounterId}\" — {tie.InnerException.Message}");
        }

        // Same EnterRoom + pump + drain shape Sts2Bindings.cs:813 uses to
        // exit combat into MapRoom; here we go the other direction. Await
        // any Task the engine returns so the room transition is synchronous
        // from the caller's perspective.
        try
        {
            var enterResult = _runManagerEnterRoom.Invoke(handle.RunManager, new[] { (object)combatRoom });
            if (enterResult is Task t) t.GetAwaiter().GetResult();
        }
        catch (TargetInvocationException tie) when (tie.InnerException is not null)
        {
            throw new InvalidOperationException(
                $"debug/start_combat: RunManager.EnterRoom failed for \"{encounterId}\" — {tie.InnerException.Message}");
        }
        _syncCtx?.Pump();
        DrainActionExecutor(handle);

        // Read-back: a successful start leaves CombatManager.IsInProgress=true
        // and at least one alive enemy. We surface both so the wire result
        // tells the caller whether the engine actually flipped into combat
        // (vs. silently no-op'd into some half-state).
        var inProgress = false;
        var enemyCount = 0;
        if (_combatManagerInstance is not null && _combatManagerIsInProgress is not null)
        {
            var cm = _combatManagerInstance.GetValue(null);
            if (cm is not null)
            {
                inProgress = (bool)_combatManagerIsInProgress.GetValue(cm)!;
                if (_combatManagerDebugOnlyGetState is not null && _combatStateEnemies is not null)
                {
                    var state = _combatManagerDebugOnlyGetState.Invoke(cm, null);
                    if (state is not null && _combatStateEnemies.GetValue(state) is System.Collections.IEnumerable enemies)
                    {
                        foreach (var e in enemies)
                        {
                            if (e is null) continue;
                            if (_enemyIsAlive is null || (bool)_enemyIsAlive.GetValue(e)!) enemyCount++;
                        }
                    }
                }
            }
        }
        return (inProgress, enemyCount);
    }

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
}
