#!/usr/bin/env bash
# Runs a ShotDriver script on a virtual screen in a fresh sandbox (a new world each run).
#   bash tools/shot.sh <script.txt> [out folder] [--save birbcube-save.json]
# Shots land in the out folder (default: ./shots) as <name>.png. Needs setup-workspace.sh first.
set -euo pipefail

SCRIPT="$(realpath "$1")"
OUT="$(realpath -m "${2:-shots}")"
SAVE=""
if [ "${3:-}" = "--save" ]; then SAVE="$(realpath "$4")"; fi

REPO="$(cd "$(dirname "$0")/.." && pwd)"
BUILD="$HOME/birbshot/build"
RUN="$HOME/birbshot/run"
[ -f "$BUILD/ShotDriver.dll" ] || { echo "Run tools/setup-workspace.sh first"; exit 1; }

# The sandbox: the driver in bin/, an empty BirdsApp.csproj above it so the game treats it as the project
# folder (its save, settings, screenshots, Designer birbs and species live here, not in your repo)
rm -rf "$RUN"
mkdir -p "$RUN/bin" "$RUN/resources" "$OUT"
cp -r "$BUILD/." "$RUN/bin/"
echo '<Project />' > "$RUN/BirdsApp.csproj"
for d in birbs species; do
    [ -d "$REPO/resources/$d" ] && cp -r "$REPO/resources/$d" "$RUN/resources/"
done
[ -n "$SAVE" ] && cp "$SAVE" "$RUN/birbcube-save.json"

xvfb-run -a -s "-screen 0 1920x1080x24" dotnet "$RUN/bin/ShotDriver.dll" "$SCRIPT" --out "$OUT" 2>&1 \
    | grep -v -E "^INFO: (TEXTURE|SHADER|VAO|FBO|IMAGE|FILEIO|MODEL|MESH|GLTF)"
exit "${PIPESTATUS[0]}"
