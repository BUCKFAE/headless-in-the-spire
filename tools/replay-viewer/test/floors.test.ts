// @vitest-environment jsdom
import { describe, it, expect, vi } from "vitest";
import { parseManifest, parseTimeline } from "../src/core/parse";
import { parseRunHistory } from "../src/core/parse-run-history";
import {
  combatTimelineForFloor,
  materialiseFloors,
  renderFloorDetail,
  renderFloorList,
  renderRunHeader,
} from "../src/view/floors";
import type { Session } from "../src/core/session";
import { SAMPLE_RUN_RAW } from "../src/fixtures/sample-run";
import { SAMPLE_RUN_NO_NEOW_RAW, SAMPLE_MANIFEST_NO_NEOW_RAW } from "../src/fixtures/sample-run-no-neow";
import { SAMPLE_TIMELINE_RAW } from "../src/fixtures/sample-timeline";

describe("materialiseFloors", () => {
  it("produces one row per map_point_history entry, numbered to match engine ActFloor", () => {
    const rows = materialiseFloors(parseRunHistory(SAMPLE_RUN_RAW));
    expect(rows).toHaveLength(5);
    // Neow (ancient entry) is engine ActFloor 1.
    expect(rows[0]!.floor).toBe(1);
    expect(rows[0]!.mapPointType).toBe("ancient");
    expect(rows[1]!.floor).toBe(2);
    expect(rows[2]!.floor).toBe(3);
    expect(rows[4]!.floor).toBe(5);
  });

  it("attaches end-of-floor playerStats to each row", () => {
    const rows = materialiseFloors(parseRunHistory(SAMPLE_RUN_RAW));
    expect(rows[1]!.playerStats?.current_hp).toBe(76);
    expect(rows[4]!.playerStats?.current_hp).toBe(40);
  });
});

describe("renderFloorList", () => {
  const rows = materialiseFloors(parseRunHistory(SAMPLE_RUN_RAW));

  it("renders one clickable row per floor with end-of-floor HP visible", () => {
    const onSelect = vi.fn();
    const root = renderFloorList(rows, null, onSelect);
    const items = root.querySelectorAll(".viewer-floor-row");
    expect(items).toHaveLength(rows.length);
    expect(items[0]!.textContent).toContain("floor 1");
    expect(items[1]!.textContent).toContain("HP 76/80");
    expect(items[4]!.textContent).toContain("HP 40/80");
  });

  it("marks the selected row with .is-selected", () => {
    const root = renderFloorList(rows, 2, () => {});
    const selected = root.querySelector(".viewer-floor-row.is-selected") as HTMLButtonElement | null;
    expect(selected?.dataset["floorIndex"]).toBe("2");
  });

  it("clicking fires onSelect with the row index", () => {
    const onSelect = vi.fn();
    const root = renderFloorList(rows, null, onSelect);
    (root.querySelectorAll<HTMLButtonElement>(".viewer-floor-row")[3]!).click();
    expect(onSelect).toHaveBeenCalledWith(3);
  });

  it("shows a death marker when HP is 0", () => {
    const synth = [...rows];
    synth[4] = { ...synth[4]!, playerStats: { ...synth[4]!.playerStats!, current_hp: 0 } };
    const root = renderFloorList(synth, null, () => {});
    const lastBtn = root.querySelectorAll(".viewer-floor-row")[4]!;
    expect(lastBtn.textContent).toMatch(/💀/);
  });
});

