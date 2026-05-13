set dotenv-load

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
    @test -d external-tools/sts2-cli || git clone --depth 1 https://github.com/wuhao21/sts2-cli.git external-tools/sts2-cli

# ── Build ─────────────────────────────────────────────────────────────────

# Build the whole solution (Sts2Headless exe + Protocol lib + GodotStubs lib).
build:
    @dotnet build Sts2Headless.slnx

# Run the headless host (prints the banner and vendor inventory for now).
run: build
    @dotnet run --project src/Sts2Headless/Sts2Headless.csproj --no-build

# Load vendor/sts2.dll and report missing GodotStubs surface (diagnostic).
inspect-sts2: build
    @dotnet run --project src/Sts2Headless/Sts2Headless.csproj --no-build -- --inspect-sts2

# Remove all bin/ and obj/ build artifacts.
clean:
    @dotnet clean Sts2Headless.slnx
    @find src -type d \( -name bin -o -name obj \) -exec rm -rf {} +

# ── Tests ─────────────────────────────────────────────────────────────────

test:
    @echo "Running unittests..."
    # TODO: Wire in C# tests once src/ exists.

test-full: test
    @echo "Running full tests..."
    # TODO: Wire in end-to-end tests.
