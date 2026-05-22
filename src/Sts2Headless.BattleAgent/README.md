# Sts2Headless.BattleAgent

One **concrete, full-run agent** — `IroncladAgent` — built on the
`Sts2Headless.Agents` framework. This is the reference / template for a "bigger"
agent: the answer to *"where do serious per-character agents go?"* is "a project
shaped like this one."

It owns no framework machinery. It composes two things it references:

- **`Sts2Headless.Agents`** — the `IAgent` contract, `HeuristicAgent` base, and
  the `AgentDriver` that runs it.
- **`Sts2Headless.BattleAgent.Core`** — the pure combat brain (forward
  simulation + planners + evaluator), used to plan each combat turn.

## What's here

```
SimAgent          combat-only IAgent — re-plans each step via ICombatPlanner,
                  translates the first SimAction to an AgentAction. Falls through
                  to HeuristicAgent defaults for non-combat phases.
IroncladAgent     the full-run agent — inherits SimAgent's combat brain and
                  overrides the non-combat phases with pluggable policies.

I{Draft,Path,Rest,Event}Policy        one decision each, for the matching phase.
Ironclad{Draft,Path,Rest,Event}Policy heuristic Ironclad implementations.
```

`IroncladAgent` is a thin composition: combat from `SimAgent`, everything else
delegated to the four policy objects. Swapping a strategy means injecting a
different policy, not editing the agent.

## Adding another character agent

Don't add a `SilentAgent` here. Create a sibling project
(`Sts2Headless.SilentAgent`) that references `Sts2Headless.Agents` and a combat
core, and follow this layout: one `IAgent`, per-phase policies, combat planning
delegated to the core. See `../Sts2Headless.Agents/README.md` for the full
framework / core / concrete-agent rationale.
