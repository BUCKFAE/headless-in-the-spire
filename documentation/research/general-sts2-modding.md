# General Slay the Spire 2 Modding — Reference

Snapshot date: 2026-05-13. STS2 is in Steam Early Access; this document reflects the
state of the game and its modding ecosystem at that point in time. Expect details to
drift as patches land.

---

## 1. Engine and runtime

- **Engine: Godot 4.x (.NET / C# variant).** Mega Crit started building STS2 in Unity
  but ported ~2 years of work to Godot after Unity's runtime-fee debacle. Steam Early
  Access launched on **2026-03-05**.
- **Language: nearly all game logic lives in a single .NET 8 C# DLL, `sts2.dll`** —
  *not* in GDScript. Decompiling it with ILSpy yields ~3,300 C# source files.
- Mods target **Godot 4.5.1 (.NET) + .NET SDK 9**.

### Install layout (Steam)

```
.../steamapps/common/Slay the Spire 2/
├─ SlayTheSpire2.exe                  # plus Godot .pck for assets
├─ data_sts2_windows_x86_64/          # contains sts2.dll on Windows
├─ data_sts2_linuxbsd_x86_64/         # …on Linux
├─ data_sts2_macos_arm64/             # …on macOS
└─ mods/                              # drop user mods here (.dll + .pck + .json manifest)
```

Sources:
- <https://www.pcgamer.com/games/card-games/slay-the-spire-2-ditched-unity-for-open-source-engine-godot-after-2-years-of-development/>
- <https://godotengine.org/showcase/slay-the-spire-2/>
- <https://www.megacrit.com/news/2026-02-19-release-date-trailer/>
- <https://github.com/ptrlrd/spire-codex>
- <https://github.com/jiegec/STS2FirstMod>

## 2. Official modding support

- Mega Crit is publicly pro-modding (paraphrased: "nothing stands in the way of putting
  googly eyes on every square inch of the game" — pre-launch Reddit AMA).
- STS2 ships with a **built-in mod loader** that scans `mods/` for `.dll` + `.pck` +
  manifest. Mods that load appear under **Settings → Mod Settings**.
- A `ModInitializer` attribute and a `MegaCrit.Sts2.Core.Modding` namespace are part of
  the public modding surface.
- **Steam Workshop** is on the roadmap but **not yet shipped** as of v0.105.x. Nexus
  Mods is the de-facto distribution hub today.
- **Developer console** exists in the engine but is disabled in retail. The Nexus mod
  **DevConsole** by Tbonex28b re-enables it (`~` or `Shift+8`, `help` for commands —
  spawn cards, add relics, set energy, jump rooms, etc.). Mega Crit may neuter this in
  future patches.
- Godot's standard launch flags work via Steam → Properties → Launch Options
  (e.g. `--rendering-driver opengl3`). Bare `--headless` / `--disable-audio` are **not
  documented to work** on the shipped binary — Mega Crit doesn't expose Godot's
  main-loop binary directly.

Sources:
- <https://sts2wiki.com/news/mega-crit-ama-reddit-feb-2026>
- <https://www.nexusmods.com/games/slaythespire2>
- <https://www.pcgamesn.com/slay-the-spire-2/roadmap>
- <https://gamerant.com/slay-the-spire-2-sts2-how-use-console-commands-cheats/>
- <https://www.escapistmagazine.com/news-slay-the-spire-2-console-commands/>

## 3. Community modding tooling

The ecosystem is small but consolidating around a few projects.

| Project | Role | URL |
| --- | --- | --- |
| **BaseLib-StS2** (Alchyr) | The STS2 equivalent of BaseMod/StSLib. Standardises adding cards, relics, monsters, hooks, scenes. Almost every content mod depends on it. | <https://github.com/Alchyr/BaseLib-StS2> |
| **ModTemplate-StS2** (Alchyr) | Official-feeling starter template + wiki covering setup, manifests, Harmony, asset packing. | <https://github.com/Alchyr/ModTemplate-StS2> ([wiki](https://github.com/Alchyr/ModTemplate-StS2/wiki/Setup)) |
| **STS2FirstMod** (jiegec) | Minimal example showing the `ModInitializer` attribute. | <https://github.com/jiegec/STS2FirstMod> |
| **spire-codex** (ptrlrd) | Decompiles `sts2.dll` (ILSpy) + `.pck` (GDRE Tools) and exposes 25+ REST endpoints for cards/relics/monsters/events. Excellent reference for game data structures. | <https://github.com/ptrlrd/spire-codex> |
| **STS2 Mod Manager** (Nexus) | GUI installer / profile manager. | <https://www.nexusmods.com/slaythespire2/mods/461> |
| **ModConfig** (Nexus #27) | Zero-dependency config helper (mods call it via reflection so it stays optional). | <https://www.nexusmods.com/slaythespire2/mods/27> |

Differences vs. STS1:

- STS1: Java + LibGDX, Javassist patching via **ModTheSpire + BaseMod + StSLib**.
- STS2: C#/.NET on Godot, **Harmony** patching, Mega Crit's **built-in loader** +
  BaseLib. Conceptual layering (loader → base library → content mods) is preserved.

**There is no ModTheSpire / BepInEx / MelonLoader equivalent on STS2.** Some clickbait
guides claim BepInEx works — it does not, BepInEx targets Unity Mono/IL2CPP, not Godot.

## 4. Code injection and hooks

- **HarmonyX / `Lib.Harmony` is bundled with the game** and is the canonical patching
  tool — `[HarmonyPatch]`, `Prefix`/`Postfix`/`Transpiler` work as usual.
- **Entry point**: decorate a static class with `[ModInitializer("MethodName")]`; the
  loader calls it after `sts2.dll` is loaded:

  ```csharp
  using MegaCrit.Sts2.Core.Modding;
  using MegaCrit.Sts2.Core.Logging;

  [ModInitializer("ModLoaded")]
  public static class FirstMod
  {
      public static void ModLoaded() { Log.Warn("MOD FINISHED LOADING"); }
  }
  ```

- **Manifest JSON** alongside the DLL specifies `id`, `display name`, dependencies.
  The `id` controls the filenames the loader expects.
- **Typical patch surfaces** seen in mods:
  - `CombatState` (combat hooks / turn flow)
  - `RunController`
  - `PauseMenu*` (UI)
  - `LocManager.SetLanguage` (postfix to inject localisation)
  - Save / load functions

Several mods use reflection-only access to optional dependencies to stay loosely
coupled across game versions.

## 5. Save files and game state

- **Locations**:
  - Windows: `%APPDATA%\SlayTheSpire2\steam\<steam_id>\profile1\saves\`
  - Linux: `~/.local/share/SlayTheSpire2/...` (Godot default)
- **Format**: human-readable **JSON** (e.g. `STS2Player.json`, `current_run.save`).
  Sensitive to syntax errors; disable Steam Cloud before manual editing.
- **In-memory state** is fully observable from inside a mod. STS2MCP, sts2-cli, and
  others walk live structs (`GameState`, `CombatState`, `PlayerState`, deck/draw/
  discard/exhaust piles, map, shops, events, intents) via reflection to build
  snapshots.

## 6. Headless / automation hooks

This is the most relevant section for the runner we're building.

- **STS2MCP** (Gennadiyev): runs as an in-game mod, starts an HTTP server on
  `localhost:15526` at game launch. Exposes `GET /api/v1/game_state`, `/profile`,
  `/compendium`, `/profiles`, `/wiki?query=…` and action endpoints to choose cards,
  targets, map nodes, events, plus profile / menu navigation. Optional Python MCP
  wrapper. Pinned to STS2 **v0.103.2**. Cross-platform single DLL.
  <https://github.com/Gennadiyev/STS2MCP>
- **sts2-cli** (wuhao21): the closest thing to a *true* headless mode today —
  extracts `sts2.dll` from Steam, applies **Harmony IL patches**, and **swaps
  `GodotSharp.dll` for a `GodotStubs` shim** so the renderer never starts. A C#
  `Sts2Headless` host speaks JSON over stdin/stdout to a Python CLI. Supports seed
  control, character + ascension selection, batch JSON protocol, JSONL logs of states
  + actions. RNG and game logic are bit-identical to the real game.
  <https://github.com/wuhao21/sts2-cli>
- **Seed control without modding**: the in-game **Custom Mode** (unlocked after
  beating Act 3 / Glory three times) lets you input a custom seed and ascension.
- **Fast Mode**: built-in Gameplay setting that ~2× animations.
- **No official "skip menu" / "auto-start run" flag** is documented; sts2-cli and
  STS2MCP both implement that themselves by driving menu actions through reflection
  on the menu controllers.

For our own headless runner, the cleanest existing precedent is to follow
**sts2-cli's pattern** (stub `GodotSharp`, patch the main loop, drive logic via the
real `sts2.dll`) and overlay structured state/action APIs on top.

## 7. Useful repositories and links

- Steam page: <https://store.steampowered.com/app/2868840/Slay_the_Spire_2/>
- Mega Crit: <https://www.megacrit.com/>
- Nexus Mods hub: <https://www.nexusmods.com/games/slaythespire2>
- Patch notes: <https://slaythespire.wiki.gg/wiki/Slay_the_Spire_2:Patch_Notes>
- SteamDB patches: <https://steamdb.info/app/2868840/patchnotes/>
- GitHub topic: <https://github.com/topics/slay-the-spire-2>
- Alchyr/ModTemplate-StS2: <https://github.com/Alchyr/ModTemplate-StS2> ([wiki](https://github.com/Alchyr/ModTemplate-StS2/wiki/Setup))
- Alchyr/BaseLib-StS2: <https://github.com/Alchyr/BaseLib-StS2>
- Gennadiyev/STS2MCP: <https://github.com/Gennadiyev/STS2MCP>
- wuhao21/sts2-cli: <https://github.com/wuhao21/sts2-cli>
- ptrlrd/spire-codex: <https://github.com/ptrlrd/spire-codex>
- jiegec/STS2FirstMod: <https://github.com/jiegec/STS2FirstMod>
- lamali292/sts2_example_mod: <https://github.com/lamali292/sts2_example_mod>
- freude916/sts2-quickRestart: <https://github.com/freude916/sts2-quickRestart>
- Roadmap coverage: <https://www.pcgamesn.com/slay-the-spire-2/roadmap>, <https://www.stratgg.com/guides/early-access/>

## 8. Stability and API churn

- Game is **Early Access**; latest beta is **v0.105.1 (2026-05-08)**, main branch at
  **v0.103.2 (2026-04-16)** — labelled "Major Update #1". Patches are roughly weekly
  on the beta channel.
- **Mod API churn is real.** Community notes warn that beta patches frequently
  invalidate BaseLib and that mods should be updated in lockstep; users are advised
  to delete `config.json` of affected mods after a BaseLib bump.
- **Pinning**: STS2MCP pins to v0.103.2 (main-branch release) rather than chasing
  beta — sensible strategy for a research / automation runner. sts2-cli does likewise
  via extracted DLLs that you can version-lock.
- Steam Workshop is still pending; expect another wave of churn when it ships
  (manifest format may change).

## Practical takeaways for our runner

1. **Target the main branch** (currently 0.103.2), opt out of Steam beta, vendor a
   copy of `sts2.dll` per game-version in the repo, and pin BaseLib to a known-good
   version.
2. **Build against `MegaCrit.Sts2.Core.Modding`** and use Harmony for any patching.
3. For true headless operation, **replicate sts2-cli's `GodotStubs` approach** rather
   than fighting the Godot main loop. Stubbing the renderer is the cheapest way to
   get bit-identical game logic without a GPU.
4. **Pin a game version per branch / per replay file.** Replays recorded against
   v0.103.2 may not be reproducible against v0.106.x if RNG ordering changes.
5. **Use spire-codex** as the source of truth for typing card / relic / enemy / event
   IDs — it's the only artefact in the ecosystem that gives us structured definitions
   to back enums with.
