import type { CombatChecksum, CombatEvent, CombatTimeline } from "./types";

// Merges the timeline's two arrays — turn-boundary checksums and
// game-action events — into one chronological list. The .mcr stores
// them separately because the engine emits them on different
// listeners, but the user expects to see "what happened" in order.
//
// The relative ordering between an event and a checksum isn't stored
// explicitly. We reconstruct it from the engine's known firing
// pattern:
//
//   - One "After player turn start" checksum at the top of every
//     player turn.
//   - During the player turn: every action the player takes is a
//     GameAction event. The turn ends when the player issues a
//     `NetEndPlayerTurnAction` or `NetReadyToBeginEnemyTurnAction`.
//   - Then a cluster of checksums:
//     "After player turn phase one end" / "after player turn phase
//     two end" / "After enemy turn start" / "After enemy turn end".
//   - Loop until combat ends.
//
// So we group checksums by player-turn (split on "After player turn
// start") and events by player-turn (split after a turn-ending
// action), then weave them: turn-start → events → phase/enemy
// checksums → next turn.
//
// Pre-state-emission .mcr files have an empty checksum list — in
// that case the merger just returns the events in their original
// order.

export type CombatLogItem =
  | { kind: "turn"; checksum: CombatChecksum }
  | { kind: "event"; event: CombatEvent }
  // Synthesised entry — the .mcr doesn't record enemy AI as events
  // (only player-originated actions show up in `events`). We
  // reconstruct "the enemy did stuff" from the HP / block delta
  // between the "After enemy turn start" and "After enemy turn end"
  // checkpoints. damageDealt is HP lost; blockAbsorbed is the prior
  // block that got chewed up. Both can be present together (block
  // partially absorbed an over-block attack).
  | { kind: "enemy"; damageDealt: number; blockAbsorbed: number };

const TURN_START_CONTEXT = "After player turn start";

// Action types that end the player's turn (i.e. transfer control to
// the enemy phase). We split event groups on these.
const TURN_ENDING_ACTION_TYPES = new Set([
  "NetEndPlayerTurnAction",
  "NetReadyToBeginEnemyTurnAction",
]);

export function buildCombatLog(timeline: CombatTimeline): CombatLogItem[] {
  if (timeline.checksums.length === 0) {
    return timeline.events.map((event) => ({ kind: "event", event }));
  }

  const turnChunks = groupChecksumsByTurn(timeline.checksums);
  const eventChunks = groupEventsByTurn(timeline.events);

  const interleaved: CombatLogItem[] = [];
  const turnCount = Math.max(turnChunks.length, eventChunks.length);
  for (let i = 0; i < turnCount; i++) {
    const cs = turnChunks[i] ?? [];
    const evs = eventChunks[i] ?? [];

    // If this chunk starts with the turn-start marker, the natural
    // order is: turn-start → events → phase/enemy-end markers.
    if (cs.length > 0 && cs[0]!.context === TURN_START_CONTEXT) {
      interleaved.push({ kind: "turn", checksum: cs[0]! });
      for (const e of evs) interleaved.push({ kind: "event", event: e });
      for (let j = 1; j < cs.length; j++) interleaved.push({ kind: "turn", checksum: cs[j]! });
    } else {
      // Defensive fallback for shapes that don't fit the heuristic
      // (older recordings, custom contexts): just dump checksums
      // then events in their original order. Better than discarding
      // anything.
      for (const c of cs) interleaved.push({ kind: "turn", checksum: c });
      for (const e of evs) interleaved.push({ kind: "event", event: e });
    }
  }
  return injectEnemyActions(interleaved);
}

// Walks the interleaved log and inserts a synthetic "enemy" item
// between every "After enemy turn start" / "After enemy turn end"
// pair, when player HP or block changed. The .mcr doesn't record
// enemy AI as events, so this is the only way to surface "the
// enemy did X" in the log.
//
// Requires that both flanking checkpoints carry a `state` payload
// with a `kind: "player"` creature. Older recordings (pre-state)
// produce no enemy entries — same fallback as the rest of the log.
function injectEnemyActions(items: CombatLogItem[]): CombatLogItem[] {
  const out: CombatLogItem[] = [];
  for (let i = 0; i < items.length; i++) {
    const item = items[i]!;
    out.push(item);
    // Trigger on "After enemy turn start": look ahead for the next
    // turn-checkpoint and compute delta.
    if (item.kind !== "turn") continue;
    if (item.checksum.context !== "After enemy turn start") continue;
    const next = findNextTurnCheckpoint(items, i + 1);
    if (next === null) continue;

    const startPlayer = playerSnapshot(item.checksum);
    const endPlayer = playerSnapshot(next.item.checksum);
    if (!startPlayer || !endPlayer) continue;

    const damageDealt = Math.max(0, startPlayer.current_hp - endPlayer.current_hp);
    const blockAbsorbed = Math.max(0, startPlayer.block - endPlayer.block);
    if (damageDealt === 0 && blockAbsorbed === 0) continue;

    out.push({ kind: "enemy", damageDealt, blockAbsorbed });
  }
  return out;
}

function findNextTurnCheckpoint(items: CombatLogItem[], from: number): { idx: number; item: Extract<CombatLogItem, { kind: "turn" }> } | null {
  for (let j = from; j < items.length; j++) {
    const it = items[j]!;
    if (it.kind === "turn") return { idx: j, item: it };
  }
  return null;
}

function playerSnapshot(c: CombatChecksum): { current_hp: number; block: number } | null {
  const p = c.state?.creatures.find((x) => x.kind === "player");
  return p ? { current_hp: p.current_hp, block: p.block } : null;
}

// Splits the checksum list at every "After player turn start" — each
// resulting chunk represents one player turn worth of checksums.
function groupChecksumsByTurn(checksums: readonly CombatChecksum[]): CombatChecksum[][] {
  const groups: CombatChecksum[][] = [];
  let cur: CombatChecksum[] = [];
  for (const c of checksums) {
    if (c.context === TURN_START_CONTEXT && cur.length > 0) {
      groups.push(cur);
      cur = [c];
    } else {
      cur.push(c);
    }
  }
  if (cur.length > 0) groups.push(cur);
  return groups;
}

// Splits the event list at every turn-ending action (NetEndPlayerTurn
// / NetReadyToBeginEnemyTurn). The turn-ending action goes with the
// turn it ENDS, not the next one.
function groupEventsByTurn(events: readonly CombatEvent[]): CombatEvent[][] {
  const groups: CombatEvent[][] = [];
  let cur: CombatEvent[] = [];
  for (const e of events) {
    cur.push(e);
    if (e.type === "GameAction" && TURN_ENDING_ACTION_TYPES.has(e.action_type)) {
      groups.push(cur);
      cur = [];
    }
  }
  if (cur.length > 0) groups.push(cur);
  return groups;
}
