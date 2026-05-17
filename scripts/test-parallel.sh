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
        --no-build --nologo --filter "Category!=Gap" -- "xUnit.MaxParallelThreads=$INT_THREADS"

start end2end \
    dotnet test tests/Sts2Headless.End2EndTests/Sts2Headless.End2EndTests.csproj \
        --no-build --nologo --filter "Category!=Gap" -- "xUnit.MaxParallelThreads=$E2E_THREADS"

start python uv run pytest clients/python/

start lint-python bash -c "uv run ruff check clients/python/ && uv run ruff format --check clients/python/"

start typecheck-python uv run pyright

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
