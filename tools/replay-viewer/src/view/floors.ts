import type { CombatTimeline } from "../core/types";
import { buildCombatLog } from "../core/combat-log";
import type {
  HistoryAncientChoice,
  HistoryCardChoice,
  HistoryCardRef,
  HistoryEventChoice,
  HistoryOwnedPotion,
  HistoryRelicChoice,
  MapPointHistoryEntry,
  MapPointPlayerStats,
  RunHistory,
} from "../core/run-history";
import type { Session } from "../core/session";
import { summarizeEvent } from "../core/summary";

// Full-run timeline view — the spine is run.json's
// `map_point_history` (per-act, per-floor list). Each floor surfaces
// room type, end-of-floor HP, rewards / choices made, and (for
// combats) expands into the per-turn combat detail derived from the
// matching `*.mcr.timeline.json`.
//
// "Floor" here means a single map-point entry; the game models the
// run as a list of map points, each holding one or more rooms. Most
// map points correspond to a single floor on the map; some
// (Neow/ancient at floor 0) are zero-step events. We render every
// entry as a row in the floor list so nothing is hidden, and label
// the row with the floor number derived from the cumulative room
// count.

// Combat-type rooms — these are the ones that produce a `.mcr` and a
// `*.mcr.timeline.json`. Non-combat rooms (event / rest_site /
// treasure / shop) never have a timeline.
const COMBAT_ROOM_TYPES = new Set(["monster", "elite", "boss"]);

// One row in the floor list, materialised from a run.json map-point
// entry. We pre-compute the floor number, room slug, and the
// end-of-floor player snapshot so the renderer is mechanical.
export interface FloorRow {
  // Engine ActFloor for this row. Matches the floor number the
  // game's UI displays and the floor number stamped into manifest
  // combat entries. Neow is engine floor 1 when present (an "ancient"
  // map_point_history entry); when Neow was skipped (`withNeow=false`
  // at recording time) the engine still counts it as floor 1, so the
  // first map_point_history entry is engine floor 2.
  floor: number;
  actIndex: number;
  mapPointType: string;
  primaryRoomType: string;
  primaryEncounter?: string;
  // 0-indexed ordinal of this row among combat-type rows in this
  // act. undefined when the row is not a combat. This is what the
  // viewer uses to look up the matching `.mcr.timeline.json` — the
  // manifest's `floor` field is the engine ActFloor and doesn't
  // align with `row.floor` cleanly across versions, but the ordinal
  // ("the N-th combat in this act") is a stable join key.
  combatOrdinal?: number;
  // End-of-floor snapshot for player 0 (single-player runs); null
  // for entries the game emitted without player_stats (rare).
  playerStats?: MapPointPlayerStats;
  // The raw entry, kept so the detail renderer can drill in.
  entry: MapPointHistoryEntry;
}

// The user sees their selected floor at the row level; clicking
// passes the index back so we can fetch detail + the matching
// combat timeline (if any).
export interface FloorSelection {
  floor: number;
  actIndex: number;
  index: number; // index into the flat materialised list
}

// Walks the map_point_history into a single flat list of floor
// rows. Floor numbers come from the `floor` field on the manifest
// when available (combat rooms) — for non-combat rooms we fall
// back to the position within the act, which produces a stable
// per-run ordering even when the game didn't record a floor number
// directly. The "ancient" map point (Neow) is rendered as floor 0.
export function materialiseFloors(runHistory: RunHistory): FloorRow[] {
  const rows: FloorRow[] = [];
  runHistory.map_point_history.forEach((act, actIndex) => {
    // Floor numbering rules:
    //   - "ancient" entry (Neow) is engine floor 1.
    //   - If there's no ancient entry in act 0, Neow was skipped
    //     (`withNeow=false`) but the engine still counts it as
    //     floor 1, so the first non-ancient row in act 0 is engine
    //     floor 2.
    //   - For acts after the first, we anchor at floor 1 of that
    //     act (no Neow in subsequent acts).
    //
    // We walk the act and increment a counter from the right base.
    const firstIsAncient = act.length > 0 && act[0]!.map_point_type === "ancient";
    // Engine floor for the FIRST entry in this act:
    //   act 0 + ancient    → 1 (Neow)
    //   act 0 + no ancient → 2 (Neow skipped, still counted)
    //   act N>0            → 1 (first room of the act)
    let nextFloor: number;
    if (actIndex === 0) {
      nextFloor = firstIsAncient ? 1 : 2;
    } else {
      nextFloor = 1;
    }
    let combatOrdinal = 0;
    for (const entry of act) {
      const primaryRoom = entry.rooms?.[0];
      const primaryRoomType = primaryRoom?.room_type ?? entry.map_point_type;
      const row: FloorRow = {
        floor: nextFloor,
        actIndex,
        mapPointType: entry.map_point_type,
        primaryRoomType,
        entry,
      };
      if (primaryRoom?.model_id !== undefined) row.primaryEncounter = primaryRoom.model_id;
      if (entry.player_stats !== undefined && entry.player_stats[0] !== undefined) {
        row.playerStats = entry.player_stats[0];
      }
      if (COMBAT_ROOM_TYPES.has(primaryRoomType)) {
        row.combatOrdinal = combatOrdinal;
        combatOrdinal += 1;
      }
      rows.push(row);
      nextFloor += 1;
    }
  });
  return rows;
}

