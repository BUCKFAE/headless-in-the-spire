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

## Iteration log

Headline arc of the session, in order:

| change                                              | act-1 boss | notes                                                                  |
|-----------------------------------------------------|------------|------------------------------------------------------------------------|
| baseline (start of session)                         | 3/50  6%   | doc said 3/50 — re-verified                                            |
| DraftPolicy: Tremble/Bully/Dismantle/Whirlwind promo | 5/50 10%   | catalog also fixed: Tremble→3 Vuln AoE, Bully+2/Vuln, Dismantle ×2/Vuln |
| + IroncladMerchantPolicy                            | 6/50 12%   | conservative card-only buys; "greedy buy relic" cost 1 win              |
| + PathPolicy floors-to-boss + lower elite HP gate   | 7/50 14%   | always rest pre-boss; elites at >=65% HP                                |
| + IroncladEventPolicy text-key heuristic            | 7/50 14%   | fixes SLIPPERY_BRIDGE-style "stay = die" events                         |
| + evaluator additive-on-death (no more flat tie)    | 8/50 16%   | also PlayerHp 3→4, EnemyHp -2→-3, IncomingDamage -3.5→-3.0              |
| + STRIKE_DUMMY relic awareness (CombatModel)        | 10/50 20%  | (intertwined with v3 sweep)                                            |
| evaluator weight sweep v3 — IncomingDamage -2.0     | 10/50 20%  | PlayerBlock 0.5 → 0.3. Counter-intuitive: less defensive is better      |
| v4/v5/v6 sweeps (33 weight variants)                | ~10/50    | hit a hard plateau                                                     |

The session moved act-1-boss winrate from 6% to 20% — a 3.3x lift —
but did **not** unlock an A0 victory (0/50 throughout). Below is the
why-not analysis.

## Where it plateaus

After the v6 sweep we ran 33 distinct evaluator variants. Nothing
beat 10/50 = 20% on act-1 boss. Specifically:

- More aggressive damage weights (EnemyHp -4 or -5) collapse early-
  fight survival (4-6/50 wins). The 6/50 evaluator's mid-act fights
  get harder because the agent walks into incoming damage to deal an
  extra 1-2 HP it doesn't need.
- More defensive weights (IncomingDamage -4.5 or -6.0) lose to the
  Act 1 boss the same way the un-tuned baseline did — too much
  blocking, not enough kill pressure on Ceremonial-Beast-tier opponents.
- MCTS: catastrophically worse (1/50). The current MCTS uses
  heuristic-eval at leaves with no real rollouts; UCB exploration
  doesn't substitute for the planner's exhaustive coverage.
- Budget: tested 1M nodes (vs default 50k); no change. We're not
  budget-limited.
- MultiTurn lookahead: even with phantom-player-turn injection it
  underperforms single-turn (3-6/50). Multi-turn's projection error
  exceeds its signal advantage on this corpus.
- BossAwareEvaluator (different weights at MaxHp>=100 enemies): 6/50.
  The threshold trips on big Act 1 elites and the boss-aggressive
  weight set burns them down too fast and dies to follow-up.

## What would break the plateau

The 10/50 ceiling is a deck-quality problem, not a planner problem.
The 22 seeds that reach the boss and lose look indistinguishable from
the 10 that win EXCEPT that the winning decks have a Vulnerable
source (Tremble or Bash) AND either Strike Dummy / multi-hit damage.
A path forward:

1. **Run-deck tracker.** Track full-deck composition across wire
   calls. Let DraftPolicy pick gap-fillers (no Strength → must take
   Inflame; no Vuln → must take Tremble) and SmithPolicy pick the
   highest-value card to upgrade (not just index 0). Engine doesn't
   expose deck contents during combat, so we'd track via reward picks
   and event card-gains.

2. **Relic-aware evaluator.** STRIKE_DUMMY plumbing is in; extend to
   AKABEKO (first attack +8), VAJRA (already in PlayerPowers),
   BRONZE_SCALES (thorns 3), BOOK_OF_FIVE_RINGS (+1 STR per Strike up
   to 5), and other common Ironclad relics.

3. **Event lookup table.** The text-key heuristic catches obvious
   patterns but misses event-specific payouts. A per-event lookup
   ("HUNGRY_FOR_MUSHROOMS → take SOUP for relic"; "WOOD_CARVINGS →
   take SNAKE for Strength relic") would compound several wins.

4. **Per-card-damage-aware sequencing.** The exhaustive planner
   explores all sequences but uses a static action-priority for
   ordering. Whirlwind specifically should always lead when energy
   is high — currently it shares priority with regular attacks.

5. **Real multi-turn lookahead via Monte Carlo rollouts.** The
   phantom-turn projection in MultiTurn is too coarse. Genuine MC
   rollouts (random card from observed deck distribution + planner
   pick at each step) would catch lines where killing now prevents
   N turns of compounding damage.

## Final state

- Default ExhaustivePlanner with the tuned `HeuristicWeights`
  (IncomingDamage -2.0, PlayerBlock 0.3, EnemyHp -3.0, PlayerHp 4.0,
  EnemyStrength -3.0).
- Catalog: Tremble/Bully/Dismantle/Taunt/AshenStrike modelled per
  STS2 community.
- DraftPolicy: Vulnerable cluster promoted; Whirlwind un-blacklisted;
  deck-size-aware skip threshold (D-tier <=12, C-tier 13-18, A-tier
  >=19 cards).
- PathPolicy: HP-aware row picks with pre-boss rest preference.
- EventPolicy: text-key heuristic blocking dangerous "stay" choices.
- MerchantPolicy: card-only + card-removal, gold-reserve guarded.
- RestPolicy: HEAL if HP<75%, SMITH otherwise.
- SimAgent: relic-aware SimState building (STRIKE_DUMMY +3 to Strikes).

Final 50-seed corpus: **0/50 A0 clears (0%) / 10/50 Act-1-boss
clears (20%)** vs starting **0/50 / 3/50 (6%)**. avg floor 11.9.

## Second pass (autonomous loop tick)

The autonomous loop continued past the original "20% plateau". One
more change unblocked a win:

  - **RunDeckTracker** (src/Sts2Headless.BattleAgent/RunDeckTracker.cs)
    tracks the running deck across the run (Ironclad starter +
    drafted/bought cards). `IroncladAgent.GetStrikeCardsInDeck()` now
    feeds the real count into `SimStateBuilder`, so
    `PerfectedStrike`'s `6 + 2/Strike` formula plans on the real
    number (5+ from starter alone) instead of the hand-visible 0-1.
    50-seed: **10/50 → 11/50 (22%)**. One more win.

Also added but didn't move the metric:

  - AKABEKO / TUNGSTEN_ROD relic awareness — strictly additive when
    the relic is present, but neither relic surfaced in the corpus's
    losing seeds.
  - BRONZE_SCALES (thorns) — tested but cost 1 win (the projection
    made the agent under-block; reverted).
  - V8 weight sweep with deck-tracker active — confirmed 11/50 is
    robust under further weight tuning. Several variants tied at
    11/50 (no-cards-drawn, huge-cards-drawn, more-lethal); nothing
    exceeded.

**Final session metric: 11/50 (22%) Act 1 boss, 0/50 A0 clear.**

The 3-act A0 clear remains unreached. The next genuinely-impactful
levers are the same three from the prior doc (event lookup table,
deeper MultiTurn / MC rollouts, more relic-aware modelling) plus
modelling the exhaust pile count so AshenStrike's `6 + 3/exhaust`
formula has real signal too.
