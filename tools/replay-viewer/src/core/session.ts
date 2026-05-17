import type { CombatTimeline, ReplayManifest } from "./types";
import type { RunHistory } from "./run-history";
import { parseManifest, parseTimeline } from "./parse";
import { parseRunHistory } from "./parse-run-history";

// A loaded run, in memory. The manifest is the index; `timelines` is
// keyed by `mcr_file` (the relative path the manifest entries use, e.g.
// `combats/act1-floor2-combat.mcr`). Not every manifest entry needs a
// timeline — if the timeline.json is missing or fails to parse, the
// combat still shows up in the manifest list but can't be clicked into.
export interface Session {
  manifest: ReplayManifest;
  timelines: Record<string, CombatTimeline>;
  // run.json — optional because the engine only writes it on
  // OnEnded (death / victory). A recording stopped mid-run won't
  // have one; the viewer renders the manifest-driven combat list
  // in that case instead of the floor-by-floor view.
  runHistory?: RunHistory;
  // Optional label — the directory name the user picked, for UI
  // breadcrumbs. Not load-bearing for any logic.
  label?: string;
}

// Ingests an arbitrary collection of `{ path, text }` pairs into a
// Session. Path-handling rules:
//
//   - The manifest.json may sit at the top level OR under a nested
//     run-id directory (e.g. `1779021871-42/manifest.json`). We strip
//     any leading directory that contains it so the timeline lookup
//     keys match the `mcr_file` paths the manifest stores
//     (`combats/act1-floor2-combat.mcr`).
//
//   - Files that don't match `manifest.json` or `*.mcr.timeline.json`
//     are silently ignored — the user may drop a directory that also
//     contains `*.mcr` binaries and `run.json`; we just don't load
//     them at this layer.
//
//   - Multiple manifest.json files: the first one wins, others are
//     ignored. (The viewer doesn't support multi-run sessions yet.)
//
// Throws if no manifest.json is found OR if the manifest fails to
// parse — both are user-visible errors worth surfacing.
export function ingestFiles(files: readonly { path: string; text: string }[]): Session {
  let manifestEntry: { path: string; text: string } | undefined;
  for (const f of files) {
    if (basename(f.path) === "manifest.json") {
      manifestEntry = f;
      break;
    }
  }
  if (!manifestEntry) {
    throw new Error("no manifest.json found in the selected files");
  }

  const manifest = parseManifest(JSON.parse(manifestEntry.text));
  const manifestDir = dirname(manifestEntry.path);

  const timelines: Record<string, CombatTimeline> = {};
  let runHistory: RunHistory | undefined;
  for (const f of files) {
    if (f.path.endsWith(".mcr.timeline.json")) {
      const rel = stripLeadingDir(f.path, manifestDir);
      const mcrFile = rel.slice(0, -".timeline.json".length);
      try {
        timelines[mcrFile] = parseTimeline(JSON.parse(f.text));
      } catch {
        // A malformed timeline shouldn't kill the rest of the
        // session; the combat just won't be clickable.
      }
    } else if (basename(f.path) === "run.json" && stripLeadingDir(f.path, manifestDir) === "run.json") {
      // Only pick up run.json that sits next to the manifest — a
      // nested combats/ directory shouldn't ever contain a run.json,
      // but if it did we'd ignore it.
      try {
        runHistory = parseRunHistory(JSON.parse(f.text));
      } catch {
        // A malformed run.json shouldn't kill the session either;
        // the manifest-only fallback view still works.
      }
    }
  }

  const session: Session = { manifest, timelines };
  if (runHistory !== undefined) session.runHistory = runHistory;
  const label = inferLabel(manifestEntry.path);
  if (label !== undefined) session.label = label;
  return session;
}

// Used by the view layer to look up the timeline for a manifest entry,
// honouring the encoding the manifest itself uses (`mcr_file` is
// always forward-slash separated per ReplayLayout.cs:55).
export function timelineFor(session: Session, mcrFile: string): CombatTimeline | undefined {
  return session.timelines[mcrFile];
}

// ── path helpers (intentionally string-only, no node:path dep) ────────

function basename(p: string): string {
  const i = p.lastIndexOf("/");
  return i < 0 ? p : p.slice(i + 1);
}

function dirname(p: string): string {
  const i = p.lastIndexOf("/");
  return i < 0 ? "" : p.slice(0, i);
}

function stripLeadingDir(p: string, dir: string): string {
  if (dir === "") return p;
  const prefix = dir + "/";
  return p.startsWith(prefix) ? p.slice(prefix.length) : p;
}

function inferLabel(manifestPath: string): string | undefined {
  // For `<run-id>/manifest.json` the label is `<run-id>`; for a
  // top-level `manifest.json` we leave label unset (the UI can fall
  // back to the seed / character).
  const d = dirname(manifestPath);
  if (d === "") return undefined;
  return basename(d);
}
