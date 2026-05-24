# STS2 Act-1 Bosses — Prep & Counter-Play (Ironclad)

Status: research deliverable, May 2026. Patch reference: v0.103.x.
Companion to `research-archetypes-synergies.md` (which the draft policy
already understands); this document maps the three Act-1 boss IDs
(`CEREMONIAL_BEAST_BOSS`, `VANTOM_BOSS`, `THE_KIN_BOSS`) to that archetype
graph.

Confidence markers:
- **(verified)** — confirmed by wiki.gg or sts2front *and* at least one
  independent community write-up.
- **(community)** — single-source community guide; treat as approximate.
- **LOW CONFIDENCE** — sourced from one community write-up only or
  inferred from move text.

A note on STS2 Act-1 boss roulette: the player rolls one of these three
plus a possible biome-specific extra (e.g. Inkblot Phantasm in the Forest
slice — *not* the same boss as Vantom; see §0). Vantom and the Kin appear
in the Overgrowth/Forest pool; Ceremonial Beast is the Forest "easy" boss.
The Ironclad agent should pre-plan for any of the three before path
commitment; the actual roll only narrows late-Act-1 path choices.

---

## 0. Disambiguation: Vantom vs. Inkblot Phantasm

These are **separate bosses** despite the shared ink theme. Vantom's
signature is **Slippery** (9 stacks → multi-hit counter); Inkblot
Phantasm's signature is **Call status cards + Intangible** (sts2front).
The user-supplied `VANTOM_BOSS` ID corresponds to the Slippery boss.
Inkblot Phantasm is out of scope for this document.

---

## 1. CEREMONIAL_BEAST_BOSS

### Stats & moves (verified, wiki.gg)
- **HP (A0):** 252 (262 at A8+).
- **Phase 1 — Plow ramp:**
  - Turn 1: **Stamp** — sets Plow threshold to 150 HP (160 at A9+). No
    direct damage. Buff intent.
  - Turn 2+: **Plow** — 18 damage (20 at A9+) **+ gains 2 Strength per
    cast**. Strength accumulates across Plow turns. Damage ramp:
    T2 = 18, T3 = 20, T4 = 22, T5 = 24, T6 = 26… until threshold hits.
- **Plow threshold (verified):** when HP drops to 150 or below, the
  Beast is **Stunned** for one full turn and **loses all Strength**.
  This is the player's hard reset and free turn.
- **Phase 2 — Beast Cry / Stomp / Crush loop:**
  - **Beast Cry** — applies **1 Ringing** (debuff: "you can only play 1
    card this turn"). Buff intent, no damage.
  - **Stomp** — 15 damage (17 at A9+). Plain attack.
  - **Crush** — 17 damage (19 at A9+) **+ gains 3 Strength permanently**
    (4 at A9+). Phase-2 Strength **does not reset**.
  - Permanent Phase-2 Str scaling: Crush #1 → +3 Str (Stomp now 18, Crush
    now 20+3=23), Crush #2 → +6 Str (Stomp now 21, Crush 26+6=32), etc.

### What kills you
- **Phase-1 over-stay.** If you can't reach 102 damage (252 - 150 = 102)
  by turn 5–6, Plow's scaling Str pushes a 22 / 24 / 26 damage hit through
  whatever block you can muster, and you bleed out before the stun lands.
- **Phase-2 lock-out via Ringing.** A turn where you draw a Block-only
  hand and Beast Cry hits is a turn where you eat full Crush (Str-scaled)
  with token mitigation.
- **Phase-2 Strength snowball.** If Phase 2 drags past Crush #2, Stomp
  starts hitting for 21+ and Crush for 30+. Block engines that scale
  linearly lose this race.

### Hard counters
- **Vulnerable.** A single Tremble (3 Vulnerable, Exhaust) cuts a Plow
  from 18 to 27 → wait that's wrong direction. Vulnerable *increases*
  damage *you* deal: Plow + Tremble means a 9-damage Bash hits for 13,
  not that incoming Plow hits softer. **Re-read:** Vulnerable on the
  Beast = your strikes hit ~50% harder. Pairs ridiculously well with
  Bully and Dismantle here. (community + archetype doc §1.2.)
