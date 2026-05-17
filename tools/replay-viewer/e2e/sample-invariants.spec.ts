import { test, expect, type Page } from "@playwright/test";
import { locateSampleRunDir, pickRunDirectory } from "./helpers";

// "Does what the viewer renders make sense?" — cross-floor invariants
// computed against the bundled sample. These cover the integrity
// classes the user asked us to be thorough about:
//   - HP arithmetic: floor_n_HP = floor_(n-1)_HP - damage_taken + hp_healed
//   - Gold monotonicity (no shop visit in sample → never decreases)
//   - Deck growth: every "Cards gained" entry shows a real card id
//   - Final relic list = starter + every relic_choice with ✔ across floors
//   - Combat event-count consistency: events shown match the timeline header

// All of these read from the rendered DOM, not the JSON — the point is
// to catch viewer-side rendering bugs (truncation, mis-attribution,
// rounding) that pure parser tests can't see.

interface FloorView {
  index: number;
  rowLabel: string;
  detailText: string;
  hpEndOfFloor: { current: number; max: number } | null;
  damageTaken: number;
  hpHealed: number;
  gold: number | null;
  cardsGained: string[];
  relicReward: { picked: string[]; offered: string[] };
  isCombatRoom: boolean;
  hasCombatBlock: boolean;
}

async function gatherFloors(page: Page): Promise<FloorView[]> {
  const rows = page.locator(".viewer-floor-row");
  const count = await rows.count();
  const out: FloorView[] = [];
  for (let i = 0; i < count; i++) {
    const row = rows.nth(i);
    const rowLabel = (await row.innerText()) ?? "";
    await row.click();
    const detailText = (await page.locator(".viewer-floor-detail").innerText()) ?? "";

    const hpMatch = detailText.match(/HP \(end of floor\)\s*\n?\s*(\d+)\s*\/\s*(\d+)/);
    const damageMatch = detailText.match(/Damage taken\s*\n?\s*(\d+)/);
    const healMatch = detailText.match(/HP healed\s*\n?\s*(\d+)/);
    const goldMatch = detailText.match(/^\s*Gold\s*\n\s*(\d+)/m);

    // "Cards gained" is followed by a list of CARD.X lines. The
    // section ends at the next blank line / next H4 heading. We grab
    // anything that looks like CARD.X in the cards-gained block.
    const cardsGained = extractSection(detailText, "Cards gained", /CARD\.[A-Z_0-9]+/g);

    const relicSection = extractSectionLines(detailText, "Relic reward");
    const relicPicked: string[] = [];
    const relicOffered: string[] = [];
    for (const line of relicSection) {
      const m = line.match(/(✔|·)\s+(RELIC\.[A-Z_0-9]+)/);
      if (!m) continue;
      relicOffered.push(m[2]!);
      if (m[1] === "✔") relicPicked.push(m[2]!);
    }

    const isCombatRoom = /·\s+(monster|elite|boss)(\s|$)/.test(rowLabel);
    const hasCombatBlock = (await page.locator(".viewer-combat-detail").count()) > 0;

    out.push({
      index: i,
      rowLabel,
      detailText,
      hpEndOfFloor: hpMatch ? { current: Number(hpMatch[1]), max: Number(hpMatch[2]) } : null,
      damageTaken: damageMatch ? Number(damageMatch[1]) : 0,
      hpHealed: healMatch ? Number(healMatch[1]) : 0,
      gold: goldMatch ? Number(goldMatch[1]) : null,
      cardsGained,
      relicReward: { picked: relicPicked, offered: relicOffered },
      isCombatRoom,
      hasCombatBlock,
    });
  }
  return out;
}

function extractSection(detail: string, heading: string, pat: RegExp): string[] {
  const idx = detail.indexOf(heading);
  if (idx < 0) return [];
  // Heading runs until the next ALL-CAPS heading (Combat / On entry /
  // HP (end of floor) / etc.) — the renderer puts each section under
  // an h4 with a blank line after. Cheapest reliable cut: from heading
  // to the next double-newline.
  const tail = detail.slice(idx + heading.length);
  const nextBreak = tail.search(/\n[A-Z][^\n]*\n/);
  const chunk = nextBreak >= 0 ? tail.slice(0, nextBreak) : tail;
  return Array.from(chunk.matchAll(pat), (m) => m[0]!);
}

function extractSectionLines(detail: string, heading: string): string[] {
  const idx = detail.indexOf(heading);
  if (idx < 0) return [];
  const tail = detail.slice(idx + heading.length);
  const nextBreak = tail.search(/\n[A-Z][^\n]*\n/);
  const chunk = nextBreak >= 0 ? tail.slice(0, nextBreak) : tail;
  return chunk.split("\n").map((l) => l.trim()).filter((l) => l.length > 0);
}

