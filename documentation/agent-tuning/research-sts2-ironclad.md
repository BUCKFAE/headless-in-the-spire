# STS2 Ironclad — Research Notes for an A0-Clearing Agent

Status: research deliverable, May 2026. STS2 is in Early Access; this is a
snapshot of community consensus from the last ~12 months (Mobalytics,
nat1gaming, sts2front, switchbladegaming, keengamer, the wiki, sts2.gg, plus
Steam-discussion threads). Cite-checked URLs at the end. Where sources
disagree, the disagreement is noted inline.

This document is meant to be **directly actionable** by another agent — the
"what" and "why" for every important decision a run touches. It does not
prescribe a single deck; it gives ranked archetypes, tier-listed cards,
trigger conditions for skips/rests/smiths, and the STS1-trained intuitions
that will get the agent killed.

---

## 0. STS2-specific facts that override STS1 intuition

Read these first. Most "obvious" STS1 plays are wrong in STS2.

- **Only 3 acts (A1–A10 ascensions implemented).** The "final boss" is The
  Architect, a *scripted, unwinnable* cutscene-kill — there is no Heart, no
  Act 4. Plan for a 3-act arc, not a 4-act one. The "win" the agent should
  optimize for is reaching The Architect alive, which functionally means
  beating the Act 3 boss.
- **Neow always offers relics**, drawn from two pools: a Positive pool and a
  Curse pool. A Curse relic is rolled first, then the Positive pool is
  adjusted, then the player picks among the curse + two positives.
  Implication: Ironclad — with its strong self-contained starter deck and
  Burning Blood healing — handles Curse-pool relics better than fragile
  characters; **Neow's Bones** ("2 random Neow relics + 1 random Curse") is
  rated *best on Ironclad and Silent* by community guides.
- **Five characters** (Ironclad, Silent, Defect, Regent, Necrobinder); only
  Ironclad is relevant here, but if cross-character cards/relics surface in
  events, expect the new ones.
- **Ironclad starting HP is 80 (highest of the roster).** Burning Blood
  heals 6 HP at end of combat (unchanged from STS1). With a 3-act run,
  Burning Blood is *more valuable in absolute terms* than in STS1 because
  there are fewer combats to be healed by it, but each one matters more.
- **Quest Cards** are new and change skip math: they sit in your deck and
  deliver a delayed payoff. The "what does this fix right *now*?" question
  becomes the load-bearing test for accepting them.
- **The Architect is not a combat encounter.** Do **not** save resources for
  it. Cash everything in at the Act 3 boss.
- **HP-economy interpretation differs.** With only 3 acts and Burning
  Blood, HP is genuinely a spendable resource — much more so than in
  STS1's 4-act grind. Self-damage archetypes are correspondingly stronger.

---

## 1. Archetypes — ranked by A0 consistency

Community sources mostly agree on three to five distinct Ironclad
archetypes; this is the consolidated picture, ordered for an agent that
wants to *clear A0* (not max EV, not climb A20):

### 1.1 Strength Scaling — **Recommended default for A0**

The most consistent and most beginner-friendly archetype. Every source
(switchbladegaming, mobalytics, pcgamesn, thegamer) names this as the
"learn first" build.

- **Core loop:** stack Strength → multi-hit attacks scale exponentially →
  kill before the enemy outpaces Burning Blood.
- **Enablers (in commonness order):** Inflame, Tremble (apply 3 Vulnerable
  exhaust — new in STS2), Flex, Limit Break.
- **Payoffs:** Heavy Blade, Twin Strike, Pommel Strike, Bully (new),
  Dismantle (new), Dominate (premium rare).
- **Why it's most consistent:** Inflame is common. Tremble is common.
  Two-Strike-style multi-hits are common. The archetype works from a single
  Strength source and at least one multi-hit attack.
- **Failure mode:** Heart Strike-style "1×big" attack decks scale poorly
  with Strength. Don't grab Bash variants when you should be grabbing
  multi-hit.

### 1.2 Vulnerable Synergy — **Strong secondary; merges with 1.1**

STS2 leans harder on Vulnerable than STS1 did, mostly because of the new
*Vulnerable-payoff* attacks.

