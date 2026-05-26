using Sts2Headless.Eval;
using Sts2Headless.Eval.Scoring;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.EvalTests;

public sealed class LexSortScoringTests
{
    [Fact]
    public void Ranks_Wins_Then_Mean_Floor_Then_Wall_Clock()
    {
        var alpha   = MakeIdentity("alpha");
        var beta    = MakeIdentity("beta");
        var gamma   = MakeIdentity("gamma");

        var cells = new List<CellResult>
        {
            // alpha: 2 wins out of 3, mean floor ~200, fast.
            MakeCell(alpha, seed: 1, terminus: CellTerminus.Victory, floor: 305, wallMs: 1000),
            MakeCell(alpha, seed: 2, terminus: CellTerminus.Victory, floor: 310, wallMs: 1000),
            MakeCell(alpha, seed: 3, terminus: CellTerminus.Death,   floor: 110, wallMs: 1000),

            // beta: 1 win out of 3, mean floor higher, slow.
            MakeCell(beta, seed: 1, terminus: CellTerminus.Victory, floor: 305, wallMs: 9000),
            MakeCell(beta, seed: 2, terminus: CellTerminus.Death,   floor: 220, wallMs: 9000),
            MakeCell(beta, seed: 3, terminus: CellTerminus.Death,   floor: 215, wallMs: 9000),

            // gamma: zero wins.
            MakeCell(gamma, seed: 1, terminus: CellTerminus.Death, floor: 110, wallMs: 500),
            MakeCell(gamma, seed: 2, terminus: CellTerminus.Death, floor: 105, wallMs: 500),
            MakeCell(gamma, seed: 3, terminus: CellTerminus.Death, floor: 100, wallMs: 500),
        };

        var ranking = new LexSortScoring().Rank(cells);

        Assert.Equal(3, ranking.Count);
        Assert.Equal("alpha", ranking[0].Agent.Name);
        Assert.Equal("beta",  ranking[1].Agent.Name);
        Assert.Equal("gamma", ranking[2].Agent.Name);
        Assert.Equal(1, ranking[0].Rank);
        Assert.Equal(2, ranking[1].Rank);
        Assert.Equal(3, ranking[2].Rank);
    }

    [Fact]
    public void Ties_Resolved_By_Agent_Name_Ordinal()
    {
        // Two agents with identical performance: lex-sort then falls
        // back to ordinal name to keep two leaderboards from the same
        // data byte-identical.
        var bravo    = MakeIdentity("bravo");
        var alpha    = MakeIdentity("alpha");

        var cells = new List<CellResult>
        {
            MakeCell(bravo, seed: 1, terminus: CellTerminus.Victory, floor: 300, wallMs: 1000),
            MakeCell(alpha, seed: 1, terminus: CellTerminus.Victory, floor: 300, wallMs: 1000),
        };

        var ranking = new LexSortScoring().Rank(cells);
        Assert.Equal("alpha", ranking[0].Agent.Name);
        Assert.Equal("bravo", ranking[1].Agent.Name);
    }

    [Fact]
    public void Empty_Cells_Returns_Empty_Ranking()
    {
        var ranking = new LexSortScoring().Rank([]);
        Assert.Empty(ranking);
    }

    [Fact]
    public void Name_And_Version_Are_Stable()
    {
        var s = new LexSortScoring();
        Assert.Equal("lex-sort", s.Name);
        Assert.Equal("1.0", s.Version);
    }

    private static AgentIdentity MakeIdentity(string name) =>
        new(Name: name, Version: "0.0.0", Language: "csharp-bundled", ManifestType: $"Test.{name}Manifest");

    private static CellResult MakeCell(
        AgentIdentity agent,
        int           seed,
        CellTerminus  terminus,
        int           floor,
        long          wallMs) =>
        new(
            EvalId:        "test",
            Agent:         agent,
            Seed:          (ulong)seed,
            Character:     Character.Ironclad,
            Ascension:     0,
            Modifiers:     [],
            Terminus:      terminus,
            FloorReached:  floor,
            FinalHp:       0,
            MaxHp:         80,
            Gold:          100,
            DeckSize:      12,
            RelicCount:    3,
            CombatCount:   0,
            EliteCount:    0,
            BossCount:     0,
            TurnsInCombat: 0,
            Steps:         42,
            WallClockMs:   wallMs,
            ReplayPath:    "cells/x/y",
            GameVersion:   "v0.0.0",
            Sts2DllSha256: "sha",
            Scoring:       new ScoringMetrics(0.0));
}
