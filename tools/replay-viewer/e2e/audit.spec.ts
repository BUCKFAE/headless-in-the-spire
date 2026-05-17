import { test } from "@playwright/test";
import { writeFileSync } from "node:fs";
import { locateSampleRunDir, pickRunDirectory } from "./helpers";

// Audit-only: not a real test. Walks every floor row, clicks into it,
// dumps the row label + detail-pane innerText to /tmp/viewer-audit.txt.
// Use to eyeball what the viewer actually renders against the bundled
// sample so we can catch rendering bugs that don't trip an explicit
// assertion.
//
// Not part of the regular suite — only runs when AUDIT=1 is set. The
// dump file lands at /tmp/viewer-audit-<timestamp>.txt.

test.skip(!process.env["AUDIT"], "set AUDIT=1 to run the viewer audit");

test("audit: dump every floor's detail pane to /tmp/viewer-audit.txt", async ({ page }) => {
  test.setTimeout(60_000);
  const sampleDir = locateSampleRunDir();
  await page.goto("/");
  await page.evaluate(() => window.localStorage.clear());
  await page.reload();
  await pickRunDirectory(page, sampleDir);

  const lines: string[] = [];
  lines.push("=== Run header ===");
  lines.push((await page.locator(".viewer-run-header").innerText()) ?? "");
  lines.push("");

  const rowCount = await page.locator(".viewer-floor-row").count();
  lines.push(`=== ${rowCount} floors ===\n`);
  for (let i = 0; i < rowCount; i++) {
    const row = page.locator(".viewer-floor-row").nth(i);
    const rowLabel = (await row.innerText()) ?? "";
    lines.push(`\n──────────────────────────────────────────────`);
    lines.push(`FLOOR INDEX ${i}: ${rowLabel}`);
    lines.push(`──────────────────────────────────────────────`);
    await row.click();
    const detail = (await page.locator(".viewer-floor-detail").innerText()) ?? "";
    lines.push(detail);
  }

  writeFileSync("/tmp/viewer-audit.txt", lines.join("\n"));
  console.log("dumped to /tmp/viewer-audit.txt");
});