- **Core loop:** apply Vulnerable cheaply → cards that scale on Vulnerable
  do extra work → enemies die in 1–2 turns.
- **Enablers:** Tremble (3 Vulnerable for 1, exhausts), Bash, Taunt
  (7 Block + 1 Vulnerable — new, 1-cost uncommon skill).
- **Payoffs:** Bully (4 dmg + 2 per Vulnerable stack — new, 0-cost
  uncommon attack), Dismantle (8 dmg, double-hits a Vulnerable target —
  new), Cruelty (damage multiplier on Vulnerable targets).
- **Synergy relics:** Paper Phrog (Vulnerable damage multiplier), Red Mask
  (turn-1 Vulnerable).
- **Why second-most consistent:** still depends on getting at least one
  Vulnerable payoff card; without one, you're just a worse Strength deck.
- **Merge note:** in practice 1.1 and 1.2 overlap heavily — Tremble +
  Heavy Blade is both. An agent should treat them as *one cluster* and
  only commit to "pure" Vulnerable when payoffs (Bully, Dismantle,
  Cruelty) have shown up.

### 1.3 Exhaust Engine — **Strongest ceiling, fragile floor**

Best at A20 per nat1gaming; risky at A0 because it needs multiple specific
pieces.

- **Core loop:** Corruption makes Skills cost 0 + Exhaust → Feel No Pain
  generates Block per exhaust → Dark Embrace draws a card per exhaust →
  Fiend Fire / Pact's End cash in the exhaust pile.
- **STS2 payoffs:** Ashen Strike (1-cost: 6 dmg + 3 per card already in
  Exhaust pile — new), Pact's End (0-cost: 17 AoE, Exhaust — new),
  Brand (lose 1 HP, exhaust 1 card, gain 1 Strength — new), Crimson
  Mantle (1-cost rare power: turn-start lose 1 HP, gain 8 Block — new).
- **Skip at A0** unless you see Corruption *or* Dark Embrace early **and**
  the rest of the deck is thin enough to draw them. Without one of these
  power cards the archetype does not function.

### 1.4 Block / Body Slam / Barricade — **Hard to assemble; skip at A0**

- **Core loop:** stack Block (Barricade prevents end-of-turn decay) →
  Body Slam converts Block → damage.
- **Why skip at A0:** requires Barricade (rare) *plus* Body Slam *plus*
  enough Block engines. A0 won't reliably hand you all three. If
  Barricade shows up early as a reward, pivot. Otherwise don't.

### 1.5 Self-Damage / Bloodletting — **High ceiling, situational**

STS2's HP-as-resource emphasis makes this stronger than its STS1
counterpart, but A0 doesn't need the ceiling.

- **Enablers:** Blood Wall (lose 2 HP, gain 16 Block — new), Brand,
  Crimson Mantle, Offering (extra cards + extra energy at HP cost — rated
  the *best card in the Ironclad kit* by nat1gaming), Bloodletting.
- **Take if offered, don't draft toward it.** Offering and Blood Wall slot
  into Strength/Vulnerable decks as ordinary good cards; Crimson Mantle
  needs you to *already have* Block synergy.

### A0 recommendation

**Strength + Vulnerable cluster.** Pick up Inflame, Tremble, Heavy Blade,
multi-hit attacks; opportunistically grab Dominate / Limit Break / Demon
Form rares; let Bully / Dismantle / Cruelty pull you toward Vulnerable
payoffs when offered. Treat Exhaust and Block as *pivot opportunities*, not
plans.

---

## 2. Card tier list (Ironclad-specific)

Tiers reflect "A0 EV when offered in a fresh slot", consolidating
nat1gaming, mobalytics, untapped.gg, sts2front, and switchblade. Where
guides disagreed I've taken the lower tier (the agent should be
conservative). Notes mark STS2-new cards with *(new)*.

### S — Almost always take

- **Offering** *(new, rare)* — +2 energy, +3 cards, lose 6 HP, Exhaust.
  Rated the single strongest Ironclad card; tempo and consistency.
- **Dominate** *(rare)* — premium Strength scaling + early Vulnerable.
- **Cruelty** *(rare)* — damage multiplier on Vulnerable enemies; near-auto
  in any Vulnerable-leaning deck.
