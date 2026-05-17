import type { CombatEvent } from "./types";

// Pure functions that turn a CombatEvent into a one-line human-readable
// summary. The viewer's event list calls these to render each row; tests
// pin the expected output per known action_type so a schema drift on the
// C# side (e.g. NetPlayCardAction renaming `target_id`) surfaces as a
// failing summary test rather than a silently degraded UI.
//
// Adding a new action_type: add a branch to `summarizeGameAction` and a
// test in summary.test.ts. The default fallback prints the action_type
// name with a "?" hint — fine for unrecognised events but loud enough
// that a reviewer notices.

export function summarizeEvent(event: CombatEvent): string {
  switch (event.type) {
    case "GameAction":
      return summarizeGameAction(event.action_type, event.action);
    case "HookAction":
      return `hook #${event.hook_id ?? "?"} (${event.game_action_type ?? "?"})`;
    case "ResumeAction":
      return `resume action #${event.action_id ?? "?"}`;
    case "PlayerChoice":
      return `choice #${event.choice_id ?? "?"}`;
  }
}

// Each `NetXAction` from the engine has a known field shape we can pluck
// from the unknown `action` blob. The blob is `unknown` at the type
// level (we deliberately don't pull the game's INetAction schema into
// the viewer's type system, see types.ts), so each branch narrows via
// runtime checks.
function summarizeGameAction(actionType: string, action: unknown): string {
  switch (actionType) {
    case "NetPlayCardAction": {
      const card = pickModelId(action, "model_id");
      const targetId = pickNumber(action, "target_id");
      if (card === null) return "play card (no model_id?)";
      return targetId === null ? `play ${card}` : `play ${card} → enemy ${targetId}`;
    }
    case "NetEndPlayerTurnAction":
      return "end player turn";
    case "NetReadyToBeginEnemyTurnAction":
      return "ready for enemy turn";
    case "NetUndoEndPlayerTurnAction":
      return "undo end player turn";
    case "NetUsePotionAction": {
      const slot = pickNumber(action, "slot_index");
      const target = pickNumber(action, "target_id");
      const slotLabel = slot === null ? "?" : `slot ${slot}`;
      const targetLabel = target === null ? "" : ` → enemy ${target}`;
      return `use potion (${slotLabel})${targetLabel}`;
    }
    case "NetDiscardPotionGameAction": {
      const slot = pickNumber(action, "slot_index");
      return `discard potion${slot === null ? "" : ` (slot ${slot})`}`;
    }
    case "NetMoveToMapCoordAction": {
      const col = pickNumber(action, "col");
      const row = pickNumber(action, "row");
      if (col === null || row === null) return "move to map coord";
      return `move to map (${col},${row})`;
    }
    case "NetPickRelicAction": {
      const relic = pickModelId(action, "model_id");
      return relic === null ? "pick relic" : `pick ${relic}`;
    }
    case "NetVoteForMapCoordAction":
      return "vote for map coord";
    case "NetVoteToMoveToNextActAction":
      return "vote to next act";
    case "NetConsoleCmdGameAction":
      return "console cmd";
    default:
      return `${actionType} (?)`;
  }
}

// ── narrowing helpers ────────────────────────────────────────────────

// Extracts a `model_id` field into its `CATEGORY.ENTRY` form. The
// engine's ModelId serialises to `{ category, entry }` in JSON; the
// human-readable concatenation matches how the .run file's
// model_id strings look ("CARD.STRIKE_IRONCLAD") so consumers don't
// have to learn a separate format.
function pickModelId(action: unknown, field: string): string | null {
  if (action === null || typeof action !== "object") return null;
  const value = (action as Record<string, unknown>)[field];
  if (value === null || typeof value !== "object") return null;
  const category = (value as Record<string, unknown>)["category"];
  const entry = (value as Record<string, unknown>)["entry"];
  if (typeof category !== "string" || typeof entry !== "string") return null;
  return `${category}.${entry}`;
}

function pickNumber(action: unknown, field: string): number | null {
  if (action === null || typeof action !== "object") return null;
  const value = (action as Record<string, unknown>)[field];
  return typeof value === "number" && Number.isFinite(value) ? value : null;
}
