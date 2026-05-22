# Sts2Headless.Agents

The **agent framework**: the contracts, the driver loop, the transport plumbing,
and the base classes you build a rule-based agent on top of. An *agent* is a pure
`snapshot → action` function (`IAgent.Decide`); everything around it — looping,
dispatch, terminal/stall detection, the wire — lives here so agents stay small.

This project deliberately references only `Protocol` and `Coverage`. It knows
nothing about combat planning, sts2.dll, or any particular character. Per AD-6
it is one home of the C#-authored behavioral source of truth.

## Layout

Files are grouped into sub-folders by concern, each with a matching
sub-namespace (`Sts2Headless.Agents.<Folder>`):

| Folder / namespace | What's in it |
|---|---|
| `Contracts/` | The vocabulary every agent and consumer speaks: `IAgent`, `ITransport`, the closed `AgentAction` union, and the `Phase` enum + `PhaseDetector`. Pure contracts, no behavior. |
| `Driving/` | The framework runtime: `AgentDriver` (the loop that runs an `IAgent` against an `ITransport` and returns a `RunOutcome`), plus the guards it wires automatically — `StallDetector`, `CombatBudgetGuard`. |
| `Hosting/` | `ITransport` implementations and process management: `HostProcess` (one subprocess host), `HostPool` / `HostPoolOptions` (bounded-concurrency pool of them). |
| `Authoring/` | Building blocks for *writing* an agent: the `HeuristicAgent` base (splits `Decide` into per-phase hooks with run-completing defaults) and the `CardMechanics` / `CombatHelpers` lookups agents lean on. |
| `Examples/` | Small, self-contained agents: `GreedyAgent` (forward progress for end-to-end tests), `PotionDrinkingAgent` (coverage), `CheatingHellRaisingSeed42Agent` (cheat-paired demo). |

The split is organizational. Because every file moved into a sub-namespace,
there's nothing left in the bare `Sts2Headless.Agents` namespace — cross-folder
references are explicit `using`s, and the build escalates unused usings to an
error so the set stays honest.

## How the pieces relate (and where "bigger" agents go)

Three projects form a clean, acyclic stack:

```
Sts2Headless.BattleAgent.Core   pure combat brain — SimState / planners / evaluator. No I/O, no host.
        ▲
Sts2Headless.Agents             THIS project — the agent framework (contracts + driver + hosting + base + examples).
        ▲
Sts2Headless.BattleAgent        one concrete production agent (IroncladAgent) composing the framework + Core.
```

So, answering the recurring question: **`BattleAgent` is "just" a concrete
implementation of the general agent abstraction defined here** — and it's the
*template* for a serious agent. The convention:

- **Throwaway / demo / coverage agents** live in `Examples/` in this project.
- **Serious, per-character agents** get their **own project** that references
  `Sts2Headless.Agents` (for the contracts + driver) and whatever combat
  substrate they need (`BattleAgent.Core`, or a future character-specific core).
  A `Sts2Headless.SilentAgent` / `Sts2Headless.DefectAgent` would follow the
  `BattleAgent` shape: one `IAgent`, composed from pluggable policies.

This keeps the framework (here) free of any one agent's strategy, and keeps each
"bigger" agent's strategy isolated in its own assembly.

## Adding code

Drop a file in the folder matching its concern and declare the matching
`Sts2Headless.Agents.<Folder>` namespace. Add explicit `using`s for any other
cluster you reference (the build will tell you which). A new rule-based agent
almost always wants to inherit `Authoring.HeuristicAgent` rather than implement
`Contracts.IAgent` directly.