- **Strength stacking.** This is a single-target HP-race fight. Every
  point of Strength on Inflame / Demon Form / Brand is a free DPT bump.
- **Bully + Tremble combo.** 0-energy for ~10 damage (4 + 2×3 stacks)
  shaves Plow turns fast.
- **Front-loaded burst (Cascade, Whirlwind, Hemokinesis).** Reaching the
  Plow threshold by turn 3 turns the boss into a free piñata.
- **Phase-2 mitigation via Vulnerable + Bully:** even under Ringing
  (1 card / turn), Bully + Vulnerable lets that one card still deal
  ~14 damage at 0 energy.

### A0 prep checklist (Ironclad)
- **Vulnerable source:** **at least 1** (Tremble preferred; Bash if you
  haven't drafted Tremble yet). Without it, this fight is a flat
  HP race and Phase 2 ramps faster than you can race.
- **Strength source:** **at least 1** (Inflame is the canonical option;
  Brand if you also have Rupture). Demon Form is overkill but turns the
  fight trivial.
- **Block engine:** modest — you need to absorb roughly 22–26 dmg/turn
  in late Phase 1 and 18–25/turn in Phase 2. Two block cards in deck is
  enough; Barricade is *not* required.
- **Power cards:** Inflame yes. Avoid Demon Form unless you have +1
  energy support (it'll sit dead in hand on turn 1 under base 3 energy).
- **Potions:** **Liquid Bronze (Thorns)** is great here — Phase 1 is many
  same-sized attacks. **Strength Potion** secures a fast Plow-threshold
  break. **Block Potion** for the Crush #2/#3 turn. **Fire Potion** can
  shave 20 HP at no card-economy cost.
- **Minimum DPT estimate:** to clear Phase 1 in 5 turns you need
  ~20 dmg/turn average (102 HP ÷ 5). With a single Inflame (+2 Str) +
  the starter Strikes that's just reachable; with one Bash + Inflame +
  one Heavy Blade it's comfortable.
- **Minimum deck size:** stock 10 cards is fine *if* you've picked up
  Inflame and one of Bash/Tremble. Aggressive removal (Neow + first
  Merchant) lifts this from "comfortable" to "trivial".

### Skip cards (look good, are traps here)
- **Pure block stalls without a kill clock.** Barricade-only / Body Slam
  starter without Body Slam in hand stalls into Phase-2 Str snowball.
- **Single-target X-cost setups in a fat deck.** Cascade in a 22+ card
  deck won't reliably surface a finisher before Phase 2 spirals.
- **Battle Trance the turn before Crush.** No-Draw caps your Phase-2
  defensive draw exactly when you need a Block card mid-turn.

### Archetype mapping
- **Best:** Strength scaling (§6.1) and Vulnerable (§6.2) — both
  load-bearing, both A0-viable.
- **Acceptable:** Self-Damage (§6.4) is fine — Inferno triggers off
  Rupture/Crimson-Mantle drain and the Beast has no Intangible. Strength
  via Rupture works.
- **Hard skip:** Block-only without Body Slam. The fight has no
  Intangible / no Artifact removal, but it *does* have Ringing which
  caps your card plays — Block engines that need 2+ cards per turn to
  break even (Crimson Mantle + Blood Wall combo) lose to Beast Cry.

---

## 2. VANTOM_BOSS

### Stats & moves (verified, wiki.gg)
- **HP (A0):** 173 (183 at A8+).
- **Opening buff:** **9 stacks of Slippery** (verified, wiki.gg). Each
  stack absorbs one HP-loss event into 1 HP. Every individual hit
  consumes one stack regardless of card power.
- **4-turn fixed cycle (repeats):**
  - T1 **Ink Blot** — 7 dmg, single hit (verified).
  - T2 **Inky Lance** — 6 damage × 2 (verified; some community guides
    report this as 14 single-hit — wiki.gg's "6 × 2" is the verified
    form).
  - T3 **Dismember** — 27 damage **+ shuffles 3 Wound cards into
    discard pile** (verified). This is the kill-window damage spike.
  - T4 **Prepare** — gains **2 Strength** (verified). Damage scales
    cycle-over-cycle: cycle 2 has Ink Blot at 9, Inky Lance at 8×2,
    Dismember at 29.

### What kills you
- **Dismember on turn 3 + Wound dilution.** 27 raw damage spike against
  a partially-blocked board followed by three unplayable Wound cards
  shuffled into your discard. The Wounds clog hand for the rest of the
  fight (no Exhaust source = permanent dead draws).
- **Slippery rope-a-dope.** If you front-load big single hits in T1/T2,
  you waste a full deck on 9 HP of damage; T3 Dismember then connects
  cleanly because you have no resources left.
- **Cycle-2 ramp.** If Slippery isn't fully stripped by end of T2,
  every subsequent cycle gets +2 Str. Dismember at 33+ damage by cycle 3
  is unsurvivable without massive block.

### Hard counters
- **Multi-hit cards.** Twin Strike (2 hits = 2 stacks for 1 energy),
  Pommel Strike (1 hit but with draw — strong tempo), Anger (cheap
  hits + self-copy). Anything that hits ≥2 times per energy is gold.
- **Whirlwind.** Verified community recommendation (games.gg). Each
  per-energy hit strips one stack — at 3 energy that's 3 stacks off in
  one card. **This is the cleanest hard counter to Slippery in the
  Ironclad kit.**
- **Dark Embrace / Feel No Pain** (Exhaust archetype) can absorb the
  Wound dilution: Wounds don't normally Exhaust on play (they're
  unplayable), but you can hold them out of hand or accept dead draws
  in a small deck. Better: take the Wounds and *never* let deck size
  matter.
- **Hellraiser-loop.** Each auto-played Strike is one stack stripped at
  zero card-cost. Stripping all 9 in turn 1 is feasible.

### A0 prep checklist (Ironclad)
- **Multi-hit source:** **mandatory.** At least one of: Twin Strike,
  Pommel Strike, Anger, Whirlwind, Hellraiser+Pommel-loop. Without one
  of these, the fight is winnable but tight — you'll spend Strikes
  inefficiently and eat full Dismember.
- **Vulnerable source:** **not load-bearing here.** Vulnerable doesn't
  remove Slippery stacks (the 1-HP cap is per-event, not
  damage-dependent). Vulnerable becomes useful only *after* T2 when
  stacks are gone.
- **Strength source:** **mid-priority.** Inflame helps your post-T2
  damage race, but pure Strength on single-hit Strikes is wasted during
  the Slippery phase. Pair it with multi-hit cards (Twin Strike +
  Inflame is excellent — 2 stacks stripped *and* both hits do real
  damage post-Slippery).
- **Block engine:** **mandatory** for T3. You need to soak 27 raw
  damage on T3 of each cycle. Plan for ~25–30 block on T3, less on
  T1/T2/T4. Defend + Iron Wave + Block Potion clears it.
- **Power cards:** Inflame is fine. **Avoid playing Demon Form on T3**
  (skip a turn of block to set up a power that'll trigger after the
  Dismember spike). Inferno is *bad* here — your own HP loss does
  nothing for Slippery removal.
- **Potions:** **Liquid Bronze (Thorns)** — exceptional here. Each
  Slippery-strip triggers Thorns ~9 times in Phase 1. **Block Potion**
  on Dismember turn. **Strength Potion** on T4 (Prepare turn) to
  out-race the next cycle. **Energy Potion** to enable Whirlwind ×5+.
- **Minimum DPT estimate (post-Slippery):** 173 HP - 9 (stripped during
  Slippery) = 164 HP to dent in ~2-3 cycles after T2. Need ~25 dmg/turn
  averaging. Comfortable with Inflame + 1 multi-hit + starter Strikes.
- **Minimum deck size:** **<=20 cards.** Dismember adds 3 Wounds per
  cast (twice in a normal A0 fight = 6 dilution). A 25-card deck has
  serious draw-quality issues by cycle 3.

### Skip cards (look good, are traps here)
- **Hemokinesis solo.** 15 damage single hit = 1 Slippery stack stripped.
  Catastrophic value loss. *Only* take it if you also have Twin Strike /
  Whirlwind to strip stacks first.
- **Heavy Blade / Pommel Strike pre-Slippery clear.** Same trap as
  Hemokinesis — big single hits during T1/T2 strip exactly one stack.
- **Big single-target X-cost (Cascade onto Hemokinesis).** Cascading
  several single-target attacks early just dumps Slippery faster *but*
  burns your finishers on 1-HP hits. Cascade-into-Whirlwind is fine;
  Cascade-into-Strike-spam is bad value.
- **Inferno** — HP loss + AoE means nothing here. Vantom is a single
  target and the AoE is wasted; the HP cost is real.
- **Rupture without Inflame.** Rupture turns HP loss into Strength;
  Strength only matters once stacks are cleared. Underperforms here
  versus Inflame.

### Archetype mapping
- **Best:** Strike-cycling / Hellraiser (§6.6), X-cost / Cascade with
  Whirlwind (§6.7). Both natively output many hits-per-energy.
- **Acceptable:** Strength scaling (§6.1) *if* paired with a multi-hit
  payoff (Twin Strike, Whirlwind). Vulnerable (§6.2) is post-Slippery
  useful only.
- **Hard skip:** Pure Self-Damage (§6.4) — Inferno's AoE is wasted;
  HP loss is sunk cost on a single target. Block-only stall (§6.5)
  loses to the +2 Str/cycle ramp.

---

## 3. THE_KIN_BOSS

### Stats & moves (verified, wiki.gg)
- **HP (A0):**
  - Kin Priest: **190 HP** (verified).
  - Kin Follower ×2: **58–59 HP each** (verified). Total HP pool ≈ 308.
- **Kin Priest 4-turn cycle:**
  - **Orb of Frailty** — 8 damage **+ 1 Frail** (-25% block next turn)
    (verified).
  - **Orb of Weakness** — 8 damage **+ 1 Weak** (-25% attack next turn)
    (verified).
  - **Soul Beam** — 3 damage × 3 (verified).
  - **Dark Ritual** — gains **2 Strength** (verified). Damage on next
    cycle scales.
- **Kin Follower 3-turn cycle (two followers, offset start):**
  - **Quick Slash** — 5 damage (verified).
  - **Boomerang** — 2 damage × 2 (verified; community sources sometimes
    call this "Power Dance" — wiki.gg is canonical).
  - **Power Dance** — gains **2 Strength** (verified).
- **Crucial mechanic (community / wiki.gg implied, LOW CONFIDENCE
  whether priest-death auto-ends fight in v0.103.x — sts2front says
  "Followers die when Priest dies"; wiki.gg only confirms Priest
  reaction to Follower deaths, not the reverse).** Treat as a soft win
  condition: priest-kill probably ends, follower-only kill definitely
  doesn't.

### What kills you
- **Triple-threat damage stacking.** Three enemies each scaling +2 Str
  per cycle means by cycle 3 you're eating ~(8+4)+(8+4)+(5+4)+(2+4)×2 =
  ~37+ unmitigated damage in a single round.
- **Frail + Weak combo turn.** When Priest plays Frailty/Weakness in
  sequence, you have 2-turn windows where your block is -25% *and*
  your damage is -25%. Your kill-clock slows exactly when their damage
  ramps.
- **Soul Beam 3×3 mid-Frail.** Multi-hit through Frail (with -25%
  block) shreds undefended players.
- **Misallocated focus.** Killing the followers first (58 HP each ≈
  116 damage spent) and *then* the priest (190 HP) means you eat ~3-4
  Priest cycles you didn't need to. Killing the Priest first ends the
  fight at ~190 damage if the auto-end mechanic holds.

### Hard counters
- **AoE.** Whirlwind, Cleave (if present), Thunderclap (if present),
  Inferno, Pact's End, Fiend Fire all hit all three at once. Whirlwind
  + Inflame trivialises this fight.
- **Single-target rush on Priest.** Heavy Blade + Inflame + Vulnerable
  (Tremble on Priest) → 190 HP in 2-3 turns is plausible.
- **Vulnerable on Priest.** A single Tremble on the Priest fast-tracks
  the rush plan (Heavy Blade hits 18 + 6 Str + 50% Vuln ≈ 36).
- **Inferno + Brand / Crimson Mantle.** 6/9 AoE every turn that you
  take HP damage — the Followers stay near death immediately, and the
  Priest takes incidental damage every turn.

### A0 prep checklist (Ironclad)
- **Strategic fork — pick before entering:**
  - **AoE plan:** any of Whirlwind, Inferno, Pact's End (rare A0), or
    enough multi-target potions (Fire Potion, Liquid Bronze).
  - **Single-target rush plan:** Heavy Blade + Inflame + Tremble +
    enough block to survive 3 Priest cycles (~24 dmg + Frail/Weak).
- **Vulnerable source:** **strongly recommended.** Vulnerable on the
  Priest cuts the rush from 4 turns to 2-3. Tremble is ideal because
  it's 1 energy + Exhausts (doesn't clog).
