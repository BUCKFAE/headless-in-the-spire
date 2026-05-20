// TypeScript mirrors of the JSON artefacts the C# recording substrate
// emits per AD-8 + Phase A.1.
//
// Source of truth: src/Sts2Headless.Replay/ReplayManifest.cs and
// src/Sts2Headless.Replay/CombatTimelineEmitter.cs. Bumping either of
// those on the C# side is meant to surface here as a compile error
// (when the viewer is rebuilt) or a test failure (the fixture-based
// tests load real JSON and assert against this shape).
//
// We deliberately avoid pulling in the game's full SerializableRun
// schema here — `initialRun` is typed as `unknown` so the viewer can
// drill into it lazily without locking the viewer to a specific
// game-version's save schema. The header.modelIdHash tells us which
// schema we're looking at; consumers that need typed access can
// narrow on a per-field basis.

// ── manifest.json (run-level index) ───────────────────────────────────

export type ReplayRunOutcome = "unknown" | "victory" | "defeat" | "abandoned";

export interface ReplayManifest {
  version: number;
  header: ReplayManifestHeader;
  combats: ReplayCombatEntry[];
  // Populated at finalize. Older recordings (manifest version 1) don't
  // carry these; the viewer falls back to deriving them on the fly.
  display_name?: string;
  outcome?: ReplayRunOutcome;
  ended_at_unix?: number;
}

export interface ReplayManifestHeader {
  game_version: string;
  sts2_dll_sha256: string;
  model_id_hash: number;
  git_commit: string;
  run_history_schema_version: number;
  protocol_version: number;
  seed: string;
  character: string;
  ascension: number;
  modifiers: string[];
  start_time_unix: number;
  // Agent label (e.g. "GreedyAgent", "manual"). Optional for back-compat
  // with manifests recorded before STS2_REPLAY_AGENT was a thing.
  agent?: string;
}

export interface ReplayCombatEntry {
  mcr_file: string;
  act_index: number;
  floor: number;
  room_type: string;
  encounter?: string | null;
  outcome: "unknown" | "victory" | "defeat" | "abandoned";
  action_count: number;
  checksum_count: number;
}

// ── timeline.json (per-combat detail) ─────────────────────────────────

export interface CombatTimeline {
  schema_version: number;
  header: CombatTimelineHeader;
  initial_run: unknown;
  choice_ids: number[];
  events: CombatEvent[];
  checksums: CombatChecksum[];
}

export interface CombatTimelineHeader {
  version: string;
  git_commit: string;
  model_id_hash: number;
  next_action_id: number;
  next_checksum_id: number;
  next_hook_id: number;
  event_count: number;
  checksum_count: number;
}

// CombatReplayEventType in the engine: GameAction / HookAction /
// ResumeAction / PlayerChoice. We model the four shapes as a
// discriminated union on `type` so the viewer can switch exhaustively
// (TypeScript catches a missing case at compile time).
export type CombatEvent =
  | CombatEventGameAction
  | CombatEventHookAction
  | CombatEventResumeAction
  | CombatEventPlayerChoice;

interface CombatEventBase {
  index: number;
  player_id?: number;
}

export interface CombatEventGameAction extends CombatEventBase {
  type: "GameAction";
  action_type: string;
  // Each INetAction subclass has a distinct field set; we leave it
  // untyped at this layer and let the view-side decoder narrow per
  // action_type (PlayCard → card + model_id + target_id, EndPlayerTurn
  // → no extra fields, etc.). See view/decoders.ts.
  action: unknown;
}

export interface CombatEventHookAction extends CombatEventBase {
  type: "HookAction";
  hook_id?: number;
  game_action_type?: string;
}

export interface CombatEventResumeAction extends CombatEventBase {
  type: "ResumeAction";
  action_id?: number;
}

export interface CombatEventPlayerChoice extends CombatEventBase {
  type: "PlayerChoice";
  choice_id?: number;
  choice_result?: unknown;
}

export interface CombatChecksum {
  id: number;
  checksum: number;
  context: string;
  // Slim snapshot of NetFullCombatState at the moment this checksum
  // fired. Optional because pre-state-emission .mcr files lack it; the
  // viewer should gracefully degrade ("HP unavailable") rather than
  // refuse to render.
  state?: CombatSnapshotState;
}

export interface CombatSnapshotState {
  creatures: CreatureSnapshot[];
  players: PlayerSnapshot[];
}

export type CreatureSnapshot =
  | { kind: "player"; player_id: number; current_hp: number; max_hp: number; block: number }
  | { kind: "monster"; monster_id: string; current_hp: number; max_hp: number; block: number }
  | { kind: "unknown"; current_hp: number; max_hp: number; block: number };

export interface PlayerSnapshot {
  player_id: number;
  energy: number;
  gold: number;
}
