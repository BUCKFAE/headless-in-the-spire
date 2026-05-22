using System.Text.Json.Nodes;
using Sts2Headless.Protocol;
using Sts2Headless.Runtime.Bindings;

namespace Sts2Headless.Cheats;

// Cheat dispatchers in the JsonNode shape the host's StdioHost expects.
// Returned as Func<JsonNode?, JsonNode?> so this project doesn't have to
// reference the Sts2Headless exe (which owns the StdioHost.Handler delegate
// type) — the host registers each entry by adapting the Func into a
// StdioHost.Handler via GateDebug.
//
// Handlers deserialise typed params, dispatch into Sts2Bindings, and
// re-serialise the typed result via the shared Protocol.WireHandlers.Typed
// adapter — same wire shape (EnvelopeIo.JsonOptions) as the core host.
public static class CheatHostMethods
{
    // Returns the cheat dispatch table keyed by wire-method name. The host
    // is responsible for gating each entry with --enable-debug (the cheat
    // identity lives in CheatMethodCatalog.All, not in this dictionary).
    public static IReadOnlyDictionary<string, Func<JsonNode?, JsonNode?>> Build(
        Sts2Bindings bindings, Func<RunHandle?> getRun) =>
        new Dictionary<string, Func<JsonNode?, JsonNode?>>
        {
            ["debug/give_relic"] = WireHandlers.Typed<DebugGiveRelicParams, DebugGiveRelicResult>(p => DebugGiveRelic(bindings, getRun, p)),
            ["debug/give_potion"] = WireHandlers.Typed<DebugGivePotionParams, DebugGivePotionResult>(p => DebugGivePotion(bindings, getRun, p)),
            ["debug/start_event"] = WireHandlers.Typed<DebugStartEventParams, DebugStartEventResult>(p => DebugStartEvent(bindings, getRun, p)),
            ["debug/apply_power"] = WireHandlers.Typed<DebugApplyPowerParams, DebugApplyPowerResult>(p => DebugApplyPower(bindings, getRun, p)),
            ["debug/afflict_card"] = WireHandlers.Typed<DebugAfflictCardParams, DebugAfflictCardResult>(p => DebugAfflictCard(bindings, getRun, p)),
            ["debug/enchant_card"] = WireHandlers.Typed<DebugEnchantCardParams, DebugEnchantCardResult>(p => DebugEnchantCard(bindings, getRun, p)),
            ["debug/set_hp"] = WireHandlers.Typed<DebugSetHpParams, DebugSetHpResult>(p => DebugSetHp(bindings, getRun, p)),
            ["debug/replace_deck"] = WireHandlers.Typed<DebugReplaceDeckParams, DebugReplaceDeckResult>(p => DebugReplaceDeck(bindings, getRun, p)),
            ["debug/read_deck"] = WireHandlers.Typed<DebugReadDeckParams, DebugReadDeckResult>(_ => DebugReadDeck(bindings, getRun)),
            ["debug/kill_all_enemies"] = WireHandlers.Typed<DebugKillAllEnemiesParams, DebugKillAllEnemiesResult>(_ => DebugKillAllEnemies(bindings, getRun)),
            ["debug/start_combat"] = WireHandlers.Typed<DebugStartCombatParams, DebugStartCombatResult>(p => DebugStartCombat(bindings, getRun, p)),
        };