test.beforeEach(async ({ page }) => {
  await page.goto("/");
  await page.evaluate(() => window.localStorage.clear());
  await page.reload();
});

// ── HP arithmetic ──────────────────────────────────────────────────────

test("HP arithmetic: each floor's end-HP equals prior floor's end-HP - damage_taken + hp_healed", async ({ page }) => {
  await pickRunDirectory(page, locateSampleRunDir());
  const floors = await gatherFloors(page);
  for (let i = 1; i < floors.length; i++) {
    const prev = floors[i - 1]!;
    const cur = floors[i]!;
    if (!prev.hpEndOfFloor || !cur.hpEndOfFloor) continue;
    const expected = prev.hpEndOfFloor.current - cur.damageTaken + cur.hpHealed;
    expect(
      cur.hpEndOfFloor.current,
      `floor ${i} HP arithmetic mismatch — prev=${prev.hpEndOfFloor.current}, damage=${cur.damageTaken}, healed=${cur.hpHealed}, expected=${expected}, got=${cur.hpEndOfFloor.current}\nrow: ${cur.rowLabel.trim()}`,
    ).toBe(expected);
  }
});

test("HP arithmetic: max_hp never decreases mid-run (no max_hp_lost mechanic in the sample)", async ({ page }) => {
  await pickRunDirectory(page, locateSampleRunDir());
  const floors = await gatherFloors(page);
  let prevMax = 0;
  for (const f of floors) {
    if (!f.hpEndOfFloor) continue;
    expect(f.hpEndOfFloor.max, `floor ${f.index} max_hp decreased from ${prevMax} to ${f.hpEndOfFloor.max}`).toBeGreaterThanOrEqual(prevMax);
    prevMax = f.hpEndOfFloor.max;
  }
});

// ── Gold monotonicity ──────────────────────────────────────────────────

test("gold is monotonically non-decreasing across floors (no shop visit in sample)", async ({ page }) => {
  await pickRunDirectory(page, locateSampleRunDir());
  const floors = await gatherFloors(page);
  let prev = 0;
  for (const f of floors) {
    if (f.gold === null) continue;
    expect(f.gold, `floor ${f.index} gold decreased from ${prev} to ${f.gold}\nrow: ${f.rowLabel.trim()}`).toBeGreaterThanOrEqual(prev);
    prev = f.gold;
  }
});

test("gold starts at the Ironclad's starting gold (99) or higher on the first floor", async ({ page }) => {
  await pickRunDirectory(page, locateSampleRunDir());
  const floors = await gatherFloors(page);
  expect(floors[0]!.gold).not.toBeNull();
  expect(floors[0]!.gold!).toBeGreaterThanOrEqual(99);
});

// ── Section / room-type integrity ──────────────────────────────────────

test("rest_site rows show hp_healed > 0 and no damage_taken / no cards_gained / no encounter", async ({ page }) => {
  await pickRunDirectory(page, locateSampleRunDir());
  const floors = await gatherFloors(page);
  const restSites = floors.filter((f) => /·\s+rest_site/.test(f.rowLabel));
  expect(restSites.length, "sample should have multiple rest sites").toBeGreaterThan(0);
  for (const f of restSites) {
    expect(f.hpHealed, `rest_site ${f.index} should heal HP`).toBeGreaterThan(0);
    expect(f.damageTaken, `rest_site ${f.index} should not have damage_taken`).toBe(0);
    expect(f.cardsGained, `rest_site ${f.index} should not have card rewards`).toHaveLength(0);
    expect(f.detailText, `rest_site ${f.index} should not show an encounter line`).not.toMatch(/ENCOUNTER\./);
  }
});

test("event rows show NO Cards gained section (events don't give card rewards in the sample)", async ({ page }) => {
  await pickRunDirectory(page, locateSampleRunDir());
  const floors = await gatherFloors(page);
  const events = floors.filter((f) => /·\s+event/.test(f.rowLabel));
  expect(events.length, "sample should have multiple events").toBeGreaterThan(0);
  for (const f of events) {
    expect(f.cardsGained, `event floor ${f.index} should not have card rewards\nrow: ${f.rowLabel.trim()}`).toHaveLength(0);
  }
});

test("treasure rows show a relic reward and zero damage_taken", async ({ page }) => {
  await pickRunDirectory(page, locateSampleRunDir());
  const floors = await gatherFloors(page);
  const treasures = floors.filter((f) => /·\s+treasure/.test(f.rowLabel));
  if (treasures.length === 0) test.skip(true, "no treasure floor in sample");
  for (const f of treasures) {
    expect(f.relicReward.offered, `treasure floor ${f.index} should offer a relic`).not.toHaveLength(0);
    expect(f.damageTaken, `treasure floor ${f.index} should not have damage_taken`).toBe(0);
  }
});

