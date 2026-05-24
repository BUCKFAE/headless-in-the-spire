# Anatomy of `wuhao21/sts2-cli`

Snapshot date: 2026-05-13. Source pinned at `external-tools/sts2-cli/`
(latest `main` at clone time).

This document records *how* sts2-cli actually works under the hood, what is
load-bearing about its approach, and what lessons we should and should not
carry into our own implementation. It supplements
[existing-headless-libraries.md](./existing-headless-libraries.md), which
covers the comparative survey.

## Topology at a glance

```
caller (Python / shell / LLM)
    │  one JSON object per line, stdin / stdout
    ▼
Sts2Headless.exe  (one process per game)
    │  in-process method calls + reflection
    ▼
sts2.dll  (the real game logic — IL-patched at setup time)
GodotStubs.dll  (replaces GodotSharp.dll — no-op rendering surface)
0Harmony + MonoMod  (runtime patching, only used for localization here)
SmartFormat, Sentry, Steamworks.NET, System.IO.Hashing  (game runtime deps)
```

Size markers: `RunSimulator.cs` is **3,584 LOC** in one file. `play.py` is
**2,132 LOC** in one file. These are the "god files" we explicitly designed
against in [01-initial-goals.md](../requirements/01-initial-goals.md).

## How it pulls game logic out of Godot

This is the load-bearing trick. STS2 is a Godot game whose logic runs on
the Godot event loop (`async` methods that yield to frame ticks). To run
it headless you need to either embed the engine (expensive, defeats the
point) or convince the game's async code to run synchronously without a
real frame loop. sts2-cli does the latter with three independent
mechanisms layered together:

1. **GodotStubs.dll** (`src/GodotStubs/`). A 1.2k-LOC C# project that
   declares all the `Godot.*` types the game references — `GodotObject`,
   `Node`, `StringName`, `Variant`, `Vector2/3`, `Color`, the `Mathf`
   helpers, the `Godot.Collections` containers, `SceneTree`, etc — with
   no-op implementations. The build emits `GodotStubs.dll` which is
   loaded under the name **`GodotSharp`** so the game's bound symbols
   resolve here instead of the real Godot binding. No rendering occurs;
   `IsInstanceValid` returns "yes," child-of-tree lookups return empty
   collections, frame signals are stubs.

2. **Two IL patches to `sts2.dll`** (`setup.sh`, via Mono.Cecil at setup
   time, **mutating the file on disk**):
   - Every nested `YieldAwaitable.YieldAwaiter.get_IsCompleted` is
     rewritten to `return true;`. `await Task.Yield()` therefore never
     yields — the continuation runs synchronously.
   - `WaitUntilQueueIsEmptyOrWaitingOnNonPlayerDrivenAction` is rewritten
     to `return Task.CompletedTask;`. This is the game's "wait for the
     animation/effect queue to drain" hook; in headless mode there is
     nothing to wait for.

3. **`InlineSynchronizationContext`** (`RunSimulator.cs`). A custom
   `SynchronizationContext` whose `Post` executes the callback inline
   immediately, with a recursion-safe queue. Installed before any game
   code runs. This catches the async paths the IL patches don't.

The combined effect: a normally async, frame-paced engine becomes a
deterministic synchronous step function that can be driven one decision
at a time over stdin/stdout. This is **the** insight worth carrying
forward, no matter what else we change.

We will likely prefer Harmony prefix patches over Mono.Cecil byte
rewrites — it's runtime-only, leaves `sts2.dll` untouched on disk, and
fits the version-pin workflow in [AD-3](../requirements/02-architecture-decisions.md)
without an extra "patched / unpatched" state.

## DLL inventory (what setup.sh copies from Steam)

From `setup.sh`, nine files are pulled out of the Steam install:

