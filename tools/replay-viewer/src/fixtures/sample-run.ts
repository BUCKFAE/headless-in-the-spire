// Synthetic run.json mirroring the shape the engine emits at
// schema_version 9 (v0.103.2). Built from observed
// vendor/sample-saves/*.run files but trimmed to the smallest payload
// that exercises every viewer rendering path:
//
//   - Neow blessing (ancient_choice with one was_chosen=true)
//   - A monster floor with a card_choices reward
//   - An event floor with event_choices
//   - A rest_site floor with hp_healed
//   - A boss floor with a relic_choice + final HP drop
//
// Same approach as src/fixtures/sample-timeline.ts: pin the shape
// locally so unit tests don't need a real recording on disk.

export const SAMPLE_RUN_RAW = {
  schema_version: 9,
  ascension: 0,
  build_id: "v0.103.2",
  game_mode: "standard",
  killed_by_encounter: "NONE.NONE",
  killed_by_event: "NONE.NONE",
  seed: "42",
  start_time: 1779021871,
  run_time: 1500,
  was_abandoned: false,
  win: true,
  modifiers: [],
  platform_type: "steam",
  map_point_history: [
    [
      {
        map_point_type: "ancient",
        rooms: [{ room_type: "event", model_id: "EVENT.NEOW", turns_taken: 0 }],
        player_stats: [{
          player_id: 1,
          current_hp: 80,
          max_hp: 80,
          ancient_choice: [
            { TextKey: "NEOWS_TORMENT", title: { key: "NEOWS_TORMENT.title", table: "relics" }, was_chosen: false },
            { TextKey: "GOLDEN_PEARL", title: { key: "GOLDEN_PEARL.title", table: "relics" }, was_chosen: true },
          ],
        }],
      },
      {
        map_point_type: "monster",
        rooms: [{ room_type: "monster", model_id: "ENCOUNTER.FUZZY_WURMS_WEAK", turns_taken: 3 }],
        player_stats: [{
          player_id: 1,
          current_hp: 76,
          max_hp: 80,
          damage_taken: 4,
          current_gold: 114,
          gold_gained: 15,
          card_choices: [
            { card: { id: "CARD.STRIKE_IRONCLAD" }, was_picked: false },
            { card: { id: "CARD.BATTLE_TRANCE" }, was_picked: true },
            { card: { id: "CARD.ANGER" }, was_picked: false },
          ],
        }],
      },
      {
        map_point_type: "monster",
        rooms: [{ room_type: "event", model_id: "EVENT.STONE_OF_ALL_TIME", turns_taken: 0 }],
        player_stats: [{
          player_id: 1,
          current_hp: 70,
          max_hp: 80,
          damage_taken: 6,
          event_choices: [
            { title: { key: "STONE_OF_ALL_TIME.choice_a", table: "events" } },
          ],
        }],
      },
      {
        map_point_type: "rest_site",
        rooms: [{ room_type: "rest_site", model_id: "REST_SITE.STANDARD", turns_taken: 0 }],
        player_stats: [{
          player_id: 1,
          current_hp: 80,
          max_hp: 80,
          hp_healed: 10,
        }],
      },
      {
        map_point_type: "boss",
        rooms: [{ room_type: "boss", model_id: "ENCOUNTER.SOUL_FYSH_BOSS", turns_taken: 8 }],
        player_stats: [{
          player_id: 1,
          current_hp: 40,
          max_hp: 80,
          damage_taken: 40,
          relic_choices: [{ choice: "RELIC.STRANGE_FRUIT", was_picked: true }],
        }],
      },
    ],
  ],
  players: [{
    id: 1,
    character: "CHARACTER.IRONCLAD",
    max_potion_slot_count: 3,
    deck: [{ id: "CARD.STRIKE_IRONCLAD" }, { id: "CARD.BATTLE_TRANCE", floor_added_to_deck: 2 }],
    relics: [{ id: "RELIC.GOLDEN_PEARL" }, { id: "RELIC.STRANGE_FRUIT", floor_added_to_deck: 5 }],
    potions: [{ id: "POTION.HEAL", slot_index: 0 }],
  }],
};
