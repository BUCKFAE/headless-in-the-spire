import type {
  HistoryAncientChoice,
  HistoryCardChoice,
  HistoryCardRef,
  HistoryCardTransformation,
  HistoryEventChoice,
  HistoryLocalisationKey,
  HistoryOwnedPotion,
  HistoryRelicChoice,
  MapPointHistoryEntry,
  MapPointPlayerStats,
  MapPointRoom,
  RunHistory,
  RunHistoryPlayer,
} from "./run-history";

// run.json is bigger and more variable than timeline.json — the game's
// serialiser skips empty collections and zero-valued numeric counters
// (per `[JsonSerializeCondition]` annotations), so the parse strategy
// is "strict on the load-bearing fields, lenient on the optional
// reward / choice arrays".
//
// Unrecognised top-level keys are silently ignored so a game-version
// schema bump that adds a field doesn't break the viewer; existing
// keys that change shape (e.g. `map_point_history` going from
// `string[][]` to `object[][]`) still fail loudly at the array
// validator. That keeps the failure mode aligned with what the viewer
// can actually render.

export function parseRunHistory(raw: unknown): RunHistory {
  const obj = expectObject(raw, "run");
  return {
    schema_version: expectInt(obj["schema_version"], "run.schema_version"),
    ascension: expectInt(obj["ascension"], "run.ascension"),
    build_id: expectString(obj["build_id"], "run.build_id"),
    game_mode: expectString(obj["game_mode"], "run.game_mode"),
    killed_by_encounter: expectString(obj["killed_by_encounter"], "run.killed_by_encounter"),
    killed_by_event: expectString(obj["killed_by_event"], "run.killed_by_event"),
    map_point_history: expectArray(obj["map_point_history"], "run.map_point_history").map((act, i) =>
      expectArray(act, `run.map_point_history[${i}]`).map((entry, j) =>
        parseMapPointEntry(entry, `run.map_point_history[${i}][${j}]`),
      ),
    ),
    seed: expectString(obj["seed"], "run.seed"),
    start_time: expectInt(obj["start_time"], "run.start_time"),
    run_time: expectInt(obj["run_time"], "run.run_time"),
    was_abandoned: expectBool(obj["was_abandoned"], "run.was_abandoned"),
    win: expectBool(obj["win"], "run.win"),
    players: expectArray(obj["players"], "run.players").map((p, i) =>
      parsePlayer(p, `run.players[${i}]`),
    ),
  };
}

function parseMapPointEntry(raw: unknown, path: string): MapPointHistoryEntry {
  const obj = expectObject(raw, path);
  const out: MapPointHistoryEntry = {
    map_point_type: expectString(obj["map_point_type"], `${path}.map_point_type`),
  };
  if (obj["rooms"] !== undefined) {
    out.rooms = expectArray(obj["rooms"], `${path}.rooms`).map((r, i) => parseRoom(r, `${path}.rooms[${i}]`));
  }
  if (obj["player_stats"] !== undefined) {
    out.player_stats = expectArray(obj["player_stats"], `${path}.player_stats`).map((s, i) =>
      parsePlayerStats(s, `${path}.player_stats[${i}]`),
    );
  }
  return out;
}

function parseRoom(raw: unknown, path: string): MapPointRoom {
  const obj = expectObject(raw, path);
  const room: MapPointRoom = {
    room_type: expectString(obj["room_type"], `${path}.room_type`),
    turns_taken: expectInt(obj["turns_taken"], `${path}.turns_taken`),
  };
  if (obj["model_id"] !== undefined) {
    room.model_id = expectString(obj["model_id"], `${path}.model_id`);
  }
  return room;
}

