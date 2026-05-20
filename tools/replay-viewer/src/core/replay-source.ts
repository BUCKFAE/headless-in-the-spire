import { parseRunsIndex, type ReplayRunIndex, type ReplayRunIndexEntry } from "./runs-index";
import { ingestFiles, type Session } from "./session";

// HTTP source for replay artifacts. The Vite dev server (and a future
// static deploy) mounts `<repo>/vendor/replays/` at `/replays/` — see
// vite.config.ts. This module wraps the fetches so callers don't have
// to know the URL layout.
//
// `runs.json` lives at `/replays/runs.json`; each run's artifacts live
// under `/replays/<rel_path>/` where `rel_path` is the value stamped
// into the index entry (e.g. `v0.103.2/1715200000-deadbeef-12345`).
//
// All fetches are no-store: the viewer is meant to reflect the disk
// state right now (you just recorded a run and want to see it), not a
// snapshot the browser cached an hour ago.

const REPLAYS_BASE = "/replays";

export interface FetchRunsIndexResult {
  // null when the dev server didn't serve a runs.json (e.g. the user is
  // looking at the static built bundle without a backing directory).
  // The UI shows the empty state in that case.
  index: ReplayRunIndex | null;
  reason?: string;
}

export async function fetchRunsIndex(): Promise<FetchRunsIndexResult> {
  let response: Response;
  try {
    response = await fetch(`${REPLAYS_BASE}/runs.json`, { cache: "no-store" });
  } catch (err) {
    return { index: null, reason: err instanceof Error ? err.message : String(err) };
  }
  if (response.status === 404) {
    return { index: null, reason: "runs.json not found — record a run, or use the file picker." };
  }
  if (!response.ok) {
    return { index: null, reason: `runs.json fetch failed: HTTP ${response.status}` };
  }
  const raw = await response.json();
  return { index: parseRunsIndex(raw) };
}

// Loads one run's artifacts (manifest + every timeline + run.json) by
// rel_path and assembles a Session via the same ingestFiles path the
// directory-picker uses. Centralising on ingestFiles keeps a single
// place that knows the on-disk layout.
export async function loadRunByRelPath(entry: ReplayRunIndexEntry): Promise<Session> {
  const runBase = `${REPLAYS_BASE}/${entry.rel_path}`;
  const manifestText = await fetchText(`${runBase}/manifest.json`);
  // The manifest knows the combat filenames (mcr_file is relative to
  // the run directory). Pull each timeline.json by appending
  // `.timeline.json` to the mcr_file path. Missing timelines are
  // tolerated; ingestFiles already swallows parse errors per timeline.
  const manifest = JSON.parse(manifestText) as { combats: { mcr_file: string }[] };
  const files: { path: string; text: string }[] = [
    { path: "manifest.json", text: manifestText },
  ];
  await Promise.all(
    manifest.combats.map(async (c) => {
      const url = `${runBase}/${c.mcr_file}.timeline.json`;
      try {
        const text = await fetchText(url);
        files.push({ path: `${c.mcr_file}.timeline.json`, text });
      } catch {
        // Timeline missing — combat still appears in the manifest, just
        // won't be clickable.
      }
    }),
  );
  try {
    const runJsonText = await fetchText(`${runBase}/run.json`);
    files.push({ path: "run.json", text: runJsonText });
  } catch {
    // run.json only lands when the run ends naturally (death/victory).
    // Absence is normal for in-progress / abandoned runs.
  }
  const session = ingestFiles(files);
  session.label = entry.display_name;
  return session;
}

async function fetchText(url: string): Promise<string> {
  const response = await fetch(url, { cache: "no-store" });
  if (!response.ok) {
    throw new Error(`fetch ${url}: HTTP ${response.status}`);
  }
  return response.text();
}
