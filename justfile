set dotenv-load

BUILD_CORES := "auto"
MSBUILD_MAX_CPU := if BUILD_CORES == "auto" { "-maxcpucount" } else { "-maxcpucount:" + BUILD_CORES }

# xUnit max parallel test threads for the per-suite recipes (test-unit /
# test-integration / test-end2end). Local default is 0 ("# of CPU cores");
# CI should export XUNIT_THREADS=2 (or similar) to cap host-subprocess
# fan-out. Passed through to xUnit via the RunSettings CLI override.
#
# `just test` fans out all suites concurrently and uses its own per-suite
# caps (XUNIT_THREADS_INTEGRATION / _END2END / _UNIT) — see
# scripts/test-parallel.sh.
XUNIT_THREADS := env_var_or_default("XUNIT_THREADS", "0")

default:
    @just --list

# ── Local setup ───────────────────────────────────────────────────────────

# First-run setup: validate STS2 install, copy game DLLs, create uv workspace .venv, install replay-viewer pnpm deps, install git hooks, generate every *Id.g.cs content manifest from the local DLL.
setup:
    just validate-sts2-installation
    just pull-game-libs
    just sync-python
    just sync-viewer
    just install-hooks
    just generate-content-ids
    just build

# Install repo git hooks (pre-commit drift guard for openrpc.json + _models.py).
install-hooks:
    @bash scripts/install-hooks.sh

# Verify STS2_GAME_DIR points at a real STS2 install with the required DLLs.
validate-sts2-installation:
    @bash scripts/validate-sts2-installation.sh

# Copy game DLLs from STS2_GAME_DIR into ./vendor (first-run bootstrap; see AD-3).
pull-game-libs:
    @bash scripts/pull-game-libs.sh

# Create / refresh the uv workspace .venv at the repo root (Python clients + dev tooling).
sync-python:
    @bash scripts/check-uv.sh
    @uv sync --all-packages

# Install the replay-viewer (tools/replay-viewer) pnpm dependencies from the checked-in lockfile.
sync-viewer:
    @bash scripts/check-pnpm.sh
    @cd tools/replay-viewer && pnpm install --frozen-lockfile

# Run the replay viewer's Vite dev server (tools/replay-viewer). Opens an HMR-enabled frontend that loads replays from vendor/replays/.
dev-viewer:
    @bash scripts/check-pnpm.sh
    @cd tools/replay-viewer && pnpm run dev

# Clone reference projects (currently sts2-cli) into external-tools/.
clone-external-tools:
    @mkdir -p external-tools
    @test -d external-tools/sts2-cli || git clone --depth 1 --single-branch --no-tags https://github.com/wuhao21/sts2-cli.git external-tools/sts2-cli

# ── Build ─────────────────────────────────────────────────────────────────

# Build the whole solution with MSBuild parallelism; override with `just BUILD_CORES=4 build`.
build:
    @dotnet build Sts2Headless.slnx {{MSBUILD_MAX_CPU}}

# Build just the host exe (and its transitive deps). Used by generate-content-ids
# so the bootstrap can produce *Id.g.cs *before* the consumer projects
# (Agents, tests) try to reference values that only exist post-generation.
build-generator:
    @dotnet build src/Sts2Headless/Sts2Headless.csproj {{MSBUILD_MAX_CPU}}

# Run the headless host (prints the banner and vendor inventory for now).
run: build
    @dotnet run --project src/Sts2Headless/Sts2Headless.csproj --no-build

# Run the headless-in-the-spire MCP server over stdio (clients/python/headless-in-the-spire-mcp). Adds the server to any MCP-aware AI (Claude Desktop / Claude Code / etc.). Pass `--enable-debug` to expose AD-7 debug tools — never use in production.
run-mcp *args:
    @bash scripts/check-uv.sh
    @uv run --frozen headless-in-the-spire-mcp {{args}}

# Load vendor/sts2.dll and report missing GodotStubs surface (diagnostic).
inspect-sts2: build
    @dotnet run --project src/Sts2Headless/Sts2Headless.csproj --no-build -- --inspect-sts2

# Install sync context + Harmony hang-patches against vendor/sts2.dll (diagnostic).
probe-init: build
    @dotnet run --project src/Sts2Headless/Sts2Headless.csproj --no-build -- --probe-init

# probe-init + walk sts2's bootstrap chain (TestMode→ModelDb→Player smoke).
probe-bootstrap: build
    @dotnet run --project src/Sts2Headless/Sts2Headless.csproj --no-build -- --probe-bootstrap

