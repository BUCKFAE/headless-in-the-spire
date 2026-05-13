# Existing Slay the Spire 2 Headless / Bot / Simulator Projects

Snapshot date: 2026-05-13. Sourced from a survey of GitHub, Nexus Mods, and Discord
discussion. The STS2 automation ecosystem is fundamentally different from STS1's —
no `sts_lightspeed` equivalent exists, and `CommunicationMod`'s niche has fragmented
across several uncoordinated projects. The dominant 2026 pattern is **MCP servers**
(LLM plays the game) rather than classic JSON-RPC stdio bots (programs / RL agents
play the game).

This document is a reference for our own implementation: what's out there, what to
borrow, and what to avoid.

---

## Closest reference projects

### 1. `wuhao21/sts2-cli` — the closest analogue to a CommunicationMod-style headless bot
- URL: <https://github.com/wuhao21/sts2-cli>
- Stack: C# 51% / Python 47% / Shell. .NET 9 SDK, Python 3.9+. Needs a Steam copy of STS2.
- Approach: **Engine reuse, no rendering.** IL-patches `sts2.dll`, replaces
  `GodotSharp.dll` with stubs in `src/GodotStubs`, Harmony for localisation. Runs
  real game logic without a renderer — the only project doing this today.
- IPC: **JSON over stdin/stdout** (the CommunicationMod pattern). Commands:
  `start_run(character, seed, ascension)`, `action(play_card, end_turn,
  select_map_node, …)`. Returns typed decision points: `map_select`, `combat_play`,
  `card_reward`, `rest_site`, `event_choice`, `shop`, `game_over`.
- State representation: structured JSON with English-named keys; logs are JSONL with
  timestamps.
- Determinism / seeding: **yes** — seed parameter on `start_run`.
- Replay: JSONL log per run in `logs/` (**auto-cleaned after 7 days** — hostile to
  long training runs).
- Parallel runs: **not addressed**; appears single-instance.
- Tests: a `/tests` directory exists. (The only project in this list with any.)
- Bindings: Python CLI in `python/play.py`; JSON protocol is language-agnostic.
- License: MIT. Stars: 188. Forks: 28. Open issues: 4. ~112 commits, active.
- Smells: stubbing `GodotSharp.dll` is fragile across patches; no parallel-run
  architecture; replays auto-deleted; protocol uses stringly-typed IDs.

**This is the single best reference for what we're building.**

### 2. `Gennadiyev/STS2MCP` — most popular, MCP-first
- URL: <https://github.com/Gennadiyev/STS2MCP>
- Stack: C# 90% / Python 9% / PowerShell. .NET 9, Python 3.11 + `uv`.
- Approach: **Mod injection** into the real game; does not reimplement logic, does
  not stub the renderer (the game still runs visually).
- IPC: HTTP/REST on `localhost:15526`, plus an optional Python MCP wrapper for
  Claude Desktop / Claude Code. Profile-level endpoints (`/api/v1/profile`) and
  live-run endpoints.
- State: structured JSON, but **narrative / token-bloated** — a single Claude
  Sonnet 4.6 run consumes 7–8M tokens.
- Determinism / seeding: optional seed at character select (SP and MP host/client).
- Multiplayer / parallel: MP co-op control supported (beta); **not** N independent
  headless instances. Single game process per box.
- Replay: not documented.
- Tests: none mentioned; tested manually against v0.103.2.
- License: MIT. Stars: 341. Forks: 57. Issues: 8. Last release v0.4.0, 2026-05-05.
- Smells: **"god partial-class" pattern** — implementation split across many
  `McpMod.*.cs` partial classes of one logical class. Heavy token cost reflects an
  unstructured, stringly-typed payload. `localhost` API is unauthenticated.

This is one of the two projects the user already reviewed.

### 3. `CharTyr/STS2-Agent` — second MCP server, similar pattern
- URL: <https://github.com/CharTyr/STS2-Agent>
- Stack: C# 48% / Python 25% / PowerShell 21% / Shell.
- Approach: mod + MCP server bundle (`STS2AIAgent/` mod, `mcp_server/` MCP wrapper).
- IPC: HTTP on `127.0.0.1:8080` (`/health` for liveness). MCP via stdio (default) or
  `http://127.0.0.1:8765/mcp`. **SSE** for state push (less polling).
- State: enriched payloads with Ascension, act/boss IDs, enemy IDs, move IDs; live
  metadata for cards / relics / monsters / potions / events. Atomic `resolve_rewards`
  action.
- Notable design: layered planner / combat handoff, tool profiles (guided / layered /
  full), debug actions disabled by default.
