import { describe, it, expect } from "vitest";
import { parseRunHistory } from "../src/core/parse-run-history";
import { SAMPLE_RUN_RAW } from "../src/fixtures/sample-run";

describe("parseRunHistory", () => {
  it("preserves the schema-version 9 shape", () => {
    const r = parseRunHistory(SAMPLE_RUN_RAW);
    expect(r.schema_version).toBe(9);
    expect(r.seed).toBe("42");
    expect(r.win).toBe(true);
    expect(r.was_abandoned).toBe(false);
  });

  it("walks every map_point_history entry into a typed shape", () => {
    const r = parseRunHistory(SAMPLE_RUN_RAW);
    expect(r.map_point_history).toHaveLength(1);
    expect(r.map_point_history[0]).toHaveLength(5);
    const ancient = r.map_point_history[0]![0]!;
    expect(ancient.map_point_type).toBe("ancient");
    expect(ancient.player_stats?.[0]?.ancient_choice?.[1]?.was_chosen).toBe(true);
  });

  it("preserves card_choices with was_picked discrimination", () => {
    const r = parseRunHistory(SAMPLE_RUN_RAW);
    const monsterFloor = r.map_point_history[0]![1]!;
    const choices = monsterFloor.player_stats?.[0]?.card_choices ?? [];
    expect(choices).toHaveLength(3);
    expect(choices.filter((c) => c.was_picked).map((c) => c.card.id)).toEqual(["CARD.BATTLE_TRANCE"]);
  });

  it("preserves event_choices, relic_choices, and final-floor HP", () => {
    const r = parseRunHistory(SAMPLE_RUN_RAW);
    const eventFloor = r.map_point_history[0]![2]!;
    expect(eventFloor.player_stats?.[0]?.event_choices?.[0]?.title.key).toBe("STONE_OF_ALL_TIME.choice_a");
    const bossFloor = r.map_point_history[0]![4]!;
    expect(bossFloor.player_stats?.[0]?.relic_choices?.[0]?.choice).toBe("RELIC.STRANGE_FRUIT");
    expect(bossFloor.player_stats?.[0]?.current_hp).toBe(40);
  });

  it("path-points missing required fields", () => {
    const bad = { ...SAMPLE_RUN_RAW, seed: 42 }; // wrong type
    expect(() => parseRunHistory(bad)).toThrow(/run\.seed: expected string/);
  });

  it("tolerates absent optional collections (sparse encoding)", () => {
    const bare = {
      ...SAMPLE_RUN_RAW,
      map_point_history: [[{
        map_point_type: "monster",
        rooms: [{ room_type: "monster", turns_taken: 1 }],
        player_stats: [{ player_id: 1, current_hp: 80, max_hp: 80 }],
      }]],
    };
    const r = parseRunHistory(bare);
    expect(r.map_point_history[0]![0]!.player_stats?.[0]?.card_choices).toBeUndefined();
  });
});