# probe-bootstrap + walk RunState→RunManager→EnterAct chain; dumps post-boot state.
probe-run-state: build
    @dotnet run --project src/Sts2Headless/Sts2Headless.csproj --no-build -- --probe-run-state

# Drive the natural enemy-turn chain (NetIds = 1uL, no fallback) and write the gap catalog to documentation/research/.
probe-natural-chain: build
    @dotnet run --project src/Sts2Headless/Sts2Headless.csproj --no-build -- --probe-natural-chain

# Drive the natural reward-claim chain (no try/catch around CardPileCmd.Add / OnSelectWrapper / OnSkipped) and dump gaps.
probe-rewards-natural-chain: build
    @dotnet run --project src/Sts2Headless/Sts2Headless.csproj --no-build -- --probe-rewards-natural-chain

# Walk a seed until the first stalled combat, then dump engine state (diagnostic).
probe-combat-stall seed="1" floor="15": build
    @dotnet run --project src/Sts2Headless/Sts2Headless.csproj --no-build -- --probe-combat-stall --seed {{seed}} --floor {{floor}}

# List every type whose name matches one of the substrings, with its declared methods (e.g. `just probe-types Doormaker,SwapPhase`). Shows generic-method args and async state-machine targets when present — useful for picking a Harmony-patchable surface.
probe-types patterns: build
    @dotnet run --project src/Sts2Headless/Sts2Headless.csproj --no-build -- --probe-types {{patterns}}

# Scan every method in sts2.dll for call/callvirt sites that match a method name (e.g. `just probe-callers SwapPhasePower`). Prints caller → closed-instantiation pairs — the way to find Harmony-patchable closed forms of an open-generic target.
probe-callers patterns: build
    @dotnet run --project src/Sts2Headless/Sts2Headless.csproj --no-build -- --probe-callers {{patterns}}

# List every member of <fqn> that sts2.dll references (e.g. `just list-members Godot.OS`).
list-members fqn: build
    @dotnet run --project src/Sts2Headless/Sts2Headless.csproj --no-build -- --list-members {{fqn}}

# Run the host in NDJSON stdio mode (AD-2). One JSON request per line on stdin, one response per line on stdout.
stdio: build
    @dotnet run --project src/Sts2Headless/Sts2Headless.csproj --no-build -- --stdio

# Drive every Python agent (greedy, random, block, attack) in parallel and record into vendor/replays/ (one shared root; the manifest stamps the agent name). Each worker spawns its own host subprocess (and its own sts2.dll), so default --workers is conservative. Forward extra flags after `--`, e.g. `just record-all -- --seeds 1 2 3 --workers 8`.
record-all *args: build
    @uv run --frozen python -m headless_in_the_spire_agents.examples.run_all_agents {{args}}

# Drive a short RandomAgent run on seed=42 with recording enabled, then dump the recorded directory tree (AD-8). Produces vendor/replays/sample/<game-version>/<run-id>/{manifest.json, combats/*.mcr, run.json on engine-side death/victory} plus a top-level runs.json the viewer reads. Useful as a smoke-test for the recording substrate end-to-end.
record-sample-replay: build
    @rm -rf vendor/replays/sample
    @uv run --frozen python -m headless_in_the_spire_agents.examples.run_all_agents --agents random --seeds 42 --out vendor/replays/sample --max-steps 25
    @echo
    @echo "=== recorded replay ==="
    @find vendor/replays/sample -type f | sort
    @echo
    @echo "=== runs.json ==="
    @cat vendor/replays/sample/runs.json 2>/dev/null || echo "(no runs.json — recording produced nothing)"

# Hand a fresh game to Claude Code over the project's .mcp.json MCP server and let it drive one full STS2 run end-to-end. Streams Claude's reasoning + tool calls live (one line per SDK stream-json event, formatted via scripts/format-claude-stream.jq). Replays land in vendor/replays/<game-version>/<run-id>/ (default root) with STS2_REPLAY_AGENT=claude-code stamped into the manifest. Only the mcp__headless-in-the-spire__* tools are allowed, so Claude cannot touch the repo. Burns Claude API tokens; Ctrl-C to stop early.
play-claude: build
    @bash -c "set -o pipefail; STS2_REPLAY_AGENT=claude-code claude -p 'You are playing one full run of Slay the Spire 2 through the headless-in-the-spire MCP server. Start with run_new (pick a character and seed). Use summarize_state for cheap polls between actions; reach for run_state only when you need the full structural payload. Drive the run end-to-end: traverse the map, play cards and end turns in combat, claim or skip rewards, resolve events, rest at fires, shop at merchants. Continue until the run ends (death or victory) or you are demonstrably stuck. Do not ask for confirmation — just play.' --model sonnet --allowedTools 'mcp__headless-in-the-spire__*' --output-format stream-json --verbose | jq -r --unbuffered -f scripts/format-claude-stream.jq"

