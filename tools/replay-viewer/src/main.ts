import { parseManifest, parseTimeline } from "./core/parse";
import { ingestFiles, type Session } from "./core/session";
import { loadSession, saveSession, clearSession, type RawSessionInput } from "./core/persistence";
import { renderRunOverview, renderCombatTimeline } from "./view/render";
import {
  combatTimelineForFloor,
  materialiseFloors,
  renderFloorDetail,
  renderFloorList,
  renderRunHeader,
  type FloorRow,
} from "./view/floors";
import { fetchRunsIndex, loadRunByRelPath } from "./core/replay-source";
import type { ReplayRunIndexEntry } from "./core/runs-index";

// Entry point. Three load paths, in priority order:
//
//   1. Sidebar (primary in dev): on load we GET /replays/runs.json; if
//      present, we render a list of runs in the left sidebar. Clicking
//      a row pulls that run's manifest + timelines + run.json via HTTP
//      and renders it.
//
//   2. Directory picker (fallback): user picks a run directory; we read
//      manifest.json + every *.mcr.timeline.json inside, build a
//      Session, persist the raw JSON to localStorage, and render.
//
//   3. Single-file fallback: manifest or timeline directly. Manifest →
//      Session-without-timelines. Timeline → single-combat view.
//
// localStorage restore is consulted on initial load so a reload keeps
// what the user was looking at.

const $directoryInput = mustFindInput("directory-input");
const $manifestInput = mustFindInput("manifest-input");
const $timelineInput = mustFindInput("timeline-input");
const $clearBtn = mustFindEl("clear-session");
const $output = mustFindEl("output");
const $detail = mustFindEl("detail");
const $errors = mustFindEl("errors");
const $status = mustFindEl("status");
const $runsList = mustFindEl("runs-list");
const $sidebarStatus = mustFindEl("sidebar-status");
const $sidebarEmpty = mustFindEl("sidebar-empty");
const $sidebarCollapse = mustFindEl("sidebar-collapse");
const $sidebarShow = mustFindEl("sidebar-show");
const $reloadRuns = mustFindEl("reload-runs");

let currentSession: Session | null = null;
let selectedMcr: string | null = null;
let materialisedFloors: FloorRow[] = [];
let selectedFloorIndex: number | null = null;
let selectedRunRelPath: string | null = null;

// ── Sidebar ─────────────────────────────────────────────────────────

const SIDEBAR_STATE_KEY = "sts2-replay-viewer:sidebar-collapsed";

function setSidebarCollapsed(collapsed: boolean): void {
  document.body.classList.toggle("sidebar-collapsed", collapsed);
  window.localStorage.setItem(SIDEBAR_STATE_KEY, collapsed ? "1" : "0");
}

function initSidebarToggle(): void {
  setSidebarCollapsed(window.localStorage.getItem(SIDEBAR_STATE_KEY) === "1");

  $sidebarCollapse.addEventListener("click", () => setSidebarCollapsed(true));
  $sidebarShow.addEventListener("click", () => setSidebarCollapsed(false));

  // Backslash to toggle — a single unmodified key that doesn't conflict
  // with browser shortcuts and is reachable on every common layout.
  document.addEventListener("keydown", (e) => {
    if (e.key === "\\" && !e.metaKey && !e.ctrlKey && !e.altKey) {
      const target = e.target as HTMLElement | null;
      if (target && (target.tagName === "INPUT" || target.tagName === "TEXTAREA")) return;
      setSidebarCollapsed(!document.body.classList.contains("sidebar-collapsed"));
    }
  });
}

async function refreshSidebar(): Promise<void> {
  $sidebarStatus.textContent = "loading…";
  $runsList.replaceChildren();
  $sidebarEmpty.hidden = true;

  const { index, reason } = await fetchRunsIndex();
  if (!index) {
    $sidebarStatus.textContent = "";
    $sidebarEmpty.hidden = false;
    $sidebarEmpty.textContent =
      reason ?? "No runs.json available — record a run, or open a run from disk via the panel below.";
    return;
  }
  $sidebarStatus.textContent = `${index.runs.length} run${index.runs.length === 1 ? "" : "s"}`;
  if (index.runs.length === 0) {
    $sidebarEmpty.hidden = false;
    $sidebarEmpty.textContent = "No runs yet — record one and click ↻.";
    return;
  }
  for (const entry of index.runs) {
    $runsList.appendChild(renderRunRow(entry));
  }
  highlightSelectedRow();
}

