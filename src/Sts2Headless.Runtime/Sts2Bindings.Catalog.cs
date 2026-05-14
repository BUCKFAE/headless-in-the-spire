using System.Reflection;

namespace Sts2Headless.Runtime;

// Phase-1 cataloging surface — diagnostic-only entry points used by
// `--probe-natural-chain` to enumerate the gaps that surface when the engine
// runs its natural enemy-turn / reward chain with all NetIds aligned to 1uL
// (the contract sts2-cli proves works end-to-end).
//
// These methods deliberately bypass the safety nets in EndTurn / SelectReward
// (the manual ForceSwitchToEnemySide retry loop, the best-effort try/catch
// around OnSelectWrapper / OnSkipped). Production wire dispatch must NOT call
// them — they exist to drive the engine's natural code path so MissingMethod-
// Exceptions and NREs surface as observable gaps we can catalogue and patch.
//
// See documentation/research/natural-chain-gaps.md for the current catalog.
public sealed partial class Sts2Bindings
{
    public sealed record CatalogedGap(
        int Iteration,
        string Phase,
        string ExceptionType,
        string Message,
        IReadOnlyList<string> StackFrames);

    public sealed record CatalogResult(
        IReadOnlyList<CatalogedGap> Gaps,
        IReadOnlyList<CatalogedGap> UniqueGaps,
        bool Converged,
        int Iterations,
        string TerminalState,
        // Verbatim stderr written during the catalog run. sts2 wraps fire-
        // and-forget tasks in TaskHelper.LogTaskExceptions, which catches and
        // routes to Logger.Error → GD.PrintErr → Console.Error. Those
        // exceptions never bubble through our reflection invoke, so the only
        // way to see them is to tee the engine's stderr stream.
        string CapturedStderr);

    // Fire PlayerCmd.EndTurn(player, false) and pump the natural chain — no
    // ForceSwitchToEnemySide fallback. Each pump iteration is wrapped in a
    // try/catch that records exceptions and continues, so a single run yields
    // the full waterfall of gaps instead of bailing on the first one.
    //
    // Returns CatalogResult{ Gaps: every exception caught, UniqueGaps: dedup'd
    // by (type, message), Converged: true iff next-player-turn was reached,
    // Iterations: how many pump cycles ran, TerminalState: why the loop ended.
    //
    // The dedup is what makes the caller's life easier: each gap typically
    // surfaces 5–10 times in the same pump run as the engine retries the same
    // failing path. We keep the full list for forensic detail and the unique
    // list for the markdown checklist.
    public CatalogResult EndTurnAndCatalog(RunHandle handle, int maxIterations = 100)
    {
        if (_playerCmdEndTurn is null)
            throw new InvalidOperationException("PlayerCmd.EndTurn not bound");
        if (_combatManagerInstance is null)
            throw new InvalidOperationException("CombatManager.Instance not bound");
        var cm = _combatManagerInstance.GetValue(null)
            ?? throw new InvalidOperationException("CombatManager.Instance was null — not in combat");
        if (_combatManagerIsInProgress is null
            || !(bool)_combatManagerIsInProgress.GetValue(cm)!)
            throw new InvalidOperationException("combat is not in progress");

        var roundBefore = ReadRound(cm);
        var gaps = new List<CatalogedGap>();
        var iteration = 0;
        var terminal = "deadline";

        // Tee Console.Error so the engine's logger output (which carries the
        // fire-and-forget exceptions sts2 swallows internally) lands in our
        // catalog alongside any synchronously-raised exceptions.
        var captured = new StringWriter();
        var originalErr = Console.Error;
        var tee = new TeeTextWriter(originalErr, captured);
        Console.SetError(tee);
        try
        {

        // Build the EndTurn arg vector: PlayerCmd.EndTurn(player, canBackOut,
        // ...optional). Pass Type.Missing for trailing optionals so reflection
        // picks up their default values rather than null-ing a non-nullable
        // parameter. Same shape as production EndTurn.
        var paramCount = _playerCmdEndTurn.GetParameters().Length;
        var args = new object?[paramCount];
        args[0] = handle.Player;
        args[1] = false;
        for (var i = 2; i < paramCount; i++) args[i] = Type.Missing;

        try
        {
            var result = _playerCmdEndTurn.Invoke(null, args);
            if (result is Task t) t.GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            gaps.Add(MakeGap(0, "PlayerCmdEndTurn", ex));
        }

        for (iteration = 1; iteration < maxIterations; iteration++)
        {
            try { _syncCtx?.Pump(); }
            catch (Exception ex) { gaps.Add(MakeGap(iteration, "SyncCtxPump", ex)); }

            try { DrainActionExecutor(handle); }
            catch (Exception ex) { gaps.Add(MakeGap(iteration, "DrainActionExecutor", ex)); }

            if (!(bool)_combatManagerIsInProgress.GetValue(cm)!) { terminal = "combat-ended"; break; }
            if (_creatureIsDead is not null)
            {
                var creature = _playerCreature.GetValue(handle.Player);
                if (creature is not null && (bool)_creatureIsDead.GetValue(creature)!)
                {
                    terminal = "player-dead";
                    break;
                }
            }
            if (_combatManagerIsPlayPhase is not null
                && (bool)_combatManagerIsPlayPhase.GetValue(cm)!)
            {
                var roundNow = ReadRound(cm);
                if (roundNow > roundBefore) { terminal = "next-player-turn"; break; }
            }

            // Same-fault detection: if the last 8 iterations all reported the
            // same exception, the engine is stuck on a single gap and further
            // pumping won't surface anything new. Stop early.
            if (gaps.Count >= 8)
            {
                var tail = gaps.TakeLast(8).ToArray();
                if (tail.All(g => g.ExceptionType == tail[0].ExceptionType
                                  && g.Message == tail[0].Message))
                {
                    terminal = "stuck-on-gap";
                    break;
                }
            }
        }

        }
        finally
        {
            Console.SetError(originalErr);
        }

        var unique = gaps
            .GroupBy(g => (g.ExceptionType, g.Message))
            .Select(grp => grp.First())
            .ToArray();

        return new CatalogResult(
            gaps, unique,
            terminal == "next-player-turn", iteration, terminal,
            captured.ToString());
    }

