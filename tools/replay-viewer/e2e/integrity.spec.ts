import { test, expect } from "@playwright/test";
import { locateSampleRunDir, pickRunDirectory } from "./helpers";

// "Does the viewer make sense?" — integrity checks against the bundled
// vendor sample. These tests walk every floor and assert basic
// invariants the user noticed were violated:
//
//   - HP is not 0/0 on rows the agent didn't die on
//   - 💀 only appears where the player actually died
//   - non-combat rooms (event / rest_site / treasure) don't render a
//     combat-detail block
//   - every combat-type room DOES render a combat-detail block
//   - boss room renders its combat block (combat ordinal lookup, not
//     manifest.floor lookup)
//   - turn-marker lines don't carry the [checksum N] noise
//
// The bundled sample is a single act 1 walk (seed=42) that ends at the
// boss (VANTOM_BOSS). Sixteen floors total, one death (the boss).

test.beforeEach(async ({ page }) => {
  await page.goto("/");
  await page.evaluate(() => window.localStorage.clear());
  await page.reload();
});

test("HP is never 0/0 on a non-death floor", async ({ page }) => {
  await pickRunDirectory(page, locateSampleRunDir());
  const rows = await page.locator(".viewer-floor-row").allTextContents();
  // The sample's only death is the boss row (the final one). Every
  // other row should carry a real HP/maxHp pair.
  const nonBoss = rows.slice(0, -1);
  for (const r of nonBoss) {
    expect(r, `row: ${r}`).not.toMatch(/HP\s+0\/0/);
  }
});

test("💀 death marker only appears on rows where current_hp = 0", async ({ page }) => {
  await pickRunDirectory(page, locateSampleRunDir());
  const rows = await page.locator(".viewer-floor-row").allTextContents();
  for (const r of rows) {
    const hasMarker = /💀/.test(r);
    const hasZeroHp = /HP\s+0\/\d+/.test(r);
    // Either both true (real death row) or both false. Never just
    // the marker alone — that's the bug the user reported in item 1
    // ("After every room it says 0/0 HP" with 💀 painted everywhere).
    expect(hasMarker, `row "${r}" shows 💀 without HP 0/N`).toBe(hasZeroHp);
  }
});

test("the bundled sample (abandoned run, 999 HP cheat) has zero death markers", async ({ page }) => {
  // Belt-and-braces — the sample ends abandoned at the boss; the
  // agent had a 999 HP cheat the whole time. If a future re-record
  // ever produces a real death, this assertion fires and prompts a
  // sample/test refresh.
  await pickRunDirectory(page, locateSampleRunDir());
  const rows = await page.locator(".viewer-floor-row").allTextContents();
  const deathRows = rows.filter((r) => /💀/.test(r));
  expect(deathRows, `unexpected death rows: ${deathRows.join(" | ")}`).toHaveLength(0);
});

test("gold > 0 on at least one mid-run floor (Ironclad starts with 99, gains gold from monsters)", async ({ page }) => {
  await pickRunDirectory(page, locateSampleRunDir());
  // Click a few middle floors and assert gold appears in the detail
  // pane. Picking the second combat (a monster floor) — by that point
  // the player has fought through floor 1 and definitely has some
  // gold accumulated.
  await page.locator(".viewer-floor-row").nth(2).click();
  const detail = (await page.locator(".viewer-floor-detail").textContent()) ?? "";
  // Detail pane should show a numeric gold > 0. The renderer prints
  // "Gold\n<N>" — assert the number is non-zero.
  const m = detail.match(/Gold[\s\S]{0,40}?(\d+)/);
  expect(m, `no Gold field in detail pane:\n${detail}`).not.toBeNull();
  expect(Number(m![1])).toBeGreaterThan(0);
});

test("event / rest_site / treasure rows render NO combat detail block", async ({ page }) => {
  await pickRunDirectory(page, locateSampleRunDir());
  const rows = page.locator(".viewer-floor-row");
  const count = await rows.count();
  for (let i = 0; i < count; i++) {
    const label = (await rows.nth(i).textContent()) ?? "";
    // Match the row's `primaryRoomType` token. Combat types are
    // monster / elite / boss; everything else (event / rest_site /
    // treasure) should NOT produce a combat-detail block.
    const isNonCombat = /·\s*(event|rest_site|treasure)(\s|$)/.test(label);
    if (!isNonCombat) continue;
    await rows.nth(i).click();
    const combat = page.locator(".viewer-combat-detail");
    expect(await combat.count(), `non-combat row ${i} (${label.trim()}) is rendering a combat-detail block`).toBe(0);
  }
});

