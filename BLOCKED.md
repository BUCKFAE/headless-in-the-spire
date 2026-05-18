# Blocked work

Engineering work that surfaced during the autonomous bug-hunting pass but
needs a human decision before it can land. Each entry names the surface,
the open question, and the cheapest unblocking step.

### Multi-character run support
- **Surface:** `src/Sts2Headless/HostMethods.cs:98-101` — `if (character != Character.Ironclad) throw new ArgumentException("...not yet supported (only Ironclad)")`. `Sts2Bindings.StartIroncladRun` is the only available entry point on the bindings layer.
- **Question:** Which characters to add and in what order (Silent / Defect / Watcher / Regent / Necrobinder), and whether `bindings.StartIroncladRun` should become `StartRun(character, …)` or a per-character family (`StartSilentRun`, …). Either shape needs character-specific starting decks, relics, and engine-side bootstrap differences.
- **Cheapest unblock:** Pick one new character (suggest Silent — closest to Ironclad in run shape) and confirm whether the bindings layer takes a character enum or stays per-character. Once decided, multi-character coverage in `CoverageSweepTests.s_runs` follows mechanically.
- **Discovered:** 2026-05-18 via fill-engine-gaps TODO scan (pass 1b) + coverage delta (pass 1c). The coverage report classifies ~80% of missing Cards/Relics/Powers as "off-class content the sweep cannot reach with single-character runs".

### Run modifiers on RunNewParams
- **Surface:** `src/Sts2Headless/HostMethods.cs:136` — `modifiers: Array.Empty<string>()` hardcoded in the replay header; `RunNewParams` has no `Modifiers` field.
- **Question:** Wire shape — `IReadOnlyList<string>` of modifier names (matches the replay header's current type) or `IReadOnlyList<ModifierId>` enum (matches CLAUDE.md's "Prefer enums over strings on the wire" rule and would use the existing `ModifierId.g.cs` manifest)? The enum form is the house style but freezes the surface to currently-known modifiers.
- **Cheapest unblock:** Add `IReadOnlyList<ModifierId>? Modifiers = null` (enum + nullable for back-compat); validate non-Unknown values; plumb through to the bindings layer.
- **Discovered:** 2026-05-18 via fill-engine-gaps TODO scan (pass 1b).

### Treasure room previewable pick/skip
- **Surface:** `src/Sts2Headless.Protocol/MethodCatalog.cs:69` — `run/leave_treasure_room` summary explicitly mentions a "future slice can split this into previewable pick/skip". Today the engine's `TreasureRoomRelicSynchronizer` auto-picks the single relic offering on entry; the wire method just exits to MapRoom.
- **Question:** Two-step shape — does it become `run/preview_treasure_room` (returns the relic offering) + `run/pick_treasure_room` (accept or skip), or a single `run/leave_treasure_room(skip: bool)` with the preview surfaced through the existing snapshot's `treasureRoom` block? The two-step preview is cleaner but adds a new method; the param-on-leave is non-breaking but less symmetric with merchant/event flows.
- **Cheapest unblock:** Confirm whether previewability is a CURRENT need (replay determinism? agent decision-making?) or speculative. If speculative, leave the catalog summary as-is and revisit when a caller surfaces.
- **Discovered:** 2026-05-18 via fill-engine-gaps catalog scan (pass 1a).

### Potion-drinking agent
- **Surface:** `src/Sts2Headless.Agents/GreedyAgent.cs` (no current `UsePotion` decision logic). Coverage report shows `potions: used: 0` across the entire sweep — the greedy agent never drinks. The wire surface (`potion/use`) is already wired.
- **Question:** Should the greedy agent learn to drink (when full? before tough combat? per character?), or should a separate `PotionDrinkingAgent` be added so the greedy stays minimal and a second sweep row exercises potion paths? The first changes existing baseline behaviour (one agent does more); the second multiplies coverage seeds.
- **Cheapest unblock:** Add a `PotionDrinkingAgent` that wraps `GreedyAgent` and additionally drinks any owned potion at the start of a non-trivial combat. Add one row to `CoverageSweepTests.s_runs` using it. ~40+ potions and their derived `*_POWER` ids become reachable.
- **Discovered:** 2026-05-18 via fill-engine-gaps coverage delta (pass 1c) — `used: 0`, biggest single coverage lever identified.

_(Coverage universe filter graduated 2026-05-18 — `IsEngineExcluded(string id)` in `CoverageAggregator` filters `DEPRECATED_*`, `FAKE_*`, `MOCK_*`, `*_DUMMY`, `*_ATTACK_MOVE_MONSTER`, plus `ONE_HP_MONSTER` / `TEN_HP_MONSTER` / `TEST_SUBJECT` / `ARCHITECT` literals out of the universe and the missing list. Markdown report now shows `universe (reachable): X (of Y manifest; Z engine-excluded)` so the trim stays visible.)_

_(Diagnostic-test trait case mismatch graduated 2026-05-18 — renamed `[Trait("category", "diagnostic")]` → `[Trait("Category", "Diagnostic")]` across 23 sites and updated `just test-integration` / `just test-end2end` to filter `Category!=Gap&Category!=Diagnostic`. CoverageSweep got the same casing treatment.)_

_(SMITH rest-site card-pick entry graduated 2026-05-18 — implemented in commit 83514c3; the SMITH summary was refreshed in commit efc86af.)_

_(Ascension wire param graduated 2026-05-18 — `int? Ascension` added to `RunNewParams`, plumbed through `Sts2Bindings.StartIroncladRun` and `ReplayHeaderFactory`. See `tests/Sts2Headless.IntegrationTests/AscensionWireParamTests.cs`.)_
