// Local screenshot helper. Drives a headless browser against the dev
// server (default http://localhost:5173) and writes PNGs. Used by
// `just snap-viewer` and ad-hoc invocations from Claude Code so a
// review can include "here's how it actually renders" without anyone
// manually screencapping.
//
// First-run setup downloads ~100 MB of browser binary:
//   PLAYWRIGHT_BROWSERS_PATH=$(pwd)/.playwright-browsers pnpm exec playwright install firefox
//
// Usage:
//   pnpm vite --port 5173 &              # start the dev server first
//   node scripts/snap.mjs <out.png> [--collapsed] [--width N] [--height N] [--url URL] [--browser firefox|webkit|chromium]
//
// `--collapsed` clicks the sidebar-collapse toggle once before snapping,
// so we can capture both states without spinning up two pages.

import { chromium, firefox, webkit } from "@playwright/test";

// Browser selection: the chromium-headless-shell binary segfaults
// under some macOS sandbox configurations, so we default to Firefox
// (most reliable launch on macOS 26 in our setup). Pass --browser
// chromium / webkit to override.

const args = process.argv.slice(2);
const out = args.find((a) => !a.startsWith("--")) ?? "snap.png";

function flagValue(name, fallback) {
  const i = args.findIndex((a) => a === `--${name}`);
  return i >= 0 && i + 1 < args.length ? args[i + 1] : fallback;
}

const url = flagValue("url", process.env.URL ?? "http://localhost:5173");
const width = Number(flagValue("width", "1280"));
const height = Number(flagValue("height", "800"));
const collapsed = args.includes("--collapsed");

const browserName = flagValue("browser", "firefox");
const browserType =
  browserName === "chromium" ? chromium :
  browserName === "webkit" ? webkit :
  firefox;
const browser = await browserType.launch({ headless: true, timeout: 120_000 });
const context = await browser.newContext({ viewport: { width, height } });
const page = await context.newPage();
await page.goto(url, { waitUntil: "domcontentloaded" });
// Give the runs.json fetch + sidebar render a moment to settle.
await page.waitForLoadState("networkidle").catch(() => {});
await page.waitForTimeout(200);

if (collapsed) {
  await page.locator("#sidebar-collapse").click();
  await page.waitForTimeout(150);
}

await page.screenshot({ path: out, fullPage: false });
console.log(`wrote ${out} (${width}x${height}${collapsed ? ", collapsed" : ""})`);
await browser.close();
