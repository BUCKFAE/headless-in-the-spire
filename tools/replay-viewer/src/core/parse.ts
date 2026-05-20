import type {
  CombatChecksum,
  CombatEvent,
  CombatSnapshotState,
  CombatTimeline,
  CombatTimelineHeader,
  CreatureSnapshot,
  PlayerSnapshot,
  ReplayCombatEntry,
  ReplayManifest,
  ReplayManifestHeader,
} from "./types";

// Strict parsers for the JSON artefacts the C# substrate emits.
//
// "Strict" means: every field listed in types.ts must be present and
// type-correct, otherwise we throw with a path-pointing message
// (e.g. "manifest.combats[3].mcr_file: expected string, got null"). A
// silent fallback would mask schema drift, which is the one bug class
// we want the viewer to surface loudly — a missing field in the
// engine's output should fail at the parse boundary, not show up as a
// blank cell six clicks deep in the UI.
//
// These functions are intentionally `unknown`-first: the input is
// always a freshly parsed JSON value (from `JSON.parse` or `fetch`),
// so structural validation is the parse boundary's job. After the
// parser returns, the rest of the codebase can treat values as
// well-typed.

// ── Manifest ──────────────────────────────────────────────────────────

export function parseManifest(raw: unknown): ReplayManifest {
  const obj = expectObject(raw, "manifest");
  const out: ReplayManifest = {
    version: expectInt(obj["version"], "manifest.version"),
    header: parseManifestHeader(obj["header"], "manifest.header"),
    combats: expectArray(obj["combats"], "manifest.combats").map((c, i) =>
      parseCombatEntry(c, `manifest.combats[${i}]`),
    ),
  };
  if (obj["display_name"] !== undefined && obj["display_name"] !== null) {
    out.display_name = expectString(obj["display_name"], "manifest.display_name");
  }
  if (obj["outcome"] !== undefined && obj["outcome"] !== null) {
    const outcome = expectString(obj["outcome"], "manifest.outcome");
    if (!["unknown", "victory", "defeat", "abandoned"].includes(outcome)) {
      throw new Error(`manifest.outcome: unexpected value "${outcome}"`);
    }
    out.outcome = outcome as "unknown" | "victory" | "defeat" | "abandoned";
  }
  if (obj["ended_at_unix"] !== undefined && obj["ended_at_unix"] !== null) {
    out.ended_at_unix = expectInt(obj["ended_at_unix"], "manifest.ended_at_unix");
  }
  return out;
}

function parseManifestHeader(raw: unknown, path: string): ReplayManifestHeader {
  const obj = expectObject(raw, path);
  const out: ReplayManifestHeader = {
    game_version: expectString(obj["game_version"], `${path}.game_version`),
    sts2_dll_sha256: expectString(obj["sts2_dll_sha256"], `${path}.sts2_dll_sha256`),
    model_id_hash: expectInt(obj["model_id_hash"], `${path}.model_id_hash`),
    git_commit: expectString(obj["git_commit"], `${path}.git_commit`),
    run_history_schema_version: expectInt(obj["run_history_schema_version"], `${path}.run_history_schema_version`),
    protocol_version: expectInt(obj["protocol_version"], `${path}.protocol_version`),
    seed: expectString(obj["seed"], `${path}.seed`),
    character: expectString(obj["character"], `${path}.character`),
    ascension: expectInt(obj["ascension"], `${path}.ascension`),
    modifiers: expectArray(obj["modifiers"], `${path}.modifiers`).map((m, i) =>
      expectString(m, `${path}.modifiers[${i}]`),
    ),
    start_time_unix: expectInt(obj["start_time_unix"], `${path}.start_time_unix`),
  };
  if (obj["agent"] !== undefined && obj["agent"] !== null) {
    out.agent = expectString(obj["agent"], `${path}.agent`);
  }
  return out;
}

function parseCombatEntry(raw: unknown, path: string): ReplayCombatEntry {
  const obj = expectObject(raw, path);
  const outcome = expectString(obj["outcome"], `${path}.outcome`);
  if (!["unknown", "victory", "defeat", "abandoned"].includes(outcome)) {
    throw new Error(`${path}.outcome: unexpected value "${outcome}"`);
  }
  return {
    mcr_file: expectString(obj["mcr_file"], `${path}.mcr_file`),
    act_index: expectInt(obj["act_index"], `${path}.act_index`),
    floor: expectInt(obj["floor"], `${path}.floor`),
    room_type: expectString(obj["room_type"], `${path}.room_type`),
    encounter: optionalString(obj["encounter"], `${path}.encounter`),
    outcome: outcome as ReplayCombatEntry["outcome"],
    action_count: expectInt(obj["action_count"], `${path}.action_count`),
    checksum_count: expectInt(obj["checksum_count"], `${path}.checksum_count`),
  };
}

// ── Timeline ──────────────────────────────────────────────────────────

export function parseTimeline(raw: unknown): CombatTimeline {
  const obj = expectObject(raw, "timeline");
  return {
    schema_version: expectInt(obj["schema_version"], "timeline.schema_version"),
    header: parseTimelineHeader(obj["header"], "timeline.header"),
    initial_run: obj["initial_run"] ?? null,
    choice_ids: expectArray(obj["choice_ids"], "timeline.choice_ids").map((id, i) =>
      expectInt(id, `timeline.choice_ids[${i}]`),
    ),
    events: expectArray(obj["events"], "timeline.events").map((e, i) =>
      parseEvent(e, `timeline.events[${i}]`),
    ),
    checksums: expectArray(obj["checksums"], "timeline.checksums").map((c, i) =>
      parseChecksum(c, `timeline.checksums[${i}]`),
    ),
  };
}

