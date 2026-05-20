using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Replay;

// The runs-index file (`<root>/runs.json`). One entry per recorded run,
// derived from each manifest.json. Rebuilt on every recorder finalize by
// walking the directory tree — that makes it race-tolerant under
// parallel hosts: even if two finalizers run simultaneously, each walks
// the directory, builds the full list, and writes (temp+rename) — the
// last writer wins, but every writer's view is correct, so no run gets
// lost in the index.
//
// This file is *not* a replay artifact in the AD-8 sense (it's not bytes
// the game produces); it's a host-side convenience for tools that want
// to enumerate runs without recursing the tree themselves. The viewer
// is the primary consumer.
public sealed record ReplayRunIndex(
    int Version,
    IReadOnlyList<ReplayRunIndexEntry> Runs)
{
    public const int CurrentVersion = 1;

    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };
}

public sealed record ReplayRunIndexEntry(
    string RunId,
    string RelPath,
    string GameVersion,
    string Seed,
    Character Character,
    string Agent,
    string DisplayName,
    ReplayCombatOutcome Outcome,
    long StartedAtUnix,
    long? EndedAtUnix,
    int CombatCount);

public static class ReplayIndex
{
    // Rebuild <root>/runs.json by walking <root>/<version>/<run-id>/manifest.json.
    // Returns the number of entries written.
    //
    // The walk is two levels deep — <version>/<run-id>/ — because that's
    // ReplayLayout.RunDirectory's shape. Files that fail to parse are
    // logged to stderr and skipped, so one corrupted manifest doesn't
    // kill the rest of the index.
    public static int Rebuild(string root)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (!Directory.Exists(root))
        {
            return 0;
        }

        var entries = new List<ReplayRunIndexEntry>();
        foreach (var versionDir in Directory.EnumerateDirectories(root))
        {
            foreach (var runDir in Directory.EnumerateDirectories(versionDir))
            {
                var manifestPath = ReplayLayout.ManifestPath(runDir);
                if (!File.Exists(manifestPath))
                {
                    continue;
                }
                try
                {
                    var json = File.ReadAllText(manifestPath);
                    var manifest = ReplayManifest.Deserialize(json);
                    var relPath = Path.GetRelativePath(root, runDir)
                        .Replace(Path.DirectorySeparatorChar, '/');
                    var entry = ToEntry(manifest, relPath, runDir);
                    entries.Add(entry);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(
                        $"ReplayIndex.Rebuild: skipping {manifestPath}: {ex.Message}");
                }
            }
        }

        // Sort newest-first so the viewer's natural top-of-list is the
        // most recent run — matches the way developers reason about
        // recordings ("the one I just made"). Ties broken by run_id
        // (which has a pid suffix) for stable ordering.
        entries.Sort((a, b) =>
        {
            var byStart = b.StartedAtUnix.CompareTo(a.StartedAtUnix);
            return byStart != 0 ? byStart : string.CompareOrdinal(b.RunId, a.RunId);
        });

        var doc = new ReplayRunIndex(Version: ReplayRunIndex.CurrentVersion, Runs: entries);
        var payload = JsonSerializer.Serialize(doc, ReplayRunIndex.JsonOptions);

        // Temp + rename so a half-written file never appears at the
        // canonical path. Two finalizers racing here will each write
        // their own temp and rename atomically; last writer wins but
        // both views were complete, so the loser's content is identical
        // up to its own timestamp.
        var indexPath = ReplayLayout.RunsIndexPath(root);
        var tempPath = indexPath + ".tmp-" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
        File.WriteAllText(tempPath, payload);
        try
        {
            File.Move(tempPath, indexPath, overwrite: true);
        }
        catch
        {
            // Best-effort cleanup if rename failed (e.g. concurrent
            // rename collided on some filesystems). The temp file is
            // self-identifying via pid so it won't be claimed by
            // another writer's reader.
            try { File.Delete(tempPath); } catch { /* ignore */ }
            throw;
        }
        return entries.Count;
    }

    private static ReplayRunIndexEntry ToEntry(ReplayManifest manifest, string relPath, string runDir)
    {
        var runId = Path.GetFileName(runDir);
        return new ReplayRunIndexEntry(
            RunId: runId,
            RelPath: relPath,
            GameVersion: manifest.Header.GameVersion,
            Seed: manifest.Header.Seed,
            Character: manifest.Header.Character,
            Agent: manifest.Header.Agent,
            DisplayName: manifest.DisplayName ?? runId,
            Outcome: manifest.Outcome,
            StartedAtUnix: manifest.Header.StartTimeUnix,
            EndedAtUnix: manifest.EndedAtUnix,
            CombatCount: manifest.Combats.Count);
    }
}
