# 03 — What comes out

After (or during) a run, the eval directory under
`replays/eval-harness/<eval-id>/` is self-contained. The shape below
is what the harness writes; anything downstream (plots, leaderboard
HTML, ad-hoc analysis) consumes only the listed files.

## Directory tree

```
replays/eval-harness/2026-05-26T19-32-04Z/
├── config.json                        # captured EvaluationHarnessConfig
├── summary.md                         # human-readable leaderboard
├── summary.json                       # machine-readable leaderboard
├── runs.jsonl                         # one line per cell, append-only
└── cells/
    ├── greedy/
    │   ├── 0/                         # seed = 0
    │   │   ├── cell.json              # the harness's forward index
    │   │   ├── manifest.json          # AD-8 — the recorder's bytes
    │   │   ├── run.json               # AD-8 — RunHistory schema_v9
    │   │   └── combats/
    │   │       ├── act1-floor3-monster.mcr
    │   │       └── act1-floor7-elite.mcr
    │   ├── 1/...
    │   └── ...
    ├── ironclad-battle-agent/
    │   └── ...
    └── ...
```

Properties:

- `replays/` is gitignored at the repo root. The bytes under it derive
  from `vendor/sts2.dll`, same proprietary-derivative posture AD-8 takes
  for `vendor/replays/`.
- Everything under `cells/<agent>/<seed>/` except `cell.json` is the
  game engine's own writers (AD-8). The harness owns only `cell.json`,
  the four root-level files, and the `cells/` skeleton.
- `runs.jsonl` is append-only and written incrementally; killing the
  harness mid-flight leaves an inspectable partial result.
- `cell.json` is a denormalised forward index so a leaderboard row
  can `cat` the cell's metadata without joining against `runs.jsonl`.

## `summary.md` (sibling of `sweep-*.md`)

```markdown
# Evaluation — 2026-05-26T19-32-04Z

Game version: `VERSION  v0.103.2`
sts2.dll SHA-256: `a1b2c3…`
Seed bank: `reference` (50 seeds, bank-version 1)
Characters: `Ironclad`
Ascensions: `0`
Modifiers: `(none)`
Scoring: `lex-sort(win-rate desc, mean-floor desc, mean-wall-clock asc)` v1.0
Determinism canary: off
Elapsed: **18.4 min**
Cells: **250** (5 agents × 50 seeds × 1 character × 1 ascension)
Workers: 8

| # | Agent | Version | Wins | Win% | Mean floor | p25 floor | Engine⚠ | Agent⚠ | Host⚠ | Timeout | Median wall |
|---|-------|---------|-----:|-----:|-----------:|----------:|--------:|-------:|------:|--------:|------------:|
| 1 | `ironclad-battle-agent`     | 0.5.1 | 11/50 | 22% | 31.4 | 18 | 0 | 0 | 0 | 0 | 1m12s |
| 2 | `greedy`                    | 0.1.0 |  4/50 |  8% | 18.7 | 11 | 1 | 0 | 0 | 0 |    38s |
| 3 | `attack`                    | 0.1.0 |  2/50 |  4% | 12.1 |  7 | 0 | 0 | 0 | 0 |    22s |
| 4 | `block`                     | 0.1.0 |  1/50 |  2% |  9.8 |  6 | 0 | 0 | 0 | 0 |    21s |
| 5 | `random`                    | 0.1.0 |  0/50 |  0% |  6.1 |  4 | 2 | 0 | 0 | 0 |    14s |

## Notable cells

| Agent | Seed | Terminus | Floor | Replay |
|-------|------|----------|------:|--------|
| `greedy` | 17 | EngineCrash | 24 | [cells/greedy/17/](cells/greedy/17/) |
| `random` | 8  | EngineCrash | 11 | [cells/random/8/](cells/random/8/)   |
| `random` | 33 | EngineCrash | 9  | [cells/random/33/](cells/random/33/) |
```

The header mirrors `documentation/coverage/sweep-cards.md` so the
tooling shape (file location, parser, link style) feels native. The
"Notable cells" table is auto-populated with any cell whose terminus
is in the crash family (EngineCrash / HostCrash / AgentCrash /
HarnessError) — those are the cells a maintainer wants to triage
first.

## `summary.json`

