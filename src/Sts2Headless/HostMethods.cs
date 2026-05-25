using System.Text.Json;
using Sts2Headless.Cheats;
using Sts2Headless.Content;
using Sts2Headless.Protocol;
using Sts2Headless.Protocol.Methods;
using Sts2Headless.Replay;
using Sts2Headless.Runtime.Bindings;
using Sts2Headless.Runtime.Hooks;
using Sts2Headless.Utils;

namespace Sts2Headless;

// Method registry. Each pass adds entries; the keys are the wire-level
// method names callers send in `{ "method": "..." }`.
//
// Handlers operate on the typed DTOs in Sts2Headless.Protocol.Methods. The
// Typed<> adapter below bridges those records to the JsonNode-shaped
// StdioHost.Handler delegate — deserialise params on entry, serialise
// result on exit, sharing EnvelopeIo.JsonOptions so the wire shape can't
// drift between the registry and the framing layer.
public static class HostMethods
{
    public static IReadOnlyDictionary<string, StdioHost.Handler> Build(string repoRoot, Sts2Bindings bindings, Session session, bool debugEnabled)
    {
        // Merged catalogue used both by host/methods (the wire-level
        // discovery surface) and by AssertParity below. Built once here
        // so every consumer sees the exact same shape.
        var fullCatalog = MethodCatalog.Core
            .Concat(CheatMethodCatalog.All)
            .Concat(ContentMethodCatalog.All)
            .ToList();

        var dict = new Dictionary<string, StdioHost.Handler>
        {
            ["host/ping"] = TypedNoParams(() => Ping(repoRoot)),
            ["host/methods"] = TypedNoParams(() => Methods(fullCatalog, debugEnabled)),
            ["run/new"] = Typed<RunNewParams, RunNewResult>(p => RunNew(bindings, session, repoRoot, p)),
            ["run/state"] = TypedNoParams(() => RunState(bindings, session)),
            ["run/summarize_state"] = TypedNoParams(() => RunSummarizeState(bindings, session)),
            ["run/select_map_node"] = Typed<RunSelectMapNodeParams, RunSelectMapNodeResult>(p => RunSelectMapNode(bindings, session, p)),
            ["run/select_event_option"] = Typed<RunSelectEventOptionParams, RunSelectEventOptionResult>(p => RunSelectEventOption(bindings, session, p)),
            ["run/select_rest_site_option"] = Typed<RunSelectRestSiteOptionParams, RunSelectRestSiteOptionResult>(p => RunSelectRestSiteOption(bindings, session, p)),
            ["run/take_treasure"] = TypedNoParams(() => RunTakeTreasure(bindings, session)),
            ["run/skip_treasure"] = TypedNoParams(() => RunSkipTreasure(bindings, session)),
            ["run/buy_merchant_item"] = Typed<RunBuyMerchantItemParams, RunBuyMerchantItemResult>(p => RunBuyMerchantItem(bindings, session, p)),
            ["run/leave_merchant_room"] = TypedNoParams(() => RunLeaveMerchantRoom(bindings, session)),
            ["run/end_turn"] = TypedNoParams(() => RunEndTurn(bindings, session)),
            ["run/play_card"] = Typed<RunPlayCardParams, RunPlayCardResult>(p => RunPlayCard(bindings, session, p)),
            ["run/use_potion"] = Typed<RunUsePotionParams, RunUsePotionResult>(p => RunUsePotion(bindings, session, p)),
            ["run/select_reward"] = Typed<RunSelectRewardParams, RunSelectRewardResult>(p => RunSelectReward(bindings, session, p)),
            ["run/skip_reward"] = Typed<RunSkipRewardParams, RunSkipRewardResult>(p => RunSkipReward(bindings, session, p)),
            ["run/enter_next_act"] = TypedNoParams(() => RunEnterNextAct(bindings, session)),
            ["run/proceed_event"] = TypedNoParams(() => RunProceedEvent(bindings, session)),
            // AD-8: typed mirror of the game's .run RunHistory file.
            // Returns null-when-unavailable through the raw-JsonNode adapter
            // so the snake_case shape (which deliberately deviates from
            // the wire's camelCase elsewhere) survives EnvelopeIo's
            // serialisation unchanged.
            ["run/history"] = RawNoParams(_ => RunHistoryHandler(session)),
        };
        // AD-7: cheat handlers (debug/*) live in the Sts2Headless.Cheats
        // assembly so the Agents project, which only references Protocol,
        // can't reach them by accident. Every cheat is wrapped in GateDebug
        // — with --enable-debug off the gate replaces the handler with one
        // that throws WireException(DebugMethodDisabled), surfacing a typed
        // wire error rather than a silent no-op or generic InternalError.
        // Catalogue entries stay registered so AssertParity / schema export
        // stay honest about the gated surface.
        foreach (var (name, handler) in CheatHostMethods.Build(bindings, () => session.Run))
        {
            dict[name] = GateDebug(name, debugEnabled, raw => handler(raw));
        }
        // Content surface (no gate — content/* exposes player-visible
        // info only; seed-deterministic reveals live under debug/* with
        // GateDebug above). One ContentReader per host instance reads
        // ModelDb lazily on first call.
        foreach (var (name, handler) in ContentHostMethods.Build(bindings))
        {
            dict[name] = new StdioHost.Handler(handler.Invoke);
        }
        // AD-5: catalogue is the source of truth shared with the schema
        // emitter. A method registered here without an entry — or vice
        // versa — fails startup rather than silently drifting the wire
        // from `protocol/openrpc.json`. The merged catalogue is Core +
        // cheats so AssertParity covers the full host surface.
        MethodCatalog.AssertParity(fullCatalog, dict.Keys);
        return dict;
    }

