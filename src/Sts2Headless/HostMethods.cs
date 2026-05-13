using System.Text.Json.Nodes;
using Sts2Headless.Runtime;

namespace Sts2Headless;

// Method registry. Each pass adds entries; the keys are the wire-level
// method names callers send in `{ "method": "..." }`.
public static class HostMethods
{
    public static IReadOnlyDictionary<string, StdioHost.Handler> Build(string repoRoot, Sts2Bindings bindings, Session session)
    {
        return new Dictionary<string, StdioHost.Handler>
        {
            ["host/ping"] = _ => Ping(repoRoot),
            ["run/new"] = p => RunNew(bindings, session, p),
            ["run/state"] = _ => RunState(bindings, session),
            ["run/select_map_node"] = p => RunSelectMapNode(bindings, session, p),
        };
    }

    // Public for unit tests: doesn't touch sts2 bindings, only reads
    // GAME_VERSION from disk, so it's safe to exercise without a game install.
    public static JsonNode? Ping(string repoRoot)
    {
        var (version, sha256) = ReadGameVersion(repoRoot);
        return new JsonObject
        {
            ["ok"] = true,
            ["gameVersion"] = version,
            ["gameSha256"] = sha256,
        };
    }

    private static JsonNode? RunNew(Sts2Bindings bindings, Session session, JsonNode? @params)
    {
        var character = (@params as JsonObject)?["character"]?.GetValue<string>() ?? "ironclad";
        var seed = (@params as JsonObject)?["seed"]?.GetValue<ulong>() ?? 1uL;

        if (!string.Equals(character, "ironclad", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"character '{character}' not yet supported (only 'ironclad')");
        }

        // Pass C: full StartRun chain (was just Player.CreateForNewRun in
        // Pass A). Lands the run at MapRoom with StartedWithNeow=false —
        // until the Neow GodotStubs gap is closed, the wire-level run starts
        // post-Neow at the map screen.
        var run = bindings.StartIroncladRun(seed);
        session.Set(run, character.ToLowerInvariant(), seed);

        var snapshot = bindings.ReadSnapshot(run);
        return new JsonObject
        {
            ["ok"] = true,
            ["character"] = character,
            ["seed"] = seed,
            ["playerType"] = run.Player.GetType().FullName,
            ["currentRoomType"] = snapshot.CurrentRoomType,
        };
    }

    private static JsonNode? RunState(Sts2Bindings bindings, Session session)
    {
        var run = session.Run
            ?? throw new InvalidOperationException("no active run — call run/new first");

        var s = bindings.ReadSnapshot(run);
        return new JsonObject
        {
            ["ok"] = true,
            ["character"] = session.Character,
            ["seed"] = session.Seed,
            ["hp"] = s.CurrentHp,
            ["maxHp"] = s.MaxHp,
            ["gold"] = s.Gold,
            ["deckSize"] = s.DeckSize,
            ["currentRoomType"] = s.CurrentRoomType,
            ["actFloor"] = s.ActFloor,
            ["isGameOver"] = s.IsGameOver,
        };
    }

    private static JsonNode? RunSelectMapNode(Sts2Bindings bindings, Session session, JsonNode? @params)
    {
        var run = session.Run
            ?? throw new InvalidOperationException("no active run — call run/new first");

        var obj = @params as JsonObject
            ?? throw new ArgumentException("run/select_map_node requires params {col, row}");
        var col = obj["col"]?.GetValue<int>()
            ?? throw new ArgumentException("run/select_map_node requires 'col'");
        var row = obj["row"]?.GetValue<int>()
            ?? throw new ArgumentException("run/select_map_node requires 'row'");

        bindings.EnterMapCoord(run, col, row);

        var s = bindings.ReadSnapshot(run);
        return new JsonObject
        {
            ["ok"] = true,
            ["col"] = col,
            ["row"] = row,
            ["currentRoomType"] = s.CurrentRoomType,
            ["actFloor"] = s.ActFloor,
            ["isGameOver"] = s.IsGameOver,
            ["hp"] = s.CurrentHp,
        };
    }

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
