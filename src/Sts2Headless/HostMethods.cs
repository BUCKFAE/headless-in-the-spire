using System.Text.Json;
using System.Text.Json.Nodes;
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
    public static IReadOnlyDictionary<string, StdioHost.Handler> Build(string repoRoot, Sts2Bindings bindings, Session session)
    {
        return new Dictionary<string, StdioHost.Handler>
        {
            ["host/ping"] = TypedNoParams(() => Ping(repoRoot)),
            ["run/new"] = Typed<RunNewParams, RunNewResult>(p => RunNew(bindings, session, p)),
            ["run/state"] = TypedNoParams(() => RunState(bindings, session)),
            ["run/select_map_node"] = Typed<RunSelectMapNodeParams, RunSelectMapNodeResult>(p => RunSelectMapNode(bindings, session, p)),
        };
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
        // Neow EventRoom (no dismiss method bound yet — opt-in for tests).
        var run = bindings.StartIroncladRun(seed, withNeow);
        session.Set(run, character, seed);

        var s = bindings.ReadSnapshot(run);
        return new RunNewResult(
            Ok: true,
            Character: character,
            Seed: seed,
            PlayerType: run.Player.GetType().FullName ?? run.Player.GetType().Name,
            CurrentRoomType: s.CurrentRoomType);
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
            IsGameOver: s.IsGameOver);
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
            IsGameOver: s.IsGameOver,
            Hp: s.CurrentHp);
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
