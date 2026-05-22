using System.Reflection;

namespace Sts2Headless.Runtime.Bindings;

// Post-combat reward handshake: claim or skip pending rewards, route
// card picks through CardPileCmd.Add so on-obtain listeners fire, stamp
// CardChoices on the run-history entry so replays remember the offered
// alternatives, and drain the pending list back to MapRoom via
// AdvanceAfterRewardsConsumed.
//
// AutoAdvanceFinishedEvent stays in Sts2Bindings.cs — ProceedEvent and
// other room handlers share it.
public sealed partial class Sts2Bindings
{
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

    private void AdvanceAfterRewardsConsumed(RunHandle handle)
    {
        if (_pendingRewards is null || _pendingRewards.Count > 0) return;
        _pendingRewards = null;
        // Now the legacy escape path is correct: combat is over, no decisions
        // left, push us back to MapRoom (or whatever the engine flips to).
        AutoAdvanceFinishedEvent(handle.RunManager, handle.RunState);
    }
}
