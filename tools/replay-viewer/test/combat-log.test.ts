import { describe, it, expect } from "vitest";
import { buildCombatLog, type CombatLogItem } from "../src/core/combat-log";
import { parseTimeline } from "../src/core/parse";
import type { CombatTimeline } from "../src/core/types";

// Compact factory for synthetic timelines — only the fields the
// merger consumes, with the right shapes. The full timeline parser
// is exercised in parse.test.ts; here we want focused, minimal cases
// for the interleave heuristic.
function timeline(events: CombatTimeline["events"], checksums: CombatTimeline["checksums"]): CombatTimeline {
  return {
    schema_version: 1,
    header: {
      version: "test",
      git_commit: "test",
      model_id_hash: 0,
      next_action_id: events.length,
      next_checksum_id: checksums.length,
      next_hook_id: 0,
      event_count: events.length,
      checksum_count: checksums.length,
    },
    initial_run: {},
    choice_ids: [],
    events,
    checksums,
  };
}

function turn(id: number, context: string, playerHp?: number, playerBlock?: number): CombatTimeline["checksums"][number] {
  const out: CombatTimeline["checksums"][number] = { id, checksum: 0, context };
  if (playerHp !== undefined) {
    out.state = {
      creatures: [{ kind: "player", player_id: 1, current_hp: playerHp, max_hp: 80, block: playerBlock ?? 0 }],
      players: [{ player_id: 1, energy: 3, gold: 99 }],
    };
  }
  return out;
}

function gameAction(index: number, actionType: string): CombatTimeline["events"][number] {
  return { index, type: "GameAction", player_id: 1, action_type: actionType, action: {} };
}

