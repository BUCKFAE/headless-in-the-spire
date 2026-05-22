using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Headless.MechanicSweep;

// Frozen result of one sweep — the rows it produced, the universe it
// covered, how long it took, what game version it ran against. The
// markdown / JSON renders are self-contained: a reader who only has the
// `.md` should be able to identify which ids crashed, which exception
// they crashed with, and how reproducible the run was.
public sealed record SweepReport(
    string Kind,
    System.Collections.Generic.IReadOnlyList<SweepRow> Rows,
    System.TimeSpan TotalElapsed,
    string GameVersion,
    bool Sampled,
    int UniverseSize)
{
    public int Crashes     => Rows.Count(r => r.Outcome == SweepOutcome.Crashed);
    public int Timeouts    => Rows.Count(r => r.Outcome == SweepOutcome.Timeout);
    public int KnownUnsafe => Rows.Count(r => r.Outcome == SweepOutcome.KnownUnsafe);
    public int Played      => Rows.Count(r => r.Outcome == SweepOutcome.Played);
    public int Triggered   => Rows.Count(r => r.Outcome == SweepOutcome.Triggered);
    public int Unreachable => Rows.Count(r => r.Outcome == SweepOutcome.Unreachable);
    public int Unplayable  => Rows.Count(r => r.Outcome == SweepOutcome.Unplayable);

    // Sort order for the report — crashes first so a human scanning the
    // markdown sees them up top, then timeouts (other failures), then
    // KnownUnsafe (informational-but-still-broken — visible next to the
    // failures so a reader can sanity-check the engine still surfaces
    // the same stack), then the truly informational outcomes.
    private static readonly SweepOutcome[] s_renderOrder =
    [
        SweepOutcome.Crashed,
        SweepOutcome.Timeout,
        SweepOutcome.KnownUnsafe,
        SweepOutcome.Unplayable,
        SweepOutcome.Unreachable,
        SweepOutcome.Played,
        SweepOutcome.Triggered,
    ];

    public string ToMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Mechanic sweep — {Kind}");
        sb.AppendLine();
        sb.AppendLine($"Game version: `{GameVersion}`");
        sb.AppendLine($"Elapsed: **{TotalElapsed.TotalMinutes:0.0} min**");
        sb.AppendLine($"Ids exercised: **{Rows.Count}** (of {UniverseSize} in manifest{(Sampled ? "; sampled subset" : "")})");
        sb.AppendLine();
        sb.AppendLine($"- Crashed:     **{Crashes}** ← failure signal");
        sb.AppendLine($"- Timeout:     **{Timeouts}** ← failure signal");
        if (KnownUnsafe > 0) sb.AppendLine($"- KnownUnsafe: **{KnownUnsafe}** ← engine paths catalogued in SweepKnownIssues");
        if (Played > 0)    sb.AppendLine($"- Played:      **{Played}**");
        if (Triggered > 0) sb.AppendLine($"- Triggered:   **{Triggered}**");
        sb.AppendLine($"- Unreachable: **{Unreachable}**");
        sb.AppendLine($"- Unplayable:  **{Unplayable}**");
        sb.AppendLine();
        sb.AppendLine("| Outcome | Id | Steps | Elapsed | Detail |");
        sb.AppendLine("|---------|----|-------|---------|--------|");
        foreach (var row in Rows
            .OrderBy(r => System.Array.IndexOf(s_renderOrder, r.Outcome))
            .ThenBy(r => r.Id, StringComparer.Ordinal))
        {
            var detail = (row.Detail ?? "")
                .Replace("|", "\\|")
                .Replace('\n', ' ')
                .Replace('\r', ' ');
            sb.AppendLine($"| {row.Outcome} | `{row.Id}` | {row.Steps} | {row.Elapsed.TotalSeconds:0.0}s | {detail} |");
        }
        return sb.ToString();
    }

    private static readonly JsonSerializerOptions s_jsonOpts = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public string ToJson() => JsonSerializer.Serialize(this, s_jsonOpts);
}