// Maps a floor row to the manifest's combat entry (and from there to
// the timeline.json) when there's one — only combat-type rooms have
// a matching .mcr. The match is by *combat ordinal within the act*,
// not by `floor` field: the manifest's `floor` is the engine ActFloor
// at combat-end time, which doesn't align with our row.floor when
// recording started mid-walk or when Neow numbering shifts. The N-th
// combat-type row in an act always corresponds to the N-th combat in
// `manifest.combats` for that act.
//
// Non-combat rows (event / rest_site / treasure / shop) return
// undefined — the lookup must never silently steal a combat from a
// later row just because their floor numbers happened to align.
export function combatTimelineForFloor(session: Session, row: FloorRow): CombatTimeline | undefined {
  if (row.combatOrdinal === undefined) return undefined;
  const combatsInAct = session.manifest.combats.filter((c) => c.act_index === row.actIndex);
  const match = combatsInAct[row.combatOrdinal];
  return match ? session.timelines[match.mcr_file] : undefined;
}

// ── Renderers ────────────────────────────────────────────────────────

export function renderFloorList(
  rows: readonly FloorRow[],
  selectedIndex: number | null,
  onSelect: (index: number) => void,
): HTMLElement {
  const root = document.createElement("section");
  root.className = "viewer-floor-list";
  const ol = document.createElement("ol");
  ol.className = "viewer-floor-rows";
  rows.forEach((row, i) => {
    const li = document.createElement("li");
    const btn = document.createElement("button");
    btn.type = "button";
    btn.className = "viewer-floor-row";
    btn.dataset["floorIndex"] = String(i);
    if (i === selectedIndex) btn.classList.add("is-selected");
    const hp = row.playerStats ? `${row.playerStats.current_hp}/${row.playerStats.max_hp}` : "—";
    const deadMark = row.playerStats?.current_hp === 0 ? " 💀" : "";
    const encounter = row.primaryEncounter ? ` · ${row.primaryEncounter}` : "";
    btn.textContent = `act ${row.actIndex + 1} · floor ${row.floor} · ${row.primaryRoomType}${encounter} — HP ${hp}${deadMark}`;
    btn.addEventListener("click", () => onSelect(i));
    li.appendChild(btn);
    ol.appendChild(li);
  });
  root.appendChild(ol);
  return root;
}

// Top-of-detail-pane summary for a selected floor: room type, exit
// HP, gold delta, plus any choice/reward arrays from
// `MapPointPlayerStats`. Combat content (per-turn HP, event stream)
// is appended below if a timeline is available.
export function renderFloorDetail(row: FloorRow, timeline: CombatTimeline | undefined): HTMLElement {
  const root = document.createElement("section");
  root.className = "viewer-floor-detail";

  const heading = document.createElement("h3");
  heading.textContent = `Act ${row.actIndex + 1} · Floor ${row.floor} — ${row.primaryRoomType}`;
  root.appendChild(heading);

  if (row.primaryEncounter) {
    const sub = document.createElement("p");
    sub.className = "viewer-floor-subtitle";
    sub.textContent = row.primaryEncounter;
    root.appendChild(sub);
  }

  const entryBlock = renderEntryLoadout(timeline);
  if (entryBlock) root.appendChild(entryBlock);

  if (row.playerStats) {
    root.appendChild(renderEndOfFloorStats(row.playerStats));
  }

  if (row.entry.player_stats?.[0]) {
    const stats = row.entry.player_stats[0];
    appendChoiceSections(root, stats);
  }

  if (timeline) {
    root.appendChild(renderCombatDetail(timeline));
  }

  return root;
}

