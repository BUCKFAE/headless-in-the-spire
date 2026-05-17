import { describe, it, expect } from "vitest";
import { parseTimeline } from "../src/core/parse";
import { summarizeEvent } from "../src/core/summary";
import { SAMPLE_TIMELINE_RAW } from "../src/fixtures/sample-timeline";

describe("summarizeEvent", () => {
  const events = parseTimeline(SAMPLE_TIMELINE_RAW).events;

  it("renders PlayCard without target as just the card", () => {
    expect(summarizeEvent(events[0]!)).toBe("play CARD.BATTLE_TRANCE");
  });

  it("renders PlayCard with target as card → enemy N", () => {
    expect(summarizeEvent(events[1]!)).toBe("play CARD.STRIKE_IRONCLAD → enemy 1");
  });

  it("renders EndPlayerTurn with a fixed string", () => {
    expect(summarizeEvent(events[2]!)).toBe("end player turn");
  });

  it("renders HookAction with hook id and action type", () => {
    expect(summarizeEvent(events[3]!)).toBe("hook #7 (Combat)");
  });

  it("renders ResumeAction with action id", () => {
    expect(summarizeEvent(events[4]!)).toBe("resume action #12");
  });

  it("renders PlayerChoice with choice id", () => {
    expect(summarizeEvent(events[5]!)).toBe("choice #3");
  });

  it("falls back loudly for unknown action types", () => {
    expect(
      summarizeEvent({
        index: 0,
        type: "GameAction",
        action_type: "NetSomeNewActionV2",
        action: {},
      }),
    ).toBe("NetSomeNewActionV2 (?)");
  });
});
