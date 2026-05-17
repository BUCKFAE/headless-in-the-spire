import { describe, it, expect } from "vitest";
import { saveSession, loadSession, clearSession, type KeyValueStore } from "../src/core/persistence";
import { SAMPLE_TIMELINE_RAW } from "../src/fixtures/sample-timeline";
import { SAMPLE_RUN_RAW } from "../src/fixtures/sample-run";

const MANIFEST_JSON = JSON.stringify({
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
});

function memoryStore(): KeyValueStore {
  const map = new Map<string, string>();
  return {
    get: (k) => map.get(k) ?? null,
    set: (k, v) => { map.set(k, v); },
    remove: (k) => { map.delete(k); },
  };
}

describe("persistence", () => {
  it("round-trips manifest + timelines via raw JSON", () => {
    const store = memoryStore();
    saveSession(
      {
        manifestJson: MANIFEST_JSON,
        timelineJsons: { "combats/act1-floor2-combat.mcr": JSON.stringify(SAMPLE_TIMELINE_RAW) },
        label: "1779021871-42",
      },
      store,
    );
    const restored = loadSession(store);
    expect(restored).not.toBeNull();
    expect(restored!.manifest.combats).toHaveLength(1);
    expect(restored!.timelines["combats/act1-floor2-combat.mcr"]).toBeDefined();
    expect(restored!.label).toBe("1779021871-42");
  });

  it("returns null when nothing has been saved", () => {
    expect(loadSession(memoryStore())).toBeNull();
  });

  it("clearSession wipes the cache", () => {
    const store = memoryStore();
    saveSession({ manifestJson: MANIFEST_JSON, timelineJsons: {} }, store);
    clearSession(store);
    expect(loadSession(store)).toBeNull();
  });

  it("drops a poisoned cache (returns null and clears, doesn't keep failing)", () => {
    const store = memoryStore();
    store.set("sts2-replay-viewer:last-session", "{ definitely not json");
    expect(loadSession(store)).toBeNull();
    // After a failed read, the cache should be cleared so the next page
    // load isn't doomed to keep hitting the same broken payload.
    expect(loadSession(store)).toBeNull();
  });

  it("drops a payload with the wrong schema version", () => {
    const store = memoryStore();
    store.set("sts2-replay-viewer:last-session", JSON.stringify({
      schema: 9999,
      manifest_json: MANIFEST_JSON,
      timeline_jsons: {},
    }));
    expect(loadSession(store)).toBeNull();
  });

  it("round-trips run.json when present", () => {
    const store = memoryStore();
    saveSession(
      {
        manifestJson: MANIFEST_JSON,
        timelineJsons: {},
        runJson: JSON.stringify(SAMPLE_RUN_RAW),
      },
      store,
    );
    const restored = loadSession(store);
    expect(restored!.runHistory?.seed).toBe("42");
    expect(restored!.runHistory?.win).toBe(true);
  });

  it("skips malformed timelines in the cache without dropping the whole session", () => {
    const store = memoryStore();
    saveSession(
      {
        manifestJson: MANIFEST_JSON,
        timelineJsons: {
          "combats/act1-floor2-combat.mcr": JSON.stringify(SAMPLE_TIMELINE_RAW),
          "combats/act1-floor4-combat.mcr": "{ broken json",
        },
      },
      store,
    );
    const restored = loadSession(store);
    expect(restored).not.toBeNull();
    expect(Object.keys(restored!.timelines)).toEqual(["combats/act1-floor2-combat.mcr"]);
  });
});
