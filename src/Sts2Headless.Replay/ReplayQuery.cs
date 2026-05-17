using System.Text.Json;
using System.Text.Json.Nodes;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Replay;

// Read-side of the replay substrate. Loads a previously-recorded run's
// `run.json` from disk and returns it through our typed mirror.
// Lives in Replay (not Protocol) because it depends on ReplayLayout —
// the on-disk shape is a Replay concern; Protocol just owns the schema.
public static class ReplayQuery
{
    // Loads the .run JSON file for the given run directory and re-encodes
    // it via `RunHistoryDocument.JsonOptions` (snake_case). The JsonNode
    // result threads through the wire envelope unchanged — important
    // because EnvelopeIo.JsonOptions has no naming policy, and we want
    // the wire output to match the .run schema byte-for-byte per AD-8.
    //
    // Throws InvalidOperationException with a caller-meaningful message
    // when the run hasn't been ended yet (the .run file is written by
    // the engine's RunHistorySaveManager.SaveHistory hook, which only
    // fires on RunManager.OnEnded — i.e. death or victory). Throws
    // FileNotFoundException for a malformed run directory.
    public static JsonNode LoadAsWireJson(string runDirectory)
    {
        if (runDirectory is null)
            throw new ArgumentNullException(nameof(runDirectory));
        var path = ReplayLayout.RunHistoryPath(runDirectory);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                $"run history not yet available at {path} — the run hasn't ended (history is only written on RunManager.OnEnded, i.e. death or victory).");
        }
        var doc = RunHistoryDocument.ParseFile(path);
        return JsonSerializer.SerializeToNode(doc, RunHistoryDocument.JsonOptions)
            ?? throw new InvalidDataException("RunHistoryDocument serialised to null JsonNode");
    }

    // Same as LoadAsWireJson but returns the typed record. Used by tests
    // that want to assert on field values directly.
    public static RunHistoryDocument Load(string runDirectory)
    {
        if (runDirectory is null)
            throw new ArgumentNullException(nameof(runDirectory));
        var path = ReplayLayout.RunHistoryPath(runDirectory);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                $"run history not yet available at {path} — the run hasn't ended (history is only written on RunManager.OnEnded, i.e. death or victory).");
        }
        return RunHistoryDocument.ParseFile(path);
    }
}
