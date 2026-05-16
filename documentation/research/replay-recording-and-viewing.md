# Replay recording and viewing — research

Snapshot date: 2026-05-14. This is a research / thinking note, not a design doc.
It pulls on the threads in [01-initial-goals.md §5](../requirements/01-initial-goals.md)
and [03-extra-goals.md](../requirements/03-extra-goals.md) ("This repo should allow
to generate and view replays") and tries to answer: **what does a "replay" mean
for this project, and what's the cheapest path to a watchable artefact?**

It deliberately does not pick a design — the open questions at the bottom are the
ones to resolve before writing code.

---

## What "replay" needs to mean here

The five existing goals leave room for two genuinely different artefacts that
both get called "replays" in casual conversation. They have very different cost
profiles and different consumers.

| Artefact | Shape | Primary consumer | Used for |
| --- | --- | --- | --- |
| **Run log** | NDJSON / JSONL: every request, every response, optional periodic state snapshots. | Test harness, RL training pipeline, post-hoc bot debugging. | Determinism canary, golden-replay tests, regression diffing, training data. |
| **Watchable replay** | Video, image sequence, animated web timeline, or a save-game playback inside the real game. | Human looking at a run. | Sharing interesting runs, eyeballing what an agent actually did, demo material. |

Goal 5 (record replays) and AD-2 (NDJSON stdio) already point straight at the
first artefact, and effectively make it free — every byte the host writes is a
replay byte. Extra-goal "generate / view replays" is asking for the second
artefact. The two should share a recording format but diverge on the rendering /
viewing side.

A useful framing: **the run log is the canonical replay**, and any watchable
form (video, image sequence, web viewer) is a *renderer* over that log. That
keeps determinism, persistence and diffability (all goal-5 requirements) on
the cheap side, and lets us experiment freely with viewers without committing
to one.

---

## State of the recording side (what we already get for free)

Per [AD-2](../requirements/02-architecture-decisions.md), each headless host's
stdio is one NDJSON message per line. `tee`ing the stream produces a `.ndjson`
file that already has:

- the full action vocabulary the caller invoked,
- every response (decision state, errors, notifications),
- timestamps if the envelope carries them (TBD),
- naturally diffable text encoding.

Compared to [sts2-cli's `logs/*.jsonl`](sts2-cli-anatomy.md) (auto-deleted after
7 days, no header, no version pin), what's missing is small:

1. **A header record at the start of the stream.** Game version + DLL SHA-256
   (from `GAME_VERSION`), seed, character, ascension, our protocol version, the
   reflection-manifest hash from [AD-3](../requirements/02-architecture-decisions.md).
   Without this we cannot detect "you're trying to replay a v0.103.2 log on
   v0.105.1" and will silently desync.
2. **Persistence policy.** Not 7-day auto-delete. Goal 5 is explicit. A simple
   `replays/<game-version>/<utc-timestamp>-<short-seed>-<character>.ndjson`
   layout fits the AD-3 `snapshots/<game-version>/...` convention.
3. **Optional snapshot index.** Periodically (every N decisions, or at every
   room transition) emit a `state_snapshot` notification with the full canonical
   state. Lets viewers seek without re-executing from t=0; lets golden-replay
   tests fail early at the first divergent snapshot.
4. **Compression.** NDJSON compresses ~10× with `zstd`. A full Act-1 run is
   ~hundreds of decisions, ~MB-scale uncompressed; we should not commit to
   storing them raw forever.

None of this is research — it's a straightforward extension of AD-2. The
research question is what to do *with* the resulting file.

---

## Viewing options, ordered by effort

### A. Re-execute the log headlessly and diff (already covered by goal-5 use cases)

Strictly not "viewing", but the cheapest way to *use* a replay: re-run the
recorded actions through a fresh headless host, compare state stream to the
original. This is [e2e-testing-and-self-feedback.md §Layer 3](e2e-testing-and-self-feedback.md)
and is also the determinism canary. Zero new work beyond the recording side.

Cost: low. Value: high (regression + canary). Watchable: **no**.

### B. Browser-based timeline viewer over the run log

Treat the `.ndjson` as a deterministic input to a thin web UI that, at each
decision point, renders:

- the active screen kind (combat / map / event / shop / rest / card reward),
- the relevant state slice (hand, draw pile count, enemies + intents, HP / block
  / energy, available actions, map node options),
- a step / scrub control across the decision points.

There is no game logic in this viewer — it only knows how to draw the JSON
states that the host already produced. Card / relic / monster / event art is
sourced from a static-data oracle (see [tools to mine](#tools-and-projects-to-inspect-or-clone)
below; [`ptrlrd/spire-codex`](https://github.com/ptrlrd/spire-codex) and
[`elliotttate/sts2-modding-mcp`](https://github.com/elliotttate/sts2-modding-mcp)
are the obvious candidates).

Cost: medium (TypeScript/React app + an asset pipeline for cards/relics). Value:
high for human inspection, RL debugging, demos. Watchable: **yes, as a
step-through; not as a video by default but trivially screen-recorded.**

Caveat: this is not pixel-accurate to the real game. It's a *summary* viewer.
That is probably fine for inspecting bot behaviour; it is not fine if the user
wanted "watch a run on YouTube".

### C. Headed playback through the real game (rendered)

The headless runner stubs `GodotSharp` so there's no renderer. But we can run a
*separate* "playback" mode that uses the real game (`SlayTheSpire2.exe` + real
`GodotSharp.dll`) with a thin **playback mod** that reads our log and drives
the same action vocabulary into the actual UI. That gives you the actual game
visuals, animations, and audio.

Two sub-options for the recording side once we're in the real game:

- **C1. OBS / external screen capture.** Trivial, works today, no mod needed
  beyond input driving. Output: lossy `.mp4`, not deterministic frame-for-frame.
- **C2. Godot's built-in Movie Maker mode (`--write-movie path.avi`).** Godot 4
  ships with a [Movie Maker mode](https://docs.godotengine.org/en/stable/tutorials/animation/creating_movies.html)
  that decouples the engine from wall-clock time and renders deterministically
  to AVI (raw / MJPEG) or PNG sequences, with audio captured to WAV. Frame rate
  is configurable; the engine runs as fast or as slow as the renderer can
  produce frames. This is **the** killer feature for "render a replay to a
  file" — it's deterministic, offline, and built in. The catch: Mega Crit's
  shipped binary doesn't expose Godot CLI flags directly
  ([general-sts2-modding.md §2](general-sts2-modding.md)), so the playback mod
  may have to enable it via reflection / `Engine.MaxFps = ...` / a launch
  wrapper. Worth a 30-min spike to confirm whether `--write-movie` is
  accessible on the retail binary.

Cost: medium-to-high. We'd need a "playback mod" that re-issues actions from a
log into the real game (essentially a thin mirror of the headless action set,
without the GodotStubs swap). It also crosses the `GodotStubs` / real-Godot
boundary — different DLL set, different bootstrap.

Value: high for *demos*, low for *inspecting bot behaviour* (animations get in
the way). It's also explicitly listed as a non-goal in
[01-initial-goals.md](../requirements/01-initial-goals.md):

> Visual rendering. We target the headless engine pattern (stub `GodotSharp`
> à la `sts2-cli`). If anyone wants to watch a run, they can replay it in
> the real game.

That non-goal language is interesting in light of extra-goal 3 — they're not
contradictory if we read "we target the headless engine pattern" as "the host
itself is headless" and the playback mode as a *separate* tool that *also*
targets the real game. Worth pinning down with the user.

Watchable: **yes, pixel-accurate, video output.**

### D. Headless rendering with a real Godot binary

A middle path: run the *real* `GodotSharp.dll` (not our stubs) headlessly via
`--headless` plus `--write-movie`, driving the game directly without an
on-screen window. This is the Godot-canonical way to render replays on CI.

Problem: STS2's shipped binary doesn't ship the Godot main loop directly,
and the existing precedent (sts2-cli) explicitly *replaces* `GodotSharp`
because the real one drags in a renderer / asset pipeline that headless
projects don't want to deal with. We'd be reversing that choice for the
playback mode. It might work, but it's almost certainly more fragile than
option C and gets us the same artefact.

Cost: high. Value: same artefact as C2. Probably not worth pursuing unless C2
is blocked.

---

## Tools and projects to inspect or clone

The user explicitly asked: "extra tools we could use / clone to inspect for
that?" Here's the candidate list, grouped by what they offer.

### Direct replay precedent in STS2

- **`wuhao21/sts2-cli`** — already cloned at `external-tools/sts2-cli/`. The
  `JSONL log per run in logs/` is the closest existing precedent for our run-log
  artefact. `RunSimulator.cs` has the action and decision serialisation we want
  to formalise. Lessons already absorbed in
  [sts2-cli-anatomy.md](sts2-cli-anatomy.md); the new question for replays is
  the *file* shape (header? snapshot index? versioned directory?). The repo's
  `logs/` is worth re-reading with that lens specifically — *not* its
  auto-delete-after-7-days policy.

- **`Gennadiyev/STS2MCP`** — already on the survey, no documented replay format,
  but they do serialise live state to JSON for the LLM. Useful only as a
  *negative* reference for what an unstructured, token-bloated state payload
  looks like.

- **`CharTyr/STS2-Agent`** — same story as STS2MCP; **SSE** push of state
  notifications is the interesting bit (suggests how a "live viewer" could
  subscribe to a running game), but no replay artefact.

### Live overlays / readers (interesting as viewer prior art, not as replay code)

- **`thequantumfalcon/spirescope`** — Python, runs `localhost:8000/live` and
  `/overlay` (OBS browser source) showing a live run tracker. **Closest
  existing thing to a JSON-driven web viewer in the ecosystem.** It reads live
  game state via screen scraping / poll, not from a log, but the UI patterns
  (room timeline, deck / relic display, OBS-compatible CSS) are directly
  reusable for option B. MIT-flavoured, ~16 stars; worth cloning into
  `external-tools/` for a read-through.
  <https://github.com/thequantumfalcon/spirescope>

- **`ebadon16/sts2-advisor`** — in-game `CanvasLayer` overlay + SQLite + a
  Cloudflare Worker backend. Closer to a HUD than a replay viewer, but its
  archetype-detection code is the kind of post-hoc analytics we'll want on top
  of replays eventually. <https://github.com/ebadon16/sts2-advisor>

### Asset oracles (load-bearing for any visual viewer)

- **`ptrlrd/spire-codex`** — already flagged in
  [existing-headless-libraries.md](existing-headless-libraries.md) as the type
  oracle. For replay viewing it's also the *art* oracle: 403 cards, 111
  monsters, spine-webgl skeletal animation rendering, per-version history. The
  frontend (Next.js 16 + spine-webgl) is **almost certainly a better starting
  point for a web viewer than building from scratch**. License is **PolyForm
  Noncommercial 1.0.0** — we can use it as a reference but cannot fork its
  frontend into a commercial product without renegotiating, and we should not
  redistribute its scraped assets.

- **`elliotttate/sts2-modding-mcp`** — extracts **15,000+ Godot assets** from
  the game, including all card / relic art, monster sprites, scene files. MIT.
  This is the asset pipeline we'd want to hook into for a self-hosted viewer
  rather than depending on spire-codex's hosted API. The repo also indexes
  3,048 entities + 144 hooks, which overlaps with our future reflection
  manifest. <https://github.com/elliotttate/sts2-modding-mcp>

### Mod / driver references for option C (rendered playback in real game)

- **`Alchyr/ModTemplate-StS2`** — the canonical "how does a mod load" reference.
  Anything we build as a playback mod starts here.
  <https://github.com/Alchyr/ModTemplate-StS2>

- **`freude916/sts2-quickRestart`** — a small mod that drives menu actions
  through reflection. Useful as a minimal example of *how to push input into
  the real game from a mod*, which is what a playback mod has to do.

- **`longkerdandy/STS2-Cli-Mod`** — "the cleanest mod/CLI process split of the
  bunch." If we go option C, splitting along the same mod-in-game /
  external-driver line is the natural shape. The Named-Pipe IPC is not what
  we'd reuse (AD-2 picks stdio) but the process boundary is the same.

### Engine-level: Godot Movie Maker

- **Godot 4 Movie Maker mode** (built into the engine, not a third-party tool).
  CLI: `godot --write-movie out.avi`. Documentation:
  <https://docs.godotengine.org/en/stable/tutorials/animation/creating_movies.html>
  Renders frames at a fixed `Engine.MaxFps` regardless of wall-clock time,
  producing deterministic AVI or PNG-sequence output with audio. **This is the
  one piece of "is there already a built-in thing we can use?" that has a
  promising answer.** The 30-min spike: launch STS2 with the Movie Maker flag
  via a launch wrapper, see if Mega Crit's binary honours it. If yes, option
  C2 becomes the obvious answer to "give me a video".

### STS1 prior art (worth a look, but transferable shape only)

STS1 had a richer replay ecosystem because `CommunicationMod` standardised the
log format. Most STS1 viewers parse `CommunicationMod`'s output or the in-game
run history JSON. None of their code transfers — STS2's data model is too
different — but the *shape* of the projects is informative:

- **CommunicationMod log replays** — the spiritual ancestor of what we're
  doing. NDJSON-ish lines, action + state pairs. Worth recognising as the
  pattern we're descending from.
- **Various "Spire log" parsers** (community tools that parse STS1's
  `~/runs/<character>/*.run` JSON into stats / charts). These are *post-hoc
  analytics*, not visual replay, but a stretch goal for our project.
- **`xaved88/bottled_ai`** — STS1 bot built on CommunicationMod; in our
  context, more relevant as a reference for what *consumes* replays than what
  produces them.

No STS1 video-replay tool I'm aware of approached what we're considering for
option C; the closest is "stream the bot playing on Twitch".

---

## What probably maps to what

A non-committal sketch — the right cut depends on what the user actually wants
the watchable artefact to look like:

| If the user wants… | Then go with… | Prior art to lean on |
| --- | --- | --- |
| Test-suite-grade deterministic replay | Run-log + Layer-3 golden-replay tests | sts2-cli `logs/`, our AD-2 stream |
| "Show me what the bot did, step by step" | Option B (web timeline viewer) | spirescope (overlay UI), spire-codex (assets), sts2-modding-mcp (self-hosted assets) |
| "Render this run to a video I can share" | Option C2 (Godot Movie Maker via playback mod) | Godot docs, ModTemplate-StS2, longkerdandy split |
| Both of the last two | Build B now, spike C2 to confirm feasibility | as above |

---

## Tensions and open questions worth resolving before code

1. **Is option C in scope at all?** [01-initial-goals.md non-goals](../requirements/01-initial-goals.md)
   says "no rendering — if anyone wants to watch a run they can replay it in
   the real game". Extra-goal 3 says "this repo should allow to generate and
   view replays". The reconciling reading is: the *host* stays headless,
   the *playback* tool is a separate concern that lives in this repo but
   targets the real game. Worth pinning down explicitly with the user before
   investing — option C2 is the only path to a literal video file, but it
   doubles the surface area (real-Godot bootstrap, mod loader, asset
   resolution).

2. **What's the canonical replay file shape?** NDJSON of the wire protocol is
   the obvious choice, but it leaves out the header (game version, seed,
   reflection-manifest hash) and the snapshot index. Both can be appended as
   special envelope records (`method: "replay_header"`, `method: "state_snapshot"`)
   without changing AD-2. This is a small but real design step.

3. **Replays across game-version bumps.** [AD-3](../requirements/02-architecture-decisions.md)
   stores `snapshots/<game-version>/...` and refuses cross-version comparisons.
   Replays should follow the same rule: refuse to re-execute a v0.103.2 replay
   on a v0.105.x DLL. But we may still want to *view* an old replay (option B
   doesn't re-execute, it just renders state). So: viewing is version-tolerant,
   re-execution is version-locked.

4. **Does Godot's Movie Maker work on Mega Crit's shipped binary?** The 30-min
   spike. If yes, option C2 is much cheaper than expected. If no, we're back to
   OBS-style capture, which is fine for demos but not deterministic.

5. **Hosted vs. self-hosted asset pipeline for option B.** spire-codex has the
   data but is non-commercial-licensed and externally hosted. sts2-modding-mcp
   extracts assets but is a Python research tool, not a stable API. Likely
   answer: vendor the assets we need (subset) into our own viewer, sourced from
   sts2-modding-mcp's extraction pipeline at version-bump time, never checked
   in (same posture as `vendor/sts2.dll`). Asset licensing for shared / public
   hosted viewers is a separate question and may bound option B's deployment.

6. **Snapshot cadence.** Every decision is the obvious choice but inflates log
   size 10–100×. Every room transition is the cheapest choice but makes
   intra-combat seek impossible. A hybrid (snapshot at room transitions + an
   action delta stream between them) is the principled middle, and matches
   what the e2e-testing note already wants for Layer-3 tests.

7. **Live viewing vs. post-hoc viewing.** Option B is naturally post-hoc
   (point it at a file). Spirescope-style live viewing (subscribe to a running
   host's stdout, render on the fly) is a small additional step on top — the
   notification channel from AD-2 carries everything we'd need. Worth
   designing the viewer to accept either a file or a stream from day one,
   even if "stream" is only wired up later.

---

## Quick recommendation (one paragraph)

The cheapest valuable path is: **formalise the run log first** (header,
persistence, snapshot index), **build option B second** (a JSON-timeline web
viewer using sts2-modding-mcp's extracted assets, modelled on spirescope's UI
patterns), and **spike option C2 in parallel** (a 30-min check on whether
Godot's `--write-movie` works on the retail binary, which would unlock a true
video-output mode via a separate playback mod). Option D probably isn't worth
the trouble. Whatever we pick, **the run log is the canonical artefact** and
every viewer is just a renderer over it — keep that boundary clean.

## Suggested external clones for `external-tools/` (read-only)

If we go ahead, these are the repos worth cloning into `external-tools/`
alongside `sts2-cli/` for direct inspection:

- `thequantumfalcon/spirescope` — closest live-viewer prior art; UI patterns.
- `ptrlrd/spire-codex` — frontend + asset pipeline reference (non-commercial
  license, read-only).
- `elliotttate/sts2-modding-mcp` — Godot asset extraction pipeline.
- `Alchyr/ModTemplate-StS2` — required reading if option C is in scope.
- `longkerdandy/STS2-Cli-Mod` — process-split shape for the same option.

None of these get vendored — they sit in `external-tools/` (gitignored,
research clones), same posture as the existing `sts2-cli` clone.