- **Strength source:** **mandatory** for the rush plan; **bonus** for
  the AoE plan (Whirlwind + Inflame scales every hit on every target).
- **Block engine:** modest. You need to survive the first 2 cycles —
  roughly 15–18 dmg/turn incoming on average, spiking to ~25 on
  Soul-Beam-during-Frail. Two block cards + one defensive potion is
  enough.
- **Power cards:** Inflame yes. Demon Form fine if energy permits.
  Inferno is *excellent* (every HP-loss → 6 AoE to all three enemies).
  Corruption + Pact's End is the dream AoE setup.
- **Potions:** **Fire Potion** to one-shot a Follower. **Liquid Bronze
  (Thorns)** is OK but less load-bearing than against Vantom
  (Followers have fewer hits per turn). **Block Potion** for the Frail
  turn. **Strength Potion** for the rush plan.
- **Minimum DPT estimate:**
  - AoE plan: ~10 AoE/turn × 3 targets ≈ 30 effective DPT for ~6 turns
    (308 HP total).
  - Rush plan: ~25 single-target/turn × 3 turns vs Priest = 75 ×
    Vulnerable-multiplier → ~115 dmg in 3 turns vs 190 HP. Tight without
    Inflame; comfortable with Inflame + Tremble.
- **Minimum deck size:** ≤22. Excess dilution means you don't draw
  Whirlwind / Heavy Blade on the turn it matters.

