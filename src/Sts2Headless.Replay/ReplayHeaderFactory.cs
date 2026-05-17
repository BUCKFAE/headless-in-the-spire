using System.Reflection;
using System.Security.Cryptography;
using Sts2Headless.Protocol.Methods;
using Sts2Headless.Runtime;

namespace Sts2Headless.Replay;

// Builds a fully-populated ReplayHeader from the live engine + run
// identity inputs. Pulls model-id hash, release info, schema version
// from the loaded sts2 assembly via reflection; pulls game version /
// dll SHA-256 from GAME_VERSION (the AD-3 pin file). Run-identity
// (seed, character, ascension, modifiers, start time) comes from the
// caller.
//
// AD-4: no compile-time reference to sts2 — every engine read goes
// through Assembly.GetType + reflection.
public static class ReplayHeaderFactory
{
    // Builds the header for a run that's about to start (or just
    // started). `gameVersion` and `sts2DllSha256` come from GAME_VERSION
    // — Read(repoRoot) is the convenience.
    public static ReplayHeader Create(
        Assembly sts2,
        string gameVersion,
        string sts2DllSha256,
        string seed,
        Character character,
        int ascension,
        IReadOnlyList<string> modifiers,
        DateTimeOffset startTime)
    {
        var modelIdHash = ReadModelIdHash(sts2);
        var gitCommit = ReadGitCommit(sts2);
        return new ReplayHeader(
            GameVersion: gameVersion,
            Sts2DllSha256: sts2DllSha256,
            ModelIdHash: modelIdHash,
            GitCommit: gitCommit,
            RunHistorySchemaVersion: ReplayHeader.CurrentRunHistorySchemaVersion,
            ProtocolVersion: ReplayHeader.CurrentProtocolVersion,
            Seed: seed,
            Character: character,
            Ascension: ascension,
            Modifiers: modifiers,
            StartTimeUnix: startTime.ToUnixTimeSeconds());
    }

    // Reads GAME_VERSION (AD-3 pin) and returns the parsed (version, sha256)
    // tuple. Falls back to "UNKNOWN" / "" if the file is absent — the
    // caller can decide whether that's acceptable for the use case.
    public static (string Version, string Sha256) ReadGameVersionPin(string? repoRoot = null)
    {
        var root = repoRoot ?? Paths.LocateRepoRoot();
        var path = Path.Combine(root, "GAME_VERSION");
        if (!File.Exists(path)) return ("UNKNOWN", "");

        string version = "UNKNOWN", sha = "";
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.StartsWith("VERSION", StringComparison.Ordinal))
                version = line["VERSION".Length..].Trim();
            else if (line.StartsWith("SHA256", StringComparison.Ordinal))
                sha = line["SHA256".Length..].Trim();
        }
        return (version, sha);
    }

    // Computes the SHA-256 of an arbitrary file (used by tests to
    // cross-check the GAME_VERSION pin matches the actual vendor/sts2.dll
    // bytes). Not load-bearing for header construction — the pin file is
    // the source of truth — but useful for diagnostics.
    public static string ComputeSha256(string path)
    {
        using var sha = SHA256.Create();
        using var fs = File.OpenRead(path);
        var hash = sha.ComputeHash(fs);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static uint ReadModelIdHash(Assembly sts2)
    {
        var cacheType = sts2.GetType("MegaCrit.Sts2.Core.Multiplayer.Serialization.ModelIdSerializationCache")
            ?? throw new InvalidOperationException("ModelIdSerializationCache not in sts2");
        var hashProp = cacheType.GetProperty("Hash", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("ModelIdSerializationCache.Hash not found");
        return (uint)(hashProp.GetValue(null) ?? throw new InvalidOperationException("ModelIdSerializationCache.Hash is null — bootstrap missing"));
    }

    // ReleaseInfoManager.Instance.ReleaseInfo?.Commit gives us the
    // build's git short-hash. The fallback chain mirrors what
    // CombatReplayWriter.RecordInitialState does (line 62 of the
    // decompile): release info, then GitHelper.ShortCommitId, then
    // "UNKNOWN". GitHelper only populates in editor builds so it's
    // effectively always null for us.
    private static string ReadGitCommit(Assembly sts2)
    {
        var managerType = sts2.GetType("MegaCrit.Sts2.Core.Debug.ReleaseInfoManager");
        var instanceProp = managerType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        var instance = instanceProp?.GetValue(null);
        var releaseInfoProp = managerType?.GetProperty("ReleaseInfo", BindingFlags.Public | BindingFlags.Instance);
        var releaseInfo = releaseInfoProp?.GetValue(instance);
        if (releaseInfo is not null)
        {
            var commitProp = releaseInfo.GetType().GetProperty("Commit", BindingFlags.Public | BindingFlags.Instance);
            if (commitProp?.GetValue(releaseInfo) is string c && !string.IsNullOrEmpty(c)) return c;
        }
        var gitHelperType = sts2.GetType("MegaCrit.Sts2.Core.Debug.GitHelper");
        var shortCommitProp = gitHelperType?.GetProperty("ShortCommitId", BindingFlags.Public | BindingFlags.Static);
        if (shortCommitProp?.GetValue(null) is string g && !string.IsNullOrEmpty(g)) return g;
        return "UNKNOWN";
    }
}
