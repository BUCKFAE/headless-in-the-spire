# Autonomous IroncladAgent tuning session — 2026-05-24

Goal: clear A0 (Ascension 0, beat Act 3 boss → `IsVictory==true`) on ≥80% of
50 deterministic seeds. The user is away for hours; I have unconstrained
budget to research, experiment, and iterate.

## Starting state

- Baseline (latest re-measure 2026-05-24, post scaling-card pass):
  - **0/50 clear A0** (0%)
  - **3/50 beat Act 1 boss** (6%)
  - avg floor: 12.9
  - wall: ~8s per measurement (50 seeds × 8 workers)
- Where deaths land:
  - 20/50 at Act 1 boss (floor 17) → 40%
  - 14/50 at floors 11-15 (mid Act 1)
  - 14/50 at floors 6-10 (early Act 1 elites)
- Planner pinned at ExhaustivePlanner (1-turn). MultiTurn currently
  *worse* — over-defensive with current weights.

## Components

| component | file | current behaviour |
|-|-|-|
| Card-play planner | `ExhaustivePlanner` | 1-turn exhaustive search |
| Combat model | `IroncladCardCatalog`, `CombatModel` | Catalog mostly correct; scaling cards landed |
| Combat evaluator | `HeuristicEvaluator` / `HeuristicWeights` | hand-tuned linear |
| Draft (card rewards) | `IroncladDraftPolicy` | static tier list, skip below C-tier |
| Path (map nodes) | `IroncladPathPolicy` | HP-aware priority table |
| Rest site | `IroncladRestPolicy` | SMITH if HP ≥ 75%, else HEAL |
| Events | `IroncladEventPolicy` | "leave" — always picks the last unlocked option |

## Hypothesis ordering (highest impact first)

1. **Events are abandoned** — every event resolves with the "leave"
   option. Many STS2 events give free cards/relics/HP. Even a coin-flip
   event handler picks free upside more often than the current code.
2. **Path policy is map-myopic** — picks one floor at a time; doesn't
   route toward boss-relics, shops with money, rest sites available in
   reach. Multi-floor routing on STS1 maps moves win rate non-trivially.
3. **Draft tier list is stale** — Whirlwind marked F-tier ("NREs in
   headless"), but the engine fix landed and the catalog has it
   modelled. PerfectedStrike and Rampage are C-tier despite being
   modelled. We're skipping strong picks.
4. **Event policy = always-leave** trades safety for losing every event
   that hands out free progression.
5. **Evaluator weights** — the doc-noted "next step", but probably
   smaller impact than fixing the above categorical refusals.

## Plan

- Add an empirically-driven event policy ("take the most-positive-EV
  unlocked option" with a small allow-list)
- Update DraftPolicy tier list: Whirlwind/PS/Rampage promoted
- Add a path-policy variant that prefers rest before boss and elites
  when fresh
- Tune evaluator weights against the new agent

I will measure after each change, write the result here, and iterate.