function renderRunRow(entry: ReplayRunIndexEntry): HTMLLIElement {
  const li = document.createElement("li");
  const btn = document.createElement("button");
  btn.type = "button";
  btn.className = "run-row";
  btn.dataset["relPath"] = entry.rel_path;

  const outcomeBadge = document.createElement("span");
  outcomeBadge.className = `run-row-outcome ${entry.outcome}`;
  outcomeBadge.textContent = entry.outcome;

  const name = document.createElement("span");
  name.className = "run-row-name";
  name.append(outcomeBadge, document.createTextNode(entry.display_name));

  const meta = document.createElement("span");
  meta.className = "run-row-meta";
  meta.textContent = `seed=${entry.seed} · ${entry.combat_count} combats · ${entry.game_version}`;

  btn.append(name, meta);
  btn.addEventListener("click", () => void selectRun(entry));
  li.appendChild(btn);
  return li;
}

function highlightSelectedRow(): void {
  for (const btn of $runsList.querySelectorAll<HTMLButtonElement>(".run-row")) {
    btn.classList.toggle("is-selected", btn.dataset["relPath"] === selectedRunRelPath);
  }
}

async function selectRun(entry: ReplayRunIndexEntry): Promise<void> {
  await tryRender(async () => {
    setStatus(`loading ${entry.display_name}…`);
    const session = await loadRunByRelPath(entry);
    currentSession = session;
    selectedMcr = null;
    selectedFloorIndex = null;
    materialisedFloors = session.runHistory ? materialiseFloors(session.runHistory) : [];
    selectedRunRelPath = entry.rel_path;
    highlightSelectedRow();
    rerender();
    setStatus(
      session.runHistory
        ? `loaded ${entry.display_name} (${materialisedFloors.length} floors)`
        : `loaded ${entry.display_name} (no run.json yet)`,
    );
  });
}

initSidebarToggle();
$reloadRuns.addEventListener("click", () => void refreshSidebar());
void refreshSidebar();

// ── Existing flows: localStorage restore + file pickers ─────────────

// Try restore on initial load.
const restored = loadSession();
if (restored) {
  currentSession = restored;
  materialisedFloors = restored.runHistory ? materialiseFloors(restored.runHistory) : [];
  rerender();
  setStatus("restored from last session");
}

$directoryInput.addEventListener("change", async () => {
  const files = Array.from($directoryInput.files ?? []);
  if (files.length === 0) return;
  await tryRender(async () => {
    const inputs: { path: string; text: string }[] = [];
    const rawTimelines: Record<string, string> = {};
    let manifestJson: string | undefined;
    let manifestRelPath: string | undefined;
    let runJson: string | undefined;
    for (const file of files) {
      const path = (file as File & { webkitRelativePath?: string }).webkitRelativePath || file.name;
      const isManifest = path.endsWith("/manifest.json") || path === "manifest.json";
      const isTimeline = path.endsWith(".mcr.timeline.json");
      const isRun = path.endsWith("/run.json") || path === "run.json";
      if (!isManifest && !isTimeline && !isRun) continue;
      const text = await file.text();
      inputs.push({ path, text });
      if (isManifest) {
        manifestJson = text;
        manifestRelPath = path;
      }
    }
    const session = ingestFiles(inputs);
    currentSession = session;
    selectedMcr = null;
    selectedFloorIndex = null;
    materialisedFloors = session.runHistory ? materialiseFloors(session.runHistory) : [];
    selectedRunRelPath = null;
    highlightSelectedRow();
    // Build the raw-JSON snapshot for persistence. We can't reuse the
    // `inputs` array directly because the timeline keys for persistence
    // must match the manifest's `mcr_file` paths exactly (the ingest
    // strips the leading run-id dir; persistence keys do the same).
    if (manifestJson && manifestRelPath !== undefined) {
      const manifestDir = manifestRelPath.includes("/") ? manifestRelPath.slice(0, manifestRelPath.lastIndexOf("/")) : "";
      for (const f of inputs) {
        if (f.path.endsWith(".mcr.timeline.json")) {
          const rel = manifestDir && f.path.startsWith(manifestDir + "/")
            ? f.path.slice(manifestDir.length + 1)
            : f.path;
          const mcrFile = rel.slice(0, -".timeline.json".length);
          rawTimelines[mcrFile] = f.text;
        } else if (f.path === `${manifestDir}/run.json` || (manifestDir === "" && f.path === "run.json")) {
          runJson = f.text;
        }
      }
      const snapshot: RawSessionInput = { manifestJson, timelineJsons: rawTimelines };
      if (runJson !== undefined) snapshot.runJson = runJson;
      if (session.label !== undefined) snapshot.label = session.label;
      saveSession(snapshot);
    }
    rerender();
    setStatus(
      session.runHistory
        ? `loaded run with ${materialisedFloors.length} floors`
        : `loaded ${session.manifest.combats.length} combats (no run.json)`,
    );
  });
});

