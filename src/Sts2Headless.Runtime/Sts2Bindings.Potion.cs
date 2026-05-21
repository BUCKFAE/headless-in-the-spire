using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Runtime;

// Potion use. Mirrors run/play_card's shape: potionIndex is the wire index
// into ReadOwnedPotions (a dense list that skips empty slots — *not* the
// underlying PotionSlots index), and targetIndex is required when the
// potion's TargetType is AnyEnemy. ResolveAnyEnemyTarget and
// AutoAdvancePostCombat live in Sts2Bindings.Combat.cs.
public sealed partial class Sts2Bindings
{
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
}