- **Limit Break** — doubles Strength; obvious snap in any Strength deck.
- **Demon Form** *(rare)* — turn-start Strength; held back only by the 3
  energy cost (see §6).
- **Corruption** *(rare)* — only if you intend to pivot Exhaust.

### A — Strong; take unless deck is already pointed elsewhere

- **Inflame** — common, scales Strength; the workhorse uncommon.
- **Heavy Blade** — Strength × 3 (× 5 upgraded) base scaler.
- **Bully** *(new, 0-cost uncommon)* — 4 dmg + 2/Vulnerable; great
  with Tremble.
- **Dismantle** *(new, uncommon)* — 8 dmg, double-hits Vulnerable; a
  20-damage 1-cost when conditions land.
- **Dark Embrace** — only if you already have ≥3 reliable exhaust sources.
- **Feel No Pain** — same condition as Dark Embrace.
- **Barricade** *(rare)* — only as Block pivot trigger.
- **Body Slam** — only if you have any Block stacking at all; safe-floor
  pick because the starting deck has 4 Defends.
- **Fiend Fire** *(rare)* — Exhaust payoff; needs hand size.

### B — Take when reward is otherwise weak

- **Tremble** *(new, common skill)* — 3 Vulnerable, 1-cost, Exhausts.
  Strong rate; B not A because it self-exhausts and so doesn't help
  Strength decks long-term. Promotes to A in Vulnerable decks.
- **Taunt** *(new, 1-cost uncommon)* — 7 Block + 1 Vulnerable; good
  rate but not transformative.
- **Iron Wave** — common 1-cost dmg+block; floor card.
- **Pommel Strike** — common; card draw on damage.
- **True Grit** — common Block + Exhaust; archetype-dependent.
- **Thunderclap** — AoE Vulnerable; great in Act 2 / multi-enemy fights.
- **Perfected Strike** *(new naming, similar to STS1)* — only with 4+
  "Strike" cards. Don't draft *for* it; take if it shows up alongside
  many Strikes already present.
- **Brand** *(new)* — Exhaust-deck enabler; B because it costs HP.
- **Blood Wall** *(new)* — 16 Block for 2 HP at 1 cost is great rate; B
  because Block-conversion cards are deck-shape dependent.
- **Ashen Strike** *(new)* — scales with Exhaust pile; only as Exhaust
  payoff.
- **Cascade** *(new)* — plays top N draw-pile cards; **risky / high
  variance.** Promotes when you have extra energy sources, dead weight
  in a tight deck.
- **Crimson Mantle** *(new, 1-cost rare power)* — passive 8 Block / turn
  for 1 HP; Block-pivot card.
- **Pact's End** *(new, 0-cost rare)* — 17 AoE finisher; Exhaust-pile
  dependent for ramp.

### C — Skip unless deck is desperate

- **Expect a Fight** *(new)* — +1 energy per attack in hand; works only in
  attack-heavy decks already; otherwise dead.
- **Rampage** *(new)* — 9 dmg, +5 to this card per play; STS1-style ramp,
  weakened by 3-act length (less time to scale).
- **Impervious** — high-rate Block but only one fight worth of value.

### D / F — Avoid

- **Most "1×big" attacks** that don't scale with Strength.
- **Searing Blow / similar single-hit upgrades** unless you have lots of
  upgrade access (Smith access in STS2 is the same as STS1 — limited).

---

## 3. Skip-reward heuristics

From sts2front "When to Skip Cards" + community consensus.

**Rule of thumb:** *"Does this card fix a real problem my deck has, or am
I taking it because I like the idea of what my deck could become later?"*
If the answer is the latter, **skip**.

**Hard skip triggers (act-aware):**

- **Deck has ≥16 cards and no remaining win-condition gap.** Above 16,
  removal beats addition almost always.
- **No card in the offer beats your current weakest playable card.**
  ("Beats" = ≥1 tier higher in the §2 list, *or* directly enables a
  payoff you already have.)
- **You're already committed to an archetype and the offer is off-archetype
  filler.** Off-archetype B-tier > on-archetype dilution? Still skip.
- **The reward includes a Quest Card and you have no near-term plan to
  satisfy its condition.**

