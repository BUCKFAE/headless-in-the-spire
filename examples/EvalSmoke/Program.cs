using Sts2Headless.Eval;
using Sts2Headless.Eval.Manifests;

var report = await EvaluationHarness.RunAsync(
    config: new EvaluationHarnessConfig
    {
        Agents = [BuiltinAgents.Greedy],
        Seeds  = SeedBanks.Smoke,
    },
    onCellComplete: cell => Console.Error.WriteLine(
        $"  [{cell.Terminus,-12}] {cell.Agent.Name,-12} seed={cell.Seed,-6} floor={cell.FloorReached,-4} {cell.WallClockMs} ms"),
    onSkip: skip => Console.Error.WriteLine(
        $"  [skip] {skip.Manifest.Name}: {skip.Reason}"),
    onLog: line => Console.Error.WriteLine(line));

Console.WriteLine();
Console.WriteLine(File.ReadAllText(report.Output.SummaryMarkdown));
Console.WriteLine();
Console.Error.WriteLine($"eval {report.EvalId} done — see {report.Output.SummaryMarkdown}");

return report.HasHarnessError ? 1 : 0;
