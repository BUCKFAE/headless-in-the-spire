# 01 — The eval program

The "main file" the user authors. The harness is exposed as a library
(`src/Sts2Headless.Eval/`); the user writes a small console program
that builds an `EvaluationHarnessConfig`, calls
`EvaluationHarness.RunAsync(config)`, and returns the exit code. Four
variants below from minimal to maxed-out, then a custom scoring
function, then the `just` recipes that drive them.

> All `using`s collapsed for terseness. Real types in this snippet:
> `GreedyAgent`, `IroncladAgent`, `RandomAgent`, `AttackAgent`,
> `BlockAgent`, `Character` (the in-repo `Sts2Headless.Protocol.Methods`
> enum). Proposed types: `EvaluationHarnessConfig`,
> `EvaluationHarness`, `AgentManifest`, `BundledAgent`,
> `BuiltinAgents`, `SeedBanks`, `HarnessBudgets`, `OutputLayout`,
> `IScoringFunction`, `WeightedScoring`, `EvaluationReportIo`,
> `AgentRanking`, `CellResult`, `AgentAggregates`.
>
> **No JSON manifest files anywhere.** Every agent is a typed C# class
> — either a `BundledAgent` subclass (for in-repo agents) or
> an `AgentManifest` subclass (for everything else). See
> [02-agents.md](./02-agents.md) for the contract. The `BuiltinAgents`
> static class exposes ready-to-use handles for the in-repo agents so
> the everyday call site reads like a "pick from a list of named
> things" — the typed-C# equivalent of "an enum where you add stuff".

## Variant 1 — minimal

The shortest useful eval. Five seeds, the in-repo `GreedyAgent`,
Ironclad A0, library defaults for everything else.

```csharp
// examples/EvalSmoke/Program.cs
using Sts2Headless.Eval;

var report = await EvaluationHarness.RunAsync(new EvaluationHarnessConfig
{
    Agents = [BuiltinAgents.Greedy],
    Seeds  = SeedBanks.Smoke,
});

Console.WriteLine(report.Summary.ToMarkdown());
return report.HasHarnessError ? 1 : 0;
```

Defaults applied automatically:

- `Characters = [Character.Ironclad]`
- `Ascensions = [0]`
- `Budgets   = { PerDecision = 30s, PerCell = 10min, MaxSteps = 4000 }`
- `Workers   = min(cellCount, ⌊cores / 2⌋)`
- `Scoring   = ScoringFunctions.Default` (lex-sort)
- `Output    = { EvalRoot = "replays/eval-harness", EvalIdGenerator = utc-timestamp }`
- `EnableDeterminismCanary = false`
- `CaptureAgentNotes       = false`

## Variant 2 — typical reference eval

The everyday "compare the in-repo agents on the reference bank" run.

```csharp
// examples/EvalReference/Program.cs
using Sts2Headless.Eval;

var report = await EvaluationHarness.RunAsync(new EvaluationHarnessConfig
{
    Agents =
    [
        BuiltinAgents.Greedy,
        BuiltinAgents.Ironclad,
        BuiltinAgents.Random,
        BuiltinAgents.Attack,
        BuiltinAgents.Block,
    ],
    Seeds   = SeedBanks.Reference,
    Workers = 8,
});

await EvaluationReportIo.WriteAsync(report);
Console.WriteLine($"eval {report.EvalId} done — see {report.Output.SummaryMarkdown}");
return report.HasHarnessError ? 1 : 0;
```

`BuiltinAgents` is a `static class` with one `public static readonly
AgentManifest` per in-repo agent — the C# "smart enum" pattern. IDE
autocomplete on `BuiltinAgents.` lists every shipped agent; adding a
new one is one line in `BuiltinAgents.cs` plus the manifest class
itself (see [02-agents.md](./02-agents.md)).

`EvaluationReportIo.WriteAsync` writes `summary.md`, `summary.json`,
and `config.json` to the eval directory. `runs.jsonl` and per-cell
`cell.json` files are written incrementally during the run, not at the
end — a kill -9 mid-flight leaves a partially populated but readable
eval.

## Variant 3 — mixed-language, external agents, every knob set

Everything the harness lets you tune, made visible in one call site.
Use as a checklist when reviewing the API surface.

