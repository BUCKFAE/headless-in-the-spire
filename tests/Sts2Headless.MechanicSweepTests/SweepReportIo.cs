using Sts2Headless.MechanicSweep;

namespace Sts2Headless.MechanicSweepTests;

// Writes sweep reports to documentation/coverage/sweep-<kind>.{md,json}.
// The directory is gitignored (same proprietary-content rationale as the
// modeldb/ probe dumps — the report enumerates ids derived from
// vendor/sts2.dll). Regenerate locally.
internal static class SweepReportIo
{
    public static (string MdPath, string JsonPath) Write(SweepReport report)
    {
        var dir = Path.Combine(RepoRoot(), "documentation", "coverage");
        Directory.CreateDirectory(dir);
        var md = Path.Combine(dir, $"sweep-{report.Kind}.md");
        var json = Path.Combine(dir, $"sweep-{report.Kind}.json");
        File.WriteAllText(md, report.ToMarkdown());
        File.WriteAllText(json, report.ToJson());
        return (md, json);
    }

    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8; i++)
        {
            if (File.Exists(Path.Combine(dir, "Sts2Headless.slnx"))) return dir;
            var p = Directory.GetParent(dir);
            if (p is null) break;
            dir = p.FullName;
        }
        throw new InvalidOperationException("repo root not found from " + AppContext.BaseDirectory);
    }
}
