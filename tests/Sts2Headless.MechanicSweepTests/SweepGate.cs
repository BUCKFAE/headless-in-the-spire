namespace Sts2Headless.MechanicSweepTests;

// Environment-variable plumbing shared across every per-kind sweep test:
//
//   * ShouldRun(kind)    — is this kind opted in (umbrella or per-kind flag)?
//   * TrySampleIds(...)  — MECHANIC_SWEEP_SAMPLE=N restricts each sweep to
//                          N deterministic-random ids for a fast pass.
//   * ReadGameVersion()  — surfaces the pinned game version in the report.
//
// Why env vars instead of [Skip]: the sweeps run for hours when unrestricted;
// they should never run by accident from `just validation::dotnet::test-end2end` or an IDE's
// "run all tests" green-bar habit. An explicit opt-in via env var is the
// same lever the old coverage / encounter sweeps used.
internal static class SweepGate
{
    // RUN_<KIND>_SWEEP=1 opts in just that kind. RUN_MECHANIC_SWEEP=1
    // opts every sweep in (the umbrella flag). Either is enough; checking
    // both lets `just validation::dotnet::sweep::cards` set the narrow flag and `just validation::dotnet::sweep::all`
    // set the umbrella one.
    public static bool ShouldRun(string kind)
    {
        if (Environment.GetEnvironmentVariable("RUN_MECHANIC_SWEEP") == "1") return true;
        var perKind = $"RUN_{kind.ToUpperInvariant()}_SWEEP";
        return Environment.GetEnvironmentVariable(perKind) == "1";
    }

    // MECHANIC_SWEEP_FOCUS_IDS=ID1,ID2,... → run only those wire ids (in
    // listed order, intersected with the universe so a typo doesn't
    // smuggle in an unknown id). Used when refining a fixture against
    // the specific ids that crashed in a prior run: re-running the full
    // sweep for a 6-id experiment wastes minutes per pass. Takes
    // precedence over MECHANIC_SWEEP_SAMPLE; setting both is harmless
    // (focus wins) but the focus form is the intentional surface.
    //
    // MECHANIC_SWEEP_SAMPLE=N → N deterministic-random ids (seeded with a
    // fixed value so a sample is reproducible across runs). null means
    // "use the full universe."
    //
    // The shuffle seeds from a constant so the same N ids land in every
    // sample — that way a fast pass is comparable to itself across CI
    // runs and developer machines.
    public static IReadOnlyList<string>? TrySampleIds(IReadOnlyCollection<string> universe)
    {
        var focus = Environment.GetEnvironmentVariable("MECHANIC_SWEEP_FOCUS_IDS");
        if (!string.IsNullOrEmpty(focus))
        {
            var wanted = focus
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.Ordinal);
            var hit = universe.Where(wanted.Contains).ToList();
            // Empty match means every focus id is unknown to this kind —
            // either a typo or the wrong sweep. Sweep classes treat
            // empty/null as "full universe" (Count: > 0 guard), which
            // would silently run hours of work. Throw instead so the
            // user sees the typo at startup.
            if (hit.Count == 0)
            {
                throw new InvalidOperationException(
                    $"MECHANIC_SWEEP_FOCUS_IDS=\"{focus}\" matched no ids in this kind's universe "
                    + $"(universe size {universe.Count}). Check the spelling, or check whether you're "
                    + "targeting the right sweep (e.g. WHIRLWIND is a card id, not a relic id).");
            }
            return hit;
        }

        var s = Environment.GetEnvironmentVariable("MECHANIC_SWEEP_SAMPLE");
        if (string.IsNullOrEmpty(s) || !int.TryParse(s, out var n) || n <= 0) return null;
        var rng = new Random(Seed: 42);
        return [.. universe
            .OrderBy(id => id, StringComparer.Ordinal)
            .OrderBy(_ => rng.Next())
            .Take(Math.Min(n, universe.Count))];
    }

    // Pinned game version surfaces in every report header. Reading
    // GAME_VERSION here means the field always reflects the build the
    // sweep actually ran against — same wire-of-truth as the replay
    // header. Only the first non-empty line is used (the file's second
    // line is the SHA256, which we don't want spamming the markdown
    // header).
    public static string ReadGameVersion()
    {
        try
        {
            var dir = AppContext.BaseDirectory;
            for (var i = 0; i < 8; i++)
            {
                var p = Path.Combine(dir, "GAME_VERSION");
                if (File.Exists(p))
                {
                    foreach (var line in File.ReadAllLines(p))
                    {
                        var t = line.Trim();
                        if (t.Length > 0) return t;
                    }
                    return "unknown";
                }
                var parent = Directory.GetParent(dir);
                if (parent is null) break;
                dir = parent.FullName;
            }
        }
        catch
        {
            // Fall through to "unknown" — never block the sweep on a
            // missing/unreadable version file.
        }
        return "unknown";
    }
}
