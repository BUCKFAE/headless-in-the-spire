# Blocked work

Engineering work that surfaced during the autonomous bug-hunting pass but
needs a human decision before it can land. Each entry names the surface,
the open question, and the cheapest unblocking step.

### Multi-character run support
- **Surface:** `src/Sts2Headless/HostMethods.cs:98-101` — `if (character != Character.Ironclad) throw new ArgumentException("...not yet supported (only Ironclad)")`. `Sts2Bindings.StartIroncladRun` is the only available entry point on the bindings layer.
- **Question:** Which characters to add and in what order (Silent / Defect / Watcher / Regent / Necrobinder), and whether `bindings.StartIroncladRun` should become `StartRun(character, …)` or a per-character family (`StartSilentRun`, …). Either shape needs character-specific starting decks, relics, and engine-side bootstrap differences.
- **Cheapest unblock:** Pick one new character (suggest Silent — closest to Ironclad in run shape) and confirm whether the bindings layer takes a character enum or stays per-character. Once decided, multi-character coverage in `CoverageSweepTests.s_runs` follows mechanically.
- **Discovered:** 2026-05-18 via fill-engine-gaps TODO scan (pass 1b) + coverage delta (pass 1c). The coverage report classifies ~80% of missing Cards/Relics/Powers as "off-class content the sweep cannot reach with single-character runs".

### Treasure room previewable pick/skip
- **Surface:** `src/Sts2Headless.Protocol/MethodCatalog.cs:69` — `run/leave_treasure_room` summary explicitly mentions a "future slice can split this into previewable pick/skip". Today the engine's `TreasureRoomRelicSynchronizer` auto-picks the single relic offering on entry; the wire method just exits to MapRoom.
- **Question:** Two-step shape — does it become `run/preview_treasure_room` (returns the relic offering) + `run/pick_treasure_room` (accept or skip), or a single `run/leave_treasure_room(skip: bool)` with the preview surfaced through the existing snapshot's `treasureRoom` block? The two-step preview is cleaner but adds a new method; the param-on-leave is non-breaking but less symmetric with merchant/event flows.
- **Cheapest unblock:** Confirm whether previewability is a CURRENT need (replay determinism? agent decision-making?) or speculative. If speculative, leave the catalog summary as-is and revisit when a caller surfaces.
- **Discovered:** 2026-05-18 via fill-engine-gaps catalog scan (pass 1a).

_(Potion-drinking agent graduated 2026-05-18 — `PotionDrinkingAgent` composes a `GreedyAgent` (sealed) and overrides `Decide` to drink the first usable potion at combat round 1 (IsPlayPhase + IsInProgress gated), targeting enemy 0 for `TargetType.AnyEnemy` potions. `CoverageSweepTests.s_runs` refactored to `(Character, ulong, AgentLabel, AgentFactory)` so multiple agent variants share the sweep; one row added running PotionDrinkingAgent on seed 42. Unit coverage in `PotionDrinkingAgentTests`.)_

_(Run modifiers wire param graduated 2026-05-18 — `IReadOnlyList<ModifierId>? Modifiers` added to `RunNewParams`, validated (Unknown rejected as InvalidParams) and echoed in `RunNewResult.Modifiers`. Wire strings flow to `ReplayHeaderFactory.Create` so the replay header is accurate. **Caveat: today modifiers are header-only metadata; engine plumb-through that would actually alter starting state (DRAFT decks, SEALED_DECK constraints) is still TODO.** Coverage in `tests/Sts2Headless.IntegrationTests/ModifiersWireParamTests.cs`.)_

_(Coverage universe filter graduated 2026-05-18 — `IsEngineExcluded(string id)` in `CoverageAggregator` filters `DEPRECATED_*`, `FAKE_*`, `MOCK_*`, `*_DUMMY`, `*_ATTACK_MOVE_MONSTER`, plus `ONE_HP_MONSTER` / `TEN_HP_MONSTER` / `TEST_SUBJECT` / `ARCHITECT` literals out of the universe and the missing list. Markdown report now shows `universe (reachable): X (of Y manifest; Z engine-excluded)` so the trim stays visible.)_

_(Diagnostic-test trait case mismatch graduated 2026-05-18 — renamed `[Trait("category", "diagnostic")]` → `[Trait("Category", "Diagnostic")]` across 23 sites and updated `just test-integration` / `just test-end2end` to filter `Category!=Gap&Category!=Diagnostic`. CoverageSweep got the same casing treatment.)_

_(SMITH rest-site card-pick entry graduated 2026-05-18 — implemented in commit 83514c3; the SMITH summary was refreshed in commit efc86af.)_

_(Ascension wire param graduated 2026-05-18 — `int? Ascension` added to `RunNewParams`, plumbed through `Sts2Bindings.StartIroncladRun` and `ReplayHeaderFactory`. See `tests/Sts2Headless.IntegrationTests/AscensionWireParamTests.cs`.)_

_(`run/history` schema-export per-type naming policy graduated 2026-05-19 — added `[SchemaSnakeCase]` marker attribute (`src/Sts2Headless.Protocol/SchemaSnakeCaseAttribute.cs`) and applied it to `RunHistoryDocument` + 13 nested records. `OpenRpcEmitter.cs` now picks `SnakeCaseLower` for marked types in both schema generation and nullable-required-list reconciliation, which automatically gives the regenerated `_models.py` correct snake_case aliases AND `default=None` on every nullable list/scalar (since they leave the schema's `required` block). Separately surfaced + fixed: `HistoryChoiceEntry.TextKey` is PascalCase in the on-disk file even though sibling fields are snake_case — explicit `[JsonPropertyName("TextKey")]` so neither the C# nor the Python parser silently zeroes it out. New Python parity test `test_run_history_parity.py` walks every `vendor/replays/**/run.json` and asserts it parses; the existing C# `RunHistoryDocumentTests` over `vendor/sample-saves/` stays untouched. Discovered via the 2026-05-19 MCP headless-claude trial; the trial's run.json is now part of the parity test corpus.)_
