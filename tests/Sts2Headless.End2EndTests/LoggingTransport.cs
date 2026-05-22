using System.Text;
using System.Text.Json;
using Sts2Headless.Agents.Contracts;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.End2EndTests;

// Wraps an ITransport and records every call into an in-memory log so a
// hanging or stalling test can dump the call sequence on failure. The
// recorded summary is compact — method name, key params, and a one-line
// digest of the result — to keep the failure message readable while
// preserving enough state to diagnose stalls.
//
// Not a production tool — used by the diagnostic boss/merchant walk tests
// to figure out where the greedy agent gets stuck.
public sealed class LoggingTransport(ITransport inner) : ITransport
{
    private readonly List<string> _log = new();

    public IReadOnlyList<string> Log => _log;

    public async Task<TResult> SendAsync<TResult>(string method, object? @params = null)
    {
        var paramsDigest = ParamsDigest(@params);
        try
        {
            var result = await inner.SendAsync<TResult>(method, @params);
            _log.Add($"{method}({paramsDigest}) → {ResultDigest(result)}");
            return result;
        }
        catch (Exception ex)
        {
            _log.Add($"{method}({paramsDigest}) THREW {ex.GetType().Name}: {ex.Message}");
            throw;
        }
    }

    private static string ParamsDigest(object? p) =>
        p is null ? "" : JsonSerializer.Serialize(p);

    private static string ResultDigest<T>(T result)
    {
        // Heuristic digest. RunStateResult-like shapes get a one-line
        // "room hp floor" summary; everything else falls back to a short
        // type name. Keeps the log scan-able when 100+ entries pile up.
        if (result is RunStateResult s) return RunSummary(s.CurrentRoomType, s.Hp, s.MaxHp, s.ActFloor, s.IsGameOver, s.CombatState);
        if (result is RunSelectMapNodeResult m) return RunSummary(m.CurrentRoomType, m.Hp, -1, m.ActFloor, m.IsGameOver, m.CombatState);
        if (result is RunPlayCardResult pc) return RunSummary(pc.CurrentRoomType, pc.Hp, -1, pc.ActFloor, pc.IsGameOver, pc.CombatState);
        if (result is RunEndTurnResult et) return RunSummary(et.CurrentRoomType, et.Hp, -1, et.ActFloor, et.IsGameOver, et.CombatState);
        if (result is RunSelectRewardResult sr) return RunSummary(sr.CurrentRoomType, sr.Hp, -1, sr.ActFloor, sr.IsGameOver, sr.CombatState);
        if (result is RunSkipRewardResult sk) return RunSummary(sk.CurrentRoomType, sk.Hp, -1, sk.ActFloor, sk.IsGameOver, sk.CombatState);
        return typeof(T).Name;
    }

    private static string RunSummary(RoomType room, int hp, int maxHp, int floor, bool isGameOver, CombatState? combat)
    {
        var sb = new StringBuilder();
        sb.Append(room).Append(" floor=").Append(floor);
        if (maxHp >= 0) sb.Append(" hp=").Append(hp).Append('/').Append(maxHp);
        else sb.Append(" hp=").Append(hp);
        if (isGameOver) sb.Append(" GAME-OVER");
        if (combat is not null)
        {
            sb.Append(" combat[round=").Append(combat.Round)
              .Append(" e=").Append(combat.Energy).Append('/').Append(combat.MaxEnergy)
              .Append(" block=").Append(combat.PlayerBlock)
              .Append(" hand=").Append(combat.Hand.Count)
              .Append(" draw=").Append(combat.DrawPileCount)
              .Append(" disc=").Append(combat.DiscardPileCount)
              .Append(" enemies=[")
              .AppendJoin(',', combat.Enemies.Select(e => $"{e.MonsterId}@{e.Hp}/{e.MaxHp}"))
              .Append(']');
            if (combat.IsPlayPhase) sb.Append(" PLAY"); else sb.Append(" ENEMY");
            if (!combat.IsInProgress) sb.Append(" ENDED");
            sb.Append(']');
        }
        return sb.ToString();
    }
}