$manifestInput.addEventListener("change", async () => {
  const file = $manifestInput.files?.[0];
  if (!file) return;
  await tryRender(async () => {
    const text = await file.text();
    const manifest = parseManifest(JSON.parse(text));
    currentSession = { manifest, timelines: {} };
    selectedMcr = null;
    rerender();
    setStatus("loaded manifest only (no timelines)");
  });
});

$timelineInput.addEventListener("change", async () => {
  const file = $timelineInput.files?.[0];
  if (!file) return;
  await tryRender(async () => {
    const text = await file.text();
    const timeline = parseTimeline(JSON.parse(text));
    currentSession = null;
    selectedMcr = null;
    $output.replaceChildren();
    $detail.replaceChildren(renderCombatTimeline(timeline));
    setStatus(`loaded ${file.name}`);
  });
});

$clearBtn.addEventListener("click", () => {
  clearSession();
  currentSession = null;
  selectedMcr = null;
  selectedFloorIndex = null;
  materialisedFloors = [];
  selectedRunRelPath = null;
  highlightSelectedRow();
  $output.replaceChildren();
  $detail.replaceChildren();
  setStatus("cleared");
});

function rerender(): void {
  if (!currentSession) {
    $output.replaceChildren();
    $detail.replaceChildren();
    return;
  }
  if (currentSession.runHistory && materialisedFloors.length > 0) {
    rerenderRunView(currentSession.runHistory);
  } else {
    rerenderManifestView();
  }
}

function rerenderRunView(runHistory: import("./core/run-history").RunHistory): void {
  const header = renderRunHeader(runHistory, currentSession?.label);
  const list = renderFloorList(materialisedFloors, selectedFloorIndex, (i) => {
    selectedFloorIndex = i;
    renderSelectedFloor();
    // Update class on rows without re-rendering the whole list.
    for (const btn of $output.querySelectorAll<HTMLButtonElement>(".viewer-floor-row")) {
      btn.classList.toggle("is-selected", btn.dataset["floorIndex"] === String(i));
    }
  });
  $output.replaceChildren(header, list);

  // Auto-select the last floor — most informative landing state
  // (final HP, last reward picks, last combat). Skip if the user
  // already had a selection (post-restore).
  if (selectedFloorIndex === null) selectedFloorIndex = materialisedFloors.length - 1;
  renderSelectedFloor();
  for (const btn of $output.querySelectorAll<HTMLButtonElement>(".viewer-floor-row")) {
    btn.classList.toggle("is-selected", btn.dataset["floorIndex"] === String(selectedFloorIndex));
  }
}

function renderSelectedFloor(): void {
  if (selectedFloorIndex === null || !currentSession) {
    $detail.replaceChildren();
    return;
  }
  const row = materialisedFloors[selectedFloorIndex];
  if (!row) {
    $detail.replaceChildren();
    return;
  }
  const timeline = combatTimelineForFloor(currentSession, row);
  $detail.replaceChildren(renderFloorDetail(row, timeline));
}

function rerenderManifestView(): void {
  $output.replaceChildren(renderRunOverview(currentSession!, selectedMcr, (mcrFile) => {
    selectedMcr = mcrFile;
    const timeline = currentSession?.timelines[mcrFile];
    if (timeline) {
      $detail.replaceChildren(renderCombatTimeline(timeline));
      for (const btn of $output.querySelectorAll<HTMLButtonElement>(".viewer-combat-row")) {
        btn.classList.toggle("is-selected", btn.dataset["mcrFile"] === mcrFile);
      }
    }
  }));
  const firstWithTimeline = currentSession!.manifest.combats.find((c) => c.mcr_file in currentSession!.timelines);
  if (firstWithTimeline) {
    selectedMcr = firstWithTimeline.mcr_file;
    $detail.replaceChildren(renderCombatTimeline(currentSession!.timelines[firstWithTimeline.mcr_file]!));
    for (const btn of $output.querySelectorAll<HTMLButtonElement>(".viewer-combat-row")) {
      btn.classList.toggle("is-selected", btn.dataset["mcrFile"] === firstWithTimeline.mcr_file);
    }
  } else {
    $detail.replaceChildren();
  }
}

async function tryRender(fn: () => Promise<void>): Promise<void> {
  $errors.textContent = "";
  try { await fn(); }
  catch (err) { $errors.textContent = err instanceof Error ? err.message : String(err); }
}

function setStatus(text: string): void {
  $status.textContent = text;
}

function mustFindEl(id: string): HTMLElement {
  const el = document.getElementById(id);
  if (!el) throw new Error(`#${id} not found in page`);
  return el;
}

function mustFindInput(id: string): HTMLInputElement {
  const el = mustFindEl(id);
  if (!(el instanceof HTMLInputElement)) throw new Error(`#${id} is not an <input>`);
  return el;
}