- Parallel runs / seeding / replay / tests: none documented. Single port → blocks
  multi-instance on one machine.
- License: **AGPL-3.0-only**. Stars: 235. Forks: 29. Issues: 1. v0.7.1, 2026-05-12.
- Smells: no tests, AGPL is restrictive for derivatives, port-binding makes
  parallelism a manual chore.

Probably the other project the user already reviewed.

---

## Other notable projects

### 4. `longkerdandy/STS2-Cli-Mod` — Named-Pipe CLI control
- URL: <https://github.com/longkerdandy/STS2-Cli-Mod>
- Stack: C# (.NET 9), `System.CommandLine`, Godot 4.5.1.
- Approach: **two-process design** — `STS2.Cli.Mod` (in-game Godot mod) and
  `STS2.Cli.Cmd` (external CLI). Explicitly marshals to the Godot main thread.
- IPC: **Named Pipe + JSON** (`snake_case`, `{"ok": true, "data": {…}}` envelopes).
  Refreshingly different from the HTTP herd; lower latency, local-only.
- State: hand, enemies, intents, powers, deck piles, 16+ "screen types" detected.
  40+ commands.
- Determinism / seeding / replay / tests / parallel: none visible; single-pipe
  design blocks parallelism.
- License: **none explicit** (only "not affiliated with Mega Crit Games") — treat as
  unlicensed / all rights reserved.
- Stars: 1. Forks: 1. v0.102.1 (2026-04-07), 219 commits — surprisingly mature for
  the star count.
- Smells: no license, no tests, single-pipe; but **architecturally the cleanest
  mod/CLI process split of the bunch.**

### 5. `ForeverVirus/Slay_The_Spire_2_AIBot` — in-process LLM agent
- URL: <https://github.com/ForeverVirus/Slay_The_Spire_2_AIBot>
- Stack: C# 100%, Godot .NET SDK 4.5.1, .NET 9, Windows only.
- Approach: pure in-process mod; **no external IPC** — LLM client and decision
  engine live inside the game DLL. `GuideHeuristicDecisionEngine`,
  `DeepSeekDecisionEngine`, `HybridDecisionEngine`. Four modes (full auto,
  semi-auto NL commands, assist overlay, Q&A). Curated `sts2_guides/` knowledge
  base.
- License: **none stated.** Stars: 16. Single contributor.
- Smells: no license, no IPC, no tests, no seeding, no replays. Useful only as a
  reference for what to embed vs. expose.

### 6. `ptrlrd/spire-codex` — static data API, not a simulator
- URL: <https://github.com/ptrlrd/spire-codex>
- Stack: TypeScript 66% / Python 28%, FastAPI + Pydantic backend, Next.js 16
  frontend, Playwright + spine-webgl for skeletal animation rendering.
- Approach: **decompile-and-parse pipeline.** GDRE Tools → ILSpy → 22 regex parsers
  over decompiled C# → SmartFormat resolution for templates like
  `{Damage:diff()}` → 25+ REST endpoints. 403 cards, 111 monsters with intents and
  AI patterns, 56/66 events with decision trees, 14 localisation languages.
- Not a runtime simulator — but **invaluable for typing card / relic / enemy / event
  IDs as enums** in our project.
- Notable: per-entity history, beta-version dropdown, changelog diffs between
  patches.
- License: **PolyForm Noncommercial 1.0.0** — careful, non-commercial only.
- Stars: 176. Forks: 37. Issues: 14. 796 commits, very active.

### 7. `ebadon16/sts2-advisor` — overlay + community win-rate data
- URL: <https://github.com/ebadon16/sts2-advisor>
- C# / .NET 9 mod (Harmony, Godot `CanvasLayer` overlay, SQLite local).
  Cloudflare Worker (TypeScript) + D1 backend.
- *Consumer* of game state, not an exposer. Archetype-detection code is worth a
  glance.

### 8. `Shawnrai/Shunrai-s-STS2-Advisor` — browser-based, manual input
- URL: <https://github.com/Shawnrai/Shunrai-s-STS2-Advisor>
- Static site on GitHub Pages. User enters deck and rewards manually — no game
  integration. MIT, 2 stars.

### 9. `thequantumfalcon/spirescope` — local companion / OBS overlay
- URL: <https://github.com/thequantumfalcon/spirescope>
- Python, local-first, no accounts. Live run tracker on `http://127.0.0.1:8000/live`
  and `/overlay` (OBS browser source). 16 stars. Reads state; doesn't drive.

