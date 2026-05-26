using System.Text.Json.Serialization;

namespace Sts2Headless.Eval.Scoring;

// Per-agent rollup of a set of CellResults. The default scoring
// function (LexSortScoring) ranks on a few of these fields; bespoke
// scoring functions consume aggregates rather than recomputing them.
//
// All numeric fields are computed best-effort and lossless: empty cell
// list ⇒ zeroed numerics, NaN-free.
public sealed record AgentAggregates(
    [property: JsonPropertyName("cells")]             int    Cells,
    [property: JsonPropertyName("wins")]              int    Wins,
    [property: JsonPropertyName("winRate")]           double WinRate,
    [property: JsonPropertyName("meanFloor")]         double MeanFloor,
    [property: JsonPropertyName("p25Floor")]          int    P25Floor,
    [property: JsonPropertyName("p50Floor")]          int    P50Floor,
    [property: JsonPropertyName("p75Floor")]          int    P75Floor,
    [property: JsonPropertyName("engineCrashes")]     int    EngineCrashes,
    [property: JsonPropertyName("hostCrashes")]       int    HostCrashes,
    [property: JsonPropertyName("agentCrashes")]      int    AgentCrashes,
    [property: JsonPropertyName("harnessErrors")]     int    HarnessErrors,
    [property: JsonPropertyName("timeouts")]          int    Timeouts,
    [property: JsonPropertyName("stalled")]           int    Stalled,
    [property: JsonPropertyName("maxSteps")]          int    MaxStepsTrips,
    [property: JsonPropertyName("medianWallClockMs")] long   MedianWallClockMs,
    [property: JsonPropertyName("meanWallClockMs")]   long   MeanWallClockMs)
{
    public static AgentAggregates From(IEnumerable<CellResult> cells)
    {
        var rows = cells.ToList();
        if (rows.Count == 0)
            return Empty;

        var floors = rows.Select(r => r.FloorReached).OrderBy(f => f).ToArray();
        var wallclocks = rows.Select(r => r.WallClockMs).OrderBy(t => t).ToArray();
        var wins = rows.Count(r => r.Terminus == CellTerminus.Victory);

        return new AgentAggregates(
            Cells:             rows.Count,
            Wins:              wins,
            WinRate:           (double)wins / rows.Count,
            MeanFloor:         floors.Average(),
            P25Floor:          Percentile(floors, 0.25),
            P50Floor:          Percentile(floors, 0.50),
            P75Floor:          Percentile(floors, 0.75),
            EngineCrashes:     rows.Count(r => r.Terminus == CellTerminus.EngineCrash),
            HostCrashes:       rows.Count(r => r.Terminus == CellTerminus.HostCrash),
            AgentCrashes:      rows.Count(r => r.Terminus == CellTerminus.AgentCrash),
            HarnessErrors:     rows.Count(r => r.Terminus == CellTerminus.HarnessError),
            Timeouts:          rows.Count(r => r.Terminus == CellTerminus.Timeout),
            Stalled:           rows.Count(r => r.Terminus == CellTerminus.Stalled),
            MaxStepsTrips:     rows.Count(r => r.Terminus == CellTerminus.MaxSteps),
            MedianWallClockMs: PercentileLong(wallclocks, 0.50),
            MeanWallClockMs:   (long)wallclocks.Average());
    }

    private static AgentAggregates Empty { get; } = new(
        Cells: 0, Wins: 0, WinRate: 0.0, MeanFloor: 0.0,
        P25Floor: 0, P50Floor: 0, P75Floor: 0,
        EngineCrashes: 0, HostCrashes: 0, AgentCrashes: 0, HarnessErrors: 0,
        Timeouts: 0, Stalled: 0, MaxStepsTrips: 0,
        MedianWallClockMs: 0, MeanWallClockMs: 0);

    private static int Percentile(int[] sortedAsc, double p)
    {
        if (sortedAsc.Length == 0) return 0;
        var idx = Math.Clamp((int)Math.Round(p * (sortedAsc.Length - 1)), 0, sortedAsc.Length - 1);
        return sortedAsc[idx];
    }

    private static long PercentileLong(long[] sortedAsc, double p)
    {
        if (sortedAsc.Length == 0) return 0;
        var idx = Math.Clamp((int)Math.Round(p * (sortedAsc.Length - 1)), 0, sortedAsc.Length - 1);
        return sortedAsc[idx];
    }
}
