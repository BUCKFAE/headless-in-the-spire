# Goals not yet discussed through with the agent

## Mods
- This repo should work (where feasible) with other mods

## Testing Speed
- The Integration / End2End Tests should run in parallel, they are already taking quite some time

## Replays / Rendering
- This repo should allow to generate and view replays
- **Status (2026-05-14): parked.** Research in
  [../research/replay-recording-and-viewing.md](../research/replay-recording-and-viewing.md).
  The recording substrate (NDJSON over stdio, per AD-2) already gives us the
  canonical replay artefact for free; viewers (web timeline / Godot Movie Maker
  / etc.) can be added later without lock-in. The one thing that matters *now*
  is **determinism hygiene** — non-determinism introduced today silently
  invalidates any replay we record tomorrow — but that's already a goal-1 /
  goal-5 / AD-3 requirement, not replay-specific. Revisit when a concrete use
  case (golden-replay tests, demo video, RL post-mortem) tells us which viewer
  to build.
