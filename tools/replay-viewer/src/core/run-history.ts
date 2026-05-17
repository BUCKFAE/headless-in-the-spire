// TypeScript mirror of the game's RunHistory JSON. The C# side
// (src/Sts2Headless.Protocol/RunHistory.cs) is the source of truth for
// the typed parts of the schema; here we mirror only the fields the
// viewer actually renders — anything else stays as `unknown` so a new
// game version that adds a field doesn't break the parser. The
// fields are snake_case to match the game's own serialiser output
// verbatim (AD-8 — adopt the game's formats unchanged).

export interface RunHistory {
  schema_version: number;
  ascension: number;
  build_id: string;
  game_mode: string;
  killed_by_encounter: string;
  killed_by_event: string;
  // [act][map_point] — first index is per-act, second is the
  // chronological list of map points visited inside that act.
  map_point_history: MapPointHistoryEntry[][];
  seed: string;
  start_time: number;
  run_time: number;
  was_abandoned: boolean;
  win: boolean;
  players: RunHistoryPlayer[];
}

export interface MapPointHistoryEntry {
  map_point_type: string; // ancient | boss | elite | monster | rest_site | shop | treasure | unknown
  rooms?: MapPointRoom[];
  player_stats?: MapPointPlayerStats[];
}

export interface MapPointRoom {
  room_type: string; // boss | elite | event | monster | rest_site | shop | treasure
  model_id?: string; // EVENT.X / ENCOUNTER.X / RELIC.X depending on room_type
  turns_taken: number;
}

// End-of-map-point snapshot for one player. The game emits a sparse
// shape: numeric counters of 0 may be omitted entirely, optional
// choice/reward arrays only appear when relevant to the room. Every
// nullable here mirrors a game-side `[JsonSerializeCondition]`.
export interface MapPointPlayerStats {
  player_id: number;
  current_hp: number;
  max_hp: number;
  current_gold?: number;
  damage_taken?: number;
  hp_healed?: number;
  gold_gained?: number;
  gold_lost?: number;
  gold_spent?: number;
  gold_stolen?: number;
  max_hp_gained?: number;
  max_hp_lost?: number;
  card_choices?: HistoryCardChoice[];
  cards_gained?: HistoryCardRef[];
  cards_removed?: HistoryCardRef[];
  cards_transformed?: HistoryCardTransformation[];
  event_choices?: HistoryEventChoice[];
  relic_choices?: HistoryRelicChoice[];
  ancient_choice?: HistoryAncientChoice[];
}

export interface HistoryCardChoice {
  card: { id: string };
  was_picked: boolean;
}

export interface HistoryCardRef {
  id: string;
  floor_added_to_deck?: number;
}

export interface HistoryCardTransformation {
  original_card: HistoryCardRef;
  final_card: HistoryCardRef;
}

export interface HistoryEventChoice {
  title: HistoryLocalisationKey;
}

export interface HistoryRelicChoice {
  choice?: string;
  was_picked: boolean;
}

export interface HistoryAncientChoice {
  text_key: string;
  title: HistoryLocalisationKey;
  was_chosen: boolean;
}

export interface HistoryLocalisationKey {
  key: string;
  table: string;
}

export interface RunHistoryPlayer {
  id: number;
  character: string;
  max_potion_slot_count: number;
  deck?: HistoryCardRef[];
  relics?: HistoryCardRef[];
  potions?: HistoryOwnedPotion[];
}

export interface HistoryOwnedPotion {
  id: string;
  slot_index: number;
}
