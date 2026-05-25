import { test, expect } from "@playwright/test";
import { locateSampleRunDir, pickRunDirectory } from "./helpers";

// Golden-state assertions against the bundled seed-42 sample. These
// lock in specific values so a future re-record / engine bump that
// shifts numeric content fires a loud test, prompting an explicit
// sample refresh rather than silently shifting the rendered output.
//
// If `just runner::record-sample-replay` is re-run with a different walk,
// expect these to fail and update both the sample and the expected
// values in lockstep.

test.beforeEach(async ({ page }) => {
  await page.goto("/");
  await page.evaluate(() => window.localStorage.clear());
  await page.reload();
});

test("sample run header — exact expected text for the seed-42 walk", async ({ page }) => {
  await pickRunDirectory(page, locateSampleRunDir());
  const header = await page.locator(".viewer-run-header").innerText();
  expect(header).toContain("seed sts2headless-42");
  expect(header).toContain("CHARACTER.IRONCLAD");
  // The bundled sample is an abandoned run (test stops at boss, no
  // actual heart-victory or death — the agent has a 999 HP cheat).
  expect(header).toContain("abandoned");
  // Three relics picked across the run: BURNING_BLOOD (starter) +
  // ODDLY_SMOOTH_STONE (elite reward) + GORGET (treasure).
  expect(header).toContain("RELIC.BURNING_BLOOD");
  expect(header).toContain("RELIC.ODDLY_SMOOTH_STONE");
  expect(header).toContain("RELIC.GORGET");
});

test("sample run has exactly 16 floors numbered 2..17", async ({ page }) => {
  await pickRunDirectory(page, locateSampleRunDir());
  const rows = await page.locator(".viewer-floor-row").allTextContents();
  expect(rows).toHaveLength(16);
  expect(rows[0]).toMatch(/floor 2\b/);
  expect(rows[15]).toMatch(/floor 17\b/);
  // Each row's floor number is exactly its index + 2 (no Neow in
  // map_point_history, engine ActFloor starts at 2).
  for (let i = 0; i < rows.length; i++) {
    expect(rows[i], `row ${i}: ${rows[i]}`).toMatch(new RegExp(`floor ${i + 2}\\b`));
  }
});

test("sample run has 8 combat rows (7 normal + 1 boss)", async ({ page }) => {
  await pickRunDirectory(page, locateSampleRunDir());
  const rows = await page.locator(".viewer-floor-row").allTextContents();
  const combats = rows.filter((r) => /·\s+(monster|elite|boss)/.test(r));
  expect(combats).toHaveLength(8);
  const monsters = combats.filter((r) => /·\s+monster/.test(r));
  const elites = combats.filter((r) => /·\s+elite/.test(r));
  const bosses = combats.filter((r) => /·\s+boss/.test(r));
  expect(monsters).toHaveLength(6);
  expect(elites).toHaveLength(1);
  expect(bosses).toHaveLength(1);
});

test("sample run's encounter sequence matches expected (combat ordinals must line up)", async ({ page }) => {
  // Anchors the manifest↔map_point_history join. If we accidentally
  // mis-attribute a combat to a non-combat floor (the prior bug),
  // these encounter→floor pairs would shift. Locking the order keeps
  // a regression visible.
  await pickRunDirectory(page, locateSampleRunDir());
  const rows = await page.locator(".viewer-floor-row").allTextContents();
  const expected: Array<[number, RegExp]> = [
    [2, /FUZZY_WURM_CRAWLER_WEAK/],
    [3, /WELLSPRING/],
    [4, /SHRINKER_BEETLE_WEAK/],
    [5, /NIBBITS_WEAK/],
    [6, /THE_LEGENDS_WERE_TRUE/],
    [7, /rest_site/],
    [8, /PHROG_PARASITE_ELITE/],
    [9, /MAWLER_NORMAL/],
    [10, /treasure/],
    [11, /rest_site/],
    [12, /SLITHERING_STRANGLER/],
    [13, /rest_site/],
    [14, /JUNGLE_MAZE_ADVENTURE/],
    [15, /FOGMOG/],
    [16, /rest_site/],
    [17, /VANTOM_BOSS/],
  ];
  for (let i = 0; i < expected.length; i++) {
    const [floor, pat] = expected[i]!;
    expect(rows[i], `row ${i} (expected floor ${floor}, ${pat}): ${rows[i]}`).toMatch(new RegExp(`floor ${floor}\\b`));
    expect(rows[i], `row ${i} content: ${rows[i]}`).toMatch(pat);
  }
});

test("boss floor exact content — encounter, HP/maxHP, gold, combat detail", async ({ page }) => {
  await pickRunDirectory(page, locateSampleRunDir());
  const rows = page.locator(".viewer-floor-row");
  await rows.last().click();
  const detail = await page.locator(".viewer-floor-detail").innerText();
  expect(detail).toContain("Floor 17");
  expect(detail).toContain("ENCOUNTER.VANTOM_BOSS");
  // Agent had 999 HP cheat; ended at 925 (took 80 damage, healed 6
  // during combat — net -74).
  expect(detail).toMatch(/925\s*\/\s*999/);
  // Gold reached ~356 by boss.
  expect(detail).toMatch(/Gold[\s\S]{0,40}?356/);
  // Boss card reward: three options offered, none picked (agent
  // didn't survive to pick).
  expect(detail).toContain("CARD.CRIMSON_MANTLE");
  expect(detail).toContain("CARD.CONFLAGRATION");
  expect(detail).toContain("CARD.STOKE");
});

test("first combat (floor 2) exact content — encounter, HP, gold, combat detail", async ({ page }) => {
  await pickRunDirectory(page, locateSampleRunDir());
  await page.locator(".viewer-floor-row").first().click();
  const detail = await page.locator(".viewer-floor-detail").innerText();
  expect(detail).toContain("Floor 2");
  expect(detail).toContain("ENCOUNTER.FUZZY_WURM_CRAWLER_WEAK");
  // Floor 2 first combat — agent took 4 damage, healed 4, ended at
  // 999/999 with 111 gold.
  expect(detail).toMatch(/999\s*\/\s*999/);
  expect(detail).toMatch(/Damage taken[\s\S]{0,20}?4/);
  expect(detail).toMatch(/HP healed[\s\S]{0,20}?4/);
  expect(detail).toMatch(/Gold[\s\S]{0,40}?111/);
  // Combat detail must be present (the missing-combat-log bug we
  // just fixed — floor 2 used to render no combat).
  const turns = await page.locator(".viewer-combat-detail .viewer-combat-log li[data-kind='turn']").count();
  expect(turns).toBeGreaterThan(0);
});