describe("renderFloorDetail", () => {
  const rows = materialiseFloors(parseRunHistory(SAMPLE_RUN_RAW));

  it("renders end-of-floor stats including HP and damage taken", () => {
    const root = renderFloorDetail(rows[1]!, undefined);
    const stats = root.querySelector(".viewer-floor-stats")?.textContent ?? "";
    expect(stats).toContain("76");
    expect(stats).toContain("80");
    expect(stats).toContain("Damage taken");
  });

  it("renders card_choices with the picked one highlighted", () => {
    const root = renderFloorDetail(rows[1]!, undefined);
    const picked = root.querySelectorAll(".viewer-choice-list li.is-picked");
    expect(picked).toHaveLength(1);
    expect(picked[0]!.textContent).toContain("CARD.BATTLE_TRANCE");
  });

  it("renders the Neow blessing choices for the ancient floor", () => {
    const root = renderFloorDetail(rows[0]!, undefined);
    expect(root.textContent).toContain("Neow's blessing");
    expect(root.textContent).toContain("GOLDEN_PEARL");
  });

  it("renders the event_choices for an event floor", () => {
    const root = renderFloorDetail(rows[2]!, undefined);
    expect(root.textContent).toContain("Event choice");
    expect(root.textContent).toContain("STONE_OF_ALL_TIME");
  });

  it("renders relic_choices for the boss floor", () => {
    const root = renderFloorDetail(rows[4]!, undefined);
    expect(root.textContent).toContain("Relic reward");
    expect(root.textContent).toContain("STRANGE_FRUIT");
  });

  it("renders entry loadout (relics + potions) when a combat timeline is present", () => {
    const timeline = parseTimeline(SAMPLE_TIMELINE_RAW);
    const root = renderFloorDetail(rows[1]!, timeline);
    const entry = root.querySelector(".viewer-entry-loadout");
    expect(entry).not.toBeNull();
    expect(entry!.textContent).toContain("On entry");
    expect(entry!.textContent).toContain("RELIC.GOLDEN_PEARL");
    expect(entry!.textContent).toContain("POTION.HEAL");
  });

  it("omits the entry loadout block when no timeline is available", () => {
    const root = renderFloorDetail(rows[2]!, undefined);
    expect(root.querySelector(".viewer-entry-loadout")).toBeNull();
  });
});

describe("renderRunHeader", () => {
  const runHistory = parseRunHistory(SAMPLE_RUN_RAW);

  it("renders outcome=VICTORY for a winning run", () => {
    const root = renderRunHeader(runHistory, "1779021871-42");
    expect(root.textContent).toContain("VICTORY");
    expect(root.textContent).toContain("seed 42");
    expect(root.textContent).toContain("CHARACTER.IRONCLAD");
  });

  it("renders final relics + potions + deck size", () => {
    const root = renderRunHeader(runHistory);
    expect(root.textContent).toContain("RELIC.GOLDEN_PEARL");
    expect(root.textContent).toContain("RELIC.STRANGE_FRUIT");
    expect(root.textContent).toContain("POTION.HEAL");
  });

  it("reports abandoned outcome when applicable", () => {
    const abandoned = { ...runHistory, win: false, was_abandoned: true };
    const root = renderRunHeader(abandoned);
    expect(root.textContent).toContain("abandoned");
  });

  it("reports killed_by_encounter when the run ended in death", () => {
    const dead = { ...runHistory, win: false, was_abandoned: false, killed_by_encounter: "ENCOUNTER.SOUL_FYSH_BOSS" };
    const root = renderRunHeader(dead);
    expect(root.textContent).toContain("killed by ENCOUNTER.SOUL_FYSH_BOSS");
  });
});

// ── Failing-bug coverage (item 1: HP / item 5: gold) ───────────────────
//
// Audit against the bundled sample surfaced 0/0 HP and 0 gold on every
// floor row. Root cause is the engine's `UpdatePlayerStatsInMapPointHistory`
// being gated on `if (TestMode.IsOn || State == null) return;` — and our
// bootstrap turns TestMode on. The fixture below mirrors the post-fix
// shape (HP/gold populated); these tests assert the renderer surfaces
// those values correctly.