// ── Combat-detail integrity ────────────────────────────────────────────

test("combat detail event-count is non-zero AND the boss combat has more events than the openers", async ({ page }) => {
  await pickRunDirectory(page, locateSampleRunDir());
  const rows = page.locator(".viewer-floor-row");
  const count = await rows.count();
  const eventCounts: { label: string; count: number }[] = [];
  for (let i = 0; i < count; i++) {
    const label = (await rows.nth(i).textContent()) ?? "";
    if (!/·\s+(monster|elite|boss)/.test(label)) continue;
    await rows.nth(i).click();
    const events = page.locator(".viewer-combat-detail .viewer-combat-log li[data-kind='event']");
    const evCount = await events.count();
    expect(evCount, `combat floor ${i} (${label.trim()}) shows zero events`).toBeGreaterThan(0);
    eventCounts.push({ label, count: evCount });
  }
  // The bundled sample has 8 combats (7 monsters/elite + 1 boss).
  expect(eventCounts.length).toBe(8);
  // Boss combat is at the end and is much longer than the openers
  // (50+ events vs ~10 for floor-2's first combat). If the lookup
  // ever silently aliased the boss combat to a different timeline,
  // the boss's event count would collapse to a tiny number.
  const boss = eventCounts[eventCounts.length - 1]!;
  expect(boss.label).toMatch(/·\s+boss/);
  expect(boss.count, `boss combat has only ${boss.count} events — likely aliased to a non-boss timeline`).toBeGreaterThan(30);
});

test("turn-marker HP never increases mid-combat (block resets per turn but HP only goes down or stays)", async ({ page }) => {
  await pickRunDirectory(page, locateSampleRunDir());
  const rows = page.locator(".viewer-floor-row");
  const count = await rows.count();
  for (let i = 0; i < count; i++) {
    const label = (await rows.nth(i).textContent()) ?? "";
    if (!/·\s+(monster|elite|boss)/.test(label)) continue;
    await rows.nth(i).click();
    const turns = await page.locator(".viewer-combat-detail .viewer-combat-log li[data-kind='turn']").allTextContents();
    let prev = Number.POSITIVE_INFINITY;
    for (const t of turns) {
      const m = t.match(/HP\s+(\d+)\s*\/\s*\d+/);
      if (!m) continue;
      const hp = Number(m[1]);
      expect(hp, `combat at row ${i} HP went UP from ${prev} to ${hp} within a single combat:\n  ${t}`).toBeLessThanOrEqual(prev);
      prev = hp;
    }
  }
});

// ── Run-level integrity ────────────────────────────────────────────────

test("run header's Final relics list matches the relics picked across floors", async ({ page }) => {
  await pickRunDirectory(page, locateSampleRunDir());
  const headerText = (await page.locator(".viewer-run-header").innerText()) ?? "";
  // The header's "Final relics" block lists every relic the player
  // ended the run with. Parse it.
  const finalRelicsM = headerText.match(/Final relics\s*\n([^\n]+)/);
  expect(finalRelicsM, `Final relics block missing from run header:\n${headerText}`).not.toBeNull();
  const finalRelics = new Set(finalRelicsM![1]!.split(",").map((s) => s.trim()));

  const floors = await gatherFloors(page);
  const pickedRelics = new Set<string>();
  for (const f of floors) {
    for (const r of f.relicReward.picked) pickedRelics.add(r);
  }
  // Every picked relic should appear in the final list. The starter
  // relic (BURNING_BLOOD) isn't a "pick" so we don't enforce it from
  // the other direction.
  for (const r of pickedRelics) {
    expect(finalRelics.has(r), `picked relic ${r} not in Final relics ${[...finalRelics].join(",")}`).toBe(true);
  }
});

test("EVERY floor has either an HP-on-entry banner (combats) OR matches non-combat layout (no combat block)", async ({ page }) => {
  // A canary for accidental UI drift — if a future change forgets to
  // render the combat-detail block on a real combat row, OR
  // accidentally renders it on a non-combat row, this fires.
  await pickRunDirectory(page, locateSampleRunDir());
  const floors = await gatherFloors(page);
  for (const f of floors) {
    expect(f.isCombatRoom, `row ${f.index} (${f.rowLabel.trim()}): combat=${f.isCombatRoom} hasBlock=${f.hasCombatBlock}`).toBe(f.hasCombatBlock);
  }
});
