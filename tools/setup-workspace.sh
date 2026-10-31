#!/usr/bin/env bash
# Sets up Claude's Linux workspace to compile and run Birb Cube, then builds the screenshot driver.
# Run once per session, after the repo has been staged into the workspace:
#   bash tools/setup-workspace.sh [repo folder]      (default: the folder this script is in, one up)
# Safe to run again; finished steps are skipped.
set -euo pipefail

REPO="$(cd "${1:-$(dirname "$0")/..}" && pwd)"
BUILD="$HOME/birbshot/build"

say() { printf '\n== %s\n' "$*"; }

# 1. Tools: .NET 10, glslang (shader checks), a virtual screen
need=()
command -v dotnet >/dev/null           || need+=(dotnet-sdk-10.0)
command -v glslangValidator >/dev/null || need+=(glslang-tools)
command -v xvfb-run >/dev/null         || need+=(xvfb)
if [ ${#need[@]} -gt 0 ]; then
    say "Installing ${need[*]}"
    apt-get update -qq
    apt-get install -y -qq "${need[@]}" >/dev/null
fi

# 2. Sound: a null device, so the game's audio opens without speakers
if ! grep -q 'type null' "$HOME/.asoundrc" 2>/dev/null; then
    say "Null sound device"
    printf 'pcm.!default { type null }\nctl.!default { type null }\n' > "$HOME/.asoundrc"
fi

# 3. Files the build needs, which only exist on your computer (git-ignored)
missing=0
for f in bin/Release/net10.0/win-x64/Raylib-cs.dll bin/Release/net10.0/win-x64/ImGui.NET.dll \
         bin/Release/net10.0/win-x64/rlImGui.dll bin/Release/net10.0/win-x64/NAudio.Core.dll \
         bin/Release/net10.0/win-x64/NAudio.Wasapi.dll bin/Release/net10.0/win-x64/NAudio.Vorbis.dll \
         bin/Release/net10.0/win-x64/NVorbis.dll \
         bin/Debug/net10.0/runtimes/linux-x64/native/libraylib.so \
         bin/Debug/net10.0/runtimes/linux-x64/native/libcimgui.so; do
    if [ ! -f "$REPO/$f" ]; then echo "MISSING: $f (stage it from your computer)"; missing=1; fi
done
[ $missing -eq 0 ] || exit 1

# 4. Build the driver (the game's code plus the script runner)
say "Building ShotDriver into $BUILD"
mkdir -p "$HOME/birbshot"
dotnet build "$REPO/tools/ShotDriver/ShotDriver.csproj" -c Release -o "$BUILD" -nologo -v q \
    --configfile "$REPO/tools/ShotDriver/nuget.config" > "$HOME/birbshot/build.log" 2>&1 \
    && built=1 || built=0
grep -E "error|warning" "$HOME/birbshot/build.log" | sort -u || true
[ $built -eq 1 ] || { echo "Build failed (full log: $HOME/birbshot/build.log)"; exit 1; }

say "Ready. Run a script with: bash $REPO/tools/shot.sh <script.txt> [out folder]"
