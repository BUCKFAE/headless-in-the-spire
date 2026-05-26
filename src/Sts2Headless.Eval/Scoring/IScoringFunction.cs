namespace Sts2Headless.Eval.Scoring;

// Pluggable scoring. The harness's default is `LexSortScoring`
// (lex-sort win-rate desc, mean-floor desc, median-wall-clock asc) —
// correctness first, depth second, efficiency as tiebreak. Callers
// pass any IScoringFunction via `EvaluationHarnessConfig.Scoring`.
//
// Implementations should be pure: same input cells ⇒ same output
// ranking, byte-for-byte. Two leaderboards produced from the same
// cell list and the same scoring function must be identical.
//
// Name + Version are recorded in `summary.json` and `summary.md` so a
// leaderboard isn't ambiguous about its own sort rule. Two
// leaderboards over the same agents with different scoring functions
// are not silently comparable.
public interface IScoringFunction
{
    string Name    { get; }
    string Version { get; }
    IReadOnlyList<AgentRanking> Rank(IReadOnlyList<CellResult> cells);
}