### Skip cards (look good, are traps here)
- **Pure single-target burst when you also lack a finisher.** Spite,
  Blood for Blood and Hemokinesis hit the Priest hard but leave the
  Followers free to scale Strength forever. Take them *in addition to*
  an AoE answer, not instead.
- **Body Slam without AoE.** Block-based damage routes only one target.
  Followers keep scaling.
- **Battle Trance on the Frailty/Weakness turn.** The No-Draw clause
  removes your ability to react when Soul Beam follows.
- **Tremble-into-single-target-rush *without* Inflame.** You commit to
  the rush plan, the Priest takes 3 turns to die, and the Followers'
  Strength snowball outscales you in those 3 turns.

### Archetype mapping
- **Best:** X-cost / Cascade + Whirlwind (§6.7), Self-Damage AoE via
  Inferno (§6.4), Exhaust + Pact's End (§6.3). All three deliver native
  AoE.
- **Acceptable:** Strength + Vulnerable rush plan (§6.1 + §6.2). Works
  with one Tremble and one Heavy Blade.
- **Hard skip:** Hellraiser/Strike-loop (§6.6). Auto-played Strikes
  fire at *random* enemies (per archetype doc §1.6), spreading damage
  across 3 targets — the worst possible distribution when the
  win-condition is killing one specific enemy. **Strike-loop into Kin
  is a known trap.** Block-only stall (§6.5) loses to triple Str ramp.

