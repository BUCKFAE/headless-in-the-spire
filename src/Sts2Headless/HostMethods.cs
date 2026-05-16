using System.Text.Json;
using Sts2Headless.Protocol;
using Sts2Headless.Protocol.Methods;
using Sts2Headless.Runtime;

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
        var dict = new Dictionary<string, StdioHost.Handler>
        {
            ["host/ping"] = TypedNoParams(() => Ping(repoRoot)),
            ["run/new"] = Typed<RunNewParams, RunNewResult>(p => RunNew(bindings, session, p)),
            ["run/state"] = TypedNoParams(() => RunState(bindings, session)),
            ["run/select_map_node"] = Typed<RunSelectMapNodeParams, RunSelectMapNodeResult>(p => RunSelectMapNode(bindings, session, p)),
            ["run/select_event_option"] = Typed<RunSelectEventOptionParams, RunSelectEventOptionResult>(p => RunSelectEventOption(bindings, session, p)),
            ["run/select_rest_site_option"] = Typed<RunSelectRestSiteOptionParams, RunSelectRestSiteOptionResult>(p => RunSelectRestSiteOption(bindings, session, p)),
            ["run/leave_treasure_room"] = TypedNoParams(() => RunLeaveTreasureRoom(bindings, session)),
            ["run/buy_merchant_item"] = Typed<RunBuyMerchantItemParams, RunBuyMerchantItemResult>(p => RunBuyMerchantItem(bindings, session, p)),
            ["run/leave_merchant_room"] = TypedNoParams(() => RunLeaveMerchantRoom(bindings, session)),
            ["run/end_turn"] = TypedNoParams(() => RunEndTurn(bindings, session)),
            ["run/play_card"] = Typed<RunPlayCardParams, RunPlayCardResult>(p => RunPlayCard(bindings, session, p)),
            ["run/use_potion"] = Typed<RunUsePotionParams, RunUsePotionResult>(p => RunUsePotion(bindings, session, p)),
            ["run/select_reward"] = Typed<RunSelectRewardParams, RunSelectRewardResult>(p => RunSelectReward(bindings, session, p)),
            ["run/skip_reward"] = Typed<RunSkipRewardParams, RunSkipRewardResult>(p => RunSkipReward(bindings, session, p)),
            ["run/enter_next_act"] = TypedNoParams(() => RunEnterNextAct(bindings, session)),
            ["run/proceed_event"] = TypedNoParams(() => RunProceedEvent(bindings, session)),
            // AD-7: every debug/* method is wrapped by GateDebug. With
            // --enable-debug off, the gate replaces the handler with one
            // that throws WireException(DebugMethodDisabled) so an
            // accidental call surfaces a typed wire error rather than a
            // silent no-op or an InternalError. The catalogue entries stay
            // registered so AssertParity / schema export stay honest.
            ["debug/give_relic"] = GateDebug("debug/give_relic", debugEnabled,
                Typed<DebugGiveRelicParams, DebugGiveRelicResult>(p => DebugGiveRelic(bindings, session, p))),
            ["debug/set_hp"] = GateDebug("debug/set_hp", debugEnabled,
                Typed<DebugSetHpParams, DebugSetHpResult>(p => DebugSetHp(bindings, session, p))),
        };
        // AD-5: catalogue is the source of truth shared with the schema
        // emitter. A method registered here without an entry — or vice
        // versa — fails startup rather than silently drifting the wire
        // from `protocol/openrpc.json`.
        MethodCatalog.AssertParity(dict.Keys);
        return dict;
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

    private static RunNewResult RunNew(Sts2Bindings bindings, Session session, RunNewParams? @params)
    {
        var character = @params?.Character ?? Character.Ironclad;
        var seed = @params?.Seed ?? 1uL;
        var withNeow = @params?.WithNeow ?? false;

        if (character != Character.Ironclad)
        {
            throw new ArgumentException($"character '{character}' not yet supported (only Ironclad)");
        }

        // Pass C: full StartRun chain (was just Player.CreateForNewRun in
        // Pass A). Default lands at MapRoom; withNeow=true lands at the
        // Neow EventRoom. Callers can drive run/select_event_option to
        // dismiss the event once it's surfaced through AvailableEventOptions.
        var run = bindings.StartIroncladRun(seed, withNeow);
        session.Set(run, character, seed);

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
            CombatState: s.CombatState,
            RewardsState: s.RewardsState,
            Relics: s.Relics,
            OwnedPotions: s.OwnedPotions);
    }

    private static RunStateResult RunState(Sts2Bindings bindings, Session session)
    {
        var run = session.Run
            ?? throw new InvalidOperationException("no active run — call run/new first");

        var s = bindings.ReadSnapshot(run);
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
            CombatState: s.CombatState,
            RewardsState: s.RewardsState,
            Relics: s.Relics,
            OwnedPotions: s.OwnedPotions);
    }

    private static RunSelectMapNodeResult RunSelectMapNode(Sts2Bindings bindings, Session session, RunSelectMapNodeParams? @params)
    {
        var run = session.Run
            ?? throw new InvalidOperationException("no active run — call run/new first");
        var args = @params
            ?? throw new ArgumentException("run/select_map_node requires params {col, row}");

        bindings.EnterMapCoord(run, args.Col, args.Row);

        var s = bindings.ReadSnapshot(run);
        return new RunSelectMapNodeResult(
            Ok: true,
            Col: args.Col,
            Row: args.Row,
            CurrentRoomType: s.CurrentRoomType,
            ActFloor: s.ActFloor,
            CurrentActIndex: s.CurrentActIndex,
            IsGameOver: s.IsGameOver,
            IsVictory: s.IsVictory,
            IsDead: s.IsDead,
            Hp: s.CurrentHp,
            AvailableMapNodes: s.AvailableMapNodes,
            AvailableEventOptions: s.AvailableEventOptions,
            AvailableRestSiteOptions: s.AvailableRestSiteOptions,
            AvailableMerchantItems: s.AvailableMerchantItems,
            CombatState: s.CombatState,
            RewardsState: s.RewardsState,
            Relics: s.Relics,
            OwnedPotions: s.OwnedPotions);
    }

    private static RunSelectEventOptionResult RunSelectEventOption(Sts2Bindings bindings, Session session, RunSelectEventOptionParams? @params)
    {
        var run = session.Run
            ?? throw new InvalidOperationException("no active run — call run/new first");
        var args = @params
            ?? throw new ArgumentException("run/select_event_option requires params {optionIndex}");

        bindings.SelectEventOption(run, args.OptionIndex);

        var s = bindings.ReadSnapshot(run);
        return new RunSelectEventOptionResult(
            Ok: true,
            OptionIndex: args.OptionIndex,
            CurrentRoomType: s.CurrentRoomType,
            ActFloor: s.ActFloor,
            CurrentActIndex: s.CurrentActIndex,
            IsGameOver: s.IsGameOver,
            IsVictory: s.IsVictory,
            IsDead: s.IsDead,
            Hp: s.CurrentHp,
            AvailableMapNodes: s.AvailableMapNodes,
            AvailableEventOptions: s.AvailableEventOptions,
            AvailableRestSiteOptions: s.AvailableRestSiteOptions,
            AvailableMerchantItems: s.AvailableMerchantItems,
            CombatState: s.CombatState,
            RewardsState: s.RewardsState,
            Relics: s.Relics,
            OwnedPotions: s.OwnedPotions);
    }

    private static RunSelectRestSiteOptionResult RunSelectRestSiteOption(Sts2Bindings bindings, Session session, RunSelectRestSiteOptionParams? @params)
    {
        var run = session.Run
            ?? throw new InvalidOperationException("no active run — call run/new first");
        var args = @params
            ?? throw new ArgumentException("run/select_rest_site_option requires params {optionIndex}");

        bindings.SelectRestSiteOption(run, args.OptionIndex);

        var s = bindings.ReadSnapshot(run);
        return new RunSelectRestSiteOptionResult(
            Ok: true,
            OptionIndex: args.OptionIndex,
            CurrentRoomType: s.CurrentRoomType,
            ActFloor: s.ActFloor,
            CurrentActIndex: s.CurrentActIndex,
            IsGameOver: s.IsGameOver,
            IsVictory: s.IsVictory,
            IsDead: s.IsDead,
            Hp: s.CurrentHp,
            AvailableMapNodes: s.AvailableMapNodes,
            AvailableEventOptions: s.AvailableEventOptions,
            AvailableRestSiteOptions: s.AvailableRestSiteOptions,
            AvailableMerchantItems: s.AvailableMerchantItems,
            CombatState: s.CombatState,
            RewardsState: s.RewardsState,
            Relics: s.Relics,
            OwnedPotions: s.OwnedPotions);
    }

    private static RunLeaveTreasureRoomResult RunLeaveTreasureRoom(Sts2Bindings bindings, Session session)
    {
        var run = session.Run
            ?? throw new InvalidOperationException("no active run — call run/new first");

        bindings.LeaveTreasureRoom(run);

        var s = bindings.ReadSnapshot(run);
        return new RunLeaveTreasureRoomResult(
            Ok: true,
            CurrentRoomType: s.CurrentRoomType,
            ActFloor: s.ActFloor,
            CurrentActIndex: s.CurrentActIndex,
            IsGameOver: s.IsGameOver,
            IsVictory: s.IsVictory,
            IsDead: s.IsDead,
            Hp: s.CurrentHp,
            AvailableMapNodes: s.AvailableMapNodes,
            AvailableEventOptions: s.AvailableEventOptions,
            AvailableRestSiteOptions: s.AvailableRestSiteOptions,
            AvailableMerchantItems: s.AvailableMerchantItems,
            CombatState: s.CombatState,
            RewardsState: s.RewardsState,
            Relics: s.Relics,
            OwnedPotions: s.OwnedPotions);
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

        var s = bindings.ReadSnapshot(run);
        return new RunBuyMerchantItemResult(
            Ok: true,
            ItemIndex: args.ItemIndex,
            CurrentRoomType: s.CurrentRoomType,
            ActFloor: s.ActFloor,
            CurrentActIndex: s.CurrentActIndex,
            IsGameOver: s.IsGameOver,
            IsVictory: s.IsVictory,
            IsDead: s.IsDead,
            Hp: s.CurrentHp,
            AvailableMapNodes: s.AvailableMapNodes,
            AvailableEventOptions: s.AvailableEventOptions,
            AvailableRestSiteOptions: s.AvailableRestSiteOptions,
            AvailableMerchantItems: s.AvailableMerchantItems,
            CombatState: s.CombatState,
            RewardsState: s.RewardsState,
            Relics: s.Relics,
            OwnedPotions: s.OwnedPotions);
    }

    private static RunLeaveMerchantRoomResult RunLeaveMerchantRoom(Sts2Bindings bindings, Session session)
    {
        var run = session.Run
            ?? throw new InvalidOperationException("no active run — call run/new first");

        bindings.LeaveMerchantRoom(run);

        var s = bindings.ReadSnapshot(run);
        return new RunLeaveMerchantRoomResult(
            Ok: true,
            CurrentRoomType: s.CurrentRoomType,
            ActFloor: s.ActFloor,
            CurrentActIndex: s.CurrentActIndex,
            IsGameOver: s.IsGameOver,
            IsVictory: s.IsVictory,
            IsDead: s.IsDead,
            Hp: s.CurrentHp,
            AvailableMapNodes: s.AvailableMapNodes,
            AvailableEventOptions: s.AvailableEventOptions,
            AvailableRestSiteOptions: s.AvailableRestSiteOptions,
            AvailableMerchantItems: s.AvailableMerchantItems,
            CombatState: s.CombatState,
            RewardsState: s.RewardsState,
            Relics: s.Relics,
            OwnedPotions: s.OwnedPotions);
    }

    private static RunEndTurnResult RunEndTurn(Sts2Bindings bindings, Session session)
    {
        var run = session.Run
            ?? throw new InvalidOperationException("no active run — call run/new first");

        bindings.EndTurn(run);

        var s = bindings.ReadSnapshot(run);
        return new RunEndTurnResult(
            Ok: true,
            CurrentRoomType: s.CurrentRoomType,
            ActFloor: s.ActFloor,
            CurrentActIndex: s.CurrentActIndex,
            IsGameOver: s.IsGameOver,
            IsVictory: s.IsVictory,
            IsDead: s.IsDead,
            Hp: s.CurrentHp,
            AvailableMapNodes: s.AvailableMapNodes,
            AvailableEventOptions: s.AvailableEventOptions,
            AvailableRestSiteOptions: s.AvailableRestSiteOptions,
            AvailableMerchantItems: s.AvailableMerchantItems,
            CombatState: s.CombatState,
            RewardsState: s.RewardsState,
            Relics: s.Relics,
            OwnedPotions: s.OwnedPotions);
    }

    private static RunPlayCardResult RunPlayCard(Sts2Bindings bindings, Session session, RunPlayCardParams? @params)
    {
        var run = session.Run
            ?? throw new InvalidOperationException("no active run — call run/new first");
        var args = @params
            ?? throw new ArgumentException("run/play_card requires params {cardIndex, targetIndex?}");

        bindings.PlayCard(run, args.CardIndex, args.TargetIndex);

        var s = bindings.ReadSnapshot(run);
        return new RunPlayCardResult(
            Ok: true,
            CardIndex: args.CardIndex,
            TargetIndex: args.TargetIndex,
            CurrentRoomType: s.CurrentRoomType,
            ActFloor: s.ActFloor,
            CurrentActIndex: s.CurrentActIndex,
            IsGameOver: s.IsGameOver,
            IsVictory: s.IsVictory,
            IsDead: s.IsDead,
            Hp: s.CurrentHp,
            AvailableMapNodes: s.AvailableMapNodes,
            AvailableEventOptions: s.AvailableEventOptions,
            AvailableRestSiteOptions: s.AvailableRestSiteOptions,
            AvailableMerchantItems: s.AvailableMerchantItems,
            CombatState: s.CombatState,
            RewardsState: s.RewardsState,
            Relics: s.Relics,
            OwnedPotions: s.OwnedPotions);
    }

    private static RunUsePotionResult RunUsePotion(Sts2Bindings bindings, Session session, RunUsePotionParams? @params)
    {
        var run = session.Run
            ?? throw new InvalidOperationException("no active run — call run/new first");
        var args = @params
            ?? throw new ArgumentException("run/use_potion requires params {potionIndex, targetIndex?}");

        bindings.UsePotion(run, args.PotionIndex, args.TargetIndex);

        var s = bindings.ReadSnapshot(run);
        return new RunUsePotionResult(
            Ok: true,
            PotionIndex: args.PotionIndex,
            TargetIndex: args.TargetIndex,
            CurrentRoomType: s.CurrentRoomType,
            ActFloor: s.ActFloor,
            CurrentActIndex: s.CurrentActIndex,
            IsGameOver: s.IsGameOver,
            IsVictory: s.IsVictory,
            IsDead: s.IsDead,
            Hp: s.CurrentHp,
            AvailableMapNodes: s.AvailableMapNodes,
            AvailableEventOptions: s.AvailableEventOptions,
            AvailableRestSiteOptions: s.AvailableRestSiteOptions,
            AvailableMerchantItems: s.AvailableMerchantItems,
            CombatState: s.CombatState,
            RewardsState: s.RewardsState,
            Relics: s.Relics,
            OwnedPotions: s.OwnedPotions);
    }

    private static RunSelectRewardResult RunSelectReward(Sts2Bindings bindings, Session session, RunSelectRewardParams? @params)
    {
        var run = session.Run
            ?? throw new InvalidOperationException("no active run — call run/new first");
        var args = @params
            ?? throw new ArgumentException("run/select_reward requires params {rewardIndex, cardIndex?}");

        bindings.SelectReward(run, args.RewardIndex, args.CardIndex);

        var s = bindings.ReadSnapshot(run);
        return new RunSelectRewardResult(
            Ok: true,
            RewardIndex: args.RewardIndex,
            CardIndex: args.CardIndex,
            CurrentRoomType: s.CurrentRoomType,
            ActFloor: s.ActFloor,
            CurrentActIndex: s.CurrentActIndex,
            IsGameOver: s.IsGameOver,
            IsVictory: s.IsVictory,
            IsDead: s.IsDead,
            Hp: s.CurrentHp,
            AvailableMapNodes: s.AvailableMapNodes,
            AvailableEventOptions: s.AvailableEventOptions,
            AvailableRestSiteOptions: s.AvailableRestSiteOptions,
            AvailableMerchantItems: s.AvailableMerchantItems,
            CombatState: s.CombatState,
            RewardsState: s.RewardsState,
            Relics: s.Relics,
            OwnedPotions: s.OwnedPotions);
    }

    private static RunSkipRewardResult RunSkipReward(Sts2Bindings bindings, Session session, RunSkipRewardParams? @params)
    {
        var run = session.Run
            ?? throw new InvalidOperationException("no active run — call run/new first");
        var args = @params
            ?? throw new ArgumentException("run/skip_reward requires params {rewardIndex}");

        bindings.SkipReward(run, args.RewardIndex);

        var s = bindings.ReadSnapshot(run);
        return new RunSkipRewardResult(
            Ok: true,
            RewardIndex: args.RewardIndex,
            CurrentRoomType: s.CurrentRoomType,
            ActFloor: s.ActFloor,
            CurrentActIndex: s.CurrentActIndex,
            IsGameOver: s.IsGameOver,
            IsVictory: s.IsVictory,
            IsDead: s.IsDead,
            Hp: s.CurrentHp,
            AvailableMapNodes: s.AvailableMapNodes,
            AvailableEventOptions: s.AvailableEventOptions,
            AvailableRestSiteOptions: s.AvailableRestSiteOptions,
            AvailableMerchantItems: s.AvailableMerchantItems,
            CombatState: s.CombatState,
            RewardsState: s.RewardsState,
            Relics: s.Relics,
            OwnedPotions: s.OwnedPotions);
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

        var s = bindings.ReadSnapshot(run);
        return new RunEnterNextActResult(
            Ok: true,
            CurrentRoomType: s.CurrentRoomType,
            ActFloor: s.ActFloor,
            CurrentActIndex: s.CurrentActIndex,
            IsGameOver: s.IsGameOver,
            IsVictory: s.IsVictory,
            IsDead: s.IsDead,
            Hp: s.CurrentHp,
            AvailableMapNodes: s.AvailableMapNodes,
            AvailableEventOptions: s.AvailableEventOptions,
            AvailableRestSiteOptions: s.AvailableRestSiteOptions,
            AvailableMerchantItems: s.AvailableMerchantItems,
            CombatState: s.CombatState,
            RewardsState: s.RewardsState,
            Relics: s.Relics,
            OwnedPotions: s.OwnedPotions);
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

        var s = bindings.ReadSnapshot(run);
        return new RunProceedEventResult(
            Ok: true,
            CurrentRoomType: s.CurrentRoomType,
            ActFloor: s.ActFloor,
            CurrentActIndex: s.CurrentActIndex,
            IsGameOver: s.IsGameOver,
            IsVictory: s.IsVictory,
            IsDead: s.IsDead,
            Hp: s.CurrentHp,
            AvailableMapNodes: s.AvailableMapNodes,
            AvailableEventOptions: s.AvailableEventOptions,
            AvailableRestSiteOptions: s.AvailableRestSiteOptions,
            AvailableMerchantItems: s.AvailableMerchantItems,
            CombatState: s.CombatState,
            RewardsState: s.RewardsState,
            Relics: s.Relics,
            OwnedPotions: s.OwnedPotions);
    }

    private static DebugSetHpResult DebugSetHp(Sts2Bindings bindings, Session session, DebugSetHpParams? @params)
    {
        var run = session.Run
            ?? throw new InvalidOperationException("no active run — call run/new first");
        var args = @params
            ?? throw new WireException(WireErrorCode.InvalidParams,
                "debug/set_hp requires params {hp, maxHp?}");

        // Validate up-front so a bad request never reaches the engine.
        // Using InvalidParams (-32602) over a generic ArgumentException so
        // generated clients see the right code on a validation failure.
        if (args.Hp < 0)
        {
            throw new WireException(WireErrorCode.InvalidParams,
                $"debug/set_hp: hp must be >= 0 (got {args.Hp})");
        }
        if (args.MaxHp is not null && args.MaxHp.Value < 1)
        {
            throw new WireException(WireErrorCode.InvalidParams,
                $"debug/set_hp: maxHp must be >= 1 (got {args.MaxHp.Value})");
        }
        // Effective max: the requested maxHp, or the current one if the
        // caller didn't ask to change it.
        var snapshotBefore = bindings.ReadSnapshot(run);
        var effectiveMaxHp = args.MaxHp ?? snapshotBefore.MaxHp;
        if (args.Hp > effectiveMaxHp)
        {
            throw new WireException(WireErrorCode.InvalidParams,
                $"debug/set_hp: hp ({args.Hp}) must be <= maxHp ({effectiveMaxHp})");
        }

        bindings.SetPlayerHp(run, args.Hp, args.MaxHp);

        // Read back through the snapshot (not the helper's tuple) so the
        // wire result reflects whatever the engine actually surfaces post-
        // write — defence in depth against a property/field divergence.
        var s = bindings.ReadSnapshot(run);
        return new DebugSetHpResult(
            Ok: true,
            Hp: s.CurrentHp,
            MaxHp: s.MaxHp,
            IsGameOver: s.IsGameOver);
    }

    private static DebugGiveRelicResult DebugGiveRelic(Sts2Bindings bindings, Session session, DebugGiveRelicParams? @params)
    {
        var run = session.Run
            ?? throw new InvalidOperationException("no active run — call run/new first");
        var args = @params
            ?? throw new ArgumentException("debug/give_relic requires params {relicId}");
        if (string.IsNullOrWhiteSpace(args.RelicId))
            throw new ArgumentException("debug/give_relic relicId must be non-empty");

        bindings.GiveRelic(run, args.RelicId);

        var s = bindings.ReadSnapshot(run);
        return new DebugGiveRelicResult(
            Ok: true,
            RelicId: args.RelicId,
            Hp: s.CurrentHp,
            MaxHp: s.MaxHp,
            Gold: s.Gold,
            DeckSize: s.DeckSize);
    }

    // Adapter that turns a typed Func<TParams?, TResult> into the JsonNode-
    // shaped delegate StdioHost.Handler expects. Deserialisation tolerates a
    // missing or null params object (TParams? default); the caller decides
    // whether to throw for required fields.
    private static StdioHost.Handler Typed<TParams, TResult>(Func<TParams?, TResult> handler)
        => raw =>
        {
            var p = raw is null ? default : raw.Deserialize<TParams>(EnvelopeIo.JsonOptions);
            var r = handler(p);
            return JsonSerializer.SerializeToNode(r, EnvelopeIo.JsonOptions);
        };

    private static StdioHost.Handler TypedNoParams<TResult>(Func<TResult> handler)
        => _ => JsonSerializer.SerializeToNode(handler(), EnvelopeIo.JsonOptions);

    private static (string? Version, string? Sha256) ReadGameVersion(string repoRoot)
    {
        var path = Path.Combine(repoRoot, "GAME_VERSION");
        if (!File.Exists(path)) return (null, null);

        string? version = null, sha = null;
        foreach (var line in File.ReadAllLines(path))
        {
            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 2) continue;
            if (parts[0] == "VERSION") version = string.Join(' ', parts.Skip(1));
            else if (parts[0] == "SHA256") sha = parts[1];
        }
        return (version, sha);
    }
}