describe("floor row + detail: HP and gold are surfaced from player_stats", () => {
  const runHistory = parseRunHistory(SAMPLE_RUN_NO_NEOW_RAW);
  const rows = materialiseFloors(runHistory);

  it("row label shows current_hp/max_hp, not 0/0", () => {
    // First row is the FUZZY_WURM_CRAWLER combat — current_hp=76/80.
    const onSelect = vi.fn();
    const list = renderFloorList(rows, null, onSelect);
    const firstRow = list.querySelectorAll(".viewer-floor-row")[0]!;
    expect(firstRow.textContent).toContain("HP 76/80");
    expect(firstRow.textContent).not.toContain("HP 0/0");
  });

  it("death marker (💀) appears only on the row where the player actually died", () => {
    const list = renderFloorList(rows, null, vi.fn());
    const allRows = list.querySelectorAll(".viewer-floor-row");
    const deadRows = Array.from(allRows).filter((el) => /💀/.test(el.textContent ?? ""));
    // The fixture has exactly one death — the boss floor.
    expect(deadRows).toHaveLength(1);
    expect(deadRows[0]!.textContent).toContain("boss");
  });

  it("detail pane shows current_gold from player_stats", () => {
    const monsterRow = rows[0]!; // current_gold=114
    const detail = renderFloorDetail(monsterRow, undefined);
    expect(detail.textContent).toContain("114");
  });
});

// ── Failing-bug coverage (items 2, 3, 4: combat attribution) ───────────
//
// Audit showed every combat was attached to the wrong row:
//   - first combat (manifest floor=2) was rendered on viewer floor=2,
//     which is an event row → user item 3 ("event has combat log").
//   - boss combat (manifest floor=17) had no matching viewer floor → user
//     item 2's mirror at the end of the run (boss combat log missing).
// Root cause: combatTimelineForFloor matches by manifest.floor, but the
// manifest's floor value is the engine's ActFloor (which counts Neow
// + skips/jumps), whereas the viewer's row.floor is the position in
// map_point_history. The two diverge; we must match by combat ordinal
// within the act.

describe("combatTimelineForFloor: matches by combat ordinal, not floor number", () => {
  // Build a session with the no-Neow fixture + three synthetic
  // timelines so we can identify which one got picked.
  function buildSession(): Session {
    const manifest = parseManifest(SAMPLE_MANIFEST_NO_NEOW_RAW);
    const tagged = (n: number) =>
      parseTimeline({
        ...SAMPLE_TIMELINE_RAW,
        events: [{
          index: 0,
          type: "GameAction",
          player_id: 1,
          action_type: "NetPlayCardAction",
          action: { card: { combat_card_index: n }, model_id: { category: "TAG", entry: `T${n}` } },
        }],
      });
    return {
      manifest,
      timelines: {
        "combats/act1-floor2-combat.mcr": tagged(0),
        "combats/act1-floor4-combat.mcr": tagged(1),
        "combats/act1-floor7-combat.mcr": tagged(2),
      },
      runHistory: parseRunHistory(SAMPLE_RUN_NO_NEOW_RAW),
    };
  }

  const rows = materialiseFloors(parseRunHistory(SAMPLE_RUN_NO_NEOW_RAW));
  // rows[0] = monster (combat #1) → timeline 0
  // rows[1] = event              → no timeline
  // rows[2] = monster (combat #2) → timeline 1
  // rows[3] = event              → no timeline
  // rows[4] = rest_site          → no timeline
  // rows[5] = boss (combat #3)    → timeline 2

  it("first combat-type row maps to the first manifest combat", () => {
    const session = buildSession();
    const t = combatTimelineForFloor(session, rows[0]!);
    expect(t).toBeDefined();
    // The first event's model_id entry encodes the timeline index.
    const e = t!.events[0]!;
    expect(e.type).toBe("GameAction");
    expect((e as { action: { model_id: { entry: string } } }).action.model_id.entry).toBe("T0");
  });

  it("third combat-type row (the boss) maps to the third manifest combat — NOT a no-match because the manifest's floor=7 happens to be present somewhere", () => {
    const session = buildSession();
    const t = combatTimelineForFloor(session, rows[5]!);
    expect(t).toBeDefined();
    expect((t!.events[0]! as { action: { model_id: { entry: string } } }).action.model_id.entry).toBe("T2");
  });

  it("an event-type row returns undefined even when its viewer-floor happens to collide with a manifest combat floor", () => {
    // rows[1] is the event at engine ActFloor=3 — no combat at floor 3
    // in the manifest, but rows[3] is the event at engine ActFloor=5
    // which (in some prior shapes) DID collide with manifest combat
    // floor=5. Either way, an event row must never produce a combat.
    const session = buildSession();
    expect(combatTimelineForFloor(session, rows[1]!)).toBeUndefined();
    expect(combatTimelineForFloor(session, rows[3]!)).toBeUndefined();
  });

  it("a rest_site row returns undefined", () => {
    const session = buildSession();
    expect(combatTimelineForFloor(session, rows[4]!)).toBeUndefined();
  });
});

