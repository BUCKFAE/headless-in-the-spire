using Sts2Headless.Eval;
using Sts2Headless.Eval.Scoring;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.EvalTests;

public sealed class AggregatesTests
{
    [Fact]
    public void Counts_Each_Crash_Attribution_Separately()
    {
        var agent = new AgentIdentity("x", "1.0", "csharp-bundled", "X");
        var cells = new List<CellResult>
        {
            MakeCell(agent, CellTerminus.EngineCrash),
            MakeCell(agent, CellTerminus.EngineCrash),
            MakeCell(agent, CellTerminus.HostCrash),
            MakeCell(agent, CellTerminus.AgentCrash),
            MakeCell(agent, CellTerminus.HarnessError),
            MakeCell(agent, CellTerminus.Timeout),
            MakeCell(agent, CellTerminus.Stalled),
            MakeCell(agent, CellTerminus.MaxSteps),
            MakeCell(agent, CellTerminus.Victory),
            MakeCell(agent, CellTerminus.Death),
        };

        var aggs = AgentAggregates.From(cells);

        Assert.Equal(10, aggs.Cells);
        Assert.Equal(1,  aggs.Wins);
        Assert.Equal(0.1, aggs.WinRate, 5);
        Assert.Equal(2,  aggs.EngineCrashes);
        Assert.Equal(1,  aggs.HostCrashes);
        Assert.Equal(1,  aggs.AgentCrashes);
        Assert.Equal(1,  aggs.HarnessErrors);
        Assert.Equal(1,  aggs.Timeouts);
        Assert.Equal(1,  aggs.Stalled);
        Assert.Equal(1,  aggs.MaxStepsTrips);
    }

    [Fact]
    public void Empty_Input_Yields_All_Zeros()
    {
        var aggs = AgentAggregates.From([]);
        Assert.Equal(0, aggs.Cells);
        Assert.Equal(0.0, aggs.WinRate);
    }

    private static CellResult MakeCell(AgentIdentity agent, CellTerminus terminus) =>
        new(
            EvalId:        "t",
            Agent:         agent,
            Seed:          1,
            Character:     Character.Ironclad,
            Ascension:     0,
            Modifiers:     [],
            Terminus:      terminus,
            FloorReached:  100,
            FinalHp:       0,
            MaxHp:         80,
            Gold:          50,
            DeckSize:      12,
            RelicCount:    1,
            CombatCount:   0,
            EliteCount:    0,
            BossCount:     0,
            TurnsInCombat: 0,
            Steps:         10,
            WallClockMs:   1000,
            ReplayPath:    "cells/x/y",
            GameVersion:   "v0",
            Sts2DllSha256: "sha",
            Scoring:       new ScoringMetrics(0.0));
}