// Per-room "what was on the player" header — relics + potions + HP +
// gold. Sourced from the player_stats snapshot AT THIS FLOOR (which
// is end-of-floor, not start-of-floor — the game's RunHistory only
// records end snapshots). Renders inside renderFloorDetail's header
// block.
function renderEndOfFloorStats(stats: MapPointPlayerStats): HTMLElement {
  const dl = document.createElement("dl");
  dl.className = "viewer-floor-stats";
  appendDef(dl, "HP (end of floor)", `${stats.current_hp} / ${stats.max_hp}${stats.current_hp === 0 ? " — died" : ""}`);
  if (stats.damage_taken !== undefined && stats.damage_taken > 0) {
    appendDef(dl, "Damage taken", String(stats.damage_taken));
  }
  if (stats.hp_healed !== undefined && stats.hp_healed > 0) {
    appendDef(dl, "HP healed", String(stats.hp_healed));
  }
  if (stats.current_gold !== undefined) {
    appendDef(dl, "Gold", String(stats.current_gold));
  }
  return dl;
}

// "What did the player walk INTO this floor with?" Item 5 from the
// user redesign: a top-of-room block showing the relics + potions
// the player had on entry. Sourced from the *combat's initial_run*
// when a timeline is available (the most accurate source — captured
// at combat start), with a fallback string for non-combat rooms
// (where we'd need the previous floor's end-of-floor snapshot to
// derive entry state, which run.json doesn't directly give us
// because relics/potions aren't tracked per map-point).
function renderEntryLoadout(timeline: CombatTimeline | undefined): HTMLElement | null {
  if (!timeline) return null;
  // initial_run is the SerializableRun at combat start. The shape is
  // the engine's full save schema; for our purposes the relevant
  // bits are `players[0].relics` and `players[0].potions`. We pluck
  // them defensively — the run can be a partial / older version
  // shape; missing fields just render as "—".
  const initial = timeline.initial_run as Record<string, unknown> | null;
  const players = (initial && Array.isArray((initial as Record<string, unknown>)["players"]))
    ? ((initial as { players: unknown[] }).players)
    : [];
  const player = players[0] as Record<string, unknown> | undefined;
  if (!player) return null;
  const relics = pickIdList(player["relics"]);
  const potions = pickIdList(player["potions"]);
  if (relics.length === 0 && potions.length === 0) return null;

  const wrap = document.createElement("section");
  wrap.className = "viewer-floor-section viewer-entry-loadout";
  const h = document.createElement("h4");
  h.textContent = "On entry";
  wrap.appendChild(h);
  const dl = document.createElement("dl");
  dl.className = "viewer-floor-stats";
  appendDef(dl, "Relics", relics.length > 0 ? relics.join(", ") : "—");
  appendDef(dl, "Potions", potions.length > 0 ? potions.join(", ") : "—");
  wrap.appendChild(dl);
  return wrap;
}

function pickIdList(raw: unknown): string[] {
  if (!Array.isArray(raw)) return [];
  const out: string[] = [];
  for (const item of raw) {
    if (typeof item === "object" && item !== null) {
      const rec = item as Record<string, unknown>;
      // Relic/potion entries serialise their ModelId as a nested
      // record with a top-level `id` string (e.g.
      // `"id": "RELIC.GOLDEN_PEARL"`) or as the full ModelId object
      // (e.g. `{ category, entry }`). Cover both shapes.
      if (typeof rec["id"] === "string") {
        out.push(rec["id"] as string);
      } else if (rec["model_id"] && typeof rec["model_id"] === "object") {
        const mid = rec["model_id"] as Record<string, unknown>;
        const cat = typeof mid["category"] === "string" ? mid["category"] : "";
        const entry = typeof mid["entry"] === "string" ? mid["entry"] : "";
        if (cat || entry) out.push(cat && entry ? `${cat}.${entry}` : entry || cat);
      }
    }
  }
  return out;
}

