# Sts2Headless.Coverage

Run **content-coverage tracking**: which game content a run actually exercised,
and corpus statistics across many runs.

- `CoverageRecorder` — observes `run/state` snapshots and the agent's chosen
  actions, accumulating the content seen/played: cards, relics, potions,
  monsters, powers, events (+ which options were taken), and hook triggers.
- `CoverageAggregator` — unions per-run `CoverageReport`s into a corpus view
  and renders the gap report (what content hasn't been touched yet).

## Depends on Protocol only

Coverage works purely over the wire DTOs (`Sts2Headless.Protocol.Methods`), so
any agent or test can record coverage without pulling in `Agents` or `Runtime`.

This was a deliberate de-tangling. The recorder used to live in
`Sts2Headless.Agents` and switch over `AgentAction` (an Agents type) inside
`OnAction`, while `AgentDriver` consumed the recorder — a dependency cycle. The
recorder now exposes index-based methods that take only Protocol types:

```csharp
recorder.RecordPlayedCard(prevState, cardIndex);
recorder.RecordUsedPotion(prevState, potionIndex);
recorder.RecordTakenEventOption(prevState, optionIndex);
```

`AgentDriver` destructures the `AgentAction` variant and calls them, reading
from the **pre-action** snapshot (after the action applies, the hand has
shrunk and indices would point at the wrong card). Other phases aren't
recorded directly — the resulting state snapshot already pulls that content
into the "seen" axes (e.g. a selected reward shows up as an owned relic next
snapshot).

## Where it's wired in

`AgentDriver.PlayRunAsync(..., coverageRecorder: ...)` calls `Observe(state)`
each tick and the `Record*` methods on each action. The end-to-end
`CoverageSweepTests` (opt-in via `RUN_COVERAGE_SWEEP=1` / `just coverage`)
drives a greedy agent over multiple seeds and dumps a report.
