// Synthetic timeline.json fixture for unit tests. Modelled on the real
// shape the C# emitter produces (see CombatTimelineEmitter.cs), trimmed
// to the smallest payload that exercises all four event types + a
// representative GameAction subset (PlayCard with target, PlayCard
// without target, EndPlayerTurn, ReadyToBeginEnemyTurn). The
// CombatTimelineEmitterTests on the C# side anchor the real shape; this
// fixture is the viewer's local mirror so we can run unit tests without
// shelling out to dotnet.
//
// `initial_run` is `{}` to keep the fixture small — the viewer treats
// initial_run as opaque (typed `unknown`) and doesn't validate it.
// Tests that need real engine state should run against a recorded
// timeline.json under replays/.

export const SAMPLE_TIMELINE_RAW = {
  schema_version: 1,
  header: {
    version: "v0.103.2",
    git_commit: "89765e1e",
    model_id_hash: 1357847701,
    next_action_id: 150,
    next_checksum_id: 0,
    next_hook_id: 0,
    event_count: 6,
    checksum_count: 2,
  },
  initial_run: {
    schema_version: 16,
    players: [{
      net_id: 1,
      character_id: "CHARACTER.IRONCLAD",
      relics: [
        { id: "RELIC.GOLDEN_PEARL" },
        { id: "RELIC.STRANGE_FRUIT" },
      ],
      potions: [
        { id: "POTION.HEAL", slot_index: 0 },
      ],
    }],
  },
  choice_ids: [] as number[],
  events: [
    {
      index: 0,
      type: "GameAction",
      player_id: 1141724446,
      action_type: "NetPlayCardAction",
      action: {
        card: { combat_card_index: 2 },
        model_id: { category: "CARD", entry: "BATTLE_TRANCE" },
      },
    },
    {
      index: 1,
      type: "GameAction",
      player_id: 1141724446,
      action_type: "NetPlayCardAction",
      action: {
        card: { combat_card_index: 4 },
        model_id: { category: "CARD", entry: "STRIKE_IRONCLAD" },
        target_id: 1,
      },
    },
    {
      index: 2,
      type: "GameAction",
      player_id: 1141724446,
      action_type: "NetEndPlayerTurnAction",
      action: {},
    },
    {
      index: 3,
      type: "HookAction",
      player_id: 1141724446,
      hook_id: 7,
      game_action_type: "Combat",
    },
    {
      index: 4,
      type: "ResumeAction",
      action_id: 12,
    },
    {
      index: 5,
      type: "PlayerChoice",
      player_id: 1141724446,
      choice_id: 3,
      choice_result: { ChoiceType: "CanonicalCard" },
    },
  ],
  checksums: [
    {
      id: 0,
      checksum: 123456,
      context: "After player turn start",
      state: {
        creatures: [
          { kind: "player", player_id: 1, current_hp: 80, max_hp: 80, block: 0 },
          { kind: "monster", monster_id: "MONSTER.FUZZY_WURM_CRAWLER", current_hp: 55, max_hp: 55, block: 0 },
        ],
        players: [
          { player_id: 1, energy: 3, gold: 99 },
        ],
      },
    },
    {
      id: 1,
      checksum: 789012,
      context: "after player turn phase two end",
      state: {
        creatures: [
          { kind: "player", player_id: 1, current_hp: 72, max_hp: 80, block: 5 },
          { kind: "monster", monster_id: "MONSTER.FUZZY_WURM_CRAWLER", current_hp: 31, max_hp: 55, block: 0 },
        ],
        players: [
          { player_id: 1, energy: 0, gold: 99 },
        ],
      },
    },
  ],
};