External agents (Python, sibling C# repos, anything else) are
registered the same way as built-ins: by instantiating a typed
manifest class. The class itself lives wherever the user wants — in
the eval program, in a sibling project, or in a NuGet the external
author publishes. The harness only sees `AgentManifest` instances; it
doesn't care where they came from.

```csharp
// examples/EvalDeep/Manifests/PythonGreedyManifest.cs
using Sts2Headless.Eval;
using Sts2Headless.Protocol.Methods;

namespace EvalDeep.Manifests;

public sealed class PythonGreedyManifest : AgentManifest
{
    public override string Name     => "python-greedy";
    public override string Version  => "0.1.0";
    public override string Language => "python";
    public override IReadOnlyList<string> Command =>
        ["uv", "run", "python", "-m", "headless_in_the_spire_agents.examples.greedy"];
    public override string Cwd => "clients/python/headless-in-the-spire-agents";
    // SupportedCharacters / SupportedAscensions inherit Ironclad / A0 defaults.
}
```

```csharp
// examples/EvalDeep/Manifests/ExperimentalManifest.cs
using Sts2Headless.Eval;
using Sts2Headless.Protocol.Methods;

namespace EvalDeep.Manifests;

public sealed class ExperimentalManifest : AgentManifest
{
    public override string Name        => "experimental";
    public override string Version     => "0.2.0-alpha";
    public override string Language    => "csharp";
    public override string Description => "MCTS-heavy Ironclad planner from sibling repo.";

    public override IReadOnlyList<string> Command =>
        ["dotnet", "run", "--project", "/home/me/code/external-agent", "--no-build"];

    public override IReadOnlyList<Character> SupportedCharacters => [Character.Ironclad];

    // Per-agent budget override — wins over EvaluationHarnessConfig.Budgets.
    public override HarnessBudgets? Budgets =>
        new() { PerDecision = TimeSpan.FromMinutes(2) };
}
```

```csharp
// examples/EvalDeep/Program.cs
using EvalDeep.Manifests;
using Sts2Headless.Eval;
using Sts2Headless.Protocol.Methods;

var config = new EvaluationHarnessConfig
{
    Agents =
    [
        // In-repo C# agents — pre-instantiated handles on BuiltinAgents.
        BuiltinAgents.Greedy,
        BuiltinAgents.Ironclad,

        // External agents — manifest is a C# class, instantiated here.
        new PythonGreedyManifest(),
        new ExperimentalManifest(),
    ],

    Seeds      = SeedBanks.Deep,           // 500 committed seeds
    Characters = [Character.Ironclad],      // (default; explicit for clarity)
    Ascensions = [0, 10],                   // sweep A0 + A10
    Modifiers  = [],                        // (default; explicit for clarity)

    Budgets = new HarnessBudgets
    {
        PerDecision = TimeSpan.FromSeconds(60),   // ExperimentalManifest overrides this
        PerCell     = TimeSpan.FromMinutes(20),
        MaxSteps    = 8000,
    },

    Workers = 16,

    Scoring = new WeightedScoring(winWeight: 0.7),  // see below

    Output = new OutputLayout
    {
        EvalRoot        = "replays/eval-harness",
        EvalIdGenerator = () => $"nightly-{DateTimeOffset.UtcNow:yyyy-MM-dd}",
    },

    EnableDeterminismCanary = true,        // FR-11
    CaptureAgentNotes       = true,        // FR-12
};

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

var report = await EvaluationHarness.RunAsync(
    config,
    cancellationToken: cts.Token,
    onCellComplete: cell => Console.WriteLine(
        $"  [{cell.Terminus,-12}] {cell.Agent.Name,-24} seed={cell.Seed,-6} act={cell.Act} floor={cell.Floor}"));

await EvaluationReportIo.WriteAsync(report);
Console.WriteLine(report.Summary.ToMarkdown());
return report.HasHarnessError ? 1 : 0;
```

Things this exposes:

- **`BuiltinAgents.<Name>`** — pre-instantiated handle to an in-repo
  `BundledAgent` subclass. The "enum where you add stuff"
  pattern: one static readonly field per shipped agent, listed in
  one file ([02-agents.md](./02-agents.md#the-builtinagents-registry)).
- **`new SomeManifest()`** — external agents are typed C# classes.
  The user writes one class per agent (Python, sibling C#, Rust,
  whatever), instantiates it in their eval program. The class lives
  wherever it makes sense — in the eval program, in a sibling
  project, or in a NuGet the external author publishes. No JSON
  file, no string-typed factory, no manifest-path lookup.
- **`SeedBanks.{Smoke,Reference,Deep}`** — references the committed
  bank files under `documentation/eval/seeds/`. A caller can also
  pass `SeedBanks.Inline([1, 2, 3])` or
  `SeedBanks.FromFile("path.json")`.
- **`HarnessBudgets`** at the config level *and* per-manifest as an
  `override Budgets => …` — per-manifest wins (per spec FR-9).
- **`OutputLayout.EvalIdGenerator`** — override the timestamp default
  when CI wants a build-number or git-SHA stamp.
- **`onCellComplete`** — non-blocking observer; the harness writes
  `runs.jsonl` either way, this is for live logs.

## Custom scoring

The default ranking is `lex-sort(win-rate desc, mean-depth desc,
mean-wall-clock asc)`. _Depth_ is the sort ordinal `act × 100 + floor` —
it exists so cells in act 2 outrank deep act-1 cells without forcing
scoring functions to do that math themselves. Roll your own by implementing
`IScoringFunction` and passing an instance to `EvaluationHarnessConfig.Scoring`.

```csharp
// examples/EvalDeep/Scoring/WeightedScoring.cs
using Sts2Headless.Eval.Scoring;

public sealed class WeightedScoring(double winWeight) : IScoringFunction
{
    public string Name    => $"weighted({winWeight:0.0}·win + {1 - winWeight:0.0}·depth)";
    public string Version => "1.0";

    public IReadOnlyList<AgentRanking> Rank(IReadOnlyList<CellResult> cells) =>
        cells
            .GroupBy(c => c.Agent.Name)
            .Select(g =>
            {
                var aggs = AgentAggregates.From(g);
                var score = winWeight * aggs.WinRate
                          + (1 - winWeight) * (aggs.MeanDepth / 300.0);
                return new AgentRanking(Agent: g.Key, Score: score, Aggregates: aggs);
            })
            .OrderByDescending(r => r.Score)
            .ToList();
}
```

`AgentAggregates.From(IEnumerable<CellResult>)` is a static helper —
it computes win rate, mean / p25 / p75 floors, crash counts split by
attribution (engine / host / agent / harness), timeout count, median
wall-clock, peak RSS. Custom scoring functions consume aggregates,
they don't recompute them.

The leaderboard `summary.md` always carries `Name` and `Version` of
the scoring function used. Two leaderboards with the same agents but
different scoring functions are not silently comparable; the file
shows you which is which.

## just recipes

The eval programs are normal C# console exes. They get `just` recipes
following the existing `scripts/<module>/justfile` convention.

```just
# documentation/evaluation-harness sketches → scripts/eval/justfile (proposed)

# Smoke eval — 5 seeds × GreedyAgent on Ironclad A0. Fast inner-loop check.
smoke:
    @just build::build
    @dotnet run --project examples/EvalSmoke --no-build

# Reference eval — 50 committed seeds × in-repo agents on Ironclad A0.
# Lands at replays/eval-harness/<timestamp>/.
reference:
    @just build::build
    @dotnet run --project examples/EvalReference --no-build

# Deep eval — full 500-seed bank against everyone opted in. Multi-hour.
# Pass `--` then extra flags to forward them to the program.
deep *args:
    @just build::build
    @dotnet run --project examples/EvalDeep --no-build -- {{args}}
```

Invoked via `just eval::smoke`, `just eval::reference`,
`just eval::deep`.

## Things this sketch deliberately doesn't show

- **No JSON manifest files for agents.** Every agent — built-in or
  external — is a typed C# class (`BundledAgent` or
  `AgentManifest` subclass). The harness never reads a manifest from
  disk at startup. `config.json` is written *as output* for
  reproducibility, but the input side is C# only.
- **No string-typed agent factory.** There is no `Agents.FromManifest(path)`,
  no `Agents.FromCommand(spec)`, no `Agents.Of("name")`. Adding an
  external agent means writing one class that inherits
  `AgentManifest` and instantiating it. The class is the registration.
- **No `--config eval-config.json` mode.** The matrix is the C#
  `EvaluationHarnessConfig`. JSON-driven invocation would be an
  unnecessary fork of the truth.
- **No `EvaluationHarness.Builder` fluent API.** Init properties on
  the config record are enough and read like a JSON object.
- **No global "default eval" with implicit agents.** Every agent is
  named at the call site. `BuiltinAgents.Greedy` being convenient is
  not the same as being automatic.
