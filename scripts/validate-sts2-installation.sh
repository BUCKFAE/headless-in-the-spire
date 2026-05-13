#!/usr/bin/env bash
# Verify that STS2_GAME_DIR is set, exists, and contains the DLLs we need.
# This is the cheap precheck — it does not copy anything. Run it before
# pull-game-libs.sh to surface configuration mistakes early.

set -euo pipefail

if [ -z "${STS2_GAME_DIR:-}" ]; then
    echo "✗ STS2_GAME_DIR is not set." >&2
    echo "  Copy .env.example to .env and set STS2_GAME_DIR there." >&2
    exit 2
fi

if [ ! -d "$STS2_GAME_DIR" ]; then
    echo "✗ STS2_GAME_DIR does not exist: $STS2_GAME_DIR" >&2
    exit 2
fi

REQUIRED=(sts2.dll 0Harmony.dll MonoMod.Backports.dll SmartFormat.dll)
missing=0
echo "📁 STS2_GAME_DIR: $STS2_GAME_DIR"
for dll in "${REQUIRED[@]}"; do
    if [ -f "$STS2_GAME_DIR/$dll" ] || find "$STS2_GAME_DIR" -maxdepth 4 -name "$dll" -type f -print -quit 2>/dev/null | grep -q .; then
        echo "  ✓ $dll"
    else
        echo "  ✗ $dll" >&2
        missing=$((missing + 1))
    fi
done

if [ "$missing" -gt 0 ]; then
    echo "" >&2
    echo "$missing critical DLL(s) missing. Make sure STS2_GAME_DIR points at" >&2
    echo "the directory that contains sts2.dll (typically the game root on" >&2
    echo "Linux, or data_sts2_macos_*/ inside the .app bundle on macOS)." >&2
    exit 3
fi

echo ""
echo "✅ STS2 install looks good."