**Take-anyway overrides:**

- Any S-tier card in §2.
- Any card that closes a known weakness (no AoE → take AoE; no Block →
  take any Block engine).
- Strength source if you have ≥0 in deck.

**Target deck sizes (consolidated from sts2front + casualgameguides):**

| End of act | Target size | Notes |
|---|---|---|
| Act 1 | 12–16 | thinner = better; removed Strikes/Defends > additions |
| Act 2 | 16–22 | window for archetype payoffs |
| Act 3 | 18–25 | hard cap; over 25, every fight risks brick draws |

Necrobinder / Silent want the lower bound; Ironclad tolerates the upper.

---

## 4. Boss-relic recommendations

Per switchbladegaming + thegamer + community tier lists. Boss relics in
STS2 are *category-defining* — even a mediocre pick beats no pick.

| Archetype | Best | Second | Avoid |
|---|---|---|---|
| Strength scaling | **Cursed Key** (energy, accept curse) | Black Star (forces 2 elites/act → more relics) | Runic Pyramid (no synergy) |
| Vulnerable | **Paper Phrog** (Vulnerable dmg ×) | Red Mask (free turn-1 Vulnerable) | — |
| Exhaust | **Runic Pyramid** (retain hand, devastating with Corruption) | Charon's Ashes | Calipers |
| Block / Barricade | **Calipers** | Sozu (save gold for shops) | — |
| Self-damage | Mark of Pain / energy boosters | Burning Blood replacements | Ectoplasm if shops still ahead |
| Generic / no archetype yet | Cursed Key | Sozu | Ectoplasm if path still has Merchants |

**Ectoplasm warning:** locks out gold gain. Only safe if Merchant is
behind you. Devastating if accepted in Act 1.

**Cursed Key:** adds a Curse on chest-open, gives +1 energy. Ironclad
absorbs Curses better than other classes (Burning Blood mitigates the HP
floor); near-default pick when offered.

---

## 5. STS1 → STS2 differences that trip up STS1 intuition

| STS1 belief | STS2 reality |
|---|---|
| "Neow gives boons, not relics" | Neow *always* gives relics; Curse-pool is part of the offer |
| "There are 4 acts; save for Heart" | Only 3 acts; Architect kills you anyway; cash in at Act 3 boss |
| "Limit Break is fine without upgrade" | Same — but with 3 acts, Smith access matters more |
| "Searing Blow scales" | Doesn't scale enough for 3 acts |
| "Multi-hits are obviously better" | Still true, *and* STS2 Vulnerable-payoff cards (Bully, Dismantle) reward going harder into Vulnerable than STS1 did |
| "Body Slam is auto-include" | No — needs Barricade-tier Block stacking, which is harder to assemble in 3 acts |
| "Demon Form is always a snap" | 3-cost is much costlier in STS2's tempo; needs ≥4 energy sources to be on-curve |
| "Skip is a beginner trap" | Inverted: skipping is *more* correct in STS2 because every dead card costs more in a shorter run |
| "Burning Blood is +6, no big deal" | 3 acts → fewer combats, so each +6 matters more |
| "Elites give relics; always fight 3" | Act 1: 2–3 elites at ≥60% HP. Act 2: 1–2 at ≥50%. Act 3: 0–1 unless deck is finished. |

**Surprises specific to STS2 Ironclad:**

- **Tremble exhausts itself.** This is *unlike* most STS1 Vulnerable
  sources. Tremble is a one-shot per shuffle, not a recurring debuff.
- **Bully and Dismantle scale with active Vulnerable**, not just
  apply-on-hit. This creates a real "set up Vulnerable, then dump payoffs"
  rhythm that didn't exist in STS1 Ironclad.
- **Crimson Mantle is a power that costs HP every turn.** Don't accept
  it in a deck without Block synergy; it'll bleed you out.
- **Pact's End needs the Exhaust pile pre-loaded.** It's not a stand-alone
  finisher.

---

## 6. Energy economy

### When to take +max-energy effects

**Max energy is the strongest single stat increase** in the game (per
switchbladegaming + community); take *almost any* energy relic.

- **Always-take:** Ectoplasm *if Merchant is behind you*; Mark of Pain;
  Velvet Choker (loses value with Corruption — skip if Exhaust); Cursed
  Key.
