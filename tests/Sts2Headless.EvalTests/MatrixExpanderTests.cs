using Sts2Headless.Eval;
using Sts2Headless.Eval.Execution;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.EvalTests;

public sealed class MatrixExpanderTests
{
    [Fact]
    public void Default_Single_Axis_Matrix_Has_Compact_Path()
    {
        var config = new EvaluationHarnessConfig
        {
            Agents = [new FakeManifest("greedy")],
            Seeds  = SeedBanks.Inline([1, 2, 3]),
        };

        var cells = MatrixExpander.Expand(config);

        Assert.Equal(3, cells.Count);
        Assert.Equal("cells/greedy/s1", cells[0].RelativeReplayDir);
        Assert.Equal("cells/greedy/s2", cells[1].RelativeReplayDir);
        Assert.Equal("cells/greedy/s3", cells[2].RelativeReplayDir);
    }

    [Fact]
    public void Multi_Character_Matrix_Encodes_Character_In_Path()
    {
        var config = new EvaluationHarnessConfig
        {
            Agents     = [new FakeManifest("greedy", supportedCharacters: [Character.Ironclad, Character.Silent])],
            Seeds      = SeedBanks.Inline([42]),
            Characters = [Character.Ironclad, Character.Silent],
        };

        var cells = MatrixExpander.Expand(config);
        Assert.Equal(2, cells.Count);
        Assert.Contains(cells, c => c.RelativeReplayDir == "cells/greedy/Ironclad-s42");
        Assert.Contains(cells, c => c.RelativeReplayDir == "cells/greedy/Silent-s42");
    }

    [Fact]
    public void Unsupported_Character_Is_Skipped_With_Reason()
    {
        var skips = new List<MatrixSkip>();
        var config = new EvaluationHarnessConfig
        {
            // Manifest only supports Ironclad; the matrix asks for both.
            Agents     = [new FakeManifest("ironclad-only")],
            Seeds      = SeedBanks.Inline([1]),
            Characters = [Character.Ironclad, Character.Silent],
        };

        var cells = MatrixExpander.Expand(config, onSkip: skips.Add);

        Assert.Single(cells);
        Assert.Equal(Character.Ironclad, cells[0].Character);
        var skip = Assert.Single(skips);
        Assert.Equal(Character.Silent, skip.Character);
        Assert.Contains("SupportedCharacters", skip.Reason);
    }

    [Fact]
    public void Per_Manifest_Budgets_Override_Config_Budgets()
    {
        var perAgent = new HarnessBudgets { PerDecision = TimeSpan.FromSeconds(90) };
        var config = new EvaluationHarnessConfig
        {
            Agents  = [new FakeManifest("greedy"),
                       new FakeManifest("ironclad", budgets: perAgent)],
            Seeds   = SeedBanks.Inline([1]),
            Budgets = new HarnessBudgets { PerDecision = TimeSpan.FromSeconds(10) },
        };

        var cells = MatrixExpander.Expand(config);
        Assert.Equal(2, cells.Count);

        var greedy   = cells.Single(c => c.Manifest.Name == "greedy");
        var ironclad = cells.Single(c => c.Manifest.Name == "ironclad");

        Assert.Equal(TimeSpan.FromSeconds(10), greedy.Budgets.PerDecision);
        Assert.Equal(TimeSpan.FromSeconds(90), ironclad.Budgets.PerDecision);
    }

    [Fact]
    public void Empty_Agent_List_Throws()
    {
        var config = new EvaluationHarnessConfig
        {
            Agents = [],
            Seeds  = SeedBanks.Inline([1]),
        };
        Assert.Throws<ArgumentException>(() => MatrixExpander.Expand(config));
    }

    private sealed class FakeManifest : AgentManifest
    {
        public FakeManifest(
            string                    name,
            IReadOnlyList<Character>? supportedCharacters = null,
            HarnessBudgets?           budgets             = null)
        {
            Name = name;
            _supportedCharacters = supportedCharacters ?? [Character.Ironclad];
            _budgets = budgets;
        }

        public override string Name { get; }
        public override string Version => "0.0.0";
        public override IReadOnlyList<string> Command => ["true"];
        public override IReadOnlyList<Character> SupportedCharacters => _supportedCharacters;
        public override HarnessBudgets? Budgets => _budgets;

        private readonly IReadOnlyList<Character> _supportedCharacters;
        private readonly HarnessBudgets? _budgets;
    }
}