    private static DebugSetHpResult DebugSetHp(Sts2Bindings bindings, Func<RunHandle?> getRun, DebugSetHpParams? @params)
    {
        var run = getRun()
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

    private static DebugReplaceDeckResult DebugReplaceDeck(Sts2Bindings bindings, Func<RunHandle?> getRun, DebugReplaceDeckParams? @params)
    {
        var run = getRun()
            ?? throw new InvalidOperationException("no active run — call run/new first");
        var args = @params
            ?? throw new WireException(WireErrorCode.InvalidParams,
                "debug/replace_deck requires params {cards: [{cardId, upgradeLevel?}]}");
        if (args.Cards is null || args.Cards.Count == 0)
            throw new WireException(WireErrorCode.InvalidParams,
                "debug/replace_deck: cards must be a non-empty list");
        foreach (var c in args.Cards)
        {
            if (string.IsNullOrWhiteSpace(c.CardId))
                throw new WireException(WireErrorCode.InvalidParams,
                    "debug/replace_deck: every card needs a non-empty cardId");
            if (c.UpgradeLevel < 0)
                throw new WireException(WireErrorCode.InvalidParams,
                    $"debug/replace_deck: upgradeLevel must be >= 0 (got {c.UpgradeLevel} for {c.CardId})");
        }

        IReadOnlyList<string> added;
        try
        {
            var pairs = args.Cards.Select(c => (c.CardId, c.UpgradeLevel)).ToList();
            added = bindings.ReplaceDeck(run, pairs);
        }
        catch (InvalidOperationException ex)
        {
            // Surface "unknown card id" and similar caller errors as
            // InvalidParams so generated clients get the right code.
            throw new WireException(WireErrorCode.InvalidParams, ex.Message);
        }

        var s = bindings.ReadSnapshot(run);
        return new DebugReplaceDeckResult(
            Ok: true,
            DeckSize: s.DeckSize,
            CardIds: added);
    }

    private static DebugReadDeckResult DebugReadDeck(Sts2Bindings bindings, Func<RunHandle?> getRun)
    {
        var run = getRun()
            ?? throw new InvalidOperationException("no active run — call run/new first");

        var cards = bindings.ReadDeck(run)
            .Select(c => new CardSpec(c.CardId, c.UpgradeLevel))
            .ToList();
        return new DebugReadDeckResult(Ok: true, DeckSize: cards.Count, Cards: cards);
    }

    private static DebugKillAllEnemiesResult DebugKillAllEnemies(Sts2Bindings bindings, Func<RunHandle?> getRun)
    {
        var run = getRun()
            ?? throw new InvalidOperationException("no active run — call run/new first");

        // No param validation: the cheat takes nothing. Outside-of-combat
        // calls return killed=0 as a no-op (see DebugKillAllEnemiesParams
        // for why — this gets fired on every tick by full-run tests).
        var (killed, combatEnded) = bindings.KillAllEnemies(run);
        return new DebugKillAllEnemiesResult(Ok: true, Killed: killed, CombatEnded: combatEnded);
    }

    private static DebugStartCombatResult DebugStartCombat(Sts2Bindings bindings, Func<RunHandle?> getRun, DebugStartCombatParams? @params)
    {
        var run = getRun()
            ?? throw new InvalidOperationException("no active run — call run/new first");
        var args = @params
            ?? throw new WireException(WireErrorCode.InvalidParams,
                "debug/start_combat requires params {encounterId}");
        if (string.IsNullOrWhiteSpace(args.EncounterId))
            throw new WireException(WireErrorCode.InvalidParams,
                "debug/start_combat: encounterId must be non-empty");

        try
        {
            var (inProgress, enemyCount) = bindings.StartCombat(run, args.EncounterId);
            return new DebugStartCombatResult(
                Ok: true,
                EncounterId: args.EncounterId,
                InProgress: inProgress,
                EnemyCount: enemyCount);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("unknown encounter id"))
        {
            // Surface unknown ids as InvalidParams so generated clients get
            // the right code (same pattern as ReplaceDeck's unknown-card path).
            throw new WireException(WireErrorCode.InvalidParams, ex.Message);
        }
    }

    private static DebugGiveRelicResult DebugGiveRelic(Sts2Bindings bindings, Func<RunHandle?> getRun, DebugGiveRelicParams? @params)
    {
        var run = getRun()
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

    private static DebugAfflictCardResult DebugAfflictCard(Sts2Bindings bindings, Func<RunHandle?> getRun, DebugAfflictCardParams? @params)
    {
        var run = getRun()
            ?? throw new InvalidOperationException("no active run — call run/new first");
        var args = @params
            ?? throw new WireException(WireErrorCode.InvalidParams,
                "debug/afflict_card requires params {afflictionId, handIndex?, amount?}");
        if (string.IsNullOrWhiteSpace(args.AfflictionId))
            throw new WireException(WireErrorCode.InvalidParams,
                "debug/afflict_card afflictionId must be non-empty");
        if (args.HandIndex < 0)
            throw new WireException(WireErrorCode.InvalidParams,
                $"debug/afflict_card handIndex must be >= 0 (got {args.HandIndex})");

        try
        {
            var cardId = bindings.AfflictCard(run, args.AfflictionId, args.HandIndex, args.Amount);
            return new DebugAfflictCardResult(
                Ok: true,
                AfflictionId: args.AfflictionId,
                HandIndex: args.HandIndex,
                CardId: cardId);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("unknown affliction id", StringComparison.Ordinal)
                                                 || ex.Message.Contains("no active combat", StringComparison.Ordinal)
                                                 || ex.Message.Contains("no card at hand index", StringComparison.Ordinal))
        {
            throw new WireException(WireErrorCode.InvalidParams, ex.Message);
        }
    }

    private static DebugEnchantCardResult DebugEnchantCard(Sts2Bindings bindings, Func<RunHandle?> getRun, DebugEnchantCardParams? @params)
    {
        var run = getRun()
            ?? throw new InvalidOperationException("no active run — call run/new first");
        var args = @params
            ?? throw new WireException(WireErrorCode.InvalidParams,
                "debug/enchant_card requires params {enchantmentId, handIndex?, amount?}");
        if (string.IsNullOrWhiteSpace(args.EnchantmentId))
            throw new WireException(WireErrorCode.InvalidParams,
                "debug/enchant_card enchantmentId must be non-empty");
        if (args.HandIndex < 0)
            throw new WireException(WireErrorCode.InvalidParams,
                $"debug/enchant_card handIndex must be >= 0 (got {args.HandIndex})");

        try
        {
            var cardId = bindings.EnchantCard(run, args.EnchantmentId, args.HandIndex, args.Amount);
            return new DebugEnchantCardResult(
                Ok: true,
                EnchantmentId: args.EnchantmentId,
                HandIndex: args.HandIndex,
                CardId: cardId);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("unknown enchantment id", StringComparison.Ordinal)
                                                 || ex.Message.Contains("no active combat", StringComparison.Ordinal)
                                                 || ex.Message.Contains("no card at hand index", StringComparison.Ordinal))
        {
            throw new WireException(WireErrorCode.InvalidParams, ex.Message);
        }
    }

    private static DebugApplyPowerResult DebugApplyPower(Sts2Bindings bindings, Func<RunHandle?> getRun, DebugApplyPowerParams? @params)
    {
        var run = getRun()
            ?? throw new InvalidOperationException("no active run — call run/new first");
        var args = @params
            ?? throw new WireException(WireErrorCode.InvalidParams,
                "debug/apply_power requires params {powerId, amount?, enemyIndex?}");
        if (string.IsNullOrWhiteSpace(args.PowerId))
            throw new WireException(WireErrorCode.InvalidParams,
                "debug/apply_power powerId must be non-empty");
        if (args.EnemyIndex is < 0)
            throw new WireException(WireErrorCode.InvalidParams,
                $"debug/apply_power enemyIndex must be >= 0 if provided (got {args.EnemyIndex})");

        try
        {
            var (appliedAmount, target) = bindings.ApplyPower(run, args.PowerId, args.Amount, args.EnemyIndex);
            return new DebugApplyPowerResult(
                Ok: true,
                PowerId: args.PowerId,
                AppliedAmount: appliedAmount,
                TargetDescription: target);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("unknown power id", StringComparison.Ordinal))
        {
            throw new WireException(WireErrorCode.InvalidParams, ex.Message);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("no enemy at index", StringComparison.Ordinal)
                                                 || ex.Message.Contains("no active combat", StringComparison.Ordinal))
        {
            throw new WireException(WireErrorCode.InvalidParams, ex.Message);
        }
    }

    private static DebugStartEventResult DebugStartEvent(Sts2Bindings bindings, Func<RunHandle?> getRun, DebugStartEventParams? @params)
    {
        var run = getRun()
            ?? throw new InvalidOperationException("no active run — call run/new first");
        var args = @params
            ?? throw new WireException(WireErrorCode.InvalidParams,
                "debug/start_event requires params {eventId}");
        if (string.IsNullOrWhiteSpace(args.EventId))
            throw new WireException(WireErrorCode.InvalidParams,
                "debug/start_event eventId must be non-empty");

        try
        {
            var (roomType, optionsCount) = bindings.StartEvent(run, args.EventId);
            return new DebugStartEventResult(
                Ok: true,
                EventId: args.EventId,
                CurrentRoomType: roomType,
                OptionsCount: optionsCount);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("unknown event id", StringComparison.Ordinal))
        {
            throw new WireException(WireErrorCode.InvalidParams, ex.Message);
        }
    }

    private static DebugGivePotionResult DebugGivePotion(Sts2Bindings bindings, Func<RunHandle?> getRun, DebugGivePotionParams? @params)
    {
        var run = getRun()
            ?? throw new InvalidOperationException("no active run — call run/new first");
        var args = @params
            ?? throw new WireException(WireErrorCode.InvalidParams,
                "debug/give_potion requires params {potionId}");
        if (string.IsNullOrWhiteSpace(args.PotionId))
            throw new WireException(WireErrorCode.InvalidParams,
                "debug/give_potion potionId must be non-empty");

        try
        {
            var (slotIndex, count) = bindings.GivePotion(run, args.PotionId);
            return new DebugGivePotionResult(
                Ok: true,
                PotionId: args.PotionId,
                SlotIndex: slotIndex,
                PotionCount: count);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("unknown potion id", StringComparison.Ordinal))
        {
            // Surface unknown ids as InvalidParams (-32602) so generated
            // clients get the right code, same pattern as start_combat /
            // replace_deck.
            throw new WireException(WireErrorCode.InvalidParams, ex.Message);
        }
    }

}
