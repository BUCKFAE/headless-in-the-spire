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

# Verify STS2_GAME_DIR and copy game DLLs
setup:
    just validate-sts2-installation
    just pull-game-libs

# Verify STS2_GAME_DIR points at a real STS2 install with the required DLLs.
validate-sts2-installation:
    @bash scripts/validate-sts2-installation.sh

# Copy game DLLs from STS2_GAME_DIR into ./vendor (first-run bootstrap; see AD-3).
pull-game-libs:
    @bash scripts/pull-game-libs.sh

# Clone reference projects (currently sts2-cli) into external-tools/.
clone-external-tools:
    @mkdir -p external-tools
    @test -d external-tools/sts2-cli || git clone --depth 1 --single-branch --no-tags https://github.com/wuhao21/sts2-cli.git external-tools/sts2-cli

# ── Build ─────────────────────────────────────────────────────────────────

# Build the whole solution with MSBuild parallelism; override with `just BUILD_CORES=4 build`.
build:
    @dotnet build Sts2Headless.slnx {{MSBUILD_MAX_CPU}}

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

# List every member of <fqn> that sts2.dll references (e.g. `just list-members Godot.OS`).
list-members fqn: build
    @dotnet run --project src/Sts2Headless/Sts2Headless.csproj --no-build -- --list-members {{fqn}}

# Run the host in NDJSON stdio mode (AD-2). One JSON request per line on stdin, one response per line on stdout.
stdio: build
    @dotnet run --project src/Sts2Headless/Sts2Headless.csproj --no-build -- --stdio

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

# Run both suites.
test: test-unit test-integration
