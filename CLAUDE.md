# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project
Birb Cube: a cozy hologram-style 3D bird simulation for Windows 10/11 x64. C# / .NET 10, Raylib-cs (rendering), Dear ImGui via rlImGui-cs (panels), NAudio (radio). No test suite or linter. The repo is still named birb-box. I commit and push with GitHub Desktop; you never commit.

## Commands
- Run: `dotnet run`. Build: `dotnet build`. It is a `WinExe` (assembly `BirbCube`), so there is no console; crashes go to `crash.txt` next to the exe plus a message box (`Program.cs`).
- `Build Birb Cube.bat`: quick local play copy with shortcuts (needs the .NET runtime to play).
- `Make Release Zip.bat`: self-contained release zip; takes its name from the version.
- Version: the single `Version` constant in `GameEngine.cs` (`"Birb Cube 5.19 Alpha"`). It also shows on the pedestal bezel and in the zip name.
- Compile checks and headless screenshots: `tools/ShotDriver` (separate program, excluded from the game build). Follow `tools/ShotDriver/README.md`; do not write a new driver. Run `bash tools/setup-workspace.sh` once, then `bash tools/shot.sh <script.txt> [outdir]` (xvfb, Linux side).
- Smoke test after every change: `claude/qa-plan.md` section 3.

## Architecture
Flat project: all `.cs` files in the repo root, no namespaces. `resources/**` is copied to the output automatically. `claude/file-map.md` has one entry per file and a "Where to find things" tuning index; use it before searching.
- `GameEngine` (singleton) owns every system and runs the frame in phases (`UpdateSystems`). `World` / `Entity`: adds and removes are queued until `FlushChanges()`; ground is Y=0, X/Z run -HalfSize to +HalfSize. `Update(dt)` simulates only; drawing is separate.
- Tunable sim numbers go in `SimTuning` (Tab tools panel, persisted in the settings file).
- Rendering (`HologramRenderer`, header explains the four passes): world into an off-screen texture via `HoloCamera` + `hologram.fs`; room with `room.fs`; `post.fs` glow/scanlines; then `Hands`. The pool shape is written twice, in `Pool.DistanceAt` and `waterDistance` in `hologram.fs`; change both together.
- Ground things go through `Rock.PushOut` and `Pool.PushOut`. Anything per species reads the `Species` entry and must work for any number of species. Weather is read from `Weather.Instance`, never pushed. More patterns: `claude/next-session-plan.md` section 2.
- Persistence: `SaveGame` writes `birbcube-save.json`; each class has `ToSave`/`FromSave`; `Settings` is a separate file. Bump `FormatVersion` on breaking layout changes. `Assets.ProjectFolder` (`Helpers.cs`) finds the project folder when developing; the `Radio/` folder is gitignored.

## Before editing
- Never edit code or docs without my OK.
- For non-trivial changes, ask only questions that change what you will build.
- For trivial fixes (a typo, a value I named), one confirmation is enough.
- When you must ask me something, use clickable multiple choice, max 4 options per round, recommended option first and labeled "(Recommended)".
- If the answer is obvious or easy to change later, decide and tell me what you chose.
- If a task touches "Open decisions" or an [Undecided] item in claude/vision.md, or is not covered by it, ask instead of guessing.

## Style
- No emojis anywhere: messages, code, comments, docs, UI text, placeholders.
- If something needs an icon, use inline SVG.
- Challenge my ideas. Do not sugarcoat problems. Tell me when a plan is bad.

## Building
- I know C# well. I burn out building piece by piece, so you do the building. Build features all the way through; do not hand me pieces to write.
- Comment in plain language: one comment per method or block saying what it does, not how. Keep file header comments (for example `HologramRenderer.cs`, `Pool.cs`) accurate.
- Keep state and logic separate from rendering.
- If you cannot run or compile the code, re-read your changes for errors before handing them over.

## After each change
- Summarize in a few lines: what changed, why, and which files you touched.
- If you used a language, library or framework pattern I may not have seen, name it and explain it in one or two sentences, once. Comparisons to C# are welcome.

## Bookkeeping
- At session start, read claude/file-map.md, claude/session-log.md and claude/vision.md before searching the code, then the Jello board named in the log. The code wins if they disagree; flag the mismatch.
- Bump the version by 0.01 each time a feature is finished. Bigger jumps only when I say.

## Wrap up
When I say "wrap up", do all of these:
1. Update claude/file-map.md if files changed.
2. Add a dated entry to claude/session-log.md (what was done, what is next) and add 1 to the session counter.
3. Propose claude/vision.md updates if a change made it wrong or finished a backlog item.
4. Bump the version by 0.01 if a feature was finished.
5. Optionally suggest 1-2 small "try it yourself" tasks. Never block on them.
6. Give me a short summary.
