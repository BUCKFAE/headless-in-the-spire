import { describe, it, expect } from "vitest";
import { ingestFiles, timelineFor } from "../src/core/session";
import { SAMPLE_TIMELINE_RAW } from "../src/fixtures/sample-timeline";
import { SAMPLE_RUN_RAW } from "../src/fixtures/sample-run";

const MANIFEST = {
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
    {
      mcr_file: "combats/act1-floor4-combat.mcr",
      act_index: 0,
      floor: 4,
      room_type: "combat_room",
      outcome: "unknown",
      action_count: 12,
      checksum_count: 0,
    },
  ],
};

describe("ingestFiles", () => {
  it("strips a leading run-id directory so timeline keys match manifest mcr_files", () => {
    const session = ingestFiles([
      { path: "1779021871-42/manifest.json", text: JSON.stringify(MANIFEST) },
      { path: "1779021871-42/combats/act1-floor2-combat.mcr.timeline.json", text: JSON.stringify(SAMPLE_TIMELINE_RAW) },
    ]);
    expect(session.manifest.combats).toHaveLength(2);
    expect(timelineFor(session, "combats/act1-floor2-combat.mcr")).toBeDefined();
    // The second combat has no timeline in the inputs — that's fine, lookup returns undefined.
    expect(timelineFor(session, "combats/act1-floor4-combat.mcr")).toBeUndefined();
  });

  it("infers label from the run-id directory", () => {
    const session = ingestFiles([
      { path: "1779021871-42/manifest.json", text: JSON.stringify(MANIFEST) },
    ]);
    expect(session.label).toBe("1779021871-42");
  });

  it("accepts a flat layout (manifest at top level)", () => {
    const session = ingestFiles([
      { path: "manifest.json", text: JSON.stringify(MANIFEST) },
      { path: "combats/act1-floor2-combat.mcr.timeline.json", text: JSON.stringify(SAMPLE_TIMELINE_RAW) },
    ]);
    expect(timelineFor(session, "combats/act1-floor2-combat.mcr")).toBeDefined();
    expect(session.label).toBeUndefined();
  });

  it("ignores unrelated files (binaries, run.json, garbage)", () => {
    const session = ingestFiles([
      { path: "1/manifest.json", text: JSON.stringify(MANIFEST) },
      { path: "1/combats/act1-floor2-combat.mcr", text: "binary-garbage" },
      { path: "1/run.json", text: "{}" },
      { path: "1/combats/act1-floor2-combat.mcr.timeline.json", text: JSON.stringify(SAMPLE_TIMELINE_RAW) },
    ]);
    expect(Object.keys(session.timelines)).toEqual(["combats/act1-floor2-combat.mcr"]);
  });

  it("silently skips a malformed timeline without breaking the run", () => {
    const session = ingestFiles([
      { path: "manifest.json", text: JSON.stringify(MANIFEST) },
      { path: "combats/act1-floor2-combat.mcr.timeline.json", text: "{ not valid json" },
    ]);
    expect(session.manifest.combats).toHaveLength(2);
    expect(timelineFor(session, "combats/act1-floor2-combat.mcr")).toBeUndefined();
  });

  it("throws when no manifest.json is present", () => {
    expect(() => ingestFiles([
      { path: "combats/act1-floor2-combat.mcr.timeline.json", text: JSON.stringify(SAMPLE_TIMELINE_RAW) },
    ])).toThrow(/no manifest\.json/);
  });

  it("ingests run.json next to the manifest", () => {
    const session = ingestFiles([
      { path: "1779021871-42/manifest.json", text: JSON.stringify(MANIFEST) },
      { path: "1779021871-42/run.json", text: JSON.stringify(SAMPLE_RUN_RAW) },
    ]);
    expect(session.runHistory).toBeDefined();
    expect(session.runHistory!.seed).toBe("42");
    expect(session.runHistory!.win).toBe(true);
  });

  it("silently skips a malformed run.json without breaking the rest", () => {
    const session = ingestFiles([
      { path: "manifest.json", text: JSON.stringify(MANIFEST) },
      { path: "run.json", text: "{ not valid json" },
    ]);
    expect(session.runHistory).toBeUndefined();
    expect(session.manifest.combats).toHaveLength(2);
  });
});