    // Drive the natural reward-claim chain for a single pending reward without
    // the production safety nets. Mirrors the production SelectReward shape but:
    //   - Card rewards go through CardPileCmd.Add(card, PileType.Deck) instead
    //     of mutating Deck.Cards directly, so listeners (relics that trigger on
    //     card-obtain, multiplayer sync) fire end-to-end.
    //   - SyncLocalObtainedCard is invoked bare — no try/catch wrapper.
    //   - Non-card rewards call OnSelectWrapper bare — no try/catch wrapper.
    //
    // Each step is wrapped in catch-record-continue so a single run yields the
    // full waterfall instead of bailing on the first exception. Stderr is teed
    // so engine-swallowed exceptions (TaskHelper.LogTaskExceptions et al.) land
    // in the catalog alongside synchronous gaps.
    public CatalogResult SelectRewardAndCatalog(RunHandle handle, int rewardIndex, int? cardIndex)
    {
        if (_pendingRewards is null || _pendingRewards.Count == 0)
            throw new InvalidOperationException("no pending rewards to select");
        if (rewardIndex < 0 || rewardIndex >= _pendingRewards.Count)
            throw new ArgumentOutOfRangeException(nameof(rewardIndex));

        var reward = _pendingRewards[rewardIndex];
        var gaps = new List<CatalogedGap>();
        var captured = new StringWriter();
        var originalErr = Console.Error;
        var tee = new TeeTextWriter(originalErr, captured);
        Console.SetError(tee);
        try
        {
            if (_cardRewardType is not null && _cardRewardType.IsInstanceOfType(reward))
            {
                if (cardIndex is null)
                    throw new ArgumentException("card-kind reward requires cardIndex");
                CatalogClaimCardReward(handle, reward, cardIndex.Value, gaps);
            }
            else
            {
                try
                {
                    var mi = _rewardOnSelectWrapper
                        ?? throw new InvalidOperationException("Reward.OnSelectWrapper not bound");
                    var resolved = mi.DeclaringType?.IsInstanceOfType(reward) == true
                        ? mi
                        : reward.GetType().GetMethod("OnSelectWrapper",
                              System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                              Type.EmptyTypes)
                          ?? mi;
                    var result = resolved.Invoke(reward, Array.Empty<object?>());
                    if (result is Task t) t.GetAwaiter().GetResult();
                }
                catch (Exception ex) { gaps.Add(MakeGap(0, "OnSelectWrapper", ex)); }
            }

            try { _syncCtx?.Pump(); }
            catch (Exception ex) { gaps.Add(MakeGap(1, "SyncCtxPump", ex)); }
            try { DrainActionExecutor(handle); }
            catch (Exception ex) { gaps.Add(MakeGap(1, "DrainActionExecutor", ex)); }
        }
        finally
        {
            Console.SetError(originalErr);
        }

        _pendingRewards.RemoveAt(rewardIndex);

        var unique = gaps
            .GroupBy(g => (g.ExceptionType, g.Message))
            .Select(grp => grp.First())
            .ToArray();
        return new CatalogResult(gaps, unique, gaps.Count == 0, 1,
            gaps.Count == 0 ? "ok" : "threw", captured.ToString());
    }