// ── Failing-bug coverage: floor numbering matches engine ActFloor ──────
//
// Every current STS2 run starts with Neow, so a fresh run.json carries
// an "ancient" entry as the first map_point_history record (engine
// floor 1). Legacy recordings produced before Neow became mandatory
// could omit that entry, but the engine's floor counter still includes
// it — so the first map-point-history entry is engine floor 2, not
// floor 1. The viewer's `floor` field on each row must surface the
// engine's ActFloor, otherwise the floor labels don't match the
// in-game UI and don't match the manifest's combat floors.

describe("materialiseFloors: floor numbering matches engine ActFloor", () => {
  it("falls back to floor 2 for legacy recordings without an ancient (Neow) entry", () => {
    const rows = materialiseFloors(parseRunHistory(SAMPLE_RUN_NO_NEOW_RAW));
    expect(rows[0]!.floor).toBe(2);
    expect(rows[1]!.floor).toBe(3);
    expect(rows[2]!.floor).toBe(4);
    expect(rows[5]!.floor).toBe(7);
  });

  it("starts at floor 1 (Neow) when there's an ancient entry, increments to 2 for the next room", () => {
    const rows = materialiseFloors(parseRunHistory(SAMPLE_RUN_RAW));
    expect(rows[0]!.mapPointType).toBe("ancient");
    expect(rows[0]!.floor).toBe(1);
    expect(rows[1]!.floor).toBe(2);
    expect(rows[2]!.floor).toBe(3);
  });
});

// ── Failing-bug coverage: combat detail clean-up ───────────────────────

describe("renderFloorDetail: combat detail rendering", () => {
  it("turn markers do NOT include the [checksum N] prefix and live inside the merged combat-log", () => {
    const rows = materialiseFloors(parseRunHistory(SAMPLE_RUN_NO_NEOW_RAW));
    const timeline = parseTimeline(SAMPLE_TIMELINE_RAW);
    const root = renderFloorDetail(rows[0]!, timeline);
    const turnEntries = root.querySelectorAll(".viewer-combat-log li[data-kind='turn']");
    expect(turnEntries.length).toBeGreaterThan(0);
    for (const li of turnEntries) {
      expect(li.textContent).not.toMatch(/\[checksum/);
    }
  });

  it("combat-log interleaves turn markers and events into a single chronological list", () => {
    const rows = materialiseFloors(parseRunHistory(SAMPLE_RUN_NO_NEOW_RAW));
    const timeline = parseTimeline(SAMPLE_TIMELINE_RAW);
    const root = renderFloorDetail(rows[0]!, timeline);
    // Single list, not two.
    expect(root.querySelectorAll(".viewer-combat-log")).toHaveLength(1);
    expect(root.querySelectorAll(".viewer-turn-list")).toHaveLength(0);
    // Both kinds appear under the same parent ol.
    const all = root.querySelectorAll(".viewer-combat-log > li");
    expect(all.length).toBe(timeline.events.length + timeline.checksums.length);
    const kinds = Array.from(all, (li) => (li as HTMLElement).dataset["kind"]);
    expect(kinds).toContain("turn");
    expect(kinds).toContain("event");
  });
});