```json
{
  "evalId": "2026-05-26T19-32-04Z",
  "gameVersion": "v0.103.2",
  "sts2DllSha256": "a1b2c3…",
  "seedBank": {"name": "reference", "version": "1", "count": 50},
  "characters": ["Ironclad"],
  "ascensions": [0],
  "modifiers": [],
  "scoring": {"name": "lex-sort", "version": "1.0"},
  "elapsedMs": 1104000,
  "cellCount": 250,
  "workers": 8,
  "ranking": [
    {
      "rank": 1,
      "agent": {"name": "ironclad-battle-agent", "version": "0.5.1"},
      "score": 0.22,
      "aggregates": {
        "wins": 11, "winRate": 0.22,
        "meanFloor": 31.4, "p25Floor": 18, "p50Floor": 28, "p75Floor": 47,
        "engineCrashes": 0, "hostCrashes": 0, "agentCrashes": 0, "timeouts": 0,
        "medianWallClockMs": 72000,
        "peakRssMbP95": 612
      }
    },
    { "rank": 2, "agent": {"name": "greedy", "version": "0.1.0"}, "...": "..." }
  ],
  "notableCells": [
    {"agent": "greedy", "seed": 17, "terminus": "EngineCrash", "replayPath": "cells/greedy/17"}
  ]
}
```

This is the contract downstream tools depend on (NFR-1).

## `runs.jsonl`

One line per cell, written incrementally as cells finish. Identical
columns to `summary.json`'s per-cell rows (when those are added), so
slicing by hand with `jq` or `pandas.read_json(lines=True)` is the
expected workflow.

```jsonl
{"evalId":"2026-05-26T19-32-04Z","agent":{"name":"greedy","version":"0.1.0","language":"csharp"},"seed":42,"character":"Ironclad","ascension":0,"modifiers":[],"terminus":"Death","floorReached":17,"finalHp":0,"maxHp":80,"gold":124,"deckSize":18,"relicCount":4,"combatCount":12,"eliteCount":1,"bossCount":0,"turnsInCombat":78,"steps":342,"wallClockMs":42137,"peakRssMb":523,"replayPath":"cells/greedy/42","gameVersion":"v0.103.2","sts2DllSha256":"a1b2…","scoring":{"score":0.0}}
{"evalId":"2026-05-26T19-32-04Z","agent":{"name":"greedy","version":"0.1.0","language":"csharp"},"seed":17,"character":"Ironclad","ascension":0,"modifiers":[],"terminus":"EngineCrash","floorReached":24,"finalHp":31,"maxHp":80,"gold":201,"deckSize":22,"relicCount":6,"combatCount":17,"eliteCount":2,"bossCount":1,"turnsInCombat":121,"steps":488,"wallClockMs":61492,"peakRssMb":541,"replayPath":"cells/greedy/17","gameVersion":"v0.103.2","sts2DllSha256":"a1b2…","scoring":{"score":0.0},"error":{"code":-32603,"message":"internal error: NullReferenceException: …","stack":"at MegaCrit.Sts2.Core.Combat.CombatManager.DoAction(…)"}}
```

## `cell.json` (per cell)

The denormalised forward index. Joins the AD-8 manifest to the eval
context so a tool walking `cells/` doesn't need `runs.jsonl`.

```json
{
  "evalId": "2026-05-26T19-32-04Z",
  "agent": {
    "name":         "greedy",
    "version":      "0.1.0",
    "language":     "csharp-bundled",
    "manifestType": "Sts2Headless.Eval.Agents.Builtin.GreedyManifest"
  },
  "seed":       42,
  "character":  "Ironclad",
  "ascension":  0,
  "modifiers":  [],
  "terminus":   "Death",
  "floorReached": 17,
  "finalHp": 0,
  "scoringMetrics": {"score": 0.0},
  "wallClockMs": 42137,
  "startedAt":   "2026-05-26T19:32:08Z",
  "completedAt": "2026-05-26T19:32:50Z",
  "gameVersion": "v0.103.2"
}
```

## `config.json` — the captured `EvaluationHarnessConfig`

A serialisation of the exact config the harness ran. Re-feeding this
into a future invocation against the same `GAME_VERSION` pin is the
canonical reproducer.

Each agent serialises to its full `AgentManifest` state — name,
version, command, capabilities, budget overrides, plus the manifest
class name for traceability. No JSON path or registry key indirection;
the captured file is self-contained.

