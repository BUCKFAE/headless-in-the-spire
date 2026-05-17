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

// Entry point. Two load paths:
//
//   1. Directory picker (primary): user picks a run directory; we read
//      manifest.json + every *.mcr.timeline.json inside, build a
//      Session, persist the raw JSON to localStorage, and render the
//      run overview with clickable combat rows.
//
//   2. Single-file fallback: user picks a manifest or a timeline file
//      directly. The manifest path opens a Session-without-timelines
//      (clickable rows render as disabled). The timeline path renders
//      a single timeline without the run context.
//
// On page load we try to restore from localStorage. If a session is
// there, we render it and the user can immediately interact; if not,
// the empty state is shown.
//
// The `currentSession` + `selectedMcr` state lives in this module —
// the renderer is pure-functional and returns a fresh DOM tree on
// each call. Re-rendering the whole page on every selection change
// is cheap because the DOM tree is small (manifest + one combat).

const $directoryInput = mustFindInput("directory-input");
const $manifestInput = mustFindInput("manifest-input");
const $timelineInput = mustFindInput("timeline-input");
const $clearBtn = mustFindEl("clear-session");
const $output = mustFindEl("output");
const $detail = mustFindEl("detail");
const $errors = mustFindEl("errors");
const $status = mustFindEl("status");

let currentSession: Session | null = null;
let selectedMcr: string | null = null;
let materialisedFloors: FloorRow[] = [];
let selectedFloorIndex: number | null = null;

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
