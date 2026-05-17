import { describe, it, expect } from "vitest";
import { parseManifest, parseTimeline } from "../src/core/parse";
import { SAMPLE_TIMELINE_RAW } from "../src/fixtures/sample-timeline";

describe("parseTimeline", () => {
  it("accepts a well-formed timeline and preserves event count", () => {
    const t = parseTimeline(SAMPLE_TIMELINE_RAW);
    expect(t.schema_version).toBe(1);
    expect(t.header.event_count).toBe(6);
    expect(t.events).toHaveLength(6);
    expect(t.checksums).toHaveLength(2);
  });

  it("narrows event types via the discriminated union", () => {
    const t = parseTimeline(SAMPLE_TIMELINE_RAW);
    const gameActions = t.events.filter((e) => e.type === "GameAction");
    expect(gameActions).toHaveLength(3);
    // Discriminated narrowing: action_type is only accessible after
    // the type === "GameAction" check.
    expect(gameActions.map((e) => e.action_type)).toEqual([
      "NetPlayCardAction",
      "NetPlayCardAction",
      "NetEndPlayerTurnAction",
    ]);
  });

  it("rejects an unknown event type loudly", () => {
    const bad = {
      ...SAMPLE_TIMELINE_RAW,
      events: [{ ...SAMPLE_TIMELINE_RAW.events[0], type: "NotARealType" }],
    };
    expect(() => parseTimeline(bad)).toThrow(/timeline\.events\[0\]\.type: unexpected value "NotARealType"/);
  });

  it("path-points missing fields", () => {
    const bad = { ...SAMPLE_TIMELINE_RAW, header: {} };
    expect(() => parseTimeline(bad)).toThrow(/timeline\.header\.version/);
  });

  it("rejects null where a string is required", () => {
    const bad = {
      ...SAMPLE_TIMELINE_RAW,
      header: { ...SAMPLE_TIMELINE_RAW.header, version: null },
    };
    expect(() => parseTimeline(bad)).toThrow(/timeline\.header\.version: expected string, got null/);
  });

  it("preserves per-checksum state block when present", () => {
    const t = parseTimeline(SAMPLE_TIMELINE_RAW);
    expect(t.checksums[0]!.state).toBeDefined();
    const creatures = t.checksums[0]!.state!.creatures;
    expect(creatures).toHaveLength(2);
    const player = creatures.find((c) => c.kind === "player");
    expect(player?.current_hp).toBe(80);
    const monster = creatures.find((c) => c.kind === "monster");
    expect(monster?.kind === "monster" ? monster.monster_id : null).toBe("MONSTER.FUZZY_WURM_CRAWLER");
  });

  it("tolerates checksums without a state block (older recordings)", () => {
    const bare = {
      ...SAMPLE_TIMELINE_RAW,
      checksums: [{ id: 0, checksum: 123, context: "ctx" }],
    };
    const t = parseTimeline(bare);
    expect(t.checksums[0]!.state).toBeUndefined();
  });

  it("path-points malformed creature entries", () => {
    const bad = {
      ...SAMPLE_TIMELINE_RAW,
      checksums: [{
        id: 0, checksum: 1, context: "ctx",
        state: { creatures: [{ kind: "player", current_hp: "not-a-number" }], players: [] },
      }],
    };
    expect(() => parseTimeline(bad)).toThrow(/timeline\.checksums\[0\]\.state\.creatures\[0\]\.current_hp/);
  });
});

describe("parseManifest", () => {
  const VALID_MANIFEST = {
    version: 1,
    header: {
      game_version: "v0.103.2",
      sts2_dll_sha256: "abc",
      model_id_hash: 1357847701,
      git_commit: "x",
      run_history_schema_version: 9,
      protocol_version: 1,
      seed: "42",
      character: "ironclad",
      ascension: 0,
      modifiers: [],
      start_time_unix: 1779016981,
    },
    combats: [
      {
        mcr_file: "combats/act1-floor2-combat.mcr",
        act_index: 0,
        floor: 2,
        room_type: "combat_room",
        outcome: "unknown",
        action_count: 9,
        checksum_count: 0,
      },
    ],
  };

  it("accepts the v1 shape produced by the C# emitter", () => {
    const m = parseManifest(VALID_MANIFEST);
    expect(m.combats[0]!.mcr_file).toBe("combats/act1-floor2-combat.mcr");
    expect(m.combats[0]!.outcome).toBe("unknown");
  });

  it("rejects unknown outcome values", () => {
    const bad = {
      ...VALID_MANIFEST,
      combats: [{ ...VALID_MANIFEST.combats[0], outcome: "draw" }],
    };
    expect(() => parseManifest(bad)).toThrow(/manifest\.combats\[0\]\.outcome: unexpected value "draw"/);
  });
});