# Walk vendor/replays/ (or the given root) and rewrite runs.json. The host rebuilds it on every recorder finalize, so you only need this after manually copying recordings in/out of the tree.
rebuild-replay-index *root: build
    @dotnet run --project src/Sts2Headless/Sts2Headless.csproj --no-build -- --rebuild-replay-index {{root}}

# Regenerate protocol/openrpc.json from Sts2Headless.Protocol records (AD-5).
export-schema: build
    @dotnet run --project src/Sts2Headless.SchemaExport/Sts2Headless.SchemaExport.csproj --no-build

# Regenerate every *Id.g.cs manifest under src/Sts2Headless.Protocol/ (cards, relics, potions, monsters, encounters, events, powers, afflictions, modifiers, enchantments, orbs). All gitignored — proprietary content sourced from vendor/sts2.dll. Run after bumping the game pin.
generate-content-ids: build-generator
    @dotnet run --project src/Sts2Headless/Sts2Headless.csproj --no-build -- --generate-content-ids

# Dump ModelDb's content inventory (one txt per AllX property + a summary) into documentation/research/modeldb/ — gitignored, proprietary content. Diagnostic; not required for builds.
probe-modeldb: build-generator
    @dotnet run --project src/Sts2Headless/Sts2Headless.csproj --no-build -- --probe-modeldb

# Regenerate the Python client's pydantic DTOs from protocol/openrpc.json (AD-5).
generate-python:
    @bash scripts/check-uv.sh
    @uv run python clients/python/headless-in-the-spire/scripts/generate_models.py

# Regenerate every wire-protocol artefact (per-kind content manifests + openrpc.json + Python DTOs). Run after touching Methods.cs or bumping the game pin.
regen: generate-content-ids export-schema generate-python

# Regenerate the AbstractModel hook-surface snapshot (tests/Sts2Headless.IntegrationTests/Coverage/known-abstract-model-hooks.txt). Run after a GAME_VERSION bump or when an intentional sts2 change adds/removes a listener method — review the diff before committing.
regen-hook-snapshot:
    @UPDATE_HOOK_SNAPSHOT=1 dotnet test tests/Sts2Headless.IntegrationTests/Sts2Headless.IntegrationTests.csproj {{MSBUILD_MAX_CPU}} --nologo --filter "FullyQualifiedName~HookSurfaceSnapshotTest"
    @echo ""
    @echo "snapshot: tests/Sts2Headless.IntegrationTests/Coverage/known-abstract-model-hooks.txt"

# Remove all bin/ and obj/ build artifacts.
clean:
    @dotnet clean Sts2Headless.slnx
    @find src -type d \( -name bin -o -name obj \) -exec rm -rf {} +

# ── Tests ─────────────────────────────────────────────────────────────────

# Run only the unit suite — no vendor/sts2.dll required. Mirrors CI.
test-unit:
    @dotnet test tests/Sts2Headless.UnitTests/Sts2Headless.UnitTests.csproj {{MSBUILD_MAX_CPU}} --nologo

# Run the integration suite (loads vendor/sts2.dll; run `just setup` first). Excludes Category=Gap (red-on-purpose tests under HarnessGaps/ — run those via `just test-gaps`), Category=Diagnostic (flaky-by-design probes — invoke individually via `--filter "Category=Diagnostic"`), and Category=Benchmark (slow throughput probes — invoke via `just bench-parallel`).
test-integration:
    @dotnet test tests/Sts2Headless.IntegrationTests/Sts2Headless.IntegrationTests.csproj {{MSBUILD_MAX_CPU}} --nologo --filter "Category!=Gap&Category!=Diagnostic&Category!=Benchmark" -- xUnit.MaxParallelThreads={{XUNIT_THREADS}}

