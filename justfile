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

# ── Tests ─────────────────────────────────────────────────────────────────

test:
    @echo "Running unittests..."
    # TODO: Wire in C# tests once src/ exists.

test-full: test
    @echo "Running full tests..."
    # TODO: Wire in end-to-end tests.
