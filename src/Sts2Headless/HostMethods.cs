using System.Text.Json;
using System.Text.Json.Nodes;
using Sts2Headless.Runtime;

namespace Sts2Headless;

// Method registry. Each pass adds entries; the keys are the wire-level
// method names callers send in `{ "method": "..." }`.
public static class HostMethods
{
    public static IReadOnlyDictionary<string, StdioHost.Handler> Build(string repoRoot, Sts2Bindings bindings)
    {
        return new Dictionary<string, StdioHost.Handler>
        {
            ["host/ping"] = _ => Ping(repoRoot),
            ["run/new"] = p => RunNew(bindings, p),
        };
    }

    private static JsonNode? Ping(string repoRoot)
    {
        var (version, sha256) = ReadGameVersion(repoRoot);
        return new JsonObject
        {
            ["ok"] = true,
            ["gameVersion"] = version,
            ["gameSha256"] = sha256,
        };
    }

    private static JsonNode? RunNew(Sts2Bindings bindings, JsonNode? @params)
    {
        var character = (@params as JsonObject)?["character"]?.GetValue<string>() ?? "ironclad";
        var seed = (@params as JsonObject)?["seed"]?.GetValue<ulong>() ?? 1uL;

        if (!string.Equals(character, "ironclad", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"character '{character}' not yet supported (only 'ironclad')");
        }

        var player = bindings.CreateIroncladRun(seed);
        return new JsonObject
        {
            ["ok"] = true,
            ["character"] = character,
            ["seed"] = seed,
            ["playerType"] = player.GetType().FullName,
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
