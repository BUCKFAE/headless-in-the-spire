namespace Sts2Headless.Eval.Scoring;

// Default IScoringFunction. Lex-sort:
//   1. Win rate (desc) — correctness first.
//   2. Mean depth (desc) — depth as the wider signal when wins are tied.
//   3. Median wall-clock (asc) — efficiency as the deterministic tiebreak.
//
// `Score` displayed on the leaderboard is the win rate. The other axes
// are tiebreakers, not contributors — flatten them into one number only
// in a custom IScoringFunction (e.g. WeightedScoring) when the call
// site genuinely wants a single composite metric.
public sealed class LexSortScoring : IScoringFunction
{
    public string Name    => "lex-sort";
    public string Version => "1.0";

    public IReadOnlyList<AgentRanking> Rank(IReadOnlyList<CellResult> cells) =>
        cells
            .GroupBy(c => c.Agent.Name)
            .Select(g =>
            {
                var aggs = AgentAggregates.From(g);
                return new AgentRanking(
                    Rank: 0,                  // assigned post-sort
                    Agent: g.First().Agent,
                    Score: aggs.WinRate,      // displayed; tiebreakers below
                    Aggregates: aggs);
            })
            .OrderByDescending(r => r.Aggregates.WinRate)
            .ThenByDescending(r => r.Aggregates.MeanDepth)
            .ThenBy(r => r.Aggregates.MedianWallClockMs)
            .ThenBy(r => r.Agent.Name, StringComparer.Ordinal)
            .Select((r, i) => r with { Rank = i + 1 })
            .ToList();
}

public static class ScoringFunctions
{
    public static IScoringFunction Default { get; } = new LexSortScoring();
}