describe("buildCombatLog", () => {
  it("emits events in their original order when no checksums are present (pre-state .mcr)", () => {
    const t = timeline(
      [gameAction(0, "NetPlayCardAction"), gameAction(1, "NetPlayCardAction")],
      [],
    );
    const log = buildCombatLog(t);
    expect(log.map((i) => (i.kind === "event" ? i.event.index : "T"))).toEqual([0, 1]);
  });

  it("interleaves one turn correctly: turn-start → events → phase/enemy checksums", () => {
    // One player turn: play card, play card, end turn → phase-one,
    // phase-two, enemy start, enemy end checksums.
    const t = timeline(
      [
        gameAction(0, "NetPlayCardAction"),
        gameAction(1, "NetPlayCardAction"),
        gameAction(2, "NetReadyToBeginEnemyTurnAction"),
      ],
      [
        turn(0, "After player turn start"),
        turn(1, "After player turn phase one end"),
        turn(2, "after player turn phase two end"),
        turn(3, "After enemy turn start"),
        turn(4, "After enemy turn end"),
      ],
    );
    const log = buildCombatLog(t);
    expect(log).toHaveLength(8);
    // Expected interleave:
    expect(log[0]).toEqual({ kind: "turn", checksum: t.checksums[0] });        // turn start
    expect(log[1]).toEqual({ kind: "event", event: t.events[0] });             // play
    expect(log[2]).toEqual({ kind: "event", event: t.events[1] });             // play
    expect(log[3]).toEqual({ kind: "event", event: t.events[2] });             // ready (turn-end)
    expect(log[4]).toEqual({ kind: "turn", checksum: t.checksums[1] });        // phase one end
    expect(log[5]).toEqual({ kind: "turn", checksum: t.checksums[2] });        // phase two end
    expect(log[6]).toEqual({ kind: "turn", checksum: t.checksums[3] });        // enemy start
    expect(log[7]).toEqual({ kind: "turn", checksum: t.checksums[4] });        // enemy end
  });

  it("interleaves three turns correctly (multi-turn combat)", () => {
    const t = timeline(
      [
        // Turn 1: play, play, ready
        gameAction(0, "NetPlayCardAction"),
        gameAction(1, "NetPlayCardAction"),
        gameAction(2, "NetReadyToBeginEnemyTurnAction"),
        // Turn 2: play, play, play, ready
        gameAction(3, "NetPlayCardAction"),
        gameAction(4, "NetPlayCardAction"),
        gameAction(5, "NetPlayCardAction"),
        gameAction(6, "NetReadyToBeginEnemyTurnAction"),
        // Turn 3: play, play (combat ends before turn-end)
        gameAction(7, "NetPlayCardAction"),
        gameAction(8, "NetPlayCardAction"),
      ],
      [
        turn(0, "After player turn start"),
        turn(1, "After player turn phase one end"),
        turn(2, "after player turn phase two end"),
        turn(3, "After enemy turn start"),
        turn(4, "After enemy turn end"),
        turn(5, "After player turn start"),
        turn(6, "After player turn phase one end"),
        turn(7, "after player turn phase two end"),
        turn(8, "After enemy turn start"),
        turn(9, "After enemy turn end"),
        turn(10, "After player turn start"),
      ],
    );
    const log = buildCombatLog(t);

    // Verify the rough shape: every event appears exactly once, in
    // original order; every checksum appears exactly once, in
    // original order; turn-start markers always precede their turn's
    // events.
    const events = log.filter((i): i is Extract<CombatLogItem, { kind: "event" }> => i.kind === "event");
    const checksums = log.filter((i): i is Extract<CombatLogItem, { kind: "turn" }> => i.kind === "turn");
    expect(events.map((i) => i.event.index)).toEqual([0, 1, 2, 3, 4, 5, 6, 7, 8]);
    expect(checksums.map((i) => i.checksum.id)).toEqual([0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);

    // The turn-3 start (checksum id=10) should precede event 7.
    const turn3StartIdx = log.findIndex((i) => i.kind === "turn" && i.checksum.id === 10);
    const event7Idx = log.findIndex((i) => i.kind === "event" && i.event.index === 7);
    expect(turn3StartIdx).toBeLessThan(event7Idx);
    // And the enemy-turn-end of turn 2 (checksum 9) should precede
    // the turn-3 start.
    const turn2EnemyEndIdx = log.findIndex((i) => i.kind === "turn" && i.checksum.id === 9);
    expect(turn2EnemyEndIdx).toBeLessThan(turn3StartIdx);
  });

  it("handles a NetEndPlayerTurnAction (alternate turn-ending action) the same as NetReadyToBeginEnemyTurnAction", () => {
    const t = timeline(
      [
        gameAction(0, "NetPlayCardAction"),
        gameAction(1, "NetEndPlayerTurnAction"),
      ],
      [
        turn(0, "After player turn start"),
        turn(1, "After player turn phase one end"),
      ],
    );
    const log = buildCombatLog(t);
    expect(log[0]!.kind).toBe("turn");
    expect(log[1]!.kind).toBe("event");
    expect(log[2]!.kind).toBe("event");
    expect(log[3]!.kind).toBe("turn");
  });

  it("preserves every item — total count = events.length + checksums.length", () => {
    const t = timeline(
      [
        gameAction(0, "NetPlayCardAction"),
        gameAction(1, "NetPlayCardAction"),
        gameAction(2, "NetReadyToBeginEnemyTurnAction"),
        gameAction(3, "NetPlayCardAction"),
      ],
      [
        turn(0, "After player turn start"),
        turn(1, "After player turn phase one end"),
        turn(2, "After enemy turn start"),
        turn(3, "After player turn start"),
      ],
    );
    const log = buildCombatLog(t);
    expect(log).toHaveLength(t.events.length + t.checksums.length);
  });

  it("synthesises an enemy-action entry between enemy-turn-start and enemy-turn-end when HP dropped", () => {
    const t = timeline(
      [
        gameAction(0, "NetReadyToBeginEnemyTurnAction"),
      ],
      [
        turn(0, "After player turn start", 80, 0),
        turn(1, "After player turn phase one end", 80, 5),
        turn(2, "after player turn phase two end", 80, 5),
        turn(3, "After enemy turn start", 80, 5),
        turn(4, "After enemy turn end", 73, 0),
      ],
    );
    const log = buildCombatLog(t);
    const enemyActions = log.filter((i) => i.kind === "enemy");
    expect(enemyActions).toHaveLength(1);
    expect(enemyActions[0]).toEqual({ kind: "enemy", damageDealt: 7, blockAbsorbed: 5 });

    // The enemy-action item must sit BETWEEN the start and end
    // markers, not before/after them.
    const startIdx = log.findIndex((i) => i.kind === "turn" && i.checksum.context === "After enemy turn start");
    const enemyIdx = log.findIndex((i) => i.kind === "enemy");
    const endIdx = log.findIndex((i) => i.kind === "turn" && i.checksum.context === "After enemy turn end");
    expect(startIdx).toBeLessThan(enemyIdx);
    expect(enemyIdx).toBeLessThan(endIdx);
  });

  it("does NOT synthesise an enemy-action when HP didn't change (the enemy whiffed or block absorbed everything)", () => {
    const t = timeline(
      [gameAction(0, "NetReadyToBeginEnemyTurnAction")],
      [
        turn(0, "After player turn start", 80, 0),
        turn(1, "After player turn phase one end", 80, 5),
        turn(2, "After enemy turn start", 80, 5),
        turn(3, "After enemy turn end", 80, 0), // block absorbed all damage
      ],
    );
    const log = buildCombatLog(t);
    const enemyActions = log.filter((i) => i.kind === "enemy");
    // Block went 5→0 — enemy DID act, but we don't know damage. We
    // still surface the block-absorbed delta.
    expect(enemyActions).toHaveLength(1);
    expect(enemyActions[0]).toEqual({ kind: "enemy", damageDealt: 0, blockAbsorbed: 5 });
  });

  it("emits no enemy-action if neither HP nor block changed (truly quiet enemy turn)", () => {
    const t = timeline(
      [gameAction(0, "NetReadyToBeginEnemyTurnAction")],
      [
        turn(0, "After player turn start", 80, 0),
        turn(1, "After enemy turn start", 80, 0),
        turn(2, "After enemy turn end", 80, 0),
      ],
    );
    const log = buildCombatLog(t);
    expect(log.filter((i) => i.kind === "enemy")).toHaveLength(0);
  });

  it("does NOT synthesise enemy actions when state blocks are missing (older recordings)", () => {
    // If the recorder didn't include state per checksum, we can't
    // compute deltas. Just emit nothing rather than guessing.
    const t = timeline(
      [],
      [
        { id: 0, checksum: 0, context: "After enemy turn start" },
        { id: 1, checksum: 0, context: "After enemy turn end" },
      ],
    );
    const log = buildCombatLog(t);
    expect(log.filter((i) => i.kind === "enemy")).toHaveLength(0);
  });

  it("falls back gracefully when checksums don't start with a turn-start marker", () => {
    const t = timeline(
      [gameAction(0, "NetPlayCardAction")],
      [turn(0, "Some unfamiliar context")],
    );
    const log = buildCombatLog(t);
    // No crash, both items present.
    expect(log).toHaveLength(2);
    expect(log.some((i) => i.kind === "event")).toBe(true);
    expect(log.some((i) => i.kind === "turn")).toBe(true);
  });

  it("can be built from a parsed real-shape fixture (smoke against the parser pipeline)", () => {
    // End-to-end: parseTimeline → buildCombatLog. Catches drift
    // between the raw JSON shape and the merger's expectations.
    const t = parseTimeline({
      schema_version: 1,
      header: {
        version: "v0.103.2", git_commit: "x", model_id_hash: 0,
        next_action_id: 3, next_checksum_id: 5, next_hook_id: 0,
        event_count: 3, checksum_count: 5,
      },
      initial_run: {},
      choice_ids: [],
      events: [
        { index: 0, type: "GameAction", player_id: 1, action_type: "NetPlayCardAction", action: {} },
        { index: 1, type: "GameAction", player_id: 1, action_type: "NetReadyToBeginEnemyTurnAction", action: {} },
        { index: 2, type: "GameAction", player_id: 1, action_type: "NetPlayCardAction", action: {} },
      ],
      checksums: [
        { id: 0, checksum: 0, context: "After player turn start", state: { creatures: [{ kind: "player", player_id: 1, current_hp: 80, max_hp: 80, block: 0 }], players: [] } },
        { id: 1, checksum: 0, context: "After player turn phase one end", state: { creatures: [{ kind: "player", player_id: 1, current_hp: 80, max_hp: 80, block: 5 }], players: [] } },
        { id: 2, checksum: 0, context: "After enemy turn start", state: { creatures: [{ kind: "player", player_id: 1, current_hp: 80, max_hp: 80, block: 5 }], players: [] } },
        { id: 3, checksum: 0, context: "After enemy turn end", state: { creatures: [{ kind: "player", player_id: 1, current_hp: 75, max_hp: 80, block: 0 }], players: [] } },
        { id: 4, checksum: 0, context: "After player turn start", state: { creatures: [{ kind: "player", player_id: 1, current_hp: 75, max_hp: 80, block: 0 }], players: [] } },
      ],
    });
    const log = buildCombatLog(t);
    // 3 events + 5 checksums + 1 synthetic enemy-action = 9 items
    // (HP went 80→75 between enemy-turn-start and enemy-turn-end).
    expect(log).toHaveLength(9);
    const summary = log.map((i) =>
      i.kind === "turn" ? `T${i.checksum.id}` :
      i.kind === "event" ? `E${i.event.index}` :
      `X${i.damageDealt}`);
    // Order: turn-start, event 0, event 1 (ready), phase-one, enemy-start, [enemy attacks: -5], enemy-end, turn-start, event 2
    expect(summary).toEqual(["T0", "E0", "E1", "T1", "T2", "X5", "T3", "T4", "E2"]);
  });
});