function parsePlayerStats(raw: unknown, path: string): MapPointPlayerStats {
  const obj = expectObject(raw, path);
  const out: MapPointPlayerStats = {
    player_id: expectInt(obj["player_id"], `${path}.player_id`),
    current_hp: expectInt(obj["current_hp"], `${path}.current_hp`),
    max_hp: expectInt(obj["max_hp"], `${path}.max_hp`),
  };
  copyOptInt(obj, "current_gold", path, out, "current_gold");
  copyOptInt(obj, "damage_taken", path, out, "damage_taken");
  copyOptInt(obj, "hp_healed", path, out, "hp_healed");
  copyOptInt(obj, "gold_gained", path, out, "gold_gained");
  copyOptInt(obj, "gold_lost", path, out, "gold_lost");
  copyOptInt(obj, "gold_spent", path, out, "gold_spent");
  copyOptInt(obj, "gold_stolen", path, out, "gold_stolen");
  copyOptInt(obj, "max_hp_gained", path, out, "max_hp_gained");
  copyOptInt(obj, "max_hp_lost", path, out, "max_hp_lost");
  if (obj["card_choices"] !== undefined) {
    out.card_choices = expectArray(obj["card_choices"], `${path}.card_choices`).map((c, i) =>
      parseCardChoice(c, `${path}.card_choices[${i}]`),
    );
  }
  if (obj["cards_gained"] !== undefined) {
    out.cards_gained = expectArray(obj["cards_gained"], `${path}.cards_gained`).map((c, i) =>
      parseCardRef(c, `${path}.cards_gained[${i}]`),
    );
  }
  if (obj["cards_removed"] !== undefined) {
    out.cards_removed = expectArray(obj["cards_removed"], `${path}.cards_removed`).map((c, i) =>
      parseCardRef(c, `${path}.cards_removed[${i}]`),
    );
  }
  if (obj["cards_transformed"] !== undefined) {
    out.cards_transformed = expectArray(obj["cards_transformed"], `${path}.cards_transformed`).map((c, i) =>
      parseCardTransformation(c, `${path}.cards_transformed[${i}]`),
    );
  }
  if (obj["event_choices"] !== undefined) {
    out.event_choices = expectArray(obj["event_choices"], `${path}.event_choices`).map((c, i) =>
      parseEventChoice(c, `${path}.event_choices[${i}]`),
    );
  }
  if (obj["relic_choices"] !== undefined) {
    out.relic_choices = expectArray(obj["relic_choices"], `${path}.relic_choices`).map((c, i) =>
      parseRelicChoice(c, `${path}.relic_choices[${i}]`),
    );
  }
  if (obj["ancient_choice"] !== undefined) {
    out.ancient_choice = expectArray(obj["ancient_choice"], `${path}.ancient_choice`).map((c, i) =>
      parseAncientChoice(c, `${path}.ancient_choice[${i}]`),
    );
  }
  return out;
}

function parseCardChoice(raw: unknown, path: string): HistoryCardChoice {
  const obj = expectObject(raw, path);
  const card = expectObject(obj["card"], `${path}.card`);
  return {
    card: { id: expectString(card["id"], `${path}.card.id`) },
    was_picked: expectBool(obj["was_picked"], `${path}.was_picked`),
  };
}

function parseCardRef(raw: unknown, path: string): HistoryCardRef {
  const obj = expectObject(raw, path);
  const out: HistoryCardRef = { id: expectString(obj["id"], `${path}.id`) };
  copyOptInt(obj, "floor_added_to_deck", path, out, "floor_added_to_deck");
  return out;
}

function parseCardTransformation(raw: unknown, path: string): HistoryCardTransformation {
  const obj = expectObject(raw, path);
  return {
    original_card: parseCardRef(obj["original_card"], `${path}.original_card`),
    final_card: parseCardRef(obj["final_card"], `${path}.final_card`),
  };
}

function parseEventChoice(raw: unknown, path: string): HistoryEventChoice {
  const obj = expectObject(raw, path);
  return { title: parseLocalisationKey(obj["title"], `${path}.title`) };
}

