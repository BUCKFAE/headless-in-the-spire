import type { Session } from "./session";
import { parseManifest, parseTimeline } from "./parse";
import { parseRunHistory } from "./parse-run-history";
import type { CombatTimeline } from "./types";

// Last-session persistence — survives reloads, scoped to localStorage.
// We re-parse on restore (rather than trusting the cached typed
// object) so a schema change to the in-memory shape doesn't poison
// future sessions, and so we get the same path-pointing error
// messages a fresh load would produce. The on-disk shape we store is
// the *raw JSON* of the manifest + each timeline — not the parsed
// session — so changing the runtime types is purely a code change.
//
// localStorage upper bound is ~5–10 MB depending on browser; a
// typical 8-combat run sits around 600 kB after JSON-stringify. If
// we ever blow that ceiling we'll need IndexedDB; today we just
// catch the `QuotaExceededError` and treat it as "no cache".
//
// SCHEMA_VERSION is bumped whenever the on-disk persistence shape
// changes in a way the loader can't tolerate. Old payloads with a
// mismatched version are dropped on read; the user sees an empty
// page rather than a parse error from a stale cache.

const STORAGE_KEY = "sts2-replay-viewer:last-session";
// Bumped when the persisted shape changes in a way the loader can't
// tolerate. v2 added `run_json` to the payload; v1 caches with no
// run_json field are still parseable (the new field is optional), but
// we bump anyway so a 1-line schema drift surfaces consistently.
const SCHEMA_VERSION = 2;

interface PersistedSession {
  schema: number;
  manifest_json: string;
  // Keyed by `mcr_file` (same key as Session.timelines), values are
  // the raw timeline.json strings. Storing the raw strings (not the
  // parsed object) keeps the persistence layer agnostic to runtime
  // type churn.
  timeline_jsons: Record<string, string>;
  // Optional — only present for runs that ended (the engine writes
  // run.json on OnEnded). Cached as raw JSON for the same reason as
  // timeline_jsons.
  run_json?: string;
  label?: string;
}

// Storage abstraction so tests can inject a Map-backed shim instead
// of relying on jsdom's localStorage. The default implementation is a
// thin guarded wrapper — if `window.localStorage` throws (private
// mode, sandboxed iframes, etc.), the viewer degrades to no-cache.
export interface KeyValueStore {
  get(key: string): string | null;
  set(key: string, value: string): void;
  remove(key: string): void;
}

export const browserLocalStorage: KeyValueStore = {
  get(key) {
    try { return window.localStorage.getItem(key); } catch { return null; }
  },
  set(key, value) {
    try { window.localStorage.setItem(key, value); } catch { /* quota / private mode */ }
  },
  remove(key) {
    try { window.localStorage.removeItem(key); } catch { /* same */ }
  },
};

// Build a persisted payload from a session + the raw JSON strings the
// user originally loaded. We accept raw strings here rather than
// re-serialising the parsed Session because Session.initial_run is
// typed `unknown` and re-stringifying through JSON.parse → JSON.stringify
// can lose key order (mostly cosmetic, but easy to avoid).
export interface RawSessionInput {
  manifestJson: string;
  timelineJsons: Record<string, string>;
  runJson?: string;
  label?: string;
}

export function saveSession(input: RawSessionInput, store: KeyValueStore = browserLocalStorage): void {
  const payload: PersistedSession = {
    schema: SCHEMA_VERSION,
    manifest_json: input.manifestJson,
    timeline_jsons: input.timelineJsons,
    ...(input.runJson !== undefined ? { run_json: input.runJson } : {}),
    ...(input.label !== undefined ? { label: input.label } : {}),
  };
  store.set(STORAGE_KEY, JSON.stringify(payload));
}

export function clearSession(store: KeyValueStore = browserLocalStorage): void {
  store.remove(STORAGE_KEY);
}

// Returns null when no cached session exists OR when the cached
// payload doesn't match the current schema (the caller renders the
// empty state). Returns null AND clears the cache when the cached
// payload fails to parse — a poisoned cache shouldn't keep failing on
// every reload.
export function loadSession(store: KeyValueStore = browserLocalStorage): Session | null {
  const raw = store.get(STORAGE_KEY);
  if (!raw) return null;
  let payload: unknown;
  try {
    payload = JSON.parse(raw);
  } catch {
    store.remove(STORAGE_KEY);
    return null;
  }
  if (!isPersistedSession(payload) || payload.schema !== SCHEMA_VERSION) {
    store.remove(STORAGE_KEY);
    return null;
  }
  try {
    const manifest = parseManifest(JSON.parse(payload.manifest_json));
    const timelines: Record<string, CombatTimeline> = {};
    for (const [mcrFile, json] of Object.entries(payload.timeline_jsons)) {
      try { timelines[mcrFile] = parseTimeline(JSON.parse(json)); } catch { /* skip */ }
    }
    const session: Session = { manifest, timelines };
    if (payload.run_json !== undefined) {
      try { session.runHistory = parseRunHistory(JSON.parse(payload.run_json)); } catch { /* drop */ }
    }
    if (payload.label !== undefined) session.label = payload.label;
    return session;
  } catch {
    store.remove(STORAGE_KEY);
    return null;
  }
}

function isPersistedSession(value: unknown): value is PersistedSession {
  if (value === null || typeof value !== "object") return false;
  const v = value as Record<string, unknown>;
  return (
    typeof v["schema"] === "number" &&
    typeof v["manifest_json"] === "string" &&
    typeof v["timeline_jsons"] === "object" &&
    v["timeline_jsons"] !== null
  );
}
