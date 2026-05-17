import { test, expect } from "@playwright/test";
import { locateSampleRunDir, pickRunDirectory } from "./helpers";

// Smoke coverage for the directory-load + persistence + clear flow,
// against the bundled vendor sample. The sample has a real run.json
// today so the viewer renders the full-run floor view — selectors
// here target the floor-view DOM. The detailed assertions for
// individual floor features (card rewards, event choices, per-turn
// HP, etc.) live in floors.spec.ts; this file just sanity-checks the
// top-level UX (load / restore / clear).
//
// Tests start on a fresh page (no shared localStorage) so order
// doesn't matter.

test.beforeEach(async ({ page }) => {
  await page.goto("/");
  await page.evaluate(() => window.localStorage.clear());
  await page.reload();
});

test("page loads with the empty-state picker", async ({ page }) => {
  await expect(page.getByRole("heading", { name: /STS2 Headless/ })).toBeVisible();
  await expect(page.locator("#directory-input")).toBeVisible();
});

test("loading the sample shows the run header + clickable floor list", async ({ page }) => {
  const sampleDir = locateSampleRunDir();
  await pickRunDirectory(page, sampleDir);

  // Run header surfaces seed + character.
  await expect(page.locator(".viewer-run-header")).toContainText(/seed/);
  await expect(page.locator(".viewer-run-header")).toContainText(/CHARACTER\.IRONCLAD/);

  // At least one floor row is rendered and clickable.
  const rows = page.locator(".viewer-floor-row");
  await expect(rows.first()).toBeVisible();
  expect(await rows.count()).toBeGreaterThan(0);

  // Auto-selected last floor: a detail pane should be visible.
  await expect(page.locator(".viewer-floor-detail")).toBeVisible();
});

test("clicking a different floor swaps the detail pane", async ({ page }) => {
  const sampleDir = locateSampleRunDir();
  await pickRunDirectory(page, sampleDir);

  const initialTitle = await page.locator(".viewer-floor-detail h3").textContent();
  const rows = page.locator(".viewer-floor-row");
  const rowCount = await rows.count();
  if (rowCount < 2) test.skip(true, "sample has only one floor — click-into-another is moot");

  // Click the first floor (different from the auto-selected last
  // floor in a multi-floor run).
  await rows.first().click();
  await expect(rows.first()).toHaveClass(/is-selected/);

  // Detail pane re-renders with a different heading.
  await expect.poll(async () => await page.locator(".viewer-floor-detail h3").textContent())
    .not.toBe(initialTitle);
});

test("reload restores the last-loaded session", async ({ page }) => {
  const sampleDir = locateSampleRunDir();
  await pickRunDirectory(page, sampleDir);

  await expect(page.locator(".viewer-floor-row").first()).toBeVisible();
  await page.reload();
  await expect(page.locator("#status")).toContainText(/restored/);
  await expect(page.locator(".viewer-floor-row").first()).toBeVisible();
});

test("manifest rows and detail pane expose neither action counts nor checksums", async ({ page }) => {
  // Items 1 + 2 from the user's redesign feedback — these are UI
  // noise we deliberately dropped. The full-run view's floor list
  // and detail pane should both stay clean.
  const sampleDir = locateSampleRunDir();
  await pickRunDirectory(page, sampleDir);

  const overviewText = (await page.locator(".viewer-floor-list").textContent()) ?? "";
  expect(overviewText).not.toMatch(/checksum/i);
  expect(overviewText).not.toMatch(/\b\d+\s*action/i);

  const detailText = (await page.locator(".viewer-floor-detail").textContent()) ?? "";
  expect(detailText).not.toMatch(/checksum/i);
});

test("clear button wipes the session and the cache", async ({ page }) => {
  const sampleDir = locateSampleRunDir();
  await pickRunDirectory(page, sampleDir);
  await expect(page.locator(".viewer-floor-row").first()).toBeVisible();

  await page.locator("#clear-session").click();
  await expect(page.locator(".viewer-floor-row")).toHaveCount(0);
  await expect(page.locator(".viewer-floor-detail")).toHaveCount(0);

  // Reload — the cleared cache should not bring the session back.
  await page.reload();
  await expect(page.locator(".viewer-floor-row")).toHaveCount(0);
});