---

## 4. Cross-boss prep summary

| Need | Beast | Vantom | Kin |
|---|---|---|---|
| Vulnerable | **mandatory** | post-T2 only | strong (rush plan) |
| Strength | **mandatory** | mid (pair w/ multi-hit) | strong |
| Multi-hit | nice | **mandatory** | bonus (Whirlwind covers AoE too) |
| AoE | none | none | **mandatory or rush plan** |
| Block engine | modest | **mandatory on T3** | modest |
| Inflame | yes | yes (paired) | yes |
| Inferno | OK | **trap** | **excellent** |
| Hellraiser | OK | excellent | **trap** |
| Whirlwind | OK | **excellent** | **excellent** |
| Tremble | **excellent** | weak (post-Slippery only) | excellent (Priest) |
| Heavy Blade | excellent | weak pre-Slippery | excellent (Priest rush) |

The **universal A0 floor** an Ironclad agent wants regardless of which
boss rolls: Inflame + Tremble + one of {Whirlwind, Twin Strike, Heavy
Blade} + ~12 effective block per turn. That covers Beast (Str + Vuln),
covers Vantom (Twin Strike / Whirlwind hits stacks), and covers Kin
(Whirlwind AoE *or* Heavy Blade rush + Tremble on Priest).

---

## 5. Draft-policy cheat sheet per boss