```json
{
  "agents": [
    {
      "manifestType":  "Sts2Headless.Eval.Agents.Builtin.GreedyManifest",
      "name":          "greedy",
      "version":       "0.1.0",
      "language":      "csharp-bundled",
      "command":       ["dotnet", "run", "--project", "src/Sts2Headless.AgentRunner",
                        "--no-build", "--", "--manifest", "Sts2Headless.Eval.Agents.Builtin.GreedyManifest"],
      "cwd":           null,
      "env":           null,
      "supportedCharacters": ["Ironclad"],
      "supportedAscensions": [0],
      "budgets":       null
    },
    {
      "manifestType":  "EvalDeep.Manifests.PythonGreedyManifest",
      "name":          "python-greedy",
      "version":       "0.1.0",
      "language":      "python",
      "command":       ["uv", "run", "python", "-m", "headless_in_the_spire_agents.examples.greedy"],
      "cwd":           "clients/python/headless-in-the-spire-agents",
      "env":           null,
      "supportedCharacters": ["Ironclad"],
      "supportedAscensions": [0],
      "budgets":       null
    },
    {
      "manifestType":  "EvalDeep.Manifests.ExperimentalManifest",
      "name":          "experimental",
      "version":       "0.2.0-alpha",
      "language":      "csharp",
      "description":   "MCTS-heavy Ironclad planner from sibling repo.",
      "command":       ["dotnet", "run", "--project", "/home/me/code/external-agent", "--no-build"],
      "supportedCharacters": ["Ironclad"],
      "supportedAscensions": [0],
      "budgets":       {"perDecision": "00:02:00"}
    }
  ],
  "seeds":      {"bank": "reference", "version": "1", "count": 50},
  "characters": ["Ironclad"],
  "ascensions": [0],
  "modifiers":  [],
  "budgets": {
    "perDecision": "00:00:30",
    "perCell":     "00:10:00",
    "maxSteps":    4000
  },
  "workers": 8,
  "scoring": {"name": "lex-sort", "version": "1.0"},
  "output": {
    "evalRoot":        "replays/eval-harness",
    "evalIdGenerator": "utc-timestamp"
  },
  "enableDeterminismCanary": false,
  "captureAgentNotes":       false,
  "harnessVersion": "0.1.0",
  "gameVersion":    "v0.103.2",
  "sts2DllSha256":  "a1b2c3…"
}
```

`manifestType` is captured for traceability only — re-running an eval
from a `config.json` re-instantiates the same manifest classes by name
(or fails loudly if the type is no longer on the classpath, which is
the right behaviour for "reproduce this exact run").

## `CellTerminus` (the closed enum used everywhere)

```
Victory      — agent beat the Act 3 boss (per sts2-game-facts.md)
Death        — agent died in combat
Abandoned    — agent emitted StopRun mid-game
Stalled      — StallDetector tripped (8 identical snapshots in a row)
MaxSteps     — Budgets.MaxSteps cap reached
Timeout      — Budgets.PerCell wall-clock cap expired
EngineCrash  — host returned a wire error (engine NRE wrapped as -32603)
HostCrash    — host process died (stdout EOF before response)
AgentCrash   — agent process died (stdout EOF before response)
HarnessError — orchestrator-side failure (rare; non-zero exit code)
```

The first six are *results* — the eval ran cleanly, the agent (or
game state, in the case of MaxSteps) determined the outcome.
EngineCrash / HostCrash / AgentCrash are *attribution* — somebody
crashed; the row records who. HarnessError is the only one that's a
"failed eval" in the sense the exit code reflects.

## Operations the directory supports

- `cat replays/eval-harness/2026-05-26T19-32-04Z/summary.md` — leaderboard.
- `jq '.terminus' replays/eval-harness/2026-05-26T19-32-04Z/runs.jsonl | sort | uniq -c` — terminus histogram.
- `jq 'select(.terminus | test("Crash$"))' replays/eval-harness/.../runs.jsonl` — every crashing cell.
- `pnpm dev` in `tools/replay-viewer/` pointed at the eval root — visual walkthrough of any cell's `.mcr`s.
- `dotnet run --project src/Sts2Headless -- --rebuild-replay-index replays/eval-harness/2026-05-26T19-32-04Z/` — rebuild `runs.json` for the viewer if a cell directory was moved around manually.