function appendChoiceSections(root: HTMLElement, stats: MapPointPlayerStats): void {
  if (stats.card_choices && stats.card_choices.length > 0) {
    root.appendChild(renderCardChoices(stats.card_choices));
  }
  if (stats.cards_gained && stats.cards_gained.length > 0) {
    root.appendChild(renderCardList("Cards gained", stats.cards_gained));
  }
  if (stats.cards_removed && stats.cards_removed.length > 0) {
    root.appendChild(renderCardList("Cards removed", stats.cards_removed));
  }
  if (stats.cards_transformed && stats.cards_transformed.length > 0) {
    const wrap = document.createElement("section");
    wrap.className = "viewer-floor-section";
    const h = document.createElement("h4");
    h.textContent = "Cards transformed";
    wrap.appendChild(h);
    const ul = document.createElement("ul");
    for (const t of stats.cards_transformed) {
      const li = document.createElement("li");
      li.textContent = `${t.original_card.id}  →  ${t.final_card.id}`;
      ul.appendChild(li);
    }
    wrap.appendChild(ul);
    root.appendChild(wrap);
  }
  if (stats.relic_choices && stats.relic_choices.length > 0) {
    root.appendChild(renderRelicChoices(stats.relic_choices));
  }
  if (stats.event_choices && stats.event_choices.length > 0) {
    root.appendChild(renderEventChoices(stats.event_choices));
  }
  if (stats.ancient_choice && stats.ancient_choice.length > 0) {
    root.appendChild(renderAncientChoices(stats.ancient_choice));
  }
}

function renderCardChoices(choices: HistoryCardChoice[]): HTMLElement {
  const wrap = document.createElement("section");
  wrap.className = "viewer-floor-section";
  const h = document.createElement("h4");
  h.textContent = "Card reward";
  wrap.appendChild(h);
  const ul = document.createElement("ul");
  ul.className = "viewer-choice-list";
  for (const c of choices) {
    const li = document.createElement("li");
    if (c.was_picked) li.classList.add("is-picked");
    li.textContent = `${c.was_picked ? "✔" : "·"}  ${c.card.id}`;
    ul.appendChild(li);
  }
  wrap.appendChild(ul);
  return wrap;
}

function renderCardList(title: string, refs: HistoryCardRef[]): HTMLElement {
  const wrap = document.createElement("section");
  wrap.className = "viewer-floor-section";
  const h = document.createElement("h4");
  h.textContent = title;
  wrap.appendChild(h);
  const ul = document.createElement("ul");
  for (const r of refs) {
    const li = document.createElement("li");
    li.textContent = r.id;
    ul.appendChild(li);
  }
  wrap.appendChild(ul);
  return wrap;
}

function renderRelicChoices(choices: HistoryRelicChoice[]): HTMLElement {
  const wrap = document.createElement("section");
  wrap.className = "viewer-floor-section";
  const h = document.createElement("h4");
  h.textContent = "Relic reward";
  wrap.appendChild(h);
  const ul = document.createElement("ul");
  ul.className = "viewer-choice-list";
  for (const c of choices) {
    const li = document.createElement("li");
    if (c.was_picked) li.classList.add("is-picked");
    li.textContent = `${c.was_picked ? "✔" : "·"}  ${c.choice ?? "(no choice)"}`;
    ul.appendChild(li);
  }
  wrap.appendChild(ul);
  return wrap;
}

function renderEventChoices(choices: HistoryEventChoice[]): HTMLElement {
  const wrap = document.createElement("section");
  wrap.className = "viewer-floor-section";
  const h = document.createElement("h4");
  h.textContent = "Event choice";
  wrap.appendChild(h);
  const ul = document.createElement("ul");
  for (const c of choices) {
    const li = document.createElement("li");
    li.textContent = `${c.title.key} (${c.title.table})`;
    ul.appendChild(li);
  }
  wrap.appendChild(ul);
  return wrap;
}

function renderAncientChoices(choices: HistoryAncientChoice[]): HTMLElement {
  const wrap = document.createElement("section");
  wrap.className = "viewer-floor-section";
  const h = document.createElement("h4");
  h.textContent = "Neow's blessing";
  wrap.appendChild(h);
  const ul = document.createElement("ul");
  ul.className = "viewer-choice-list";
  for (const c of choices) {
    const li = document.createElement("li");
    if (c.was_chosen) li.classList.add("is-picked");
    li.textContent = `${c.was_chosen ? "✔" : "·"}  ${c.text_key}`;
    ul.appendChild(li);
  }
  wrap.appendChild(ul);
  return wrap;
}

