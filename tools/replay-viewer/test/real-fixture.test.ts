import { describe, it, expect } from "vitest";
import { existsSync, readFileSync, readdirSync } from "node:fs";
import { join } from "node:path";
import { parseManifest, parseTimeline } from "../src/core/parse";
import { summarizeEvent } from "../src/core/summary";

// End-to-end smoke against whatever sits in replays/. The recording
// substrate writes manifest.json + per-combat *.mcr +
// *.mcr.timeline.json under `<root>/<game-version>/<run-id>/`. If the
// repo has the seed-42 sample bundled (`just runner::record-sample-replay`),
// we round-trip each timeline.json through the parser and ensure
// summarizeEvent doesn't throw on anything in the wild. The walk picks
// up every bucket under replays/ (manual, sample, eval-harness, …) —
// more recordings ⇒ broader coverage, automatically.
//
// Skipped silently if no recordings are present — running the viewer's
// tests must not require having driven a real run yet.

const REPLAYS_ROOT = join(__dirname, "..", "..", "..", "replays");

describe("real-world timeline.json round-trip", () => {
  it("parses every timeline.json under replays/ and summarises every event", () => {
    if (!existsSync(REPLAYS_ROOT)) return;
    const timelinePaths = walkForTimelines(REPLAYS_ROOT);
    if (timelinePaths.length === 0) return;

    let sawStateBlock = false;
    for (const path of timelinePaths) {
      const raw: unknown = JSON.parse(readFileSync(path, "utf8"));
      const timeline = parseTimeline(raw);
      expect(timeline.header.event_count).toBe(timeline.events.length);
      for (const e of timeline.events) {
        // summarizeEvent must never throw — the fallback branch handles
        // unrecognised action types with a "?" hint.
        expect(typeof summarizeEvent(e)).toBe("string");
      }
      for (const c of timeline.checksums) {
        if (c.state) {
          sawStateBlock = true;
          // The viewer relies on every checksum-with-state having at
          // least one creature so per-turn HP renders.
          expect(c.state.creatures.length).toBeGreaterThan(0);
        }
      }
    }
    // If the bundled sample has checksums at all, we expect at least
    // one to carry a state block — that's the recording-side guarantee
    // from CombatTimelineEmitter. (A pre-state-emission sample would
    // have neither checksums nor state, so sawStateBlock stays false
    // and we don't fail.)
    const sawChecksums = timelinePaths.some((p) => {
      const t = parseTimeline(JSON.parse(readFileSync(p, "utf8")));
      return t.checksums.length > 0;
    });
    if (sawChecksums) expect(sawStateBlock).toBe(true);
  });

  it("parses every manifest.json under replays/", () => {
    if (!existsSync(REPLAYS_ROOT)) return;
    const manifestPaths = walkForManifests(REPLAYS_ROOT);
    if (manifestPaths.length === 0) return;
    for (const path of manifestPaths) {
      const raw: unknown = JSON.parse(readFileSync(path, "utf8"));
      const m = parseManifest(raw);
      expect(m.combats.length).toBeGreaterThanOrEqual(0);
    }
  });
});

function walkForTimelines(root: string): string[] {
  const out: string[] = [];
  for (const entry of readdirSync(root, { withFileTypes: true })) {
    const full = join(root, entry.name);
    if (entry.isDirectory()) {
      out.push(...walkForTimelines(full));
    } else if (entry.isFile() && entry.name.endsWith(".timeline.json")) {
      out.push(full);
    }
  }
  return out;
}

function walkForManifests(root: string): string[] {
  const out: string[] = [];
  for (const entry of readdirSync(root, { withFileTypes: true })) {
    const full = join(root, entry.name);
    if (entry.isDirectory()) {
      out.push(...walkForManifests(full));
    } else if (entry.isFile() && entry.name === "manifest.json") {
      out.push(full);
    }
  }
  return out;
}
