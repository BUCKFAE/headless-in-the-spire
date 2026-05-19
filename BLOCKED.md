# Blocked work

Engineering work that surfaced during the autonomous bug-hunting pass but
needs a human decision before it can land. Each entry names the surface,
the open question, and the cheapest unblocking step.

### Multi-character run support
- **Surface:** `src/Sts2Headless/HostMethods.cs:98-101` — `if (character != Character.Ironclad) throw new ArgumentException("...not yet supported (only Ironclad)")`. `Sts2Bindings.StartIroncladRun` is the only available entry point on the bindings layer.
- **Question:** Which characters to add and in what order (Silent / Defect / Watcher / Regent / Necrobinder), and whether `bindings.StartIroncladRun` should become `StartRun(character, …)` or a per-character family (`StartSilentRun`, …). Either shape needs character-specific starting decks, relics, and engine-side bootstrap differences.
- **Cheapest unblock:** Pick one new character (suggest Silent — closest to Ironclad in run shape) and confirm whether the bindings layer takes a character enum or stays per-character. Once decided, multi-character coverage in `CoverageSweepTests.s_runs` follows mechanically.
- **Discovered:** 2026-05-18 via fill-engine-gaps TODO scan (pass 1b) + coverage delta (pass 1c). The coverage report classifies ~80% of missing Cards/Relics/Powers as "off-class content the sweep cannot reach with single-character runs".

### `run/history` schema-export skips the per-type snake_case naming policy
- **Surface:** `src/Sts2Headless.SchemaExport/OpenRpcEmitter.cs` — the emitter reads `EnvelopeIo.JsonOptions` (no `PropertyNamingPolicy`) and falls back to `JsonNamingPolicy.CamelCase` for nullability reconciliation (line 224). It never consults `RunHistoryDocument.JsonOptions`, which sets `SnakeCaseLower` for the on-disk + wire shape (`src/Sts2Headless.Protocol/RunHistory.cs:65-72`). Consequences:
  1. `protocol/openrpc.json` documents `RunHistoryDocument` properties in PascalCase (`SchemaVersion`, `MapPointHistory`, …) — but the wire-emitted payload from `Sts2Headless.Replay/ReplayQuery.cs:35` is snake_case (`schema_version`, `map_point_history`, …). Schema lies about the on-wire shape.
  2. `clients/python/headless-in-the-spire/src/headless_in_the_spire/_models.py` inherits the PascalCase aliases. Calling `client.run_history()` on a real completed run raises `ValidationError` (40+ field errors on the run.json from the 2026-05-19 MCP trial): some fields fail because the alias is PascalCase but the wire key is snake_case; others fail because `Annotated[X | None, Field(alias=...)]` lacks a default, so a nullable wire key the game omits entirely is treated as a missing required field. The `headless-in-the-spire-mcp` `run_history` tool therefore returns a 500-shaped error to any AI that calls it after a real run end.
- **Question:** Two coupled decisions, plus a possible test addition:
  - **Schema layer:** does the exporter learn to read a per-type naming policy (look for a `JsonOptions` static on the record, or annotate with a new `[SchemaNamingPolicy(...)]` attribute), or do we hand-author the `RunHistoryDocument` portion of `openrpc.json` and skip emission for it? Reading the per-type `JsonOptions` is cleaner but adds reflection-by-convention; a marker attribute is explicit but adds API surface.
  - **Codegen layer:** the `datamodel-code-generator` invocation in `scripts/generate_models.py` should produce `Annotated[X | None, Field(alias=..., default=None)]` for every nullable wire field, not just for fields whose schema declares an explicit default. Otherwise even fixing the casing leaves the missing-key path broken.
  - **Test layer:** add a Python parity test that round-trips a real `.run` file (e.g. `vendor/replays/<a recorded run>/run.json`) through `RunHistoryDocument.model_validate_json`. Today there's no Python-side regression net for the on-disk → wire → typed-Python path, only the C# `RunHistoryDocumentTests` over `vendor/sample-saves/`.
- **Cheapest unblock:** Decide schema-side approach (reflection-by-convention vs marker attribute) and codegen fix together; both are needed for `client.run_history()` to work, and shipping either alone is wasted travel.
- **Discovered:** 2026-05-19 by spawning a headless `claude -p` with the new `headless-in-the-spire-mcp` server attached. Sonnet played 7 floors of Ironclad seed=42, recording 5 `.mcr` files + a 12 KB `run.json`. Reading that `run.json` via the Python wire client's `RunHistoryDocument` model surfaced the casing+nullability mismatch. Replay artefacts at `vendor/replays/mcp-claude-trial-20260519-090653/`.

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
