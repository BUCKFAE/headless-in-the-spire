# STS2 Ironclad — Archetype & Synergy Graph for Draft Policy

Status: research deliverable, May 2026. Extends
`research-sts2-ironclad.md` (effects, tiers, A0 plan); this document focuses
on the *synergy graph* — which cards enable which payoffs, which combinations
are toxic, and which cards pivot between archetypes — so that an automated
draft policy can score offers by enabler/payoff distance rather than by flat
tier.

Confidence markers used throughout:
- **(verified)** — confirmed against the prior research doc and/or at least
  two community sources cited inline.
- **(LOW CONFIDENCE)** — sourced from a single community write-up or
  inferred from STS1 behavior + STS2 commentary. Policy code should
  flag-but-not-trust these.
- **(REMOVED?)** — community discussion indicates the card may no longer be
  in STS2. Drafter should gate on a presence check via the card catalog.

---

## 0. Card effect reference (delta against prior doc)

The prior doc covers most effects; new/clarified rows below.

| Card | Cost | Type | Effect (base / upgraded) | Source |
|---|---|---|---|---|
| Headbutt | 1 | Common attack | 9/12 dmg, place 1 card from discard on top of draw pile. **No block.** | sts2front |
| Hemokinesis | 1 | Uncommon attack | 15/20 dmg, lose 2 HP | wiki.gg |
| Bloodletting | 0 | Skill | Lose 3 HP, +2 energy | sts2front |
| Offering | 0 | Rare skill | Lose 6 HP, +2 energy, draw 3/5, Exhaust | sts2front |
| Battle Trance | 0 | Uncommon skill | Draw 3/4 cards, apply *No Draw* (can't draw rest of turn) | wiki.gg, sts2front |
| Brand | 0 | Rare skill | Exhaust 1 card from hand, lose 1 HP, +1/+2 Strength | untapped.gg / wiki.gg |
| Blood Wall | 1 | Skill | 16 block, lose 2 HP (per prior doc) | prior doc |
| Crimson Mantle | 1 | Rare power | Turn-start lose 1 HP, gain 8 block | prior doc |
| Inferno | 1 | Uncommon power | Turn-start lose 1 HP. Whenever you lose HP on your turn, deal 6/9 to ALL enemies. | wiki.gg / untapped.gg |
| Rupture | 1 | Power | When you lose HP from a card on your turn, +1/+2 Strength | sts2front |
| Spite | 1 | Attack | 6 dmg; if you lost HP this turn, hit 2/3 times | mobalytics |
| Blood for Blood | 4→start | Attack | Cost decreases by 1 each time you lose HP (LOW CONFIDENCE on exact start cost; behavior verified against community) | keengamer |
| Heavy Blade | 2 | Common attack | 14/18 dmg + 3× Strength scaling | sts2front |
| Body Slam | 1→0(+) | Attack | Damage = current block | sts2front |
| Barricade | 3 | Rare power | Block is not removed at turn end | sts2front |
| Inflame | 1 | Power | +2/+3 Strength | sts2front |
| Bully | 0 | Uncommon attack | 4 dmg + 2 per Vulnerable on target (per prior doc) | prior doc |
| Dismantle | 1 | Uncommon attack | 8 dmg; doubles vs. Vulnerable target (prior doc) | prior doc |
| Tremble | 1 | Common skill | Apply 3 Vulnerable, Exhaust (prior doc) | prior doc |
| Taunt | 1 | Uncommon skill | 7 block + 1 Vulnerable (prior doc) | prior doc |
| Ashen Strike | 1 | Uncommon attack | 6 dmg + 3 per card in Exhaust pile | mobalytics |
| Pact's End | 0 | Rare attack | 17/23 AoE; requires 3+ cards in Exhaust pile; Exhausts | sts2front, wiki.gg |
| Cascade | X | Rare skill | Play top X / X+1 cards of draw pile | untapped.gg, sts2front |
| Rampage | 1 | Attack | 9 dmg; +5 to *this card* per play (resets between combats) | prior doc + community |
| Expect a Fight | 1 | Skill | +1 energy per attack in hand (LOW CONFIDENCE on exact trigger window) | prior doc |
| Corruption | 3 | Rare power | Skills cost 0. Whenever you play a Skill, Exhaust it. | community |
| Demon Form | 3 | Rare power | Turn-start +2/+3 Strength | sts2front |
| Dark Embrace | 2→1(+) | Power | Whenever a card is Exhausted, draw 1 | sts2front |
| Feel No Pain | 1 | Power | Whenever a card is Exhausted, gain 3/4 block | sts2front |
| Fiend Fire | 2 | Rare attack | Exhaust your hand; deal 7/10 dmg per card Exhausted this way | community |
| Juggernaut | 2 | Rare power | When you gain block, deal 5/7 (LOW CONFIDENCE on number) to a random enemy | keengamer |
| Hellraiser | 2 | Rare power | When you draw a Strike, auto-play it on a random enemy | slaythespire2.space, tck.mn |
| Whirlwind | X | Uncommon attack | 5/8 AoE per energy spent | sts2front |
| Limit Break | — | — | **(REMOVED?)** community Steam discussion claims removed from STS2; prior doc still lists it. Drafter must gate on catalog presence. | Steam discussion |

---

## 1. Archetypes (synergy-graph oriented)

For each archetype: enablers (cards that *create* the engine), payoffs (cards
that *consume* enabler state for excess value), bonus picks (good but
non-load-bearing), anti-synergies (cards whose presence actively breaks the
engine), and pivot points.

A0-viability is the rough probability a fresh Neow run can *reliably* end
up at this archetype with a vanilla path, expressed as
{High / Medium / Low / Boss-only}.

### 1.1 Strength Scaling

A0 viability: **High**. The default fallback in the prior doc.

- **Core enablers (2):** Inflame, Demon Form. Brand and Spot Weakness (if
  present) are tertiary.
- **Payoffs:** Heavy Blade (3× Str), Twin Strike (2× Str via two hits),
  Pommel Strike, Whirlwind (per-energy hit, every hit scales), Dismantle
  (8 dmg + Str twice on Vulnerable), Rampage (when scaled in single combat).
- **Bonus picks:** Bully (Vulnerable+Str both scale it), Cleave (AoE Str
  scaling — LOW CONFIDENCE in STS2 print), Iron Wave (block half scales
  with Str if STS1 carryover; LOW CONFIDENCE), Inferno (synergy with Brand
  for AoE).
- **Anti-synergies:** Single-hit big-base attacks where Str is divided by
  card count (Bash variants); Body Slam without Block synergy (a Str-only
  deck rarely accumulates 50+ block).
- **Pivot points:** Brand (also feeds Self-Damage + Exhaust), Dismantle
  (also Vulnerable), Heavy Blade (also fine in Block-pivot decks once
  Barricade lands).

### 1.2 Vulnerable Synergy

A0 viability: **High** (heavily overlaps Strength).

- **Core enablers (2-3):** Tremble (3 Vulnerable for 1 energy, Exhausts),
  Bash, Taunt (7 block + 1 Vulnerable).
- **Payoffs:** Bully (+2 dmg per stack), Dismantle (×2 vs Vulnerable),
  Cruelty (Vulnerable damage multiplier — per prior doc), Thunderclap
  (Vulnerable + AoE).
- **Bonus picks:** Heavy Blade (still good but doesn't *need* Vulnerable),
  Paper Phrog relic, Red Mask relic.
- **Anti-synergies:** Cards that finish a target before Vulnerable can be
  applied (Rampage scaled, Fiend Fire) — wasted on dead enemies.
- **Pivot points:** Tremble → also fine in Exhaust decks for pile-loading,
  Taunt → also Block-pivot.

### 1.3 Exhaust Engine

A0 viability: **Medium** (needs Corruption *or* Dark Embrace early).

- **Core enablers (2):** Corruption (all skills cost 0 + Exhaust) OR Dark
  Embrace (draw per exhaust) + Feel No Pain (block per exhaust). The
  archetype works with *either* power tier present; both together is the
  ceiling.
- **Payoffs:** Fiend Fire (clears hand for damage), Pact's End (17 AoE
  once pile ≥3), Ashen Strike (6 + 3/pile), Brand (exhaust + Str).
- **Bonus picks:** Tremble (self-exhausts → feeds pile), Headbutt (loops
  Fiend Fire / Pact's End by tucking them after exhaust), Sever Soul
  (LOW CONFIDENCE in STS2), Charon's Ashes relic.
- **Anti-synergies:** Powers that don't trigger Corruption discount
  (Corruption affects Skills, not Powers — verify on the catalog);
  Battle Trance (its No-Draw clause **breaks** Dark Embrace cycling — see
  §3); Rampage (its scaling counter resets in same combat if it gets
  exhausted by Corruption, LOW CONFIDENCE).
- **Pivot points:** Brand (also Self-Damage + Strength), Crimson Mantle
  (also Self-Damage), Feel No Pain (also Block).

### 1.4 Self-Damage / Bloodletting / Rupture

A0 viability: **Medium-High** if Rupture or Inferno shows up.

- **Core enablers (3):** Rupture (Str on HP loss), Inferno (AoE damage on
  HP loss), Bloodletting / Offering (cheap HP→energy / HP→draw).
- **Payoffs:** Hemokinesis (1E for 15 dmg + Rupture/Inferno trigger from
  the 2 HP loss), Spite (extra hits if you lost HP), Blood for Blood
  (cheaper as HP loss accumulates), Brand (HP + Str + Exhaust), Crimson
  Mantle (1 HP/turn → 8 block + +Str via Rupture + AoE via Inferno),
  Blood Wall (16 block for 2 HP, triggers both).
- **Bonus picks:** Offering (premium even outside this archetype),
  Bloodletting (tempo).
- **Anti-synergies:** Cards that heal you to full or block all HP loss
  (Healing potions used mid-combat); decks with HP buffer too small
  (Ironclad starts 80 HP and Burning Blood heals 6 — the floor is real,
  but with two Crimson Mantles + Inferno + Brand you can drop 12+ HP/turn
  in a single fight).
- **Pivot points:** Brand → Exhaust, Crimson Mantle → Block, Blood Wall →
  Block, Offering → Cycling.

### 1.5 Block + Barricade + Body Slam

A0 viability: **Low** without Barricade; **High** if Barricade lands
early.

- **Core enablers (2-3):** Barricade (block doesn't decay), Body Slam
  (damage = current block). A non-Body-Slam Block deck is a survival
  build, not an archetype.
- **Payoffs:** Body Slam (the only direct payoff), Juggernaut (passive
  damage as block stacks), Crimson Mantle (8 block/turn under Barricade
  becomes a runaway), Blood Wall (16 block for 2 HP — huge under
  Barricade).
- **Bonus picks:** Taunt (block + Vulnerable), Iron Wave, Impervious
  (one-shot big block), Headbutt (re-tuck Body Slam to draw next turn —
  modest synergy), Sturdy Clamp relic.
- **Anti-synergies:** Cards that *spend* block to deal damage but reset
  it (LOW CONFIDENCE — if any STS2 card behaves this way); excessive
  Exhaust (you want to redraw Body Slam, not exhaust it).
- **Pivot points:** Crimson Mantle (Self-Damage + Block), Blood Wall
  (Self-Damage + Block), Taunt (Vulnerable + Block).

### 1.6 0-Cost Cycling / Hellraiser Infinite

A0 viability: **Low-Medium**. Needs Hellraiser + Pommel Strike ×2-3.

- **Core enablers (2-3):** Hellraiser (auto-plays drawn Strikes), Pommel
  Strike (draw on damage), Battle Trance (one-shot draw burst — but see
  anti-synergy).
- **Payoffs:** Every Strike in the deck (the starter 5 already qualify),
  Perfected Strike (scales with Strike count), Anger (free dmg + self-copy
  + Strike-named).
- **Bonus picks:** Bloodletting (energy for non-Strike turns), Inflame
  (every Strike hits harder).
- **Anti-synergies:** **Battle Trance** — applies *No Draw* for the rest
  of the turn, which kills the Pommel-Strike-→-draw-→-Strike loop. Treat
  it as a soft anti-synergy: a single early-turn cast is fine; a deck
  built around it is not the same archetype as Hellraiser-loop.
  Also: thorns enemies, draw caps (Slime / similar — LOW CONFIDENCE for
  STS2 specific encounters).
- **Pivot points:** Pommel Strike → Strength deck, Anger → Strike deck
  *or* dilution (Anger discard-copies bloat the deck fast).

### 1.7 Big Free Attacks / X-Cost / Cascade

A0 viability: **Low**. Needs energy ramp + Cascade *or* Whirlwind +
high-cost cards.

- **Core enablers (2):** Energy generation (Bloodletting, Offering, +max
  energy relic), and either Whirlwind (AoE per energy) or Cascade
  (auto-plays draw-pile top, ignoring cost).
- **Payoffs:** Whirlwind (the canonical X-cost finisher), Cascade
  played at high X to cheat out expensive cards, Fiend Fire (off-color
  finisher), Pact's End (0-cost finisher — synergy with Headbutt to tuck).
- **Bonus picks:** Headbutt (top-decks the finisher before Cascade),
  Demon Form (turns the finisher into a real threat). The classic
  Hemokinesis-×-Cascade auto-play build is documented as a first-clear
  build (vortexgaming).
- **Anti-synergies:** Anger (puts cheap junk in your draw pile — Cascade
  burns X on a 0-impact card), 0-cost junk in general (Tremble that
  exhausts itself before Cascade fires is wasted X), large deck size
  (low chance the finisher is near the top of the draw pile).
- **Pivot points:** Headbutt (Cycling + X-cost + Block), Demon Form
  (Strength + X-cost), Offering (Self-Damage + Cycling + X-cost).

---

## 2. Card-pair synergy table

Strength: **S** = archetype-defining, take both whenever possible; **A** =
strong, both-better-with-each-other; **B** = mild; **C** = situational.

| Card A | Card B | Strength | Why |
|---|---|---|---|
| Rupture | Brand | S | Brand HP-loss + Strength + Exhaust; Rupture doubles the Str. |
| Rupture | Crimson Mantle | S | 1 HP/turn → +Str passively forever. |
| Rupture | Hemokinesis | S | 15 dmg + auto-trigger Str. |
| Rupture | Bloodletting | A | Cheap energy + +1 Str per cast. |
| Inferno | Brand | S | 6/9 AoE per HP-loss; Brand is the cheapest HP-loss trigger (0 energy). |
| Inferno | Crimson Mantle | S | Passive 6/9 AoE every turn for free. |
| Inferno | Hemokinesis | A | Single-target 15 + 6 AoE in one card. |
| Corruption | Feel No Pain | S | Free skills + 3 block per skill. |
| Corruption | Dark Embrace | S | Free skills + draw on each — infinite-cycle base. |
| Corruption | Body Slam | A | Body Slam is an Attack (verify), so it doesn't 0-cost under Corruption. **LOW CONFIDENCE** that Body Slam stays an Attack; if it became a Skill in STS2 it's S. |
| Dark Embrace | Feel No Pain | A | Both fire on every Exhaust. |
| Dark Embrace | Brand | A | Brand exhausts a hand card → draw + Str. |
| Dark Embrace | Tremble | B | Tremble exhausts itself → +1 draw. |
| Feel No Pain | Tremble | B | Self-exhaust → block. |
| Ashen Strike | Corruption | A | Skills exhaust quickly → big multiplier. |
| Pact's End | Corruption | A | Skills exhaust → meets 3-pile threshold by turn 2. |
| Pact's End | Headbutt | A | Tuck Pact's End for guaranteed reload. |
| Barricade | Body Slam | S | The canonical block-payoff combo. |
| Barricade | Crimson Mantle | S | 8 block/turn that doesn't decay = scaling damage via Body Slam. |
| Barricade | Juggernaut | A | Every block instance pings; under Barricade you keep stacking. |
| Barricade | Blood Wall | A | 16 free permanent block per cast. |
| Body Slam | Juggernaut | A | Both convert block to damage. |
| Hellraiser | Pommel Strike | S | Strike → draw → auto-play next Strike. |
| Hellraiser | Battle Trance | C | Single fire is fine; *No Draw* kills loop afterwards. |
| Hellraiser | Perfected Strike | A | Big damage auto-played; bigger if many Strikes in deck. |
| Hellraiser | Anger | A | Free Strikes that self-copy on play. |
| Inflame | Heavy Blade | S | 3×Str scaler hits hardest off cheapest Str source. |
| Inflame | Whirlwind | S | Every hit scales with Str. |
| Inflame | Dismantle | A | Doubles on Vulnerable; Str adds on top. |
| Demon Form | Heavy Blade | S | Late-act snowball. |
| Demon Form | Whirlwind | S | Every turn adds Str to every hit. |
| Tremble | Bully | S | 3 Vulnerable → Bully hits for 4 + 6 = 10 for 0 energy. |
| Tremble | Dismantle | S | Vulnerable → 16 dmg for 1 energy. |
| Tremble | Cruelty | S | Vulnerable-payoff multiplier (per prior doc). |
| Taunt | Bully | A | Block + 1 Vulnerable + payoff in same turn. |
| Cascade | Whirlwind | S | Cheat out X-cost as a "free" play. |
| Cascade | Headbutt | A | Tuck the finisher before Cascade. |
| Cascade | Hemokinesis | A | Auto-play 15-dmg+HP-loss combos (vortexgaming first-clear). |
| Cascade | Anger | C | **Anti-synergy if Anger floods the draw pile**; useful only in a slim deck. |
| Offering | Cascade | A | Energy + draw + Cascade payoff. |
| Offering | Rupture | A | -6 HP → +1/+2 Str (×6 stacks? — Rupture triggers per HP-loss *event*, not per HP; LOW CONFIDENCE on stacking). |
| Headbutt | Body Slam | B | Tuck Body Slam for next turn (block carries under Barricade). |
| Headbutt | Hellraiser | B | Tuck any Strike to guarantee auto-play. |
| Battle Trance | Corruption | A | Burst-draw Skills → all 0-cost + Exhaust → Dark Embrace fires after Battle Trance's No-Draw lifts next turn. |
| Fiend Fire | Demon Form | A | Each Exhausted card hits with full Str. |
| Fiend Fire | Inflame | A | Same scaling, smaller. |
| Spite | Bloodletting | A | Pre-cast HP loss → Spite triples (or doubles). |
| Spite | Brand | A | Free HP loss → Spite scales. |
| Blood for Blood | Bloodletting | A | Each HP-loss event drops the cost. |
| Blood for Blood | Crimson Mantle | A | Turn-start HP loss → Blood for Blood is auto-cheaper. |
| Rampage | Headbutt | A | Tuck Rampage to keep stacking it. |
| Rampage | Corruption | C | If Rampage gets exhausted by some interaction the stack resets — LOW CONFIDENCE. |
| Expect a Fight | Attack-heavy deck | B | +1 energy per attack in hand — wants ≥3 attacks at start of turn. |

---

## 3. STS2-specific card archetype + best partner (quick lookup)

| Card | Best archetype | Best partner | Notes |
|---|---|---|---|
| Bully | Vulnerable | Tremble | 0-cost; scales linearly with Vulnerable stacks. |
| Tremble | Vulnerable / Exhaust pile-loader | Bully or Dismantle | Self-exhausts → also feeds Pact's End. |
| Dismantle | Vulnerable + Strength | Tremble + Inflame | Doubles on Vulnerable, scales with Str. |
| Taunt | Block-pivot + Vulnerable | Barricade, Body Slam | Block + 1 stack of Vulnerable. |
| Ashen Strike | Exhaust | Corruption / Tremble | Scales with pile size; weak early, strong by turn 3. |
| Pact's End | Exhaust | Corruption + Headbutt | 17 AoE for 0E when pile ≥3. |
| Brand | Self-Damage / Exhaust / Strength (triple pivot) | Rupture or Inferno | The single biggest pivot card in the kit. |
| Blood Wall | Block / Self-Damage | Barricade or Rupture+Inferno | 16 block for 2 HP @ 1E is premium rate. |
| Crimson Mantle | Self-Damage / Block | Rupture, Inferno, Barricade | Best in decks that already mitigate the HP drain. |
| Cascade | X-cost / Cycling | Whirlwind, Headbutt, Hemokinesis | Hates Anger flooding draw pile. |
| Rampage | Strength early-fight ramp | Headbutt | Scales within a fight, not across them. Weaker in 3-act run. |
| Hellraiser | Strike-cycling | Pommel Strike ×2-3 | New STS2 archetype; one of the few pseudo-infinites. |
| Inferno | Self-Damage AoE | Brand, Crimson Mantle | Cheapest AoE-per-turn in the game with one HP-loss card. |
| Headbutt | Cycling / Block pivot / X-cost | Cascade, Pact's End, Body Slam | A *tool*, not a payoff; rarely a take-by-itself. |
| Battle Trance | One-shot draw, **NOT cycling** | Corruption (turn 1), Strength | The No-Draw clause makes it actively bad in Hellraiser/Pommel loops. |

---

## 4. The "Block + Headbutt" archetype question

The user named **"Block + Headbutt"** as a starting archetype. Per verified
effect text, **Headbutt does not grant block** in STS2 — it deals 9/12 dmg
and tucks a discard-pile card on top of the draw pile (sts2front, untapped.gg,
sts2.gg).

The most natural reading of "Block + Headbutt" is therefore **the Block
archetype using Headbutt as a *tool* to re-deck Body Slam or a key block
power**, *not* as a block source itself. The draft policy should:

- Treat Headbutt as a B-tier pivot tool, not a Block enabler.
- Only weight it up when Body Slam / Barricade / Crimson Mantle is already
  in deck.
- Never let Headbutt count toward the "block engine" enabler count.

If a future patch gives Headbutt a block clause, this assumption breaks and
the catalog check will surface it.

---

## 5. Cross-archetype pivot graph

```
                Brand
              /   |   \
   Self-Damage  Exhaust  Strength
       |          |        |
   Rupture/    Corruption  Inflame
   Inferno     /  |  \     |
       \      /   |   \    |
        Crimson Dark   Feel No Pain
        Mantle  Embrace        \
          |       \             Block pivot ---- Body Slam ---- Barricade
          +------ Block --------+                       |
                                                    Juggernaut
                                Vulnerable
                                /   |   \
                          Tremble Bully Dismantle
                              \    |    /
                               Cruelty
                                    |
                              Strength overlap
```

Key pivot cards (count of archetypes they're a top pick in):
- **Brand**: 3 (Self-Damage, Exhaust, Strength).
- **Crimson Mantle**: 3 (Self-Damage, Block, Strength-via-Rupture).
- **Tremble**: 2 (Vulnerable, Exhaust pile-loader).
- **Headbutt**: 3 (Block, X-cost/Cascade, Cycling/Hellraiser).
- **Offering**: 4 (everywhere — premium standalone).
- **Cascade**: 2 (X-cost, Cycling).

---

## 6. Draft policy cheat sheet (codifiable)

Each archetype below is structured for direct translation to a scoring
function. `enabler` = card whose presence in deck pushes the score for
*payoff* offers upward. `payoff` = card whose value depends on enablers
already in deck. `anti` = penalty if both card and archetype-flag are set.
Card names match the catalog generation in `src/Sts2Headless.Runtime/`
(verify on next sweep).

### 6.1 Strength Scaling

```
enablers:   [Inflame, Demon Form, Brand, SpotWeakness?]
payoffs:    [HeavyBlade, TwinStrike, PommelStrike, Whirlwind, Dismantle, Rampage, Bully]
bonus:      [IronWave, Cleave, Inferno, Anger]
anti:       []   # additive deck, few hard anti-synergies
threshold:  enablers >= 1  ->  payoffs scored at +2 over base
            enablers >= 2  ->  payoffs scored at +3 over base
pivot_to:   [Vulnerable (if Tremble appears), SelfDamage (if Rupture appears)]
```

### 6.2 Vulnerable

```
enablers:   [Tremble, Bash, Taunt, Thunderclap]
payoffs:    [Bully, Dismantle, Cruelty]
bonus:      [HeavyBlade, Inflame]   # double-dipping with Strength
anti:       []   # additive
threshold:  enablers >= 1 AND payoffs >= 1  ->  archetype confirmed
            otherwise lean Strength
pivot_to:   [Strength (always overlapping), Block (Taunt also slots)]
```

### 6.3 Exhaust Engine

```
enablers:   [Corruption, DarkEmbrace, FeelNoPain]
payoffs:    [FiendFire, PactsEnd, AshenStrike, Brand]
bonus:      [Tremble, Headbutt, CharonsAshesRelic]
anti:       [BattleTrance (in Dark-Embrace-cycling sub-archetype)]
threshold:  enablers must include at least one of [Corruption, DarkEmbrace]
            AND deck_size <= 22   # bricks above that
            otherwise hard skip Ashen Strike / Pact's End
pivot_to:   [SelfDamage (Brand bridge), Block (FeelNoPain bridge)]
```

### 6.4 Self-Damage

```
enablers:   [Rupture, Inferno, Bloodletting, Offering]
payoffs:    [Hemokinesis, Spite, BloodForBlood, CrimsonMantle, BloodWall, Brand]
bonus:      [Offering]   # premium even outside archetype
anti:       []   # but watch HP floor: refuse to take more HP-loss cards
                 # if current_hp < 40% and no healing relic
threshold:  Rupture OR Inferno in deck  ->  payoffs scored at +3
            Both in deck                ->  payoffs scored at +4
            Neither                     ->  Self-Damage payoffs are mid B-tier
pivot_to:   [Exhaust (Brand), Block (CrimsonMantle, BloodWall), Strength (Rupture stacks Str)]
```

### 6.5 Block / Barricade / Body Slam

```
enablers:   [Barricade, Juggernaut, CrimsonMantle, BloodWall, FeelNoPain]
payoffs:    [BodySlam, Juggernaut]
bonus:      [Taunt, IronWave, Impervious, Headbutt (as cycling tool only)]
anti:       []
threshold:  Barricade in deck  ->  archetype committed; payoffs at S
            no Barricade        ->  Block is a survival kit, not an archetype
                                    Body Slam scored as C-tier
pivot_to:   [SelfDamage (CrimsonMantle, BloodWall both pivot)]
```

### 6.6 Strike-Cycling / Hellraiser

```
enablers:   [Hellraiser, PommelStrike, Anger]
payoffs:    [PerfectedStrike, every Strike in deck]
bonus:      [Bloodletting (energy), Inflame (Str on every Strike)]
anti:       [BattleTrance]   # No-Draw breaks the loop; tolerable as one-shot
            [excessive_card_draw_disablers]
threshold:  Hellraiser in deck AND PommelStrike count >= 2
            otherwise treat as plain Strength deck with Strike support
pivot_to:   [Strength]
```

### 6.7 X-Cost / Cascade Big Plays

```
enablers:   [Cascade, Whirlwind, Bloodletting, Offering, +energy relics]
payoffs:    [Whirlwind, Cascade, FiendFire, PactsEnd]
bonus:      [Headbutt (tucking), DemonForm, Hemokinesis]
anti:       [Anger]                      # floods draw pile with junk
            [deck_size > 22]             # finisher dilution
            [Tremble that self-exhausts before Cascade fires]
threshold:  Cascade OR Whirlwind in deck AND >=1 energy generator
            otherwise X-cost is an opportunistic side-grab
pivot_to:   [SelfDamage (Offering bridge), Cycling]
```

### 6.8 Global rules (apply to all archetypes)

1. **Headbutt scoring**: B-tier baseline. +1 if deck contains Body Slam,
   Pact's End, Cascade, Whirlwind, or Rampage. Never an Enabler.
2. **Brand scoring**: A-tier in Self-Damage, Exhaust, *and* Strength.
   Whenever Brand is offered, score it as the union (max) of the three
   archetype values — don't let single-archetype scoring underrate it.
3. **Offering scoring**: S-tier global. Don't gate on archetype.
4. **Crimson Mantle gate**: Refuse if deck has no Rupture / Inferno /
   Barricade / Juggernaut / Block engine. Standalone it bleeds the player.
5. **Demon Form gate**: Refuse if confirmed energy upgrades < 1.
   Restate prior doc rule.
6. **Anti-Battle-Trance gate**: If deck contains Hellraiser, score Battle
   Trance at C-tier (acceptable but de-prioritised).
7. **Cascade-Anger conflict**: If deck contains Cascade, score Anger at
   D-tier (active anti-synergy).
8. **Limit Break check**: Before scoring Limit Break as S-tier, query the
   card catalog (`Sts2Headless.MechanicSweep` ids) to confirm presence.
   Community discussion suggests removal; if absent from catalog, route
   the equivalent role to Inflame + Demon Form.
9. **Deck size guard**: Above 22 cards, *all* archetype payoffs lose 1
   tier (dilution dominates). Above 25 cards, refuse non-S cards by
   default unless they directly close a known weakness.

---

## 7. Confidence audit

- **Verified against prior doc**: Headbutt effect, Tremble, Taunt, Bully,
  Dismantle, Pact's End, Ashen Strike, Crimson Mantle, Blood Wall, Brand,
  Offering, Bloodletting, Cascade, Rampage, Whirlwind, Body Slam,
  Barricade, Heavy Blade, Inflame, Demon Form, Corruption, Dark Embrace,
  Feel No Pain, Fiend Fire.
- **Newly added / verified this pass**: Inferno (1E power, HP-loss → 6/9
  AoE), Rupture (1E power, +1/+2 Str on HP loss), Hemokinesis (1E, 15/20
  dmg, -2 HP), Spite (lost-HP-this-turn → multiple hits), Blood for Blood
  (cost decreases per HP-loss), Hellraiser (2E power, auto-plays drawn
  Strikes), Juggernaut (gain-block → damage), Battle Trance (3/4 draw +
  No Draw debuff), Brand (0E, exhaust + 1 HP + 1/2 Str).
- **LOW CONFIDENCE / drafter must verify on catalog**:
  - Limit Break presence in STS2.
  - Body Slam type tag (Attack vs Skill) — matters for Corruption.
  - Rupture trigger semantics — is it "per HP-loss event" or "per HP
    point lost"? Treat as per-event by default; offering of 6 HP loss
    still triggers once.
  - Expect a Fight exact trigger window (per-card draw vs end-of-turn).
  - Juggernaut damage value.
  - Whether Anger explicitly counts as a Strike-named card (the
    Strike-cycling / Hellraiser archetype assumes yes; community sources
    treat it ambiguously).
  - Whether Rampage's counter survives Exhaust effects in Corruption decks.

A wired-up `MechanicSweep` pass or a `Sts2Headless.Commands.probe-modeldb`
call will resolve every LOW CONFIDENCE entry above; the draft policy
should *not* assume these without that verification.

---

## Sources

(Prior-doc sources are inherited; new sources cited this pass:)

- sts2front, Headbutt — https://sts2front.com/cards/headbutt/
- sts2front, Battle Trance — https://sts2front.com/cards/battle-trance/
- sts2front, Brand — https://sts2front.com/cards/brand/
- sts2front, Cascade — https://sts2front.com/cards/cascade/
- sts2front, Ironclad card analysis — https://sts2front.com/builds/ironclad-card-analysis/
- wiki.gg, Pact's End — https://slaythespire.wiki.gg/wiki/Slay_the_Spire_2:Pact%27s_End
- wiki.gg, Crimson Mantle — https://slaythespire.wiki.gg/wiki/Slay_the_Spire_2:Crimson_Mantle
- wiki.gg, Inferno — https://slaythespire.wiki.gg/wiki/Slay_the_Spire_2:Inferno
- wiki.gg, Hemokinesis — https://slaythespire.wiki.gg/wiki/Slay_the_Spire_2:Hemokinesis
- wiki.gg, Battle Trance — https://slaythespire.wiki.gg/wiki/Slay_the_Spire_2:Battle_Trance
- untapped.gg, Cascade — https://sts2.untapped.gg/en/cards/cascade
- untapped.gg, Brand — https://sts2.untapped.gg/en/cards/brand
- untapped.gg, Ashen Strike — https://sts2.untapped.gg/en/cards/ashen-strike
- untapped.gg, Inferno — https://sts2.untapped.gg/en/cards/inferno
- untapped.gg, Battle Trance — https://sts2.untapped.gg/en/cards/battle-trance
- mobalytics, Ironclad guide — https://mobalytics.gg/slay-the-spire-2/characters/ironclad-guide
- keengamer, Best Ironclad Builds — https://www.keengamer.com/articles/guides/slay-the-spire-2-best-ironclad-builds/
- slaythespire-2.com, Bloodletting Build — https://slaythespire-2.com/builds/ironclad-bloodletting-build
- slaythespire-2.com, Self-Wound Build — https://slaythespire-2.com/builds/ironclad-self-wound-build
- slaythespire-2.com, Barricade Build — https://slaythespire-2.com/builds/ironclad-barricade-build
- slaythespire2.space, Hellraiser guide — https://slaythespire2.space/guides/hellraiser/
- tck.mn, Hellraiser infinite analysis — https://tck.mn/blog/hellraiser-infinite/
- vortexgaming, Hemokinesis × Cascade first-clear — https://vortexgaming.io/en/postdetail/691980
- gamerblurb, Whirlwind guide — https://gamerblurb.com/articles/slay-the-spire-2-whirlwind-guide
- gamerant, Best Ironclad combos — https://gamerant.com/slay-the-spire-2-sts2-best-ironclad-cards-combos/
- Steam discussion, Limit Break / Catalyst removal — https://steamcommunity.com/app/2868840/discussions/0/798965318967026979/