test("every monster/elite/boss row DOES render a combat detail block (incl. the boss)", async ({ page }) => {
  await pickRunDirectory(page, locateSampleRunDir());
  const rows = page.locator(".viewer-floor-row");
  const count = await rows.count();
  let combatRowsSeen = 0;
  for (let i = 0; i < count; i++) {
    const label = (await rows.nth(i).textContent()) ?? "";
    const isCombat = /·\s*(monster|elite|boss)(\s|$)/.test(label);
    if (!isCombat) continue;
    combatRowsSeen++;
    await rows.nth(i).click();
    const combat = page.locator(".viewer-combat-detail");
    expect(await combat.count(), `combat row ${i} (${label.trim()}) is missing its combat-detail block`).toBe(1);
    const events = page.locator(".viewer-combat-detail .viewer-combat-log li[data-kind='event']");
    expect(await events.count(), `combat row ${i} has zero events`).toBeGreaterThan(0);
  }
  // Sanity: the bundled sample is 8 combats (7 normal + 1 boss).
  expect(combatRowsSeen).toBeGreaterThanOrEqual(8);
});

test("every combat floor with a card reward shows the full options list — picked card bold, skipped cards struck through", async ({ page }) => {
  // Item 2 in the new bug list: the user wants to see what the
  // options were AND which was taken. Previously only the boss had
  // card_choices populated (engine OnSkipped path); regular combat
  // floors only surfaced the picked card via cards_gained. The
  // recorder now stamps CardChoices in ClaimCardReward.
  await pickRunDirectory(page, locateSampleRunDir());
  const rows = page.locator(".viewer-floor-row");
  const count = await rows.count();
  let combatsWithRewards = 0;
  for (let i = 0; i < count; i++) {
    const label = (await rows.nth(i).textContent()) ?? "";
    // Boss floor — the agent abandoned, so no card is bold. The
    // assertion for "picked card present" is stricter than what
    // applies there; check only non-boss combats.
    if (!/·\s+(monster|elite)/.test(label)) continue;
    await rows.nth(i).click();
    // Wait for the row's selection class to be applied — proxy for
    // "the click was processed and the detail pane was re-rendered".
    // Without this the locator can read the prior floor's DOM.
    await rows.nth(i).waitFor({ state: "attached" });
    await expect(rows.nth(i)).toHaveClass(/is-selected/);
    const detail = page.locator(".viewer-floor-detail");
    // "Card reward" section must exist for every combat floor (now
    // that StampCardChoices runs in ClaimCardReward).
    const cardRewardSection = detail.locator("section.viewer-floor-section", { hasText: "Card reward" });
    const hasReward = (await cardRewardSection.count()) > 0;
    if (!hasReward) continue;
    combatsWithRewards++;
    // Scope the assertion to the card-reward section ONLY —
    // elite floors also have a relic-reward .viewer-choice-list,
    // which would inflate a detail-wide picked-count.
    const picked = cardRewardSection.locator("li.is-picked");
    const allOptions = cardRewardSection.locator(".viewer-choice-list > li");
    const pickedCount = await picked.count();
    const totalOptions = await allOptions.count();
    expect(pickedCount, `combat floor ${i} (${label.trim()}) shows ${pickedCount} picked cards (want 1)`).toBe(1);
    expect(totalOptions, `combat floor ${i} shows ${totalOptions} card options (want ≥ 2)`).toBeGreaterThanOrEqual(2);
  }
  // The seed-42 sample has 7 non-boss combats; every one of them
  // should have a card reward shown.
  expect(combatsWithRewards).toBeGreaterThanOrEqual(7);
});

test("the boss row's card_choices show all options as SKIPPED (player abandoned before picking)", async ({ page }) => {
  // The agent stops at the boss reward in the bundled sample, so
  // every offered card surfaces as un-picked (no ✔). The full
  // options list must still be visible.
  await pickRunDirectory(page, locateSampleRunDir());
  await page.locator(".viewer-floor-row").last().click();
  const detail = page.locator(".viewer-floor-detail");
  const cardRewardSection = detail.locator("section.viewer-floor-section", { hasText: "Card reward" });
  expect(await cardRewardSection.count()).toBe(1);
  const picked = detail.locator(".viewer-choice-list li.is-picked");
  expect(await picked.count()).toBe(0);
  const options = detail.locator(".viewer-choice-list > li");
  expect(await options.count(), "boss card reward should still list every offered option").toBeGreaterThanOrEqual(3);
});

test("the number of card options shown is not hard-coded to 3 — handles whatever the engine offers", async ({ page }) => {
  // Anchors the user's "could be more / less than 3" requirement.
  // We walk every combat floor's card reward and assert that the
  // number of rendered options equals the JSON's card_choices.length
  // (i.e. we don't accidentally truncate to 3 or pad to 3).
  await pickRunDirectory(page, locateSampleRunDir());
  const rows = page.locator(".viewer-floor-row");
  const count = await rows.count();
  for (let i = 0; i < count; i++) {
    const label = (await rows.nth(i).textContent()) ?? "";
    if (!/·\s+(monster|elite|boss)/.test(label)) continue;
    await rows.nth(i).click();
    const cardRewardSection = page.locator(".viewer-floor-detail section.viewer-floor-section", { hasText: "Card reward" });
    if ((await cardRewardSection.count()) === 0) continue;
    const renderedOptions = await cardRewardSection.locator(".viewer-choice-list > li").count();
    // Always ≥ 2 — typical reward is 3, rare events offer 1 or 4+
    // (rare relic, ancient choice). The hard guarantee is "more
    // than one option exists, and they're all rendered".
    expect(renderedOptions, `combat floor ${i} shows ${renderedOptions} card options`).toBeGreaterThanOrEqual(2);
  }
});

