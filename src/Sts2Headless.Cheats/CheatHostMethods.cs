using System.Text.Json;
using System.Text.Json.Nodes;
using Sts2Headless.Protocol;
using Sts2Headless.Runtime;

namespace Sts2Headless.Cheats;

// Cheat dispatchers in the JsonNode shape the host's StdioHost expects.
// Returned as Func<JsonNode?, JsonNode?> so this project doesn't have to
// reference the Sts2Headless exe (which owns the StdioHost.Handler delegate
// type) — the host registers each entry by adapting the Func into a
// StdioHost.Handler via GateDebug.
//
// Mirrors the pattern in Sts2Headless.HostMethods for the core surface:
// each handler deserialises typed params, dispatches into Sts2Bindings,
// and re-serialises the typed result. Sharing EnvelopeIo.JsonOptions
// keeps the cheat wire shape from drifting from the core wire shape.
public static class CheatHostMethods
{
    // Returns the cheat dispatch table keyed by wire-method name. The host
    // is responsible for gating each entry with --enable-debug (the cheat
    // identity lives in CheatMethodCatalog.All, not in this dictionary).
    public static IReadOnlyDictionary<string, Func<JsonNode?, JsonNode?>> Build(
        Sts2Bindings bindings, Func<RunHandle?> getRun) =>
        new Dictionary<string, Func<JsonNode?, JsonNode?>>
        {
            ["debug/give_relic"] = Typed<DebugGiveRelicParams, DebugGiveRelicResult>(p => DebugGiveRelic(bindings, getRun, p)),
            ["debug/set_hp"] = Typed<DebugSetHpParams, DebugSetHpResult>(p => DebugSetHp(bindings, getRun, p)),
            ["debug/replace_deck"] = Typed<DebugReplaceDeckParams, DebugReplaceDeckResult>(p => DebugReplaceDeck(bindings, getRun, p)),
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

    // Adapter — same shape as the core's HostMethods.Typed<>, kept private
    // here so the cheat surface is wire-compatible without depending on the
    // exe's adapter. Sharing EnvelopeIo.JsonOptions is what keeps the wire
    // shape (camelCase, enum converters, etc.) from drifting.
    private static Func<JsonNode?, JsonNode?> Typed<TParams, TResult>(Func<TParams?, TResult> handler)
        => raw =>
        {
            var p = raw is null ? default : raw.Deserialize<TParams>(EnvelopeIo.JsonOptions);
            var r = handler(p);
            return JsonSerializer.SerializeToNode(r, EnvelopeIo.JsonOptions);
        };
}