Each block below is structured for direct translation into Archetype-
flagged scoring deltas against the existing `Archetype` enum
(`StrengthScaling`, `Vulnerable`, `ExhaustEngine`, `SelfDamage`, `Block`,
`StrikeCycling`, `XCostCascade` — names per the archetype doc §6).

### 5.1 CEREMONIAL_BEAST_BOSS

```
enabler boosts:
  - Inflame:           +2 (Strength is mandatory for the HP race)
  - Tremble:           +2 (Vulnerable on a 252-HP single target)
  - Bash:              +1 (fallback Vulnerable enabler)
  - DemonForm:         +1  if (energy_relic_count >= 1) else 0
  - Brand:             +1  (Str + Exhaust pivot)

payoff boosts:
  - HeavyBlade:        +2 (3x Str scaling shines vs single target)
  - Bully:             +2 (Vulnerable payoff at 0 energy under Ringing)
  - Dismantle:         +2 (Vulnerable payoff, single energy)
  - Hemokinesis:       +1 (single big hit OK here; no Slippery)
  - Whirlwind:         +1 (per-energy single-target hits scale w/ Str)

skip overrides (cap at C-tier regardless of base score):
  - Pure-Block without BodySlam-in-deck   : Block engine alone loses
                                            to Phase-2 Str snowball
  - BattleTrance if already have block deficit on T4-T6 window
  - Cascade if deck_size >= 22            : finisher won't surface
                                            before Phase-2 ramp
```

### 5.2 VANTOM_BOSS

```
enabler boosts:
  - Whirlwind:         +3 (the cleanest Slippery counter in the kit)
  - Hellraiser:        +2  if (PommelStrike_count >= 2)
  - TwinStrike:        +2 (canonical multi-hit)
  - PommelStrike:      +2 (multi-hit + draw)
  - Anger:             +2 (multi-hit + self-copy)
  - Inflame:           +1 (only paired with a multi-hit payoff)

payoff boosts:
  - Whirlwind:         +2 (also a payoff with energy support)
  - FeelNoPain:        +1 (mitigates the Dismember turn; Wounds dilute)
  - DarkEmbrace:       +1 (helps cycle through Wound dilution)
  - Defend / Iron Wave:+1 (T3 spike absorption)

skip overrides (cap at D-tier regardless of base score):
  - Hemokinesis WITHOUT Whirlwind/TwinStrike in deck
                                          : burns 15 dmg on 1 stack
  - HeavyBlade WITHOUT multi-hit in deck  : same trap, lower magnitude
  - Inferno                               : single-target boss; HP loss
                                            wasted
  - Rupture WITHOUT Inflame               : Str matters only post-T2
  - DemonForm if (current_energy_relic_count == 0)
                                          : dead card during Slippery
  - Cascade into a Strike-heavy draw pile : burns finishers on 1-HP
```

### 5.3 THE_KIN_BOSS