// Per-floor combat detail: ONE chronological log interleaving turn
// boundaries (with HP + block snapshots) and game-action events. The
// .mcr stores these as two separate arrays; `buildCombatLog` weaves
// them back together using the engine's known firing pattern (see
// core/combat-log.ts).
//
// Each `<li>` is tagged with `data-kind="turn" | "event"` so styling
// can distinguish the two without splitting the list, and so e2e
// tests can navigate either subset by class selector while still
// asserting overall chronology.
function renderCombatDetail(timeline: CombatTimeline): HTMLElement {
  const wrap = document.createElement("section");
  wrap.className = "viewer-floor-section viewer-combat-detail";
  const h = document.createElement("h4");
  h.textContent = "Combat log";
  wrap.appendChild(h);

  const list = document.createElement("ol");
  list.className = "viewer-combat-log";
  const log = buildCombatLog(timeline);
  for (const item of log) {
    const li = document.createElement("li");
    if (item.kind === "turn") {
      li.dataset["kind"] = "turn";
      const player = item.checksum.state?.creatures.find((c) => c.kind === "player");
      const hp = player ? `${player.current_hp}/${player.max_hp}` : "?";
      const block = player ? ` · block ${player.block}` : "";
      // The checksum id is internal detail (multiplayer-sync index);
      // the user just wants the human-readable phase tag + HP/block.
      li.textContent = `— ${item.checksum.context} — HP ${hp}${block} —`;
    } else if (item.kind === "enemy") {
      li.dataset["kind"] = "enemy";
      // The .mcr doesn't record what each enemy did — only the
      // net HP / block delta between "After enemy turn start" and
      // "After enemy turn end". Render in a way that makes the
      // synthetic origin obvious (so a future "real intent
      // log" doesn't look like the same kind of entry).
      const parts: string[] = [];
      if (item.damageDealt > 0) parts.push(`-${item.damageDealt} HP`);
      if (item.blockAbsorbed > 0) parts.push(`block absorbed ${item.blockAbsorbed}`);
      li.textContent = `⚔ enemy acts — ${parts.join(", ")}`;
    } else {
      li.dataset["kind"] = "event";
      li.dataset["eventType"] = item.event.type;
      li.dataset["eventIndex"] = String(item.event.index);
      li.textContent = `[${String(item.event.index).padStart(3, "0")}] ${summarizeEvent(item.event)}`;
    }
    list.appendChild(li);
  }
  wrap.appendChild(list);
  return wrap;
}

// Top-of-run summary: who, what, how it ended. Rendered above the
// floor list so the user knows which run they're looking at without
// scanning rows.
export function renderRunHeader(runHistory: RunHistory, label?: string): HTMLElement {
  const root = document.createElement("section");
  root.className = "viewer-run-header";
  const heading = document.createElement("h2");
  const labelText = label ? `Run ${label}` : `Run`;
  heading.textContent = `${labelText} — seed ${runHistory.seed} · ${runHistory.players[0]?.character ?? "?"} (asc ${runHistory.ascension})`;
  root.appendChild(heading);

  const dl = document.createElement("dl");
  dl.className = "viewer-meta";
  const outcome = runHistory.win ? "VICTORY"
    : runHistory.was_abandoned ? "abandoned"
    : runHistory.killed_by_encounter !== "NONE.NONE" ? `killed by ${runHistory.killed_by_encounter}`
    : runHistory.killed_by_event !== "NONE.NONE" ? `killed by ${runHistory.killed_by_event}`
    : "unfinished";
  appendDef(dl, "Outcome", outcome);
  appendDef(dl, "Build", runHistory.build_id);
  appendDef(dl, "Run time", formatDuration(runHistory.run_time));
  appendDef(dl, "Started", new Date(runHistory.start_time * 1000).toISOString());
  const player = runHistory.players[0];
  if (player) {
    const relics = player.relics?.map((r) => r.id).join(", ") ?? "—";
    const potions = player.potions?.map((p: HistoryOwnedPotion) => p.id).join(", ") ?? "—";
    appendDef(dl, "Final relics", relics);
    appendDef(dl, "Final potions", potions);
    appendDef(dl, "Final deck size", String(player.deck?.length ?? 0));
  }
  root.appendChild(dl);
  return root;
}

function formatDuration(seconds: number): string {
  if (seconds <= 0) return "0s";
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  const s = seconds % 60;
  return h > 0 ? `${h}h ${m}m ${s}s` : m > 0 ? `${m}m ${s}s` : `${s}s`;
}

function appendDef(dl: HTMLElement, label: string, value: string): void {
  const dt = document.createElement("dt");
  dt.textContent = label;
  const dd = document.createElement("dd");
  dd.textContent = value;
  dl.appendChild(dt);
  dl.appendChild(dd);
}
