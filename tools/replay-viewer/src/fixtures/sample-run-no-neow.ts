// Legacy-shape fixture: every current STS2 run produces an "ancient"
// (Neow) entry as the first map_point_history record, but older
// recordings could omit it (the `withNeow=false` wire path that has
// since been removed). The first map_point_history entry is the first
// real combat at engine ActFloor=2 (Neow at engine ActFloor=1 is
// skipped, but the floor counter still includes it). Kept as a
// robustness fixture so the viewer can still render historical
// captures without an ancient row.
//
// HP/gold values mirror what a properly-stamped run.json would carry —
// i.e. AFTER the TestMode-gated UpdatePlayerStatsInMapPointHistory fix
// lands. The unit tests against this fixture lock in the expected post-
// fix shape; the run-history.test.ts file already covers the
// pre-fix-tolerant shape.

export const SAMPLE_RUN_NO_NEOW_RAW = {
  schema_version: 9,
  ascension: 0,
  build_id: "v0.103.2",
  game_mode: "standard",
  killed_by_encounter: "ENCOUNTER.VANTOM_BOSS",
  killed_by_event: "NONE.NONE",
  seed: "sts2headless-42",
  start_time: 1779021871,
  run_time: 600,
  was_abandoned: false,
  win: false,
  modifiers: [],
  platform_type: "none",
  map_point_history: [
    [
      // Engine floor 2 — first real room after Neow.
      {
        map_point_type: "monster",
        rooms: [{ room_type: "monster", model_id: "ENCOUNTER.FUZZY_WURM_CRAWLER_WEAK", turns_taken: 3 }],
        player_stats: [{ player_id: 1, current_hp: 76, max_hp: 80, current_gold: 114, damage_taken: 4, hp_healed: 4 }],
      },
      // Engine floor 3 — event.
      {
        map_point_type: "unknown",
        rooms: [{ room_type: "event", model_id: "EVENT.WELLSPRING", turns_taken: 0 }],
        player_stats: [{ player_id: 1, current_hp: 76, max_hp: 80, current_gold: 114 }],
      },
      // Engine floor 4 — second combat.
      {
        map_point_type: "monster",
        rooms: [{ room_type: "monster", model_id: "ENCOUNTER.SHRINKER_BEETLE_WEAK", turns_taken: 4 }],
        player_stats: [{
          player_id: 1, current_hp: 67, max_hp: 80, current_gold: 129, damage_taken: 15, hp_healed: 6,
          card_choices: [
            { card: { id: "CARD.STRIKE_IRONCLAD" }, was_picked: false },
            { card: { id: "CARD.EXPECT_A_FIGHT" }, was_picked: true },
            { card: { id: "CARD.ANGER" }, was_picked: false },
          ],
        }],
      },
      // Engine floor 5 — event (NOT a combat). User specifically
      // called this out: floor 5 was being misrendered with combat
      // events attached because the lookup matched by floor number,
      // and manifest combat[2] is at floor=5 but corresponds to the
      // NEXT row, not this one.
      {
        map_point_type: "unknown",
        rooms: [{ room_type: "event", model_id: "EVENT.THE_LEGENDS_WERE_TRUE", turns_taken: 0 }],
        player_stats: [{
          player_id: 1, current_hp: 60, max_hp: 80, current_gold: 129, damage_taken: 7,
          event_choices: [{ title: { key: "THE_LEGENDS_WERE_TRUE.choice_a", table: "events" } }],
        }],
      },
      // Engine floor 6 — rest_site.
      {
        map_point_type: "rest_site",
        rooms: [{ room_type: "rest_site", turns_taken: 0 }],
        player_stats: [{ player_id: 1, current_hp: 80, max_hp: 80, current_gold: 129, hp_healed: 20 }],
      },
      // Engine floor 7 — boss. Agent died here.
      {
        map_point_type: "boss",
        rooms: [{ room_type: "boss", model_id: "ENCOUNTER.VANTOM_BOSS", turns_taken: 8 }],
        player_stats: [{
          player_id: 1, current_hp: 0, max_hp: 80, current_gold: 129, damage_taken: 80,
          card_choices: [
            { card: { id: "CARD.CRIMSON_MANTLE" }, was_picked: false },
          ],
        }],
      },
    ],
  ],
  players: [{
    id: 1,
    character: "CHARACTER.IRONCLAD",
    max_potion_slot_count: 3,
    deck: [{ id: "CARD.STRIKE_IRONCLAD" }, { id: "CARD.EXPECT_A_FIGHT", floor_added_to_deck: 4 }],
    relics: [{ id: "RELIC.BURNING_BLOOD" }],
    potions: [{ id: "POTION.HEAL", slot_index: 0 }],
  }],
};

// Manifest matching the no-Neow run. Combat floors mirror what the
// engine's `state.ActFloor` returns at combat-end time — so they
// SKIP non-combat rooms even though map_point_history records them.
//
// Three combats: floor 2 (first monster), floor 4 (second monster),
// floor 7 (boss). The viewer must not match by floor number — it
// must match by *combat ordinal within the act*, otherwise
// non-combat rows whose floor number happens to collide with a
// combat (none here, but easy to construct) would silently steal
// the wrong combat's events.
export const SAMPLE_MANIFEST_NO_NEOW_RAW = {
  version: 1,
  header: {
    game_version: "v0.103.2",
    sts2_dll_sha256: "synthetic",
    model_id_hash: 1357847701,
    git_commit: "test",
    run_history_schema_version: 9,
    protocol_version: 1,
    seed: "42",
    character: "ironclad",
    ascension: 0,
    modifiers: [],
    start_time_unix: 1779021871,
  },
  combats: [
    {
      mcr_file: "combats/act1-floor2-combat.mcr",
      act_index: 0,
      floor: 2,
      room_type: "combat_room",
      outcome: "unknown",
      action_count: 6,
      checksum_count: 11,
    },
    {
      mcr_file: "combats/act1-floor4-combat.mcr",
      act_index: 0,
      floor: 4,
      room_type: "combat_room",
      outcome: "unknown",
      action_count: 9,
      checksum_count: 16,
    },
    {
      mcr_file: "combats/act1-floor7-combat.mcr",
      act_index: 0,
      floor: 7,
      room_type: "combat_room",
      outcome: "unknown",
      action_count: 12,
      checksum_count: 41,
    },
  ],
};
