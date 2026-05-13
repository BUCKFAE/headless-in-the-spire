#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
BADGE_DIR="${1:-"$ROOT/.github/badges"}"

die() {
    printf 'error: %s\n' "$*" >&2
    exit 1
}

json_escape() {
    local value="$1"
    value="${value//\\/\\\\}"
    value="${value//\"/\\\"}"
    value="${value//$'\n'/ }"
    printf '%s' "$value"
}

write_badge() {
    local name="$1"
    local label="$2"
    local message="$3"
    local color="$4"
    local logo="${5:-}"
    local logo_color="${6:-}"
    local path="$BADGE_DIR/$name.json"

    mkdir -p "$BADGE_DIR"

    {
        printf '{\n'
        printf '  "schemaVersion": 1,\n'
        printf '  "label": "%s",\n' "$(json_escape "$label")"
        printf '  "message": "%s",\n' "$(json_escape "$message")"
        if [[ -n "$logo" ]]; then
            printf '  "namedLogo": "%s",\n' "$(json_escape "$logo")"
        fi
        if [[ -n "$logo_color" ]]; then
            printf '  "logoColor": "%s",\n' "$(json_escape "$logo_color")"
        fi
        printf '  "color": "%s"\n' "$(json_escape "$color")"
        printf '}\n'
    } > "$path"

    printf 'Wrote %s: %s=%s\n' "$path" "$label" "$message"
}

xml_value() {
    local file="$1"
    local tag="$2"

    sed -n "s/.*<$tag>\\([^<]*\\)<\\/$tag>.*/\\1/p" "$file" | head -n 1
}

package_versions() {
    local package="$1"
    local path="$2"

    find "$path" -type f -name '*.csproj' -exec awk -v package="$package" '
        $0 ~ /<PackageReference/ && $0 ~ "Include=\"" package "\"" {
            if (match($0, /Version="[^"]+"/)) {
                print substr($0, RSTART + 9, RLENGTH - 10)
            }
        }
    ' {} + | sort -u | paste -sd '/' -
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

target_framework="$(xml_value "$ROOT/Directory.Build.props" TargetFramework)"
[[ -n "$target_framework" ]] || die "TargetFramework not found in Directory.Build.props"
dotnet_version="${target_framework#net}"

lang_version="$(xml_value "$ROOT/Directory.Build.props" LangVersion)"
[[ -n "$lang_version" ]] || die "LangVersion not found in Directory.Build.props"

xunit_version="$(package_versions xunit "$ROOT/tests")"
[[ -n "$xunit_version" ]] || die "xunit PackageReference not found under tests/"

unit_count="$(count_tests "$ROOT/tests/Sts2Headless.UnitTests")"
integration_count="$(count_tests "$ROOT/tests/Sts2Headless.IntegrationTests")"
total_count="$(count_tests "$ROOT/tests")"

godot_stubs_version="$(xml_value "$ROOT/src/GodotStubs/GodotStubs.csproj" AssemblyVersion)"
[[ -n "$godot_stubs_version" ]] || die "AssemblyVersion not found in GodotStubs.csproj"
godot_stubs_version="${godot_stubs_version%.0}"

protocol="$(sed -n 's/^\/\/ AD-2: \([^ ]*\).*/\1/p' "$ROOT/src/Sts2Headless.Protocol/Envelope.cs" | head -n 1)"
[[ -n "$protocol" ]] || die "protocol marker not found in Envelope.cs"

vendor_message="tracked"
vendor_color="red"
if ! git -C "$ROOT" ls-files --error-unmatch vendor/sts2.dll >/dev/null 2>&1; then
    vendor_message="local only"
    vendor_color="orange"
fi

write_badge dotnet ".NET" "$dotnet_version" "512BD4" "dotnet" "white"
write_badge csharp "C#" "$lang_version" "239120" "csharp" "white"
write_badge xunit "xUnit" "$xunit_version" "5E2B97"
write_badge tests "tests" "$total_count" "blue"
write_badge godot-stubs "Godot stubs" "$godot_stubs_version" "478CBF" "godotengine" "white"
write_badge protocol "protocol" "$protocol" "0f766e"
write_badge vendor-dlls "vendor DLLs" "$vendor_message" "$vendor_color"

printf 'Counted %s tests (%s unit, %s integration)\n' \
    "$total_count" "$unit_count" "$integration_count"