# Run the end-to-end suite (multi-room arcs; same vendor/sts2.dll requirement). Same Gap + Diagnostic + Benchmark exclusion as `test-integration`.
test-end2end:
    @dotnet test tests/Sts2Headless.End2EndTests/Sts2Headless.End2EndTests.csproj {{MSBUILD_MAX_CPU}} --nologo --filter "Category!=Gap&Category!=Diagnostic&Category!=Benchmark" -- xUnit.MaxParallelThreads={{XUNIT_THREADS}}

# Run ONLY the HarnessGaps tests (Category=Gap, born red on purpose — they document harness limitations with a planned fix). Green means a gap closed and the test should graduate out of HarnessGaps/. See tests/Sts2Headless.IntegrationTests/HarnessGaps/README.md.
test-gaps:
    @dotnet test tests/Sts2Headless.IntegrationTests/Sts2Headless.IntegrationTests.csproj {{MSBUILD_MAX_CPU}} --nologo --filter "Category=Gap" -- xUnit.MaxParallelThreads={{XUNIT_THREADS}}
    @dotnet test tests/Sts2Headless.End2EndTests/Sts2Headless.End2EndTests.csproj {{MSBUILD_MAX_CPU}} --nologo --filter "Category=Gap" -- xUnit.MaxParallelThreads={{XUNIT_THREADS}}

# Run the HostPool parallel-runs throughput benchmark (Category=Benchmark, off by default). Tune workers/runs via STS2_BENCH_WORKERS / STS2_BENCH_RUNS / STS2_BENCH_MAX_STEPS env vars. Goal #3 measurement: how many concurrent headless runs/day a workstation can drive.
bench-parallel:
    @dotnet test tests/Sts2Headless.End2EndTests/Sts2Headless.End2EndTests.csproj {{MSBUILD_MAX_CPU}} --nologo --filter "FullyQualifiedName~ParallelHostThroughputBenchmark" --logger "console;verbosity=detailed" -- xUnit.MaxParallelThreads=1

# ── Mechanic sweeps (per-id smoke matrix) ────────────────────────────────
# Each sweep drives every id in a kind's manifest through a minimal
# "exercise this one thing" fixture and classifies the outcome.
# Crashed / Timeout are failure signals; Played / Unreachable / Unplayable
# are informational. Reports land in documentation/coverage/sweep-<kind>.{md,json}
# (gitignored — proprietary content from vendor/sts2.dll).
#
# Slow by design — full passes can take hours. Use `sweep-sample <N>` for
# a fast subset that's comparable across runs (deterministic seed=42).

# Run the full per-CardId smoke sweep (~577 ids, single-card deck +
# SLIMES_NORMAL fixture, target=0). Crashed → test fails.
sweep-cards:
    @RUN_CARD_SWEEP=1 dotnet test tests/Sts2Headless.MechanicSweepTests/Sts2Headless.MechanicSweepTests.csproj {{MSBUILD_MAX_CPU}} --nologo --filter "FullyQualifiedName~CardSweepTests" --logger "console;verbosity=detailed" -- xUnit.MaxParallelThreads=1
    @echo ""
    @echo "report: documentation/coverage/sweep-cards.md"

# Run the full per-RelicId smoke sweep (~294 ids, give_relic + fixed
# 4-card deck + 2 turns + kill_all_enemies, draining TriggeredSincePrev
# to distinguish Triggered vs Played). Crashed → test fails.
sweep-relics:
    @RUN_RELIC_SWEEP=1 dotnet test tests/Sts2Headless.MechanicSweepTests/Sts2Headless.MechanicSweepTests.csproj {{MSBUILD_MAX_CPU}} --nologo --filter "FullyQualifiedName~RelicSweepTests" --logger "console;verbosity=detailed" -- xUnit.MaxParallelThreads=1
    @echo ""
    @echo "report: documentation/coverage/sweep-relics.md"

# Run the full per-PotionId smoke sweep (~64 ids, give_potion +
# start_combat(SLIMES_NORMAL) + use_potion + kill_all_enemies, draining
# TriggeredSincePrev to distinguish Triggered vs Played). Crashed → fail.
sweep-potions:
    @RUN_POTION_SWEEP=1 dotnet test tests/Sts2Headless.MechanicSweepTests/Sts2Headless.MechanicSweepTests.csproj {{MSBUILD_MAX_CPU}} --nologo --filter "FullyQualifiedName~PotionSweepTests" --logger "console;verbosity=detailed" -- xUnit.MaxParallelThreads=1
    @echo ""
    @echo "report: documentation/coverage/sweep-potions.md"

