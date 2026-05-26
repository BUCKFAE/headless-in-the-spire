using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sts2Headless.Eval.Json;
using Sts2Headless.Protocol.Methods;
using Sts2Headless.Utils;

namespace Sts2Headless.Eval;

// Serialisers for the three harness-owned root-level files:
//   * config.json — the full EvaluationHarnessConfig as-run, including
//     per-agent manifest fingerprints. Drives reproducibility (FR-10).
//   * summary.json — machine-readable per-eval rollup. NFR-1 contract.
//   * summary.md — human-readable mirror of summary.json. Deterministic-
//     ordered for diffability; matches the documentation/coverage/sweep-*
//     shape so the tooling feel is native.
public static class EvaluationReportIo
{
    public static string HarnessVersion { get; } = "0.1.0";

    public static void WriteConfig(
        string                  configJsonPath,
        EvaluationHarnessConfig config,
        string                  evalId,
        GameVersionPin?         gameVersion)
    {
        var manifestSnapshots = config.Agents.Select(a => new SerialisedManifest(
            ManifestType:        a.GetType().FullName ?? a.GetType().Name,
            Name:                a.Name,
            Version:             a.Version,
            Language:            a.Language,
            Description:         a.Description,
            Command:             a.Command,
            Cwd:                 a.Cwd,
            Env:                 a.Env,
            SupportedCharacters: a.SupportedCharacters,
            SupportedAscensions: a.SupportedAscensions,
            SupportedModifiers:  a.SupportedModifiers,
            Budgets:             a.Budgets)).ToList();

        var capture = new SerialisedConfig(
            EvalId:        evalId,
            HarnessVersion: HarnessVersion,
            GameVersion:   gameVersion?.Version ?? "",
            Sts2DllSha256: gameVersion?.Sha256 ?? "",
            Agents:        manifestSnapshots,
            Seeds:         new SeedBankReference(config.Seeds.Name, config.Seeds.Version, config.Seeds.Seeds.Count),
            Characters:    config.Characters,
            Ascensions:    config.Ascensions,
            Modifiers:     config.Modifiers,
            Budgets:       config.Budgets,
            Workers:       config.Workers,
            Scoring:       new ScoringFunctionReference(config.Scoring.Name, config.Scoring.Version),
            Output:        new SerialisedOutputLayout(config.Output.EvalRoot, "function"),
            EnableDeterminismCanary: config.EnableDeterminismCanary,
            CaptureAgentNotes:       config.CaptureAgentNotes);

        Directory.CreateDirectory(Path.GetDirectoryName(configJsonPath)!);
        File.WriteAllText(configJsonPath, JsonSerializer.Serialize(capture, EvalJson.Pretty));
    }

    public static void WriteSummary(EvaluationOutputPaths paths, EvaluationSummary summary)
    {
        File.WriteAllText(paths.SummaryJson,     JsonSerializer.Serialize(summary, EvalJson.Pretty));
        File.WriteAllText(paths.SummaryMarkdown, RenderMarkdown(summary));
    }

    private static string RenderMarkdown(EvaluationSummary s)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Evaluation — {s.EvalId}");
        sb.AppendLine();
        sb.AppendLine($"Game version: `{s.GameVersion}`  ");
        sb.AppendLine($"sts2.dll SHA-256: `{s.Sts2DllSha256}`  ");
        sb.AppendLine($"Seed bank: `{s.SeedBank.Name}` ({s.SeedBank.Count} seeds, version {s.SeedBank.Version})  ");
        sb.AppendLine($"Characters: {(s.Characters.Count == 0 ? "_(none)_" : "`" + string.Join("`, `", s.Characters) + "`")}  ");
        sb.AppendLine($"Ascensions: {(s.Ascensions.Count == 0 ? "_(none)_" : "`" + string.Join("`, `", s.Ascensions) + "`")}  ");
        sb.AppendLine($"Modifiers: {(s.Modifiers.Count == 0 ? "_(none)_" : "`" + string.Join("`, `", s.Modifiers) + "`")}  ");
        sb.AppendLine($"Scoring: `{s.Scoring.Name}` v{s.Scoring.Version}  ");
        sb.AppendLine($"Elapsed: **{FormatMs(s.ElapsedMs)}**  ");
        sb.AppendLine($"Cells: **{s.CellCount}**  ");
        sb.AppendLine($"Workers: {s.Workers}");
        sb.AppendLine();

