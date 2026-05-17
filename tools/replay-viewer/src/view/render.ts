import type { CombatTimeline, ReplayManifest } from "../core/types";
import type { Session } from "../core/session";
import { summarizeEvent } from "../core/summary";

// Pure DOM construction — no event listeners, no global state. The
// entry point (main.ts) wires up file-input + scrub state and calls
// into these functions to (re-)render. Keeping the renderers pure
// makes them trivial to unit-test once jsdom is wired up; for now the
// data layer carries the test surface.
//
// HTML structure is plain semantic markup with a few stable class names
// (`viewer-…`). Styling lives in index.html's <style> block.

// Renders a whole run: header metadata + a clickable list of combats.
// `selectedMcr` highlights the active row; `onSelect` fires when a
// combat is clicked. The detail pane (rendered separately via
// renderCombatTimeline) is the caller's responsibility — keeping the
// two halves split makes it trivial to re-render only the detail when
// the selection changes.
//
// Combats that don't have a corresponding timeline in the session
// (manifest entry present but the timeline.json was missing / failed
// to parse on ingest) render as disabled rows with a "(no timeline)"
// suffix. The user still sees the manifest entry exists.
export function renderRunOverview(
  session: Session,
  selectedMcr: string | null,
  onSelect: (mcrFile: string) => void,
): HTMLElement {
  const root = document.createElement("section");
  root.className = "viewer-manifest";

  const heading = document.createElement("h2");
  const label = session.label ?? `seed ${session.manifest.header.seed}`;
  heading.textContent = `Run ${label} — ${session.manifest.header.game_version} (${session.manifest.header.character})`;
  root.appendChild(heading);

  const meta = document.createElement("dl");
  meta.className = "viewer-meta";
  appendDef(meta, "Seed", session.manifest.header.seed);
  appendDef(meta, "Combats", String(session.manifest.combats.length));
  appendDef(meta, "Ascension", String(session.manifest.header.ascension));
  appendDef(meta, "Model id hash", String(session.manifest.header.model_id_hash));
  appendDef(meta, "Started at", new Date(session.manifest.header.start_time_unix * 1000).toISOString());
  root.appendChild(meta);

  const combats = document.createElement("ol");
  combats.className = "viewer-combat-list";
  for (const c of session.manifest.combats) {
    const li = document.createElement("li");
    const hasTimeline = c.mcr_file in session.timelines;
    const btn = document.createElement("button");
    btn.type = "button";
    btn.className = "viewer-combat-row";
    btn.dataset["mcrFile"] = c.mcr_file;
    if (c.mcr_file === selectedMcr) btn.classList.add("is-selected");
    if (!hasTimeline) btn.classList.add("is-disabled");
    btn.disabled = !hasTimeline;
    const suffix = hasTimeline ? "" : " (no timeline)";
    btn.textContent = `act ${c.act_index + 1} · floor ${c.floor} · ${c.room_type}${suffix}`;
    if (hasTimeline) {
      btn.addEventListener("click", () => onSelect(c.mcr_file));
    }
    li.appendChild(btn);
    combats.appendChild(li);
  }
  root.appendChild(combats);

  return root;
}

export function renderManifestSummary(manifest: ReplayManifest): HTMLElement {
  const root = document.createElement("section");
  root.className = "viewer-manifest";

  const heading = document.createElement("h2");
  heading.textContent = `Run on ${manifest.header.game_version} — seed ${manifest.header.seed} (${manifest.header.character})`;
  root.appendChild(heading);

  const meta = document.createElement("dl");
  meta.className = "viewer-meta";
  appendDef(meta, "Combats", String(manifest.combats.length));
  appendDef(meta, "Ascension", String(manifest.header.ascension));
  appendDef(meta, "Model id hash", String(manifest.header.model_id_hash));
  appendDef(meta, "Started at", new Date(manifest.header.start_time_unix * 1000).toISOString());
  root.appendChild(meta);

  const combats = document.createElement("ol");
  combats.className = "viewer-combat-list";
  for (const c of manifest.combats) {
    const li = document.createElement("li");
    li.textContent = `act ${c.act_index + 1} floor ${c.floor} — ${c.room_type} — ${c.mcr_file}`;
    combats.appendChild(li);
  }
  root.appendChild(combats);

  return root;
}

export function renderCombatTimeline(timeline: CombatTimeline): HTMLElement {
  const root = document.createElement("section");
  root.className = "viewer-timeline";

  const heading = document.createElement("h3");
  heading.textContent = "Combat";
  root.appendChild(heading);

  const list = document.createElement("ol");
  list.className = "viewer-event-list";
  for (const e of timeline.events) {
    const li = document.createElement("li");
    li.dataset["eventType"] = e.type;
    li.dataset["eventIndex"] = String(e.index);
    li.textContent = `[${String(e.index).padStart(3, "0")}] ${summarizeEvent(e)}`;
    list.appendChild(li);
  }
  root.appendChild(list);

  return root;
}

function appendDef(dl: HTMLElement, label: string, value: string): void {
  const dt = document.createElement("dt");
  dt.textContent = label;
  const dd = document.createElement("dd");
  dd.textContent = value;
  dl.appendChild(dt);
  dl.appendChild(dd);
}
