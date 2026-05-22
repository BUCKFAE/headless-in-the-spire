using System.Reflection;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Runtime.Bindings;

// Combat surface: end-of-turn pump, play-card enqueue, and the post-
// combat reward-generation handshake that turns "combat just ended" into
// pending RewardsState the wire surfaces. Lives in its own partial so
// the binding file isn't dominated by the EndTurn pump and the
// AutoAdvancePostCombat/TryGeneratePendingRewards split.
//
// Shared helpers (ReadRound, ResolveAnyEnemyTarget, AutoAdvancePostCombat,
// TryGeneratePendingRewards) stay private and live here even though
// non-combat callers (UsePotion, KillAllEnemies, the Catalog partial)
// invoke them — partial-class semantics give every Sts2Bindings.* file
// transparent access.
public sealed partial class Sts2Bindings
{
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
}