    // Catalogue-introspection handler. Returns every method the host
    // knows about — including debug ones — so clients can render menus
    // / autocomplete / "this method exists but is gated" hints without
    // having to parse openrpc.json. Debug-only entries are flagged via
    // isDebugOnly, and the top-level debugEnabled mirrors the host's
    // --enable-debug flag so a client can tell at a glance whether a
    // debug entry is actually callable in this session.
    public static HostMethodsResult Methods(IReadOnlyList<MethodEntry> catalog, bool debugEnabled)
    {
        var methods = catalog
            .OrderBy(e => e.Name, StringComparer.Ordinal)
            .Select(e => new HostMethodInfo(
                Name: e.Name,
                Summary: e.Summary,
                HasParams: e.ParamsType is not null,
                IsDebugOnly: e.IsDebugOnly))
            .ToList();
        return new HostMethodsResult(Ok: true, DebugEnabled: debugEnabled, Methods: methods);
    }

    // AD-7: wrap a debug handler so it refuses to run unless --enable-debug
    // is set on the host process. The error code is intentionally distinct
    // from MethodNotFound (-32601) and InternalError (-32603) so clients
    // can branch on "the host knows this method exists but it's gated off"
    // — useful for tooling that wants to surface a clear message rather
    // than letting the call look like a typo.
    private static StdioHost.Handler GateDebug(string methodName, bool debugEnabled, StdioHost.Handler inner)
    {
        if (debugEnabled) return inner;
        return _ => throw new WireException(
            WireErrorCode.DebugMethodDisabled,
            $"{methodName} is a debug-only method and this host process was not started with --enable-debug. " +
            $"Debug methods are never available by default; enabling them is an explicit operator action.");
    }

    // Public for unit tests: doesn't touch sts2 bindings, only reads
    // GAME_VERSION from disk, so it's safe to exercise without a game install.
    public static HostPingResult Ping(string repoRoot)
    {
        var (version, sha256) = ReadGameVersion(repoRoot);
        return new HostPingResult(Ok: true, GameVersion: version, GameSha256: sha256);
    }