# Run the full per-EventId smoke sweep (~66 ids, start_event + iterate
# options (pick 0 each page) until the event resolves). Catches event-
# option crashes (historical card-select-screen NRE family). Crashed → fail.
sweep-events:
    @RUN_EVENT_SWEEP=1 dotnet test tests/Sts2Headless.MechanicSweepTests/Sts2Headless.MechanicSweepTests.csproj {{MSBUILD_MAX_CPU}} --nologo --filter "FullyQualifiedName~EventSweepTests" --logger "console;verbosity=detailed" -- xUnit.MaxParallelThreads=1
    @echo ""
    @echo "report: documentation/coverage/sweep-events.md"

# Run the full per-EncounterId smoke sweep (~80 ids, start_combat +
# fixed Strike/Defend deck + 2 turns + kill_all_enemies). Implicitly
# exercises every monster's intent path. Crashed → test fails.
sweep-encounters:
    @RUN_ENCOUNTER_SWEEP=1 dotnet test tests/Sts2Headless.MechanicSweepTests/Sts2Headless.MechanicSweepTests.csproj {{MSBUILD_MAX_CPU}} --nologo --filter "FullyQualifiedName~EncounterSweepTests" --logger "console;verbosity=detailed" -- xUnit.MaxParallelThreads=1
    @echo ""
    @echo "report: documentation/coverage/sweep-encounters.md"

# Run the full per-PowerId smoke sweep (~270 ids, apply_power → 2
# end_turns → kill_all_enemies). Player-target first, falls back to
# first enemy on wire-level refusal. Crashed → test fails.
sweep-powers:
    @RUN_POWER_SWEEP=1 dotnet test tests/Sts2Headless.MechanicSweepTests/Sts2Headless.MechanicSweepTests.csproj {{MSBUILD_MAX_CPU}} --nologo --filter "FullyQualifiedName~PowerSweepTests" --logger "console;verbosity=detailed" -- xUnit.MaxParallelThreads=1
    @echo ""
    @echo "report: documentation/coverage/sweep-powers.md"

# Run a fast subset of every mechanic sweep — N deterministic-random ids
# per kind. Good for "did I break the smoke surface" checks before
# launching a full multi-hour pass.
sweep-sample N="20":
    @RUN_MECHANIC_SWEEP=1 MECHANIC_SWEEP_SAMPLE={{N}} dotnet test tests/Sts2Headless.MechanicSweepTests/Sts2Headless.MechanicSweepTests.csproj {{MSBUILD_MAX_CPU}} --nologo --filter "Category=MechanicSweep" --logger "console;verbosity=detailed" -- xUnit.MaxParallelThreads=1

# Run the umbrella mechanic sweep — every kind, full universe. Hours.
# Use after a `GAME_VERSION` bump to find every regression in one go.
sweep-all:
    @RUN_MECHANIC_SWEEP=1 dotnet test tests/Sts2Headless.MechanicSweepTests/Sts2Headless.MechanicSweepTests.csproj {{MSBUILD_MAX_CPU}} --nologo --filter "Category=MechanicSweep" --logger "console;verbosity=detailed" -- xUnit.MaxParallelThreads=1

# Run every Python workspace member's tests via the uv workspace .venv.
test-python:
    @bash scripts/check-uv.sh
    @uv run pytest clients/python/

# Lint Python (ruff check + ruff format --check). Workspace-wide.
lint-python:
    @bash scripts/check-uv.sh
    @uv run ruff check clients/python/
    @uv run ruff format --check clients/python/

# Auto-fix Python (ruff check --fix + ruff format).
fix-python:
    @bash scripts/check-uv.sh
    @uv run ruff check --fix clients/python/
    @uv run ruff format clients/python/

# Static type-check Python with pyright (strict mode).
typecheck-python:
    @bash scripts/check-uv.sh
    @uv run pyright

# Run every test suite (C# unit + integration + end2end + Python) plus lint + typecheck, fanned out in parallel after a single build. ~3x faster than `test-sequential` on a 16-core box; per-suite caps via XUNIT_THREADS_INTEGRATION (default 8), XUNIT_THREADS_END2END (default 4), XUNIT_THREADS_UNIT (default 4).
test:
    @bash scripts/test-parallel.sh

# Same suites as `just test`, but each step runs sequentially with live output. Useful for clean per-suite logs or when the parallel orchestrator is the suspect.
test-sequential: test-unit test-integration test-end2end test-python lint-python typecheck-python
