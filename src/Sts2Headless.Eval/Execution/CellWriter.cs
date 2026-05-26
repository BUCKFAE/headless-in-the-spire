using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sts2Headless.Eval.Json;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Eval.Execution;

// Writes the harness-owned files: `runs.jsonl` (append-only at the eval
// root) and `cell.json` (per cell, denormalised forward index from the
// AD-8 directory back to the runs.jsonl row).
//
// Append serialisation is locked so concurrent cell finishers can't
// interleave half-lines. Each line is one CellResult; `cell.json` is a
// strict subset (the same fields trimmed of resource accounting + error
// payload, kept under 1 KB so a directory walk can `cat` thousands).
internal sealed class CellWriter : IDisposable
{
    private readonly object _lock = new();
    private readonly FileStream _runsStream;
    private readonly StreamWriter _runsWriter;

    public string RunsJsonlPath { get; }

    public CellWriter(string runsJsonlPath)
    {
        RunsJsonlPath = runsJsonlPath;
        Directory.CreateDirectory(Path.GetDirectoryName(runsJsonlPath)!);
        _runsStream = new FileStream(runsJsonlPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        _runsWriter = new StreamWriter(_runsStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public void Append(CellResult result, string cellDirAbsolute)
    {
        var line = JsonSerializer.Serialize(result, EvalJson.Wire);
        lock (_lock)
        {
            _runsWriter.WriteLine(line);
            _runsWriter.Flush();
        }

        Directory.CreateDirectory(cellDirAbsolute);
        var cellJson = JsonSerializer.Serialize(CellJson.From(result), EvalJson.Pretty);
        File.WriteAllText(Path.Combine(cellDirAbsolute, "cell.json"), cellJson);
    }

    public void Dispose()
    {
        _runsWriter.Dispose();
        _runsStream.Dispose();
    }
}

// Strict subset of CellResult written into each cell's directory. The
// goal is "a directory walker can `cat cell.json` to find the run row
// without parsing runs.jsonl". Skip resource accounting and the error
// payload — both are noise for the cat-friendly case.
internal sealed record CellJson(
    [property: JsonPropertyName("evalId")]         string                    EvalId,
    [property: JsonPropertyName("agent")]          AgentIdentity             Agent,
    [property: JsonPropertyName("seed")]           ulong                     Seed,
    [property: JsonPropertyName("character")]      Character                 Character,
    [property: JsonPropertyName("ascension")]      int                       Ascension,
    [property: JsonPropertyName("modifiers")]      IReadOnlyList<ModifierId> Modifiers,
    [property: JsonPropertyName("terminus")]       CellTerminus              Terminus,
    [property: JsonPropertyName("act")]            int                       Act,
    [property: JsonPropertyName("floor")]          int                       Floor,
    [property: JsonPropertyName("finalHp")]        int                       FinalHp,
    [property: JsonPropertyName("scoringMetrics")] ScoringMetrics            ScoringMetrics,
    [property: JsonPropertyName("wallClockMs")]    long                      WallClockMs,
    [property: JsonPropertyName("startedAt")]      string?                   StartedAt,
    [property: JsonPropertyName("completedAt")]    string?                   CompletedAt,
    [property: JsonPropertyName("gameVersion")]    string                    GameVersion)
{
    public static CellJson From(CellResult r) => new(
        EvalId:         r.EvalId,
        Agent:          r.Agent,
        Seed:           r.Seed,
        Character:      r.Character,
        Ascension:      r.Ascension,
        Modifiers:      r.Modifiers,
        Terminus:       r.Terminus,
        Act:            r.Act,
        Floor:          r.Floor,
        FinalHp:        r.FinalHp,
        ScoringMetrics: r.Scoring,
        WallClockMs:    r.WallClockMs,
        StartedAt:      r.StartedAt,
        CompletedAt:    r.CompletedAt,
        GameVersion:    r.GameVersion);
}