    private static RunNewResult RunNew(Sts2Bindings bindings, Session session, string repoRoot, RunNewParams? @params)
    {
        var character = @params?.Character ?? Character.Ironclad;
        var seed = @params?.Seed ?? 1uL;
        var withNeow = @params?.WithNeow ?? false;
        var ascension = @params?.Ascension ?? 0;
        var modifiers = @params?.Modifiers ?? Array.Empty<ModifierId>();

        if (ascension < 0)
        {
            throw new ArgumentException($"ascension must be non-negative, got {ascension}");
        }
        for (var i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i] == ModifierId.Unknown)
            {
                throw new WireException(WireErrorCode.InvalidParams,
                    $"modifiers[{i}] is ModifierId.Unknown — pass a known modifier id (see ModifierId enum)");
            }
        }

        // Finalise any prior recorder BEFORE bindings.StartRun runs
        // RunManager.CleanUp on the old run — the prefix on CleanUp will also
        // do this, but doing it explicitly here makes the host's lifetime
        // visible without relying on the Harmony hook firing. Idempotent.
        session.Recorder?.FinalizeRun();
        if (session.Recorder is not null) ReplayHook.Unbind(session.Recorder);

        // Full StartRun chain. Default lands at MapRoom; withNeow=true lands
        // at the Neow EventRoom. Callers can drive run/select_event_option
        // to dismiss the event once it's surfaced through
        // AvailableEventOptions. The bindings layer enforces that every
        // Character enum value has a registered factory (Bind() throws
        // otherwise) — no per-character branch needed here.
        var run = bindings.StartRun(character, seed, withNeow, ascension);

        // AD-8: recording is on by default. STS2_REPLAY_OUT unset → land
        // in <repoRoot>/vendor/replays. An explicit path overrides;
        // STS2_REPLAY_OUT=off|disabled|none|0|no|false disables. The hook
        // is install-once (idempotent), so subsequent runs reuse the same
        // Harmony patches; only the bound recorder changes.
        ReplayRecorder? recorder = null;
        var replayOut = ReplayLayout.ResolveRoot(
            Environment.GetEnvironmentVariable("STS2_REPLAY_OUT"),
            repoRoot);
        if (replayOut is not null)
        {
            var (gameVersion, sha) = ReplayHeaderFactory.ReadGameVersionPin(repoRoot);
            // Serialise each ModifierId through EnvelopeIo.JsonOptions and strip
            // the surrounding quotes so the replay header carries the engine's
            // wire-name (e.g. "ALL_STAR"), matching what CombatReplayWriter
            // records natively.
            var modifierWireNames = modifiers
                .Select(m => JsonSerializer.Serialize(m, EnvelopeIo.JsonOptions).Trim('"'))
                .ToList();
            var agent = Environment.GetEnvironmentVariable("STS2_REPLAY_AGENT");
            var header = ReplayHeaderFactory.Create(
                sts2: bindings.Sts2,
                gameVersion: gameVersion,
                sts2DllSha256: sha,
                seed: seed.ToString(System.Globalization.CultureInfo.InvariantCulture),
                character: character,
                ascension: ascension,
                modifiers: modifierWireNames,
                startTime: DateTimeOffset.UtcNow,
                agent: agent);
            recorder = new ReplayRecorder(bindings.Sts2, replayOut, header);
            ReplayHook.Install(bindings.Sts2);
            ReplayHook.Bind(recorder);
            // The engine's CombatReplayWriter defaults IsEnabled to
            // `!TestMode.IsOn`, and we set TestMode.IsOn in bootstrap.
            // Flipping it back here is what makes the engine actually
            // call RecordInitialState on room entries.
            recorder.EnableEngineRecording();
        }

        session.Set(run, character, seed, recorder);

        // Clear stale trigger events from the previous run (or from the
        // bootstrap's CreateIroncladSmoke step) so the first run/state of
        // a fresh run doesn't surface trigger events that don't belong to
        // it. Symmetric with the buffer being process-global: per-run
        // semantics live in the host, not the log.
        TriggerLog.Reset();

        var s = bindings.ReadSnapshot(run);
        return new RunNewResult(
            Ok: true,
            Character: character,
            Seed: seed,
            PlayerType: run.Player.GetType().FullName ?? run.Player.GetType().Name,
            CurrentRoomType: s.CurrentRoomType,
            AvailableMapNodes: s.AvailableMapNodes,
            AvailableEventOptions: s.AvailableEventOptions,
            AvailableRestSiteOptions: s.AvailableRestSiteOptions,
            AvailableMerchantItems: s.AvailableMerchantItems,
            AvailableTreasureRelics: s.AvailableTreasureRelics,
            CombatState: s.CombatState,
            RewardsState: s.RewardsState,
            Relics: s.Relics,
            OwnedPotions: s.OwnedPotions,
            Modifiers: modifiers);
    }

    private static RunStateResult RunState(Sts2Bindings bindings, Session session)
    {
        var run = session.Run
            ?? throw new InvalidOperationException("no active run — call run/new first");

        var s = bindings.ReadSnapshot(run);
        // Drain the trigger log into this snapshot. Each run/state response
        // owns the window since the previous response — skipping run/state
        // means losing that window's events (TriggerLog.Capacity bounds the
        // leak; triggeredDropped > 0 tells the caller). Drain BEFORE we
        // return; the next caller's window starts clean.
        var (triggered, dropped) = TriggerLog.Drain();
        return new RunStateResult(
            Ok: true,
            Character: session.Character,
            Seed: session.Seed,
            Hp: s.CurrentHp,
            MaxHp: s.MaxHp,
            Gold: s.Gold,
            DeckSize: s.DeckSize,
            CurrentRoomType: s.CurrentRoomType,
            ActFloor: s.ActFloor,
            CurrentActIndex: s.CurrentActIndex,
            IsGameOver: s.IsGameOver,
            IsVictory: s.IsVictory,
            IsDead: s.IsDead,
            AvailableMapNodes: s.AvailableMapNodes,
            AvailableEventOptions: s.AvailableEventOptions,
            AvailableRestSiteOptions: s.AvailableRestSiteOptions,
            AvailableMerchantItems: s.AvailableMerchantItems,
            AvailableTreasureRelics: s.AvailableTreasureRelics,
            CombatState: s.CombatState,
            RewardsState: s.RewardsState,
            Relics: s.Relics,
            OwnedPotions: s.OwnedPotions,
            TriggeredSincePrev: triggered,
            TriggeredDropped: dropped,
            BossEncounterId: s.BossEncounterId,
            SecondBossEncounterId: s.SecondBossEncounterId);
    }

    private static RunSummarizeStateResult RunSummarizeState(Sts2Bindings bindings, Session session)
    {
        // Build the same RunStateResult run/state would surface, then
        // render it through the shared RunSummary helper so the wire
        // text is identical to a manual run/state → client-side render.
        var state = RunState(bindings, session);
        return new RunSummarizeStateResult(Ok: true, Summary: RunSummary.Render(state));
    }

    private static RunSelectMapNodeResult RunSelectMapNode(Sts2Bindings bindings, Session session, RunSelectMapNodeParams? @params)
    {
        var run = session.Run
            ?? throw new InvalidOperationException("no active run — call run/new first");
        var args = @params
            ?? throw new ArgumentException("run/select_map_node requires params {col, row}");

        bindings.EnterMapCoord(run, args.Col, args.Row);

        return bindings.ReadSnapshot(run).ToRunSelectMapNodeResult(args.Col, args.Row);
    }

    private static RunSelectEventOptionResult RunSelectEventOption(Sts2Bindings bindings, Session session, RunSelectEventOptionParams? @params)
    {
        var run = session.Run
            ?? throw new InvalidOperationException("no active run — call run/new first");
        var args = @params
            ?? throw new ArgumentException("run/select_event_option requires params {optionIndex}");

        bindings.SelectEventOption(run, args.OptionIndex);

        return bindings.ReadSnapshot(run).ToRunSelectEventOptionResult(args.OptionIndex);
    }

    private static RunSelectRestSiteOptionResult RunSelectRestSiteOption(Sts2Bindings bindings, Session session, RunSelectRestSiteOptionParams? @params)
    {
        var run = session.Run
            ?? throw new InvalidOperationException("no active run — call run/new first");
        var args = @params
            ?? throw new ArgumentException("run/select_rest_site_option requires params {optionIndex}");

        // Stage any caller-supplied card-select hints in the selector's
        // queue before the option runs. SMITH raises one
        // CardSelectCmd.FromDeckForUpgrade prompt the engine intercepts
        // via our installed selector; this is how indices reach it.
        // Mirrors the play_card pattern (HostMethods.cs RunPlayCard).
        var selector = bindings.CardSelector;
        if (selector is not null)
        {
            selector.ClearPending();
            if (args.CardSelectIndices is { Count: > 0 } hints)
            {
                foreach (var hint in hints)
                {
                    selector.QueueSelection(hint);
                }
            }
        }

        try
        {
            bindings.SelectRestSiteOption(run, args.OptionIndex);
        }
        finally
        {
            selector?.ClearPending();
        }

        return bindings.ReadSnapshot(run).ToRunSelectRestSiteOptionResult(args.OptionIndex);
    }

    private static RunTakeTreasureResult RunTakeTreasure(Sts2Bindings bindings, Session session)
    {
        var run = session.Run
            ?? throw new InvalidOperationException("no active run — call run/new first");

        bindings.LeaveTreasureRoom(run, skip: false);

        return bindings.ReadSnapshot(run).ToRunTakeTreasureResult();
    }

    private static RunSkipTreasureResult RunSkipTreasure(Sts2Bindings bindings, Session session)
    {
        var run = session.Run
            ?? throw new InvalidOperationException("no active run — call run/new first");

        bindings.LeaveTreasureRoom(run, skip: true);

        return bindings.ReadSnapshot(run).ToRunSkipTreasureResult();
    }

    private static RunBuyMerchantItemResult RunBuyMerchantItem(Sts2Bindings bindings, Session session, RunBuyMerchantItemParams? @params)
    {
        var run = session.Run
            ?? throw new InvalidOperationException("no active run — call run/new first");
        var args = @params
            ?? throw new WireException(WireErrorCode.InvalidParams,
                "run/buy_merchant_item requires params {itemIndex}");
        if (args.ItemIndex < 0)
        {
            throw new WireException(WireErrorCode.InvalidParams,
                $"run/buy_merchant_item: itemIndex must be >= 0 (got {args.ItemIndex})");
        }

        bool purchased;
        try
        {
            purchased = bindings.BuyMerchantItem(run, args.ItemIndex);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            // Out-of-range index is a caller mistake — surface as
            // InvalidParams so generated clients see the right code rather
            // than a generic InternalError.
            throw new WireException(WireErrorCode.InvalidParams, ex.Message);
        }
        if (!purchased)
        {
            // The engine's OnTryPurchaseWrapper returns false when the
            // purchase was refused (insufficient gold, already-sold slot,
            // cancel signal). Convert to InvalidParams so the caller can
            // distinguish "rejected for a reason" from "the slice broke".
            throw new WireException(WireErrorCode.InvalidParams,
                $"run/buy_merchant_item: merchant rejected purchase at itemIndex={args.ItemIndex} " +
                "(likely insufficient gold or already sold). Re-read availableMerchantItems for the current state.");
        }

        return bindings.ReadSnapshot(run).ToRunBuyMerchantItemResult(args.ItemIndex);
    }

    private static RunLeaveMerchantRoomResult RunLeaveMerchantRoom(Sts2Bindings bindings, Session session)
    {
        var run = session.Run
            ?? throw new InvalidOperationException("no active run — call run/new first");

        bindings.LeaveMerchantRoom(run);

        return bindings.ReadSnapshot(run).ToRunLeaveMerchantRoomResult();
    }

    private static RunEndTurnResult RunEndTurn(Sts2Bindings bindings, Session session)
    {
        var run = session.Run
            ?? throw new InvalidOperationException("no active run — call run/new first");

        bindings.EndTurn(run);

        return bindings.ReadSnapshot(run).ToRunEndTurnResult();
    }

    private static RunPlayCardResult RunPlayCard(Sts2Bindings bindings, Session session, RunPlayCardParams? @params)
    {
        var run = session.Run
            ?? throw new InvalidOperationException("no active run — call run/new first");
        var args = @params
            ?? throw new ArgumentException("run/play_card requires params {cardIndex, targetIndex?}");

        // Stage any caller-supplied card-select hints in the selector's queue
        // before the play runs. Each inner list is one prompt the engine
        // raises; consumed FIFO during OnPlay. We clear any leftover hints
        // before AND after to keep stale ones from bleeding across requests.
        var selector = bindings.CardSelector;
        if (selector is not null)
        {
            selector.ClearPending();
            if (args.CardSelectIndices is { Count: > 0 } hints)
            {
                foreach (var hint in hints)
                {
                    selector.QueueSelection(hint);
                }
            }
        }

        try
        {
            bindings.PlayCard(run, args.CardIndex, args.TargetIndex);
        }
        finally
        {
            selector?.ClearPending();
        }

        return bindings.ReadSnapshot(run).ToRunPlayCardResult(args.CardIndex, args.TargetIndex);
    }

    private static RunUsePotionResult RunUsePotion(Sts2Bindings bindings, Session session, RunUsePotionParams? @params)
    {
        var run = session.Run
            ?? throw new InvalidOperationException("no active run — call run/new first");
        var args = @params
            ?? throw new ArgumentException("run/use_potion requires params {potionIndex, targetIndex?}");

        bindings.UsePotion(run, args.PotionIndex, args.TargetIndex);

        return bindings.ReadSnapshot(run).ToRunUsePotionResult(args.PotionIndex, args.TargetIndex);
    }

    private static RunSelectRewardResult RunSelectReward(Sts2Bindings bindings, Session session, RunSelectRewardParams? @params)
    {
        var run = session.Run
            ?? throw new InvalidOperationException("no active run — call run/new first");
        var args = @params
            ?? throw new ArgumentException("run/select_reward requires params {rewardIndex, cardIndex?}");

        bindings.SelectReward(run, args.RewardIndex, args.CardIndex);

        return bindings.ReadSnapshot(run).ToRunSelectRewardResult(args.RewardIndex, args.CardIndex);
    }

    private static RunSkipRewardResult RunSkipReward(Sts2Bindings bindings, Session session, RunSkipRewardParams? @params)
    {
        var run = session.Run
            ?? throw new InvalidOperationException("no active run — call run/new first");
        var args = @params
            ?? throw new ArgumentException("run/skip_reward requires params {rewardIndex}");

        bindings.SkipReward(run, args.RewardIndex);

        return bindings.ReadSnapshot(run).ToRunSkipRewardResult(args.RewardIndex);
    }

    private static RunEnterNextActResult RunEnterNextAct(Sts2Bindings bindings, Session session)
    {
        var run = session.Run
            ?? throw new InvalidOperationException("no active run — call run/new first");

        try
        {
            bindings.EnterNextAct(run);
        }
        catch (InvalidOperationException ex)
        {
            // EnterNextAct's caller guard ("only legal on a boss tile")
            // is a precondition failure, not an engine bug. Surface it as
            // InvalidParams so generated clients see the right code.
            throw new WireException(WireErrorCode.InvalidParams, ex.Message);
        }

        return bindings.ReadSnapshot(run).ToRunEnterNextActResult();
    }

    private static RunProceedEventResult RunProceedEvent(Sts2Bindings bindings, Session session)
    {
        var run = session.Run
            ?? throw new InvalidOperationException("no active run — call run/new first");

        try
        {
            bindings.ProceedEvent(run);
        }
        catch (InvalidOperationException ex)
        {
            throw new WireException(WireErrorCode.InvalidParams, ex.Message);
        }

        return bindings.ReadSnapshot(run).ToRunProceedEventResult();
    }

    // Wraps the shared WireHandlers.Typed adapter into StdioHost.Handler —
    // structurally identical signatures, but the exe owns its own delegate
    // type so handler dictionaries stay strongly typed.
    private static StdioHost.Handler Typed<TParams, TResult>(Func<TParams?, TResult> handler)
        => new(WireHandlers.Typed(handler).Invoke);

    private static StdioHost.Handler TypedNoParams<TResult>(Func<TResult> handler)
        => _ => JsonSerializer.SerializeToNode(handler(), EnvelopeIo.JsonOptions);

    // Adapter for handlers that produce a JsonNode directly (rather than
    // a typed result the envelope serialises). Used when the wire shape
    // for a specific method deviates from EnvelopeIo's default policy —
    // currently just `run/history`, whose snake_case payload would
    // otherwise be re-named by serialisation.
    private static StdioHost.Handler RawNoParams(Func<System.Text.Json.Nodes.JsonNode?, System.Text.Json.Nodes.JsonNode?> handler)
        => raw => handler(raw);

    private static System.Text.Json.Nodes.JsonNode RunHistoryHandler(Session session)
    {
        var recorder = session.Recorder
            ?? throw new InvalidOperationException(
                "run/history requires recording to be active — STS2_REPLAY_OUT is set to a disable sentinel (off/disabled/none) or this run/new completed before recording was enabled. Unset STS2_REPLAY_OUT (or point it at a directory) and call run/new again.");
        // ReplayQuery throws InvalidOperationException with a
        // caller-meaningful message when run.json isn't on disk yet.
        return ReplayQuery.LoadAsWireJson(recorder.RunDirectory);
    }

    private static (string? Version, string? Sha256) ReadGameVersion(string repoRoot)
    {
        var pin = GameVersionPin.Read(repoRoot);
        return (pin?.Version, pin?.Sha256);
    }
}
