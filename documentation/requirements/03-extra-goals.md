# Goals not yet discussed through with the agent

## Mods
- This repo should work (where feasible) with other mods

## Testing Speed
- The Integration / End2End Tests should run in parallel, they are already taking quite some time

## Replays / Rendering
- This repo should allow to generate and view replays
- **Status (2026-05-16): in flight under
  [AD-8](./02-architecture-decisions.md#ad-8--replay-artefacts-adopt-the-games-mcr--run-verbatim).**
  The canonical artefacts are the game's `.mcr` (per-combat binary
  deterministic replay, with built-in `NetFullCombatState` checksums)
  and `.run` (per-run `RunHistory` JSON, schema_version 9), adopted
  verbatim. The recording layer lives in `src/Sts2Headless.Replay/`,
  hooks into `CombatReplayWriter` via Harmony, and writes a small
  `manifest.json` to tie a run's combats + history together with our
  pin metadata. Determinism canary is the in-process `.mcr`
  re-executor (hard failure on the seed=42 corpus, info elsewhere).
  Pixel-accurate viewing — loading a `.mcr` back into the retail
  game's `NRun` scene via the same code path
  `NMultiplayerTest.LoadReplay` uses — is *unlocked* by the recording
  substrate but lives in a downstream mod outside this repo,
  designed-for but not built. See AD-8 for the full reasoning and the
  2026-05-14 research note for the prior-art survey it superseded.