        var leaderboard = new MarkdownTable()
            .AddColumns(
                ("#",            MarkdownAlign.Right),
                ("Agent",        MarkdownAlign.Left),
                ("Version",      MarkdownAlign.Left),
                ("Wins",         MarkdownAlign.Right),
                ("Win%",         MarkdownAlign.Right),
                ("Mean floor",   MarkdownAlign.Right),
                ("p25 floor",    MarkdownAlign.Right),
                ("Engine⚠",      MarkdownAlign.Right),
                ("Agent⚠",       MarkdownAlign.Right),
                ("Host⚠",        MarkdownAlign.Right),
                ("Timeout",      MarkdownAlign.Right),
                ("Median wall",  MarkdownAlign.Right));
        foreach (var r in s.Ranking)
        {
            var a = r.Aggregates;
            leaderboard.AddRow(
                r.Rank.ToString(CultureInfo.InvariantCulture),
                $"`{r.Agent.Name}`",
                r.Agent.Version,
                $"{a.Wins}/{a.Cells}",
                $"{(a.WinRate * 100).ToString("0.#", CultureInfo.InvariantCulture)}%",
                a.MeanFloor.ToString("0.#", CultureInfo.InvariantCulture),
                a.P25Floor.ToString(CultureInfo.InvariantCulture),
                a.EngineCrashes.ToString(CultureInfo.InvariantCulture),
                a.AgentCrashes.ToString(CultureInfo.InvariantCulture),
                a.HostCrashes.ToString(CultureInfo.InvariantCulture),
                a.Timeouts.ToString(CultureInfo.InvariantCulture),
                FormatMs(a.MedianWallClockMs));
        }
        leaderboard.RenderTo(sb);

        if (s.NotableCells.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Notable cells");
            sb.AppendLine();
            var notable = new MarkdownTable()
                .AddColumns(
                    ("Agent",     MarkdownAlign.Left),
                    ("Seed",      MarkdownAlign.Right),
                    ("Terminus",  MarkdownAlign.Left),
                    ("Floor",     MarkdownAlign.Right),
                    ("Replay",    MarkdownAlign.Left));
            foreach (var nc in s.NotableCells)
            {
                notable.AddRow(
                    $"`{nc.Agent}`",
                    nc.Seed.ToString(CultureInfo.InvariantCulture),
                    nc.Terminus.ToString(),
                    nc.Floor.ToString(CultureInfo.InvariantCulture),
                    $"[{nc.ReplayPath}/]({nc.ReplayPath}/)");
            }
            notable.RenderTo(sb);
        }
        return sb.ToString();
    }

    private static string FormatMs(long ms)
    {
        if (ms < 1000) return $"{ms}ms";
        var ts = TimeSpan.FromMilliseconds(ms);
        if (ts.TotalMinutes < 1) return $"{ts.TotalSeconds:0.0}s";
        if (ts.TotalHours   < 1) return $"{(int)ts.TotalMinutes}m{ts.Seconds}s";
        return $"{(int)ts.TotalHours}h{ts.Minutes}m";
    }

    // ── Captured config shape ────────────────────────────────────────────
    // Lives here so config.json round-trips identically across runs of
    // the same harness version. Agent manifests are captured as a typed
    // record (not via reflection on AgentManifest) so the on-disk shape
    // is grep-able from this file alone.
    internal sealed record SerialisedConfig(
        [property: JsonPropertyName("evalId")]                  string                    EvalId,
        [property: JsonPropertyName("harnessVersion")]          string                    HarnessVersion,
        [property: JsonPropertyName("gameVersion")]             string                    GameVersion,
        [property: JsonPropertyName("sts2DllSha256")]           string                    Sts2DllSha256,
        [property: JsonPropertyName("agents")]                  IReadOnlyList<SerialisedManifest> Agents,
        [property: JsonPropertyName("seeds")]                   SeedBankReference         Seeds,
        [property: JsonPropertyName("characters")]              IReadOnlyList<Character>  Characters,
        [property: JsonPropertyName("ascensions")]              IReadOnlyList<int>        Ascensions,
        [property: JsonPropertyName("modifiers")]               IReadOnlyList<ModifierId> Modifiers,
        [property: JsonPropertyName("budgets")]                 HarnessBudgets            Budgets,
        [property: JsonPropertyName("workers")]                 int?                      Workers,
        [property: JsonPropertyName("scoring")]                 ScoringFunctionReference  Scoring,
        [property: JsonPropertyName("output")]                  SerialisedOutputLayout    Output,
        [property: JsonPropertyName("enableDeterminismCanary")] bool                      EnableDeterminismCanary,
        [property: JsonPropertyName("captureAgentNotes")]       bool                      CaptureAgentNotes);

    internal sealed record SerialisedManifest(
        [property: JsonPropertyName("manifestType")]        string                              ManifestType,
        [property: JsonPropertyName("name")]                string                              Name,
        [property: JsonPropertyName("version")]             string                              Version,
        [property: JsonPropertyName("language")]            string?                             Language,
        [property: JsonPropertyName("description")]         string?                             Description,
        [property: JsonPropertyName("command")]             IReadOnlyList<string>               Command,
        [property: JsonPropertyName("cwd")]                 string?                             Cwd,
        [property: JsonPropertyName("env")]                 IReadOnlyDictionary<string,string>? Env,
        [property: JsonPropertyName("supportedCharacters")] IReadOnlyList<Character>            SupportedCharacters,
        [property: JsonPropertyName("supportedAscensions")] IReadOnlyList<int>                  SupportedAscensions,
        [property: JsonPropertyName("supportedModifiers")]  IReadOnlyList<ModifierId>?          SupportedModifiers,
        [property: JsonPropertyName("budgets")]             HarnessBudgets?                     Budgets);

    internal sealed record SerialisedOutputLayout(
        [property: JsonPropertyName("evalRoot")]        string EvalRoot,
        [property: JsonPropertyName("evalIdGenerator")] string EvalIdGenerator);
}
