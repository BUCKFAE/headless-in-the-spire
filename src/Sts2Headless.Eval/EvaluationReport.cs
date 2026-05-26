namespace Sts2Headless.Eval;

// What `EvaluationHarness.RunAsync` returns. Lets the caller branch on
// `HasHarnessError` for the exit code (NFR-4: zero on a clean matrix
// run, regardless of agent / engine crashes; non-zero only when the
// harness itself failed) and inspect the typed summary / output paths
// without re-reading files.
public sealed record EvaluationReport(
    string                       EvalId,
    string                       EvalDirectory,         // absolute path to <eval-root>/<eval-id>/
    EvaluationOutputPaths        Output,
    EvaluationSummary            Summary,
    IReadOnlyList<CellResult>    Cells,
    EvaluationHarnessConfig      Config)
{
    // True iff at least one cell tripped `HarnessError` (the only
    // terminus that is the harness's fault). NFR-4: the exit code
    // distinguishes "the harness broke" from "an agent crashed", since
    // the latter is an expected result, not a CI gate.
    public bool HasHarnessError =>
        Cells.Any(c => c.Terminus == CellTerminus.HarnessError);
}

public sealed record EvaluationOutputPaths(
    string EvalDirectory,
    string ConfigJson,
    string SummaryJson,
    string SummaryMarkdown,
    string RunsJsonl,
    string CellsDirectory);
