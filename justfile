set dotenv-load

BUILD_CORES := "auto"
MSBUILD_MAX_CPU := if BUILD_CORES == "auto" { "-maxcpucount" } else { "-maxcpucount:" + BUILD_CORES }

# xUnit max parallel test threads. Local default is 0 ("# of CPU cores"); CI
# should export XUNIT_THREADS=2 (or similar) to cap host-subprocess fan-out.
# Passed through to xUnit via the RunSettings CLI override.
XUNIT_THREADS := env_var_or_default("XUNIT_THREADS", "0")

default:
    @just --list

# ── Local setup ───────────────────────────────────────────────────────────

# First-run setup: validate STS2 install, copy game DLLs, create uv workspace .venv, install git hooks, generate every *Id.g.cs content manifest from the local DLL.
setup:
    just validate-sts2-installation
    just pull-game-libs
    just sync-python
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

# List every member of <fqn> that sts2.dll references (e.g. `just list-members Godot.OS`).
list-members fqn: build
    @dotnet run --project src/Sts2Headless/Sts2Headless.csproj --no-build -- --list-members {{fqn}}

# Run the host in NDJSON stdio mode (AD-2). One JSON request per line on stdin, one response per line on stdout.
stdio: build
    @dotnet run --project src/Sts2Headless/Sts2Headless.csproj --no-build -- --stdio

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

# Remove all bin/ and obj/ build artifacts.
clean:
    @dotnet clean Sts2Headless.slnx
    @find src -type d \( -name bin -o -name obj \) -exec rm -rf {} +

# ── Tests ─────────────────────────────────────────────────────────────────

# Run only the unit suite — no vendor/sts2.dll required. Mirrors CI.
test-unit:
    @dotnet test tests/Sts2Headless.UnitTests/Sts2Headless.UnitTests.csproj {{MSBUILD_MAX_CPU}} --nologo

# Run the integration suite (loads vendor/sts2.dll; run `just setup` first).
test-integration:
    @dotnet test tests/Sts2Headless.IntegrationTests/Sts2Headless.IntegrationTests.csproj {{MSBUILD_MAX_CPU}} --nologo -- xUnit.MaxParallelThreads={{XUNIT_THREADS}}

# Run the end-to-end suite (multi-room arcs; same vendor/sts2.dll requirement).
test-end2end:
    @dotnet test tests/Sts2Headless.End2EndTests/Sts2Headless.End2EndTests.csproj {{MSBUILD_MAX_CPU}} --nologo -- xUnit.MaxParallelThreads={{XUNIT_THREADS}}

# Run the content-coverage sweep (greedy agent over multiple seeds with 999 HP cheat) and dump documentation/coverage/latest.{md,json}. Gitignored — proprietary content sourced from vendor/sts2.dll. Off by default in `just test-end2end`; this recipe sets RUN_COVERAGE_SWEEP=1 to opt in.
coverage:
    @RUN_COVERAGE_SWEEP=1 dotnet test tests/Sts2Headless.End2EndTests/Sts2Headless.End2EndTests.csproj {{MSBUILD_MAX_CPU}} --nologo --filter "FullyQualifiedName~CoverageSweepTests" -- xUnit.MaxParallelThreads=1
    @echo ""
    @echo "report: documentation/coverage/latest.md"

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

# Run every test suite (C# unit + integration + end2end + Python) plus lint + typecheck.
test: test-unit test-integration test-end2end test-python lint-python typecheck-python
