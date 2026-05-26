using Sts2Headless.Eval;
using Sts2Headless.Eval.Manifests;

var report = await EvaluationHarness.RunAsync(
    config: new EvaluationHarnessConfig
    {
        Agents = BuiltinAgents.All,
        Seeds  = SeedBanks.Reference,
    },
    onCellComplete: cell => Console.Error.WriteLine(
        $"  [{cell.Terminus,-12}] {cell.Agent.Name,-12} seed={cell.Seed,-6} act={cell.Act} floor={cell.Floor,-3} {cell.WallClockMs} ms"));

Console.WriteLine();
Console.WriteLine(File.ReadAllText(report.Output.SummaryMarkdown));
Console.WriteLine();
Console.Error.WriteLine($"eval {report.EvalId} done — see {report.Output.SummaryMarkdown}");

return report.HasHarnessError ? 1 : 0;