- **Conditional:** Runic Dome (loses intent info — risky in scripted boss
  fights; safer if you have a strong block plan).

### When cycling beats more energy

- When deck size is **≥22 cards** and key payoffs only show 1–2 times per
  combat. Drawing the payoff is worth more than 1 more energy that fizzles
  on Strikes.
- When the archetype is **Exhaust + Dark Embrace** — you already cycle
  fast; another energy is fine but cycling is better-rate.

### When flat efficiency beats both

- When deck has ≥3 Strength sources stacked: Heavy Blade for 3 energy
  hits harder per energy point than spending the same energy on +1 max.
  Don't trade away upgrade slots for energy if Strength is already deep.

### Demon Form gate

3-cost. Per switchbladegaming, **only viable with 4+ energy sources**. At
base 3 energy it consumes the entire turn. **Rule:** don't take Demon
Form before you have a confirmed energy upgrade (relic or potion or
ritual).

---

## 7. Path / map strategy

### Elite frequency targets

| Act | Elite count | HP gate |
|---|---|---|
| Act 1 | 2–3 | ≥60% HP, must have ≥1 reliable damage card |
| Act 2 | 1–2 | ≥50% HP, archetype taking shape |
| Act 3 | 0–1 | ≥40% HP, deck finished |

Ironclad's Burning Blood + 80 HP makes 3-elite Act 1 the *standard* line
for Strength decks. Skip the third elite if you don't have a Vulnerable
source by the time you'd reach it.

### Act 1 elites to be wary of (Ironclad-specific)

- **Byrdonis** — high threat; skip without burst.
- **Phrog Parasite** — skip if deck is already ≥25 cards.
- **Bygone Effigy** — fine for Strength; bad for low-hand decks.

### Rest vs Smith heuristic

Per sts2.gg map-routing guide:

- **Rest** if HP < 40% heading into an Elite or Boss without potions.
- **Rest** if next two combats include an Elite with no rest after.
- **Smith** otherwise, prioritizing:
  1. Powers (Inflame, Demon Form, Corruption, Crimson Mantle).
  2. Workhorse attacks (Heavy Blade, Twin Strike, Bully, Dismantle).
  3. Tremble / key Vulnerable sources.
  4. Defends (only once core archetype is upgraded).

**Always check for a post-elite rest** when pathing. A rest *after* the
elite makes the elite line strictly safer.

### Shop value

- Buy **removal** at any price ≤75g if deck has ≥2 Strikes/Defends still in.
- Buy a **key archetype card** if offered (Inflame, Bully, Dismantle,
  Corruption, etc.) at "merchant rate" — assume ~50–75g for commons,
  ~75–150g for uncommons, ~150–300g for rares (untapped.gg / mobalytics
  approximation).
- Buy **potions** liberally before bosses; sell them via Sozu only if
  taken pre-Act-2.
- Phial Holster (Neow relic, new) drastically shifts potion economy; if
  taken at Neow, lean into pot-buying.

### Pathing priorities (Ironclad-specific)

Per nat1gaming Ultimate Ironclad Guide: *"Ideally a path with 3+ campfires,
a lot of fights, and a late elite or two are exactly what you should be
hoping for in Act 1."*

Concrete priority order when choosing a node:

1. **Card-reward fight** if deck not yet committed.
2. **Elite** if HP/deck thresholds met (see table).
3. **Shop** before Act 1 boss if gold ≥150.
4. **Rest** under HP threshold.
5. **Event** — STS2 events are higher-variance; treat unknown as the
   *lowest* priority unless documented to be safe.
6. **Treasure** — always pick up if on path; never path *toward* if it
   costs an elite.

---

## 8. Quick reference for the agent

**Default A0 plan:**

1. Neow: take Phial Holster, Winged Boots, or any Positive relic that
   matches Strength/Vulnerable. Neow's Bones is fine; skip Ectoplasm and
   gold-locking relics.
2. Act 1: 2–3 elites, target Inflame + Tremble + 1 multi-hit attack by
   Act 1 boss. End act at 14–16 cards.
3. Boss reward 1: Cursed Key > Paper Phrog > Black Star for Strength;
   Runic Pyramid if Corruption already in deck.
