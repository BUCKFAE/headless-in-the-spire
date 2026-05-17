#!/usr/bin/env bash
# Fan out the whole test suite. Builds once, then runs the three C# test
# assemblies + Python tests / lint / typecheck concurrently. Logs are
# captured per task and dumped in completion order, so a failing task's
# output is the last thing on screen.
#
# Per-task thread caps (defaults tuned for a 16-core box):
#   XUNIT_THREADS_INTEGRATION (default 8)
#   XUNIT_THREADS_END2END     (default 4)
#   XUNIT_THREADS_UNIT        (default 4)
#
# `just test-sequential` keeps the original step-by-step recipe for
# debugging or for environments where the parallel fan-out is too noisy.

set -uo pipefail

cd "$(dirname "$0")/.."

echo "[build] starting..."
if ! just build; then
    echo "[build] FAILED"
    exit 1
fi

INT_THREADS="${XUNIT_THREADS_INTEGRATION:-8}"
E2E_THREADS="${XUNIT_THREADS_END2END:-4}"
UNIT_THREADS="${XUNIT_THREADS_UNIT:-4}"

LOG_DIR="$(mktemp -d -t sts2-test-parallel.XXXXXX)"
trap 'rm -rf "$LOG_DIR"' EXIT

declare -A PIDS

start() {
    local name=$1
    shift
    ( "$@" ) > "$LOG_DIR/$name.log" 2>&1 &
    PIDS[$name]=$!
    echo "[$name] started (pid=${PIDS[$name]}, log=$LOG_DIR/$name.log)"
}

start unit \
    dotnet test tests/Sts2Headless.UnitTests/Sts2Headless.UnitTests.csproj \
        --no-build --nologo -- "xUnit.MaxParallelThreads=$UNIT_THREADS"

start integration \
    dotnet test tests/Sts2Headless.IntegrationTests/Sts2Headless.IntegrationTests.csproj \
        --no-build --nologo --filter "Category!=Gap" -- "xUnit.MaxParallelThreads=$INT_THREADS"

start end2end \
    dotnet test tests/Sts2Headless.End2EndTests/Sts2Headless.End2EndTests.csproj \
        --no-build --nologo --filter "Category!=Gap" -- "xUnit.MaxParallelThreads=$E2E_THREADS"

start python uv run pytest clients/python/

start lint-python bash -c "uv run ruff check clients/python/ && uv run ruff format --check clients/python/"

start typecheck-python uv run pyright

failed=()

while [ ${#PIDS[@]} -gt 0 ]; do
    completed_pid=""
    wait -n -p completed_pid
    rc=$?
    if [ -z "${completed_pid:-}" ]; then
        # bash <5.1 lacks `wait -p`. Fall back to draining tasks in submission
        # order; the summary still works, just without per-task interleaving.
        for name in unit integration end2end python lint-python typecheck-python; do
            if [ -n "${PIDS[$name]:-}" ]; then
                completed_pid="${PIDS[$name]}"
                wait "$completed_pid"
                rc=$?
                break
            fi
        done
    fi
    if [ -z "${completed_pid:-}" ]; then
        # Shouldn't happen, but bail rather than infinite-loop.
        echo "internal error: no completed pid"
        exit 2
    fi
    for name in "${!PIDS[@]}"; do
        if [ "${PIDS[$name]}" = "$completed_pid" ]; then
            unset 'PIDS[$name]'
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
            break
        fi
    done
done

echo
if [ ${#failed[@]} -eq 0 ]; then
    echo "all green"
    exit 0
else
    echo "failed: ${failed[*]}"
    exit 1
fi