| DLL | Role | Replaceable? |
| --- | --- | --- |
| `sts2.dll` | All game logic. Proprietary, never vendor. | No. |
| `0Harmony.dll` | Runtime IL patching library. | Yes (NuGet `Lib.Harmony`). |
| `MonoMod.Backports.dll`, `MonoMod.ILHelpers.dll` | Harmony deps. | Yes (NuGet `MonoMod.RuntimeDetour`). |
| `SmartFormat.dll`, `SmartFormat.ZString.dll` | Game's string templating. | Could vendor from NuGet, but the game references specific versions. |
| `Sentry.dll` | Error reporting. | Yes (NuGet, or stub). |
| `Steamworks.NET.dll` | Steam SDK bindings. | Yes (or stub — we don't need Steam). |
| `System.IO.Hashing.dll` | Utility (xxHash). | Yes (NuGet). |

`GodotSharp.dll` is deliberately **not** copied from Steam — the build
emits `GodotStubs.dll → GodotSharp` to replace it.

For our project, the bootstrap script needs `sts2.dll` from Steam at
minimum; everything else is replaceable with NuGet packages or stubs. We
should grab them from Steam too on first install to guarantee API match,
but they're not the licensed bytes — they're third-party libraries.

## Steam install layout (auto-detection)

| Platform | Game directory (DLLs live at this path) |
| --- | --- |
| Linux (default Steam) | `~/.steam/steam/steamapps/common/Slay the Spire 2/` |
| Linux (alt Steam) | `~/.local/share/Steam/steamapps/common/Slay the Spire 2/` |
| macOS (Apple Silicon) | `~/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/Resources/data_sts2_macos_arm64` |
| macOS (Intel) | …same parent…/`data_sts2_macos_x86_64` |
| Windows | `C:/Program Files (x86)/Steam/steamapps/common/Slay the Spire 2/` |

On Linux the DLLs sit directly under the game root. On macOS they live in
the platform-specific `data_sts2_*` subdir inside the `.app` bundle.
Steam libraries on secondary disks land under
`/run/media/<user>/<disk>/SteamLibrary/steamapps/common/Slay the Spire 2/`
or similar — auto-detection should consult `libraryfolders.vdf`
(`~/.steam/steam/steamapps/libraryfolders.vdf`) rather than hard-code
roots.

## Wire protocol shape (and how it differs from ours)

The actual on-the-wire messages from `Program.cs`:

```json
// Request  (caller → game)
{"cmd": "start_run", "character": "Ironclad", "seed": "test", "ascension": 0, "lang": "en"}
{"cmd": "action", "action": "play_card", "args": {"card_index": 0, "target_index": 0}}
{"cmd": "load_save", "path": "saves/foo.save", "lang": "en"}
{"cmd": "enter_room", "type": "combat", "encounter": "SHRINKER_BEETLE_WEAK"}
{"cmd": "set_draw_order", "cards": ["Strike", "Defend", ...]}
{"cmd": "set_player", "hp": 80, "max_hp": 80, "gold": 99, ...}
{"cmd": "quit", "path": "saves/checkpoint.save"}

// Response (game → caller). The "decision" field is the state-machine name.
{"type": "ready", "version": "0.2.0"}
{"decision": "combat_play", "round": 1, "energy": 3, "hand": [...], "enemies": [...]}
{"decision": "map_select", "map": {...}, "options": [...]}
{"decision": "card_reward", "cards": [...]}
{"decision": "rest_site", ...}
{"decision": "event_choice", "options": [...]}
{"decision": "shop", ...}
{"decision": "game_over", ...}
{"type": "error", "message": "..."}
{"type": "quit_result", "success": true, "save": {...}}
```

Snake-case via `JsonNamingPolicy.SnakeCaseLower`, `null`s omitted on
write. One JSON value per line — *de facto* NDJSON but never spec'd as
such. Snapshot of action vocabulary: `play_card`, `end_turn`,
`choose_option`, `skip_card_reward`, `select_bundle`, `select_cards`,
`skip_select`, `proceed`, `select_map_node`.

Differences from our design ([AD-2](../requirements/02-architecture-decisions.md)):

- **No request `id`.** Strictly synchronous — caller blocks on each
  response. Cannot interleave multiple requests, cannot correlate
  notifications. Fine for a player CLI; insufficient for our parallel
  RL / debug-while-running use cases.
- **No notification channel.** Every output line is a response to the
  most recent input. Async events (state changes, side effects, log
  lines) have to be inferred from response payloads.
- **`type` vs. `decision` are tangled.** `type` is used for protocol
  meta ("ready", "error", "quit_result"); `decision` is the game
  state-machine state. Sometimes both, sometimes neither, sometimes
  `type` is missing from a state response.
- **Args are dynamically typed strings/ints with no schema.** Mistyped
  args fail at C# parse time with a generic exception.

The shape is reasonable for a first cut and matches the spirit of
NDJSON+JSON-RPC closely enough that **our protocol is a refinement of
this one, not a departure**. The lessons are mostly about what to tighten.

## Reflection / type access strategy

`RunSimulator.cs` opens with a wall of `using` directives that pull in
the game's namespaces directly:

```csharp
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
// … 20 more …
```

Game types are referenced by direct symbol, so the build breaks at
compile time if anything is renamed or removed. This satisfies stage 2 of
our compat check (compile-time check from [AD-3](../requirements/02-architecture-decisions.md))
but skips stage 1 (reflection-manifest diff) — there's no central
registry of reflective access, so a field that's *present at compile
time but absent at runtime* (e.g. nulled out by a patch) fails late.

For our project this is what justifies the reflection manifest: catch
runtime-only divergence before tests run, not after.

## How tests work

`tests/conftest.py`:

- A `Game` Python class wraps the headless C# process. Each test gets
  its own fresh `Game` instance via the `@pytest.fixture` (no sharing,
  no parallel pool).
- The fixture spawns `dotnet run --no-build --project Sts2Headless` per
  test and tears it down at the end. Startup is dominated by the JIT
  warm-up of `sts2.dll`.
- Helpers: `start`, `act`, `enter_room`, `set_player`, `set_draw_order`,
  `auto_combat`, `skip_neow`.
- Assertions are **structural** (field present, type sensible) and
  sometimes mechanical (energy decremented after `play_card`, HP
  decreased after attack against a 0-block target). No snapshot diffs,
  no golden replays, no determinism canary.

The "regression test" (`CLAUDE.md`): `play_full_run.py 5` per character,
"all 5 must complete". This is a crash-only smoke test, not a
correctness test.

What we gain by going further:

- **Fixture injection** via the `set_player` / `set_draw_order` /
  `enter_room` hooks is already in place. We can reuse the pattern but
  formalise the fixture-builder as a typed API rather than a JSON
  command grab-bag.
- **A reusable process pool** (one warm `dotnet` process per worker) so
  test startup time isn't paid per-test. Shipped as
  `src/Sts2Headless.Agents/Hosting/HostPool.cs` + `HostProcess.cs`;
  covered by
  `tests/Sts2Headless.End2EndTests/ParallelHostPoolTests.cs`.
- **Determinism canaries**: rerun the same seed → byte-equal stream.
- **Snapshot tests**: persist the response stream from a known scenario,
  diff on rerun. This is missing entirely from sts2-cli.

## Things to take, things to leave

### Take

- The **GodotStubs swap** — it's the only viable headless strategy
  short of embedding Godot. We re-implement it, but in C#-with-tests
  rather than as a single dump.
- The **inline synchronization context + yield neutralisation**
  combination. We may prefer Harmony patches over Mono.Cecil rewrites
  but the *concept* (kill the async pumping in three independent ways)
  is right.
- **Save injection** as a fixture mechanism. Saves are JSON; rewriting
  one to construct a specific combat-encounter state is more reliable
  than trying to play forward from a seed.
- **NDJSON-shaped stdio**. Already validated as workable in practice.
- **The localization JSON files** — these are reusable game-data
  references (cards, relics, events, characters). 96 files total under
  `localization_eng/` and `localization_zhs/`. We can mine these for
  enum / type generation rather than hand-listing IDs.

### Leave behind

- **Mono.Cecil byte rewriting of `sts2.dll` on disk.** Prefer runtime
  Harmony patches — `sts2.dll` stays as a verified, hashable artefact;
  no "is this the patched copy?" state.
- **A 3,584-line god-file** for the run lifecycle. Split by
  responsibility: state-machine driver, JSON I/O, fixture injection,
  decision serialisation, the patch set itself.
- **Stringly-typed action payloads**. Generate typed DTOs from the C#
  schema and validate at the JSON boundary.
- **No request `id` / no notification channel**. We commit to JSON-RPC
  envelope from day one — it costs almost nothing and unblocks parallel,
  async events, and structured replays.
- **One `dotnet run` per test**. We invest in a process pool and a warm
  cache (shipped — see HostPool / ParallelHostPoolTests above).
- **Auto-deleted logs after 7 days.** Replays are first-class artefacts;
  they don't expire silently.

## Concrete pieces of code that earn their keep

If we ever need a reference for *how* a thing works, these are the
files worth re-reading inside `external-tools/sts2-cli/`:

- `src/GodotStubs/Core.cs` — `GodotObject`, `Node`, `Resource` stubs.
- `src/GodotStubs/Types.cs` — `StringName`, `NodePath`, `Variant`.
- `src/GodotStubs/Math.cs` — `Mathf`, `Vector2/3`, `Color`.
- `setup.sh` — the Mono.Cecil patcher source is inlined here (~80
  LOC). Useful as a quick "what would Harmony need to patch?" map.
- `src/Sts2Headless/Program.cs` — assembly-resolver hook (the
  `AssemblyLoadContext.Default.Resolving` callback) is the cleanest
  example of how to point .NET at a Steam directory without copying.
- `src/Sts2Headless/RunSimulator.cs` lines 1–90 — the
  `InlineSynchronizationContext` is exactly what we'll port.
- `tests/conftest.py` — the `Game` wrapper is a template for our
  Python client, minus the bits we're changing.
