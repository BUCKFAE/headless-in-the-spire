using System.Text.Json;
using Sts2Headless.Agents;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.End2EndTests;

// Wraps an ITransport, tracks the most-recently-observed CombatState
// (from any wire response that surfaces one), and on any failing
// `run/play_card` call enriches the thrown exception's message with
// the CardId the agent was attempting to play, the room context, and
// the last N wire calls. That's enough to map an engine NRE back to a
// specific card without re-running with a fresh recorder.
//
// Built specifically for the IroncladAgentA0Tests crash hunt — when the
// hunt is done this can probably be folded into LoggingTransport or
// deleted.
public sealed class CrashTracingTransport(ITransport inner) : ITransport
{
    private readonly List<string> _log = new();
    private CombatState? _latestCombat;
    private RoomType _latestRoom;
    private int _latestFloor;
    private int _latestHp;

    public IReadOnlyList<string> Log => _log;

    public async Task<TResult> SendAsync<TResult>(string method, object? @params = null)
    {
        var paramsJson = @params is null ? "" : JsonSerializer.Serialize(@params);

        // Pre-resolve the played-card diagnostic so it survives even if
        // SendAsync throws — params is captured here, the latest hand
        // is in _latestCombat.
        var playedCardDigest = method == "run/play_card"
            ? TryDescribePlayCard(@params)
            : null;

        try
        {
            var result = await inner.SendAsync<TResult>(method, @params);
            ObserveResult(result);
            _log.Add($"{method}({paramsJson}) → {ResultDigest(result)}");
            return result;
        }
        catch (Exception ex) when (method == "run/play_card")
        {
            // Rewrap with the card diagnostic.
            var enriched = $"{ex.Message}\n"
                + $"played-card: {playedCardDigest ?? "<no combat state cached>"}\n"
                + $"room: {_latestRoom} floor: {_latestFloor} hp: {_latestHp}\n"
                + $"recent wire calls (last 8):\n"
                + string.Join("\n", _log.TakeLast(8));
            throw new CrashTracingException(enriched, ex);
        }
    }

    private void ObserveResult<T>(T result)
    {
        // For every result that exposes a CurrentRoomType + CombatState,
        // cache them so the next play_card can report context.
        switch (result)
        {
            case RunStateResult s:
                _latestRoom = s.CurrentRoomType;
                _latestFloor = s.ActFloor;
                _latestHp = s.Hp;
                _latestCombat = s.CombatState;
                break;
            case RunSelectMapNodeResult m:
                _latestRoom = m.CurrentRoomType;
                _latestFloor = m.ActFloor;
                _latestHp = m.Hp;
                _latestCombat = m.CombatState;
                break;
            case RunPlayCardResult pc:
                _latestRoom = pc.CurrentRoomType;
                _latestFloor = pc.ActFloor;
                _latestHp = pc.Hp;
                _latestCombat = pc.CombatState;
                break;
            case RunEndTurnResult et:
                _latestRoom = et.CurrentRoomType;
                _latestFloor = et.ActFloor;
                _latestHp = et.Hp;
                _latestCombat = et.CombatState;
                break;
            case RunSelectRewardResult sr:
                _latestRoom = sr.CurrentRoomType;
                _latestFloor = sr.ActFloor;
                _latestHp = sr.Hp;
                _latestCombat = sr.CombatState;
                break;
            case RunSkipRewardResult sk:
                _latestRoom = sk.CurrentRoomType;
                _latestFloor = sk.ActFloor;
                _latestHp = sk.Hp;
                _latestCombat = sk.CombatState;
                break;
        }
    }

    private string? TryDescribePlayCard(object? @params)
    {
        if (@params is not RunPlayCardParams p) return null;
        if (_latestCombat is null) return $"index={p.CardIndex} target={p.TargetIndex} (no combat cache)";
        var card = _latestCombat.Hand.FirstOrDefault(c => c.Index == p.CardIndex);
        if (card is null) return $"index={p.CardIndex} target={p.TargetIndex} (NOT IN HAND — hand={string.Join(',', _latestCombat.Hand.Select(h => $"{h.Id}@{h.Index}"))})";
        return $"index={p.CardIndex} target={p.TargetIndex} card={card.Id} cost={card.Cost} canPlay={card.CanPlay} targetType={card.TargetType}";
    }

    private static string ResultDigest<T>(T result)
    {
        if (result is RunStateResult s) return $"{s.CurrentRoomType} f={s.ActFloor} hp={s.Hp}";
        if (result is RunSelectMapNodeResult m) return $"{m.CurrentRoomType} f={m.ActFloor} hp={m.Hp}";
        if (result is RunPlayCardResult pc) return $"{pc.CurrentRoomType} f={pc.ActFloor} hp={pc.Hp}";
        if (result is RunEndTurnResult et) return $"{et.CurrentRoomType} f={et.ActFloor} hp={et.Hp}";
        if (result is RunSelectRewardResult sr) return $"{sr.CurrentRoomType} f={sr.ActFloor} hp={sr.Hp}";
        if (result is RunSkipRewardResult sk) return $"{sk.CurrentRoomType} f={sk.ActFloor} hp={sk.Hp}";
        return typeof(T).Name;
    }
}

public sealed class CrashTracingException(string message, Exception inner) : Exception(message, inner)
{
}