function parseRelicChoice(raw: unknown, path: string): HistoryRelicChoice {
  const obj = expectObject(raw, path);
  const out: HistoryRelicChoice = { was_picked: expectBool(obj["was_picked"], `${path}.was_picked`) };
  if (obj["choice"] !== undefined && obj["choice"] !== null) {
    out.choice = expectString(obj["choice"], `${path}.choice`);
  }
  return out;
}

function parseAncientChoice(raw: unknown, path: string): HistoryAncientChoice {
  const obj = expectObject(raw, path);
  return {
    text_key: expectString(obj["TextKey"] ?? obj["text_key"], `${path}.text_key`),
    title: parseLocalisationKey(obj["title"], `${path}.title`),
    was_chosen: expectBool(obj["was_chosen"], `${path}.was_chosen`),
  };
}

function parseLocalisationKey(raw: unknown, path: string): HistoryLocalisationKey {
  const obj = expectObject(raw, path);
  return {
    key: expectString(obj["key"], `${path}.key`),
    table: expectString(obj["table"], `${path}.table`),
  };
}

function parsePlayer(raw: unknown, path: string): RunHistoryPlayer {
  const obj = expectObject(raw, path);
  const out: RunHistoryPlayer = {
    id: expectInt(obj["id"], `${path}.id`),
    character: expectString(obj["character"], `${path}.character`),
    max_potion_slot_count: expectInt(obj["max_potion_slot_count"], `${path}.max_potion_slot_count`),
  };
  if (obj["deck"] !== undefined) {
    out.deck = expectArray(obj["deck"], `${path}.deck`).map((c, i) => parseCardRef(c, `${path}.deck[${i}]`));
  }
  if (obj["relics"] !== undefined) {
    out.relics = expectArray(obj["relics"], `${path}.relics`).map((c, i) => parseCardRef(c, `${path}.relics[${i}]`));
  }
  if (obj["potions"] !== undefined) {
    out.potions = expectArray(obj["potions"], `${path}.potions`).map((p, i) => parsePotion(p, `${path}.potions[${i}]`));
  }
  return out;
}

function parsePotion(raw: unknown, path: string): HistoryOwnedPotion {
  const obj = expectObject(raw, path);
  return {
    id: expectString(obj["id"], `${path}.id`),
    slot_index: expectInt(obj["slot_index"], `${path}.slot_index`),
  };
}

// ── primitives (small dup with parse.ts is fine; same validators) ─────

function expectObject(raw: unknown, path: string): Record<string, unknown> {
  if (raw === null || typeof raw !== "object" || Array.isArray(raw)) {
    throw new Error(`${path}: expected object, got ${describe(raw)}`);
  }
  return raw as Record<string, unknown>;
}

function expectArray(raw: unknown, path: string): unknown[] {
  if (!Array.isArray(raw)) throw new Error(`${path}: expected array, got ${describe(raw)}`);
  return raw;
}

function expectString(raw: unknown, path: string): string {
  if (typeof raw !== "string") throw new Error(`${path}: expected string, got ${describe(raw)}`);
  return raw;
}

function expectInt(raw: unknown, path: string): number {
  if (typeof raw !== "number" || !Number.isFinite(raw)) {
    throw new Error(`${path}: expected number, got ${describe(raw)}`);
  }
  return raw;
}

function expectBool(raw: unknown, path: string): boolean {
  if (typeof raw !== "boolean") throw new Error(`${path}: expected boolean, got ${describe(raw)}`);
  return raw;
}

function copyOptInt(
  src: Record<string, unknown>,
  key: string,
  basePath: string,
  dst: object,
  dstKey: string,
): void {
  if (src[key] === undefined) return;
  (dst as Record<string, unknown>)[dstKey] = expectInt(src[key], `${basePath}.${key}`);
}

function describe(value: unknown): string {
  if (value === null) return "null";
  if (Array.isArray(value)) return "array";
  return typeof value;
}
