// TypeScript mirror of <root>/runs.json — the host-side enumeration of
// recorded runs. Source of truth: src/Sts2Headless.Replay/ReplayIndex.cs.
// Bumping the C# CurrentVersion is meant to surface here as either a
// compile error (when the viewer is rebuilt) or a parse error at load
// time.

import type { ReplayRunOutcome } from "./types";

export interface ReplayRunIndex {
  version: number;
  runs: ReplayRunIndexEntry[];
}

export interface ReplayRunIndexEntry {
  run_id: string;
  rel_path: string;
  game_version: string;
  seed: string;
  character: string;
  agent: string;
  display_name: string;
  outcome: ReplayRunOutcome;
  started_at_unix: number;
  ended_at_unix?: number;
  combat_count: number;
}

const VALID_OUTCOMES: readonly ReplayRunOutcome[] = ["unknown", "victory", "defeat", "abandoned"];

export function parseRunsIndex(raw: unknown): ReplayRunIndex {
  const obj = expectObject(raw, "runs_index");
  return {
    version: expectInt(obj["version"], "runs_index.version"),
    runs: expectArray(obj["runs"], "runs_index.runs").map((r, i) =>
      parseEntry(r, `runs_index.runs[${i}]`),
    ),
  };
}

function parseEntry(raw: unknown, path: string): ReplayRunIndexEntry {
  const obj = expectObject(raw, path);
  const outcomeRaw = expectString(obj["outcome"], `${path}.outcome`);
  if (!VALID_OUTCOMES.includes(outcomeRaw as ReplayRunOutcome)) {
    throw new Error(`${path}.outcome: unexpected value "${outcomeRaw}"`);
  }
  const out: ReplayRunIndexEntry = {
    run_id: expectString(obj["run_id"], `${path}.run_id`),
    rel_path: expectString(obj["rel_path"], `${path}.rel_path`),
    game_version: expectString(obj["game_version"], `${path}.game_version`),
    seed: expectString(obj["seed"], `${path}.seed`),
    character: expectString(obj["character"], `${path}.character`),
    agent: expectString(obj["agent"], `${path}.agent`),
    display_name: expectString(obj["display_name"], `${path}.display_name`),
    outcome: outcomeRaw as ReplayRunOutcome,
    started_at_unix: expectInt(obj["started_at_unix"], `${path}.started_at_unix`),
    combat_count: expectInt(obj["combat_count"], `${path}.combat_count`),
  };
  if (obj["ended_at_unix"] !== undefined && obj["ended_at_unix"] !== null) {
    out.ended_at_unix = expectInt(obj["ended_at_unix"], `${path}.ended_at_unix`);
  }
  return out;
}

// Duplicate validators kept local so this module stays standalone (no
// circular import with parse.ts). They're three lines each — the
// duplication cost is lower than the entanglement cost.

function expectObject(raw: unknown, path: string): Record<string, unknown> {
  if (raw === null || typeof raw !== "object" || Array.isArray(raw)) {
    throw new Error(`${path}: expected object, got ${describe(raw)}`);
  }
  return raw as Record<string, unknown>;
}

function expectArray(raw: unknown, path: string): unknown[] {
  if (!Array.isArray(raw)) {
    throw new Error(`${path}: expected array, got ${describe(raw)}`);
  }
  return raw;
}

function expectString(raw: unknown, path: string): string {
  if (typeof raw !== "string") {
    throw new Error(`${path}: expected string, got ${describe(raw)}`);
  }
  return raw;
}

function expectInt(raw: unknown, path: string): number {
  if (typeof raw !== "number" || !Number.isFinite(raw)) {
    throw new Error(`${path}: expected number, got ${describe(raw)}`);
  }
  return raw;
}

function describe(value: unknown): string {
  if (value === null) return "null";
  if (Array.isArray(value)) return "array";
  return typeof value;
}
