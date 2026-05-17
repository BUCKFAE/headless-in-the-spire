// @vitest-environment jsdom
import { describe, it, expect, vi } from "vitest";
import { parseTimeline } from "../src/core/parse";
import { renderCombatTimeline, renderRunOverview } from "../src/view/render";
import { ingestFiles } from "../src/core/session";
import { SAMPLE_TIMELINE_RAW } from "../src/fixtures/sample-timeline";

// Smoke test for the DOM renderer. We intentionally do NOT exhaustively
// assert markup structure — the render layer is meant to be cheap to
// iterate on, and locking down the precise HTML tree would make every
// styling tweak require a test edit. The assertions here only catch
// catastrophic regressions: missing event rows, missing summaries,
// checksum table absence when checksums exist.

describe("renderCombatTimeline", () => {
  it("renders one <li> per event, with summary text", () => {
    const t = parseTimeline(SAMPLE_TIMELINE_RAW);
    const root = renderCombatTimeline(t);
    const items = root.querySelectorAll(".viewer-event-list li");
    expect(items).toHaveLength(t.events.length);
    expect(items[0]!.textContent).toContain("play CARD.BATTLE_TRANCE");
    expect(items[1]!.textContent).toContain("→ enemy 1");
  });

  it("never renders a checksum table (deliberately dropped from the UI)", () => {
    const t = parseTimeline(SAMPLE_TIMELINE_RAW);
    const root = renderCombatTimeline(t);
    // The checksum table was UI noise: high-cardinality, low-value
    // for the viewer audience. Locked in here so a future "show
    // verification details" feature doesn't accidentally bring it
    // back at the top level — a future debug pane should live behind
    // an explicit toggle.
    expect(root.querySelector(".viewer-checksum-table")).toBeNull();
  });
});

const MANIFEST = {
  version: 1,
  header: {
    game_version: "v0.103.2",
    sts2_dll_sha256: "abc",
    model_id_hash: 1357847701,
    git_commit: "x",
    run_history_schema_version: 9,
    protocol_version: 1,
    seed: "42",
    character: "ironclad",
    ascension: 0,
    modifiers: [],
    start_time_unix: 1779016981,
  },
  combats: [
    { mcr_file: "combats/a.mcr", act_index: 0, floor: 2, room_type: "combat_room", outcome: "unknown", action_count: 9, checksum_count: 0 },
    { mcr_file: "combats/b.mcr", act_index: 0, floor: 4, room_type: "combat_room", outcome: "unknown", action_count: 12, checksum_count: 5 },
  ],
};

describe("renderRunOverview", () => {
  it("renders one clickable button per combat and disables combats without a timeline", () => {
    const session = ingestFiles([
      { path: "manifest.json", text: JSON.stringify(MANIFEST) },
      { path: "combats/a.mcr.timeline.json", text: JSON.stringify(SAMPLE_TIMELINE_RAW) },
    ]);
    const onSelect = vi.fn();
    const root = renderRunOverview(session, null, onSelect);
    const buttons = root.querySelectorAll<HTMLButtonElement>(".viewer-combat-row");
    expect(buttons).toHaveLength(2);
    expect(buttons[0]!.disabled).toBe(false);
    expect(buttons[1]!.disabled).toBe(true);
    expect(buttons[1]!.textContent).toContain("(no timeline)");
  });

  it("clicking a row fires onSelect with the mcr_file key", () => {
    const session = ingestFiles([
      { path: "manifest.json", text: JSON.stringify(MANIFEST) },
      { path: "combats/a.mcr.timeline.json", text: JSON.stringify(SAMPLE_TIMELINE_RAW) },
    ]);
    const onSelect = vi.fn();
    const root = renderRunOverview(session, null, onSelect);
    const enabledBtn = root.querySelector<HTMLButtonElement>(".viewer-combat-row:not(:disabled)")!;
    enabledBtn.click();
    expect(onSelect).toHaveBeenCalledTimes(1);
    expect(onSelect).toHaveBeenCalledWith("combats/a.mcr");
  });

  it("marks the currently-selected row with .is-selected", () => {
    const session = ingestFiles([
      { path: "manifest.json", text: JSON.stringify(MANIFEST) },
      { path: "combats/a.mcr.timeline.json", text: JSON.stringify(SAMPLE_TIMELINE_RAW) },
    ]);
    const root = renderRunOverview(session, "combats/a.mcr", () => {});
    const selected = root.querySelector<HTMLButtonElement>(".viewer-combat-row.is-selected");
    expect(selected).not.toBeNull();
    expect(selected!.dataset["mcrFile"]).toBe("combats/a.mcr");
  });
});