```
enabler boosts:
  - Whirlwind:         +3 (clean AoE + scales per energy)
  - Inferno:           +3 (AoE per HP-loss; trivializes Followers)
  - Corruption:        +2  if (PactsEnd in deck or Skill_count >= 6)
  - Tremble:           +2 (Vulnerable on Priest for the rush plan)
  - Inflame:           +2 (rush plan needs it; AoE plan loves it)
  - Brand:             +1 (Inferno feeder)
  - CrimsonMantle:     +1 (passive HP-loss → Inferno trigger)

payoff boosts:
  - PactsEnd:          +2 (17 AoE clears Followers immediately)
  - Whirlwind:         +2 (also payoff with energy support)
  - HeavyBlade:        +2 (Priest rush finisher)
  - Bully:             +1 (Vulnerable payoff vs Priest)
  - Dismantle:         +1 (Vulnerable payoff vs Priest)
  - FiendFire:         +1 (off-color AoE; needs Exhaust pile loaded)

skip overrides (cap at C/D-tier regardless of base score):
  - Hellraiser                            : auto-targets RANDOM enemy;
                                            damage spreads across 3
                                            targets when you need to
                                            focus one. **Hard trap.**
  - Spite / BloodForBlood / Hemokinesis without an AoE answer in deck
                                          : Followers scale forever
  - BodySlam without Whirlwind/Inferno in deck
                                          : single-target only
  - BattleTrance played on Priest-Frailty turn (sequencing rule,
                                            not a draft penalty —
                                            policy uses at play-time)
```

### 5.4 Universal Ironclad floor (apply when boss roll is unknown)

```
enabler floor:
  - Inflame in deck    : if missing AND offered, +3
  - Tremble in deck    : if missing AND offered, +3
  - one of {Whirlwind, TwinStrike, HeavyBlade}
                       : if all missing AND any offered, +3
  - block engine >= 12 effective per turn (Defend + Iron Wave + Taunt
                                            counts here): if missing,
                                            boost Defend/IronWave by +1

global overrides:
  - DemonForm gate (archetype doc §6.8 rule 5) still applies
  - Offering is S-tier global (rule 3)
  - Brand is union(Self-Damage, Exhaust, Strength) (rule 2)
```

---

## Sources

- wiki.gg, Ceremonial Beast — https://slaythespire.wiki.gg/wiki/Slay_the_Spire_2:Ceremonial_Beast
- wiki.gg, Vantom — https://slaythespire.wiki.gg/wiki/Slay_the_Spire_2:Vantom
- wiki.gg, The Kin — https://slaythespire.wiki.gg/wiki/Slay_the_Spire_2:The_Kin
- wiki.gg, Bosses index — https://slaythespire.wiki.gg/wiki/Slay_the_Spire_2:Bosses
- sts2front, All Bosses Act by Act — https://sts2front.com/tips/all-bosses-act-by-act/
- thephrasemaker, Ceremonial Beast guide — https://thephrasemaker.com/2026/03/09/slay-the-spire-2-ceremonial-beast-boss-guide/
- thephrasemaker, Vantom guide — https://thephrasemaker.com/2026/03/08/slay-the-spire-2-vantom-boss-guide/
- games.gg, Vantom — https://games.gg/slay-the-spire-2/guides/slay-the-spire-2-how-to-beat-vantom/
- sts2companion, Vantom — https://www.sts2companion.com/bosses/vantom
- gamerblurb, Kin boss guide — https://gamerblurb.com/articles/slay-the-spire-2-kin-boss-guide
- selphie1999gaming, The Kin guide — https://selphie1999gaming.com/game-guides/slay-the-spire-2/slay-the-spire-2-the-kin-boss-guide-how-to-beat-the-kin/
- keengamer, STS2 Bosses — https://www.keengamer.com/articles/guides/slay-the-spire-2-bosses-and-how-to-beat-them/
- mobalytics, STS2 Bosses — https://mobalytics.gg/slay-the-spire-2/encounters/bosses
- deltiasgaming, Ceremonial Beast — https://deltiasgaming.com/how-to-beat-ceremonial-beast-in-slay-the-spire-2-act-1/
