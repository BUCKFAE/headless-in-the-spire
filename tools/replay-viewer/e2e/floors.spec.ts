import { test, expect } from "@playwright/test";
import { mkdirSync, mkdtempSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { pickRunDirectory } from "./helpers";
import { SAMPLE_RUN_RAW } from "../src/fixtures/sample-run";
import { SAMPLE_TIMELINE_RAW } from "../src/fixtures/sample-timeline";

// Materialises a synthetic run directory under a per-test tmpdir,
// using the same TS fixtures the unit tests build against. This lets
// the e2e suite exercise the full-run floor view (items 3, 5, 6, 7
// from the user redesign) without depending on the bundled sample's
// shape — the bundled sample under replays/sample/ has no run.json
// today because the act-1-boss recording test stops before
// RunManager.OnEnded fires.
//
// Synthesizing here rather than checking in static fixture JSON
// keeps the fixtures in one place (src/fixtures/) and avoids drift.
function buildFullRunFixture(): string {
  const root = mkdtempSync(join(tmpdir(), "sts2-viewer-e2e-"));
  const runDir = join(root, "v0.103.2", "1779021871-42");
  mkdirSync(runDir, { recursive: true });
  mkdirSync(join(runDir, "combats"), { recursive: true });

  // Manifest pointing at one combat — combat-row click-through in the
  // viewer needs a manifest entry whose floor matches the floor in
  // the run history. The monster floor at floor 1 is the natural
  // target.
  const manifest = {
    version: 1,
    header: {
      game_version: "v0.103.2",
      sts2_dll_sha256: "synthetic",
      model_id_hash: 1357847701,
      git_commit: "test",
      run_history_schema_version: 9,
      protocol_version: 1,
      seed: "42",
      character: "ironclad",
      ascension: 0,
      modifiers: [],
      start_time_unix: 1779021871,
    },
    combats: [{
      mcr_file: "combats/act1-floor1-combat.mcr",
      act_index: 0,
      floor: 1,
      room_type: "combat_room",
      outcome: "unknown",
      action_count: 6,
      checksum_count: 2,
    }],
  };

  writeFileSync(join(runDir, "manifest.json"), JSON.stringify(manifest));
  writeFileSync(join(runDir, "run.json"), JSON.stringify(SAMPLE_RUN_RAW));
  writeFileSync(join(runDir, "combats", "act1-floor1-combat.mcr.timeline.json"), JSON.stringify(SAMPLE_TIMELINE_RAW));
  // The .mcr binary itself is not load-bearing for the viewer — it
  // just needs to exist if a future change tries to scan for it.
  writeFileSync(join(runDir, "combats", "act1-floor1-combat.mcr"), Buffer.from(""));

  // setInputFiles on a webkitdirectory input wants the directory the
  // user would have picked, which in the layout above is the
  // `1779021871-42` run-id dir (one level above combats/).
  return runDir;
}

test.beforeEach(async ({ page }) => {
  await page.goto("/");
  await page.evaluate(() => window.localStorage.clear());
  await page.reload();
});

test("full-run view shows floor list with end-of-floor HP and death markers", async ({ page }) => {
  const dir = buildFullRunFixture();
  await pickRunDirectory(page, dir);

  // Header reports VICTORY because the synthetic fixture has win:true.
  await expect(page.locator(".viewer-run-header")).toContainText(/VICTORY/);

  // The floor list should have 5 rows (one per map_point in the sample).
  const rows = page.locator(".viewer-floor-row");
  await expect(rows).toHaveCount(5);

  // Per-floor HP is visible on each row.
  await expect(rows.nth(1)).toContainText(/HP 76\/80/);
  await expect(rows.nth(4)).toContainText(/HP 40\/80/);
});

test("clicking the Neow floor shows the blessing choice", async ({ page }) => {
  const dir = buildFullRunFixture();
  await pickRunDirectory(page, dir);

  // The first row is the ancient (Neow) floor. Click it; the detail
  // pane should surface the Neow blessing list with GOLDEN_PEARL
  // marked as chosen.
  await page.locator(".viewer-floor-row").first().click();
  const detail = page.locator(".viewer-floor-detail");
  await expect(detail).toContainText("Neow's blessing");
  const picked = detail.locator(".viewer-choice-list li.is-picked");
  await expect(picked).toContainText("GOLDEN_PEARL");
});

test("monster floor surfaces card rewards with the picked one highlighted", async ({ page }) => {
  const dir = buildFullRunFixture();
  await pickRunDirectory(page, dir);

  // Floor 1 is the monster floor with the card_choices reward.
  await page.locator(".viewer-floor-row").nth(1).click();
  const detail = page.locator(".viewer-floor-detail");
  await expect(detail).toContainText("Card reward");
  await expect(detail.locator(".viewer-choice-list li.is-picked")).toContainText("CARD.BATTLE_TRANCE");
  // Non-picked options still show up — the user wants to see what
  // they passed on.
  await expect(detail).toContainText("CARD.STRIKE_IRONCLAD");
  await expect(detail).toContainText("CARD.ANGER");
});

test("monster floor's combat detail surfaces per-turn HP from checksum states", async ({ page }) => {
  const dir = buildFullRunFixture();
  await pickRunDirectory(page, dir);

  await page.locator(".viewer-floor-row").nth(1).click();
  const detail = page.locator(".viewer-floor-detail");
  await expect(detail).toContainText("Combat");
  // The synthesized timeline has two checksums, both with "turn" in
  // the context — both should land as turn markers carrying HP.
  const turns = detail.locator(".viewer-combat-log li[data-kind='turn']");
  await expect(turns).toHaveCount(2);
  await expect(turns.first()).toContainText(/HP /);
});

test("event floor shows the event choice taken", async ({ page }) => {
  const dir = buildFullRunFixture();
  await pickRunDirectory(page, dir);

  await page.locator(".viewer-floor-row").nth(2).click();
  await expect(page.locator(".viewer-floor-detail")).toContainText("Event choice");
  await expect(page.locator(".viewer-floor-detail")).toContainText("STONE_OF_ALL_TIME");
});

test("monster floor shows entry loadout (relics + potions) at the top of the detail pane", async ({ page }) => {
  // Item 5 from the user redesign: per-room view should show what
  // relics/potions the player had walking in. Only combat floors
  // can populate this today (the data is in the .mcr's initial_run);
  // non-combat floors silently omit the block, by design.
  const dir = buildFullRunFixture();
  await pickRunDirectory(page, dir);

  await page.locator(".viewer-floor-row").nth(1).click();
  const entry = page.locator(".viewer-entry-loadout");
  await expect(entry).toBeVisible();
  await expect(entry).toContainText("RELIC.GOLDEN_PEARL");
  await expect(entry).toContainText("POTION.HEAL");
});

test("boss floor shows the relic reward and final HP", async ({ page }) => {
  const dir = buildFullRunFixture();
  await pickRunDirectory(page, dir);

  await page.locator(".viewer-floor-row").last().click();
  const detail = page.locator(".viewer-floor-detail");
  await expect(detail).toContainText("Relic reward");
  await expect(detail).toContainText("RELIC.STRANGE_FRUIT");
  await expect(detail).toContainText("40");
});
