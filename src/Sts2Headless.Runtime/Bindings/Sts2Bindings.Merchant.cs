using System.Reflection;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Runtime.Bindings;

// Merchant-room operations. Wire flow:
//   * snapshot surfaces AvailableMerchantItems (via ReadAvailableMerchantItems);
//   * caller picks an index via BuyMerchantItem → MerchantEntry.OnTryPurchaseWrapper;
//   * LeaveMerchantRoom drives EnterRoom(MapRoom) — no engine auto-advance.
// All backed by the `_merchant*` / `_runManager*` fields declared in
// Sts2Bindings.cs. ReadEntryId is a shared helper kept in the main file.
public sealed partial class Sts2Bindings
{
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
    private (MerchantKind Kind, CardId? CardId, RelicId? RelicId, PotionId? PotionId)
        ClassifyMerchantEntry(object entry)
    {
        if (_merchantCardEntryType is not null && _merchantCardEntryType.IsInstanceOfType(entry))
        {
            CardId? cardId = null;
            if (_merchantCardEntryCreationResult is not null && _cardCreationResultCard is not null)
            {
                var creation = _merchantCardEntryCreationResult.GetValue(entry);
                var cardModel = creation is null ? null : _cardCreationResultCard.GetValue(creation);
                var wire = cardModel is not null ? ReadEntryId(_cardModelId, cardModel) : null;
                if (wire is not null) cardId = CardIdNames.FromWire(wire);
            }
            return (MerchantKind.Card, cardId, null, null);
        }
        if (_merchantRelicEntryType is not null && _merchantRelicEntryType.IsInstanceOfType(entry))
        {
            RelicId? relicId = null;
            if (_merchantRelicEntryModel is not null)
            {
                var model = _merchantRelicEntryModel.GetValue(entry);
                var wire = model is not null ? ReadEntryId(_relicModelId, model) : null;
                if (wire is not null) relicId = RelicIdNames.FromWire(wire);
            }
            return (MerchantKind.Relic, null, relicId, null);
        }
        if (_merchantPotionEntryType is not null && _merchantPotionEntryType.IsInstanceOfType(entry))
        {
            PotionId? potionId = null;
            if (_merchantPotionEntryModel is not null)
            {
                var model = _merchantPotionEntryModel.GetValue(entry);
                var wire = model is not null ? ReadEntryId(_potionModelId, model) : null;
                if (wire is not null) potionId = PotionIdNames.FromWire(wire);
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
}