4. Act 2: 1–2 elites, pick up Bully / Dismantle / Heavy Blade / Limit
   Break; smith powers first. End act at 18–22 cards.
5. Boss reward 2: archetype-specific (see §4).
6. Act 3: 0–1 elites, finish deck. Cash everything into Act 3 boss; do
   not save resources for the Architect cutscene.

**Skip whenever** §3 hard-skip triggers fire. **Smith whenever** §7 Rest
threshold isn't tripped. **Take +max energy** whenever it doesn't lock
out a still-future Merchant.

---

## Sources

Last refreshed 2026-05; STS2 patch v0.103.0 baseline.

- nat1gaming, "Ironclad Card Tier List – Slay the Spire 2 – 5/8 Update"
  — https://nat1gaming.com/sts2/tier-list/ironclad-card-tier-list/
- nat1gaming, "The Ultimate Guide to Ironclad in STS2"
  — https://nat1gaming.com/sts2/character-guide/the-ultimate-guide-to-ironclad-in-slay-the-spire-2/
- Mobalytics, "Slay the Spire 2 Ironclad Guide"
  — https://mobalytics.gg/slay-the-spire-2/characters/ironclad-guide
- Switchblade Gaming, "Best Ironclad Builds in Slay the Spire 2 (2026)"
  — https://www.switchbladegaming.com/strategy-games/slay-the-spire-2-ironclad-build/
- Switchblade Gaming, "Slay the Spire 2 Tips and Tricks: 20 Things the Game Never Tells You"
  — https://www.switchbladegaming.com/strategy-games/slay-the-spire-2-tips/
- Keengamer, "The Complete Guide to the Ironclad in Slay the Spire 2"
  — https://www.keengamer.com/articles/guides/the-complete-guide-to-the-ironclad-in-slay-the-spire-2/
- TheGamer, "Best Build And Relics For The Ironclad In Slay The Spire 2"
  — https://www.thegamer.com/slay-the-spire-2-best-builds-relics-cards-the-ironclad-strength-vulnerable-guide/
- sts2front, "When to Skip Cards"
  — https://sts2front.com/tips/when-to-skip-cards/
- sts2front, "Deck Thinning & Card Removal Strategy"
  — https://sts2front.com/tips/deck-thinning-strategy/
- sts2.gg, "Neow Relic Guide"
  — https://sts2.gg/guides/neow-relic-guide
- sts2.gg, "Map Routing in Slay the Spire 2"
  — https://sts2.gg/guides/map-routing-guide
- Slay the Spire Wiki (wiki.gg), "Slay the Spire 2: Ironclad"
  — https://slaythespire.wiki.gg/wiki/Slay_the_Spire_2:Ironclad
- Slay the Spire Wiki, "Tremble", "Taunt", "Crimson Mantle", "Elites"
  — https://slaythespire.wiki.gg/wiki/Slay_the_Spire_2:Tremble
  — https://slaythespire.wiki.gg/wiki/Slay_the_Spire_2:Taunt
  — https://slaythespire.wiki.gg/wiki/Slay_the_Spire_2:Crimson_Mantle
  — https://slaythespire.wiki.gg/wiki/Slay_the_Spire_2:Elites
- untapped.gg, "Bully card" and Ironclad card list
  — https://sts2.untapped.gg/en/cards/bully
  — https://sts2.untapped.gg/en/tier-list/cards/ironclad
- bossdown.com, "Slay the Spire 2 Elite Guide"
  — https://bossdown.com/guides/slay-the-spire-2-all-elites/
- pcgamesn, "Slay the Spire 2 Ironclad guide"
  — https://www.pcgamesn.com/slay-the-spire-2/ironclad
- egamersworld, "Best Ironclad Builds Tier list"
  — https://egamersworld.com/blog/ironclad-builds-tierlist-strongest-strong-average--TirZr9Tl8F

**Caveat:** Several sources disagreed on Corruption's tier (S vs A) and
on whether Demon Form belongs at S; this document took the more
conservative read. None of the sources independently surveyed the same
A0-specific play data, so the "A0-consistency" ordering in §1 is a
synthesis, not a measured win-rate ranking.
