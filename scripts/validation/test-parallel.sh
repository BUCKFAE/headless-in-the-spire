#!/usr/bin/env bash
# Fan out the whole test suite. Builds once, then runs the three C# test
# assemblies + Python tests / lint / typecheck concurrently. Logs are
# captured per task and dumped in completion order, so a failing task's
# output is the last thing on screen.
#
# STS2_TEST_FULL=1 widens the fan-out to every validation step we have:
# the standard suites above, plus Category=Gap (born-red, surfaces
# graduated gaps), the Benchmark suite, and all eight MechanicSweep
# kinds. Driven by `just validation::test-full`.
#
# Per-task thread caps (defaults tuned for a 16-core box):
#   XUNIT_THREADS_INTEGRATION (default 8)
#   XUNIT_THREADS_END2END     (default 4)
#   XUNIT_THREADS_UNIT        (default 4)
#
# `just validation::test-sequential` keeps the original step-by-step
# recipe for debugging or for environments where the parallel fan-out
# is too noisy.

set -uo pipefail

cd "$(dirname "$0")/../.."

MODE_FULL="${STS2_TEST_FULL:-0}"

echo "[build] starting..."
if ! just build::build; then
    echo "[build] FAILED"
    exit 1
fi

INT_THREADS="${XUNIT_THREADS_INTEGRATION:-8}"
E2E_THREADS="${XUNIT_THREADS_END2END:-4}"
UNIT_THREADS="${XUNIT_THREADS_UNIT:-4}"

LOG_DIR="$(mktemp -d -t sts2-test-parallel.XXXXXX)"
trap 'rm -rf "$LOG_DIR"' EXIT
echo "[logs] $LOG_DIR"

# Parallel indexed arrays keep this script working on stock macOS bash 3.2,
# which lacks `declare -A`. Each task gets the same index across NAMES/PIDS;
# completed slots set PIDS[i]="" so the drain loop can skip them.
NAMES=()
PIDS=()

start() {
    local name=$1
    shift
    ( "$@" ) > "$LOG_DIR/$name.log" 2>&1 &
    local pid=$!
    NAMES+=("$name")
    PIDS+=("$pid")
    echo "[$name] started (pid=$pid, log=$LOG_DIR/$name.log)"
}

start unit \
    dotnet test tests/Sts2Headless.UnitTests/Sts2Headless.UnitTests.csproj \
        --no-build --nologo -- "xUnit.MaxParallelThreads=$UNIT_THREADS"

start integration \
    dotnet test tests/Sts2Headless.IntegrationTests/Sts2Headless.IntegrationTests.csproj \
        --no-build --nologo --filter "Category!=Gap&Category!=Diagnostic&Category!=Benchmark" -- "xUnit.MaxParallelThreads=$INT_THREADS"

start end2end \
    dotnet test tests/Sts2Headless.End2EndTests/Sts2Headless.End2EndTests.csproj \
        --no-build --nologo --filter "Category!=Gap&Category!=Diagnostic&Category!=Benchmark" -- "xUnit.MaxParallelThreads=$E2E_THREADS"

start python uv run pytest clients/python/

start lint-python bash -c "uv run ruff check clients/python/ && uv run ruff format --check clients/python/"

start typecheck-python uv run pyright

if [ "$MODE_FULL" = "1" ]; then
    # Category=Gap is "born red on purpose" — green means a gap closed
    # and the test should graduate. Expect failures here on a healthy
    # codebase; the value is the diff vs. last run.
    start gaps-integration \
        dotnet test tests/Sts2Headless.IntegrationTests/Sts2Headless.IntegrationTests.csproj \
            --no-build --nologo --filter "Category=Gap" -- "xUnit.MaxParallelThreads=$INT_THREADS"

    start gaps-end2end \
        dotnet test tests/Sts2Headless.End2EndTests/Sts2Headless.End2EndTests.csproj \
            --no-build --nologo --filter "Category=Gap" -- "xUnit.MaxParallelThreads=$E2E_THREADS"

    start bench-parallel \
        dotnet test tests/Sts2Headless.End2EndTests/Sts2Headless.End2EndTests.csproj \
            --no-build --nologo --filter "FullyQualifiedName~ParallelHostThroughputBenchmark" -- "xUnit.MaxParallelThreads=1"

    # MechanicSweepTests is a single project; each kind is selected by
    # FullyQualifiedName + its own RUN_*_SWEEP env var (the test class
    # is skipped otherwise). Each sweep loads sts2.dll fully and is
    # serial inside xUnit (MaxParallelThreads=1) — eight in parallel
    # is memory-heavy but the wall-clock win is large.
    for spec in \
        "cards:CardSweepTests:RUN_CARD_SWEEP" \
        "relics:RelicSweepTests:RUN_RELIC_SWEEP" \
        "potions:PotionSweepTests:RUN_POTION_SWEEP" \
        "events:EventSweepTests:RUN_EVENT_SWEEP" \
        "encounters:EncounterSweepTests:RUN_ENCOUNTER_SWEEP" \
        "powers:PowerSweepTests:RUN_POWER_SWEEP" \
        "afflictions:AfflictionSweepTests:RUN_AFFLICTION_SWEEP" \
        "enchantments:EnchantmentSweepTests:RUN_ENCHANTMENT_SWEEP"; do
        IFS=":" read -r kind cls envvar <<< "$spec"
        start "sweep-$kind" \
            env "$envvar=1" \
            dotnet test tests/Sts2Headless.MechanicSweepTests/Sts2Headless.MechanicSweepTests.csproj \
                --no-build --nologo --filter "FullyQualifiedName~$cls" -- xUnit.MaxParallelThreads=1
    done
fi

remaining() {
    local n=0
    local p
    for p in "${PIDS[@]}"; do
        [ -n "$p" ] && n=$((n + 1))
    done
    echo "$n"
}

failed=()

# Drain in submission order: wait on the next live PID and report it
# when it finishes. Bash 3.2 lacks `wait -n -p`, so this is the simplest
# portable shape; per-task interleaving in the summary matches the old
# bash 3.2 fallback path. Slightly less responsive than the original
# `wait -n -p` (a later-submitted task that finishes first still has to
# wait for earlier tasks to be reaped), but the totals are unaffected.
while [ "$(remaining)" -gt 0 ]; do
    completed_pid=""
    completed_idx=-1
    for i in "${!PIDS[@]}"; do
        if [ -n "${PIDS[$i]}" ]; then
            completed_pid="${PIDS[$i]}"
            completed_idx=$i
            break
        fi
    done
    if [ -z "$completed_pid" ]; then
        echo "internal error: no live pid"
        exit 2
    fi
    wait "$completed_pid"
    rc=$?
    name="${NAMES[$completed_idx]}"
    PIDS[$completed_idx]=""
    if [ $rc -eq 0 ]; then
        marker="ok"
    else
        marker="FAIL (exit $rc)"
        failed+=("$name")
    fi
    echo
    echo "──── [$name] $marker ────"
    cat "$LOG_DIR/$name.log"
    echo "──── [$name] end ────"
done

echo
if [ ${#failed[@]} -eq 0 ]; then
    echo "all green"
    exit 0
else
    echo "failed: ${failed[*]}"
    exit 1
fi
