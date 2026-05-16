#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
BADGE_DIR="${1:-"$ROOT/.github/badges"}"

write_badge() {
    local name="$1"
    local label="$2"
    local message="$3"
    local color="$4"
    local path="$BADGE_DIR/$name.json"

    mkdir -p "$BADGE_DIR"

    jq -n \
        --arg label "$label" \
        --arg message "$message" \
        --arg color "$color" \
        '{schemaVersion: 1, label: $label, message: $message, color: $color}' \
        > "$path"

    printf 'Wrote %s: %s=%s\n' "$path" "$label" "$message"
}

count_tests() {
    local path="$1"

    if [[ ! -d "$path" ]]; then
        printf '0\n'
        return
    fi

    find "$path" -type f -name '*.cs' -exec awk '
        /^[[:space:]]*\[([[:alnum:]_.]+\.)?(Fact|Theory)(Attribute)?(\(|\])/ { count++ }
        END { print count + 0 }
    ' {} + | awk '{ total += $1 } END { print total + 0 }'
}

count_csharp_loc() {
    local total=0
    local path
    for path in "$@"; do
        [[ -d "$path" ]] || continue
        local subtotal
        subtotal="$(find "$path" -type f -name '*.cs' \
            -not -path '*/bin/*' -not -path '*/obj/*' \
            -exec cat {} + | wc -l)"
        total=$(( total + subtotal ))
    done
    printf '%s\n' "$total"
}

format_loc() {
    local n="$1"
    if (( n >= 1000 )); then
        awk -v n="$n" 'BEGIN { printf "%.1fk", n / 1000 }'
    else
        printf '%s' "$n"
    fi
}

unit_count="$(count_tests "$ROOT/tests/Sts2Headless.UnitTests")"
integration_count="$(count_tests "$ROOT/tests/Sts2Headless.IntegrationTests")"
total_count="$(count_tests "$ROOT/tests")"

csharp_loc="$(count_csharp_loc "$ROOT/src" "$ROOT/tests")"
csharp_loc_label="$(format_loc "$csharp_loc")"

write_badge tests "tests" "$total_count" "blue"
write_badge csharp-loc "C# LoC" "$csharp_loc_label" "239120"

printf 'Counted %s tests (%s unit, %s integration)\n' \
    "$total_count" "$unit_count" "$integration_count"