### 10. `elliotttate/sts2-modding-mcp` — MCP for mod *development*, not gameplay
- URL: <https://github.com/elliotttate/sts2-modding-mcp>
- Python 55% / C# 41%. 151 MCP tools. Roslyn-based decompilation, indexes 3,048+
  entities and 144 hooks, extracts 15,000+ Godot assets, automated playtesting +
  live scene inspection. MIT, 13 stars. v3.7.0 (2026-03-25).
- Useful for programmatically introspecting hooks/entities; **not** runtime control.

### 11. `Alchyr/BaseLib-StS2`
- URL: <https://github.com/Alchyr/BaseLib-StS2>
- C# 100%, MIT, 325 stars, v3.1.3 (2026-05-13). Foundational dependency for content
  mods. No direct relevance for our bot/headless work beyond "know what most mods
  build on."

### 12. Other minor mentions
- **`ttxttx1111/sts2-llm`** — Copilot prompt/skill pack driving runs *via*
  `Gennadiyev/STS2MCP`. Demonstrates how thin a client becomes once a good MCP
  exists. <https://github.com/ttxttx1111/sts2-llm>
- **`Slay-the-Spire-2-Drawing`** — Python, 141 stars. **Screen-scraping** bot for the
  "digital amber" drawing minigame. Reference for what people resort to when no API
  exists.
- **`MqttTheSpire`** — C# mod publishing run events to MQTT for home automation.
  Illustrates the eventing pattern.
- **`STS2-DevMode`** — C# mod adding dev-mode toggles. Forking could speed
  iteration.
- **Nexus `sts2AITeammate` (mod #366)** — fake-multiplayer AI teammate. No public
  GitHub repo found; reportedly "functional but strategically weak."
- **iambb5445/MiniSTS** — **STS1**, not STS2. Python combat reimplementation,
  GPL-3.0, 24 stars. Published with AIIDE 2024 / FDG 2024 papers. No STS2 sibling
  yet.
- **xaved88/bottled_ai** — **STS1**, Python, uses CommunicationMod. 71 stars.
  Reference for a mature non-ML STS bot.

---

## Cross-cutting observations

1. **No `sts_lightspeed` equivalent exists for STS2.** Nobody has reimplemented
   combat in a fast, non-Godot language. This is the largest gap in the ecosystem.
2. **No CommunicationMod heir.** sts2-cli and STS2-Cli-Mod are the closest spiritual
   successors; neither has critical-mass community gravity. There is no de-facto wire
   protocol.
3. **MCP has eaten the niche.** STS2MCP and STS2-Agent are the popular projects,
   both optimised for "LLM plays the game," not "RL trains against millions of runs"
   or "we run a property-based test suite." None are designed for parallel headless
   throughput.
4. **Parallel execution is universally absent.** Every project assumes one game
   instance per machine. Ports are hardcoded (15526, 8080, 8765, 8000); named pipes
   are global.
5. **Seeding is rare.** Only sts2-cli and STS2MCP clearly expose it. Determinism
   for testing / replays is mostly unsolved.
6. **Replay recording is essentially absent.** Only sts2-cli writes JSONL logs, and
   it auto-deletes after 7 days.
7. **Tests are essentially absent everywhere.** Only sts2-cli has a tests directory;
   nobody else even pretends.
8. **Stringly-typed APIs dominate.** State payloads describe cards / relics /
   enemies by string ID or human-readable name, often locale-dependent.
9. **God files / partial-class sprawl.** STS2MCP's `McpMod.*.cs` is a warning sign —
   when a single logical class spans 10+ files, it's hard to reason about and fork.
10. **License hygiene is patchy.** STS2-Agent is AGPL, spire-codex is non-commercial,
    several projects (STS2-Cli-Mod, ForeverVirus AIBot) have **no license**. Vet
    before vendoring.
11. **Engine fragility.** Every mod-injection project depends on stable C# symbol
    names in `sts2.dll`; STS2 is in early access and patching weekly. Pin to a known
    game version (v0.103.2 is the current "main branch" pin) and budget for
    breakage on each game update.
12. **Godot main-thread marshalling** is a recurring concern; STS2-Cli-Mod calls
    this out explicitly. Any direct game-state read needs to hop to the main thread.
    Design the IPC layer around that constraint up front rather than discovering it
    later.

## Reading priority before we start writing code

1. **`wuhao21/sts2-cli`** — for the headless-engine approach (`GodotStubs`) and the
   stdio JSON protocol shape.
2. **`longkerdandy/STS2-Cli-Mod`** — for the cleanest mod/CLI process split and the
   Named Pipe transport.
3. **`ptrlrd/spire-codex`** — as a static-data oracle to type IDs as proper enums
   in our project.
4. **`Gennadiyev/STS2MCP`** — only to see what *not* to do regarding god files and
   token-bloated state representations.