function parseTimelineHeader(raw: unknown, path: string): CombatTimelineHeader {
  const obj = expectObject(raw, path);
  return {
    version: expectString(obj["version"], `${path}.version`),
    git_commit: expectString(obj["git_commit"], `${path}.git_commit`),
    model_id_hash: expectInt(obj["model_id_hash"], `${path}.model_id_hash`),
    next_action_id: expectInt(obj["next_action_id"], `${path}.next_action_id`),
    next_checksum_id: expectInt(obj["next_checksum_id"], `${path}.next_checksum_id`),
    next_hook_id: expectInt(obj["next_hook_id"], `${path}.next_hook_id`),
    event_count: expectInt(obj["event_count"], `${path}.event_count`),
    checksum_count: expectInt(obj["checksum_count"], `${path}.checksum_count`),
  };
}

function parseEvent(raw: unknown, path: string): CombatEvent {
  const obj = expectObject(raw, path);
  const index = expectInt(obj["index"], `${path}.index`);
  const playerIdRaw = obj["player_id"];
  const playerId = playerIdRaw === undefined ? undefined : expectInt(playerIdRaw, `${path}.player_id`);
  const type = expectString(obj["type"], `${path}.type`);
  switch (type) {
    case "GameAction":
      return {
        index,
        type,
        ...(playerId !== undefined ? { player_id: playerId } : {}),
        action_type: expectString(obj["action_type"], `${path}.action_type`),
        action: obj["action"] ?? null,
      };
    case "HookAction":
      return {
        index,
        type,
        ...(playerId !== undefined ? { player_id: playerId } : {}),
        ...(obj["hook_id"] !== undefined ? { hook_id: expectInt(obj["hook_id"], `${path}.hook_id`) } : {}),
        ...(obj["game_action_type"] !== undefined
          ? { game_action_type: expectString(obj["game_action_type"], `${path}.game_action_type`) }
          : {}),
      };
    case "ResumeAction":
      return {
        index,
        type,
        ...(playerId !== undefined ? { player_id: playerId } : {}),
        ...(obj["action_id"] !== undefined ? { action_id: expectInt(obj["action_id"], `${path}.action_id`) } : {}),
      };
    case "PlayerChoice":
      return {
        index,
        type,
        ...(playerId !== undefined ? { player_id: playerId } : {}),
        ...(obj["choice_id"] !== undefined ? { choice_id: expectInt(obj["choice_id"], `${path}.choice_id`) } : {}),
        ...(obj["choice_result"] !== undefined ? { choice_result: obj["choice_result"] } : {}),
      };
    default:
      throw new Error(`${path}.type: unexpected value "${type}"`);
  }
}

function parseChecksum(raw: unknown, path: string): CombatChecksum {
  const obj = expectObject(raw, path);
  const out: CombatChecksum = {
    id: expectInt(obj["id"], `${path}.id`),
    checksum: expectInt(obj["checksum"], `${path}.checksum`),
    context: expectString(obj["context"], `${path}.context`),
  };
  if (obj["state"] !== undefined) {
    out.state = parseState(obj["state"], `${path}.state`);
  }
  return out;
}

function parseState(raw: unknown, path: string): CombatSnapshotState {
  const obj = expectObject(raw, path);
  return {
    creatures: expectArray(obj["creatures"], `${path}.creatures`).map((c, i) =>
      parseCreature(c, `${path}.creatures[${i}]`),
    ),
    players: expectArray(obj["players"], `${path}.players`).map((p, i) =>
      parsePlayer(p, `${path}.players[${i}]`),
    ),
  };
}

function parseCreature(raw: unknown, path: string): CreatureSnapshot {
  const obj = expectObject(raw, path);
  const kind = expectString(obj["kind"], `${path}.kind`);
  const currentHp = expectInt(obj["current_hp"], `${path}.current_hp`);
  const maxHp = expectInt(obj["max_hp"], `${path}.max_hp`);
  const block = expectInt(obj["block"], `${path}.block`);
  switch (kind) {
    case "player":
      return {
        kind,
        player_id: expectInt(obj["player_id"], `${path}.player_id`),
        current_hp: currentHp,
        max_hp: maxHp,
        block,
      };
    case "monster":
      return {
        kind,
        monster_id: expectString(obj["monster_id"], `${path}.monster_id`),
        current_hp: currentHp,
        max_hp: maxHp,
        block,
      };
    case "unknown":
      return { kind, current_hp: currentHp, max_hp: maxHp, block };
    default:
      throw new Error(`${path}.kind: unexpected value "${kind}"`);
  }
}

function parsePlayer(raw: unknown, path: string): PlayerSnapshot {
  const obj = expectObject(raw, path);
  return {
    player_id: expectInt(obj["player_id"], `${path}.player_id`),
    energy: expectInt(obj["energy"], `${path}.energy`),
    gold: expectInt(obj["gold"], `${path}.gold`),
  };
}

// ── Primitive validators ──────────────────────────────────────────────

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

function optionalString(raw: unknown, path: string): string | null {
  if (raw === null || raw === undefined) return null;
  return expectString(raw, path);
}

function describe(value: unknown): string {
  if (value === null) return "null";
  if (Array.isArray(value)) return "array";
  return typeof value;
}
