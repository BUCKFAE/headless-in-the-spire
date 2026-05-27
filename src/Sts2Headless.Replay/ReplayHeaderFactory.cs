using System.Reflection;
using Sts2Headless.Protocol.Methods;
using Sts2Headless.Utils;

namespace Sts2Headless.Replay;

// Builds a fully-populated ReplayHeader from the live engine + run
// identity inputs. Pulls model-id hash, release info, schema version
// from the loaded sts2 assembly via reflection; pulls game version
// label + live sts2.dll SHA-256 from Sts2Identity (single helper, see
// its file-level docs for why pin-SHA and live-SHA must not be
// conflated). Run-identity (seed, character, ascension, modifiers,
// start time) comes from the caller.
//
// AD-4: no compile-time reference to sts2 — every engine read goes
// through Assembly.GetType + reflection.
public static class ReplayHeaderFactory
{
    public static ReplayHeader Create(
        Assembly sts2,
        string seed,
        Character character,
        int ascension,
        IReadOnlyList<string> modifiers,
        DateTimeOffset startTime,
        string? agent = null)
    {
        var identity = Sts2Identity.Current;
        var modelIdHash = ReadModelIdHash(sts2);
        var gitCommit = ReadGitCommit(sts2);
        return new ReplayHeader(
            GameVersion: identity.GameVersion,
            Sts2DllSha256: identity.Sts2DllSha256,
            ModelIdHash: modelIdHash,
            GitCommit: gitCommit,
            RunHistorySchemaVersion: ReplayHeader.CurrentRunHistorySchemaVersion,
            ProtocolVersion: ReplayHeader.CurrentProtocolVersion,
            Seed: seed,
            Character: character,
            Ascension: ascension,
            Modifiers: modifiers,
            StartTimeUnix: startTime.ToUnixTimeSeconds(),
            Agent: string.IsNullOrWhiteSpace(agent) ? ReplayHeader.UnknownAgent : agent);
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