    // Drive the natural skip chain — CardReward.OnSkipped() bare, no try/catch.
    public CatalogResult SkipRewardAndCatalog(RunHandle handle, int rewardIndex)
    {
        if (_pendingRewards is null || _pendingRewards.Count == 0)
            throw new InvalidOperationException("no pending rewards to skip");
        if (rewardIndex < 0 || rewardIndex >= _pendingRewards.Count)
            throw new ArgumentOutOfRangeException(nameof(rewardIndex));

        var reward = _pendingRewards[rewardIndex];
        if (_cardRewardType is null || !_cardRewardType.IsInstanceOfType(reward))
            throw new InvalidOperationException("only card rewards are skippable");

        var gaps = new List<CatalogedGap>();
        var captured = new StringWriter();
        var originalErr = Console.Error;
        var tee = new TeeTextWriter(originalErr, captured);
        Console.SetError(tee);
        try
        {
            try
            {
                var mi = _cardRewardOnSkipped
                    ?? throw new InvalidOperationException("CardReward.OnSkipped not bound");
                var result = mi.Invoke(reward, Array.Empty<object?>());
                if (result is Task t) t.GetAwaiter().GetResult();
            }
            catch (Exception ex) { gaps.Add(MakeGap(0, "OnSkipped", ex)); }

            try { _syncCtx?.Pump(); }
            catch (Exception ex) { gaps.Add(MakeGap(1, "SyncCtxPump", ex)); }
            try { DrainActionExecutor(handle); }
            catch (Exception ex) { gaps.Add(MakeGap(1, "DrainActionExecutor", ex)); }
        }
        finally
        {
            Console.SetError(originalErr);
        }

        _pendingRewards.RemoveAt(rewardIndex);

        var unique = gaps
            .GroupBy(g => (g.ExceptionType, g.Message))
            .Select(grp => grp.First())
            .ToArray();
        return new CatalogResult(gaps, unique, gaps.Count == 0, 1,
            gaps.Count == 0 ? "ok" : "threw", captured.ToString());
    }

    private void CatalogClaimCardReward(RunHandle handle, object cardReward, int cardIndex, List<CatalogedGap> gaps)
    {
        if (_cardRewardCards?.GetValue(cardReward) is not System.Collections.IEnumerable cardsEnumerable)
            throw new InvalidOperationException("CardReward.Cards was null or not enumerable");
        var cards = new List<object>();
        foreach (var c in cardsEnumerable) if (c is not null) cards.Add(c);
        if (cardIndex < 0 || cardIndex >= cards.Count)
            throw new ArgumentOutOfRangeException(nameof(cardIndex));
        var picked = cards[cardIndex];

        // Engine path: CardPileCmd.Add(card, PileType.Deck). This routes through
        // listener pipelines (relic on-card-obtain hooks fire) — the whole point
        // of the probe is to surface the gaps that path raises with NetIds = 1uL.
        try
        {
            if (_cardPileCmdAdd is null || _pileTypeDeckValue is null)
                throw new InvalidOperationException("CardPileCmd.Add or PileType.Deck not bound");
            var paramCount = _cardPileCmdAdd.GetParameters().Length;
            var args = new object?[paramCount];
            args[0] = picked;
            args[1] = _pileTypeDeckValue;
            for (var i = 2; i < paramCount; i++) args[i] = Type.Missing;
            var result = _cardPileCmdAdd.Invoke(null, args);
            if (result is Task t) t.GetAwaiter().GetResult();
        }
        catch (Exception ex) { gaps.Add(MakeGap(0, "CardPileCmdAdd", ex)); }

        try
        {
            if (_runManagerRewardSynchronizer is not null && _rewardSyncSyncLocalObtainedCard is not null)
            {
                var sync = _runManagerRewardSynchronizer.GetValue(handle.RunManager);
                if (sync is not null) _rewardSyncSyncLocalObtainedCard.Invoke(sync, new[] { picked });
            }
        }
        catch (Exception ex) { gaps.Add(MakeGap(0, "SyncLocalObtainedCard", ex)); }
    }

    // Mirrors Console.Error to two writers. We need the original stderr to keep
    // working (the user still wants to see engine output live during a probe
    // run) AND a copy in-memory so the catalog file can include it.
    private sealed class TeeTextWriter : TextWriter
    {
        private readonly TextWriter _a;
        private readonly TextWriter _b;
        public TeeTextWriter(TextWriter a, TextWriter b) { _a = a; _b = b; }
        public override System.Text.Encoding Encoding => _a.Encoding;
        public override void Write(char value) { _a.Write(value); _b.Write(value); }
        public override void Write(string? value) { _a.Write(value); _b.Write(value); }
        public override void WriteLine(string? value) { _a.WriteLine(value); _b.WriteLine(value); }
        public override void Flush() { _a.Flush(); _b.Flush(); }
    }

    // Strip TargetInvocationException (always wraps reflection-thrown errors)
    // and grab the top-N stack frames. 8 frames is enough to identify the
    // sts2 method that triggered the gap; deeper frames are framework noise.
    private static CatalogedGap MakeGap(int iteration, string phase, Exception ex)
    {
        var actual = ex is TargetInvocationException tie && tie.InnerException is not null
            ? tie.InnerException
            : ex;
        var trace = (actual.StackTrace ?? string.Empty)
            .Split('\n')
            .Select(s => s.TrimEnd('\r').TrimStart())
            .Where(s => s.Length > 0)
            .Take(8)
            .ToArray();
        return new CatalogedGap(
            iteration,
            phase,
            actual.GetType().FullName ?? actual.GetType().Name,
            actual.Message,
            trace);
    }
}