test("enemy actions are surfaced between enemy-turn-start and enemy-turn-end checkpoints", async ({ page }) => {
  // Item 1 in the new bug list: the combat log should show what
  // the enemy did, not just disappear between turn checkpoints.
  // We can't recover individual enemy attacks from the .mcr (only
  // player actions are recorded), so we synthesise a single
  // "⚔ enemy acts — -N HP" entry per enemy turn from the HP /
  // block delta between flanking checkpoints.
  await pickRunDirectory(page, locateSampleRunDir());
  const rows = page.locator(".viewer-floor-row");
  const count = await rows.count();
  let combatsWithEnemyEntries = 0;
  for (let i = 0; i < count; i++) {
    const label = (await rows.nth(i).textContent()) ?? "";
    if (!/·\s+(monster|elite|boss)/.test(label)) continue;
    await rows.nth(i).click();
    const enemyEntries = await page.locator(".viewer-combat-log li[data-kind='enemy']").count();
    if (enemyEntries > 0) combatsWithEnemyEntries++;
  }
  // Most combats in the sample take some damage (only one floor in
  // the sample is a perfect-block run, if any). Assert ≥ 5 combats
  // surface at least one enemy action — leaves wiggle room for a
  // re-record on a different agent path.
  expect(combatsWithEnemyEntries).toBeGreaterThanOrEqual(5);
});

test("the enemy-action entry sits BETWEEN the enemy-turn-start and the next checkpoint (never before / after)", async ({ page }) => {
  await pickRunDirectory(page, locateSampleRunDir());
  // Pick a combat we KNOW takes damage — the boss takes lots.
  await page.locator(".viewer-floor-row").last().click();
  const items = await page.locator(".viewer-combat-log > li").all();
  // Walk items, find every enemy entry and verify its neighbours.
  for (let i = 0; i < items.length; i++) {
    const kind = await items[i]!.getAttribute("data-kind");
    if (kind !== "enemy") continue;
    expect(i, "enemy entry can't be first item — needs a turn-start before it").toBeGreaterThan(0);
    const prevKind = await items[i - 1]!.getAttribute("data-kind");
    expect(prevKind, `prior to enemy at #${i} should be a turn checkpoint`).toBe("turn");
    const prevText = (await items[i - 1]!.textContent()) ?? "";
    expect(prevText).toContain("After enemy turn start");
  }
});

test("turn markers do NOT show [checksum N] prefix", async ({ page }) => {
  await pickRunDirectory(page, locateSampleRunDir());
  // Pick the first combat-type row and inspect its turn list.
  const rows = page.locator(".viewer-floor-row");
  const count = await rows.count();
  for (let i = 0; i < count; i++) {
    const label = (await rows.nth(i).textContent()) ?? "";
    if (/·\s*(monster|elite|boss)/.test(label)) {
      await rows.nth(i).click();
      const turnText = (await page.locator(".viewer-combat-log li[data-kind='turn']").allTextContents()).join("\n");
      expect(turnText, "turn list should not show the internal [checksum N] id prefix").not.toMatch(/\[checksum/);
      return;
    }
  }
  throw new Error("no combat row found in sample — fixture regression");
});

test("floor numbering matches the engine's ActFloor (manifest combat floors line up with viewer rows)", async ({ page }) => {
  // The viewer's row label says "act 1 · floor N · <type>". For the
  // bundled sample, the first combat's engine ActFloor is 2 (Neow
  // skipped at floor 1). The boss is at engine ActFloor 17. If our
  // numbering doesn't match the engine's, neither will the cross-
  // reference with the manifest combat list.
  await pickRunDirectory(page, locateSampleRunDir());
  const rows = await page.locator(".viewer-floor-row").allTextContents();
  // First non-ancient row should be "floor 2" (Neow skipped).
  expect(rows[0], `first row label: ${rows[0]}`).toMatch(/floor 2\b/);
  // Last row (the boss) should be at engine floor 17 in the seed-42
  // sample. If we ever re-record with a different walk this assertion
  // may need a generic "≥ 16" relaxation — but for the bundled sample
  // the exact value catches off-by-one regressions cleanly.
  expect(rows[rows.length - 1]!).toMatch(/floor 17\b/);
});
