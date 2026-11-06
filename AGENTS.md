# AGENTS.md

Extra information for AI coding agents. The README is for players and stays short. Claude Code reads `CLAUDE.md`; any other agent should read it too, since it holds the working rules, architecture guide and session routine. This file adds what is not there.

## Stack
C# (.NET 10), [Raylib-cs](https://github.com/ChrisDill/Raylib-cs) for rendering, Dear ImGui via [rlImGui-cs](https://github.com/raylib-extras/rlImGui-cs) for the tools panel, NAudio for the radio. Windows 10/11 x64 only.

## Build and run
Needs the [.NET 10 SDK](https://dotnet.microsoft.com/download).
- Run: `dotnet run`. Compile check: `dotnet build`.
- `Build Birb Cube.bat`: fast local copy in a "Birb Cube" folder next to the project, with shortcuts. Needs the .NET 10 runtime to play.
- `Make Release Zip.bat`: self-contained release zip (no .NET needed to play), written to "Birb Cube Release" next to the project. The name comes from `GameEngine.Version`.

## Files and folders
- `resources/` (shaders, sounds, fonts, starfield, species, birbs) is copied to the build output automatically.
- The save, settings and `Radio/` folder sit next to the exe and are git-ignored.
- `tools/` is excluded from the game build (`BirdsApp.csproj`): the screenshot driver, the Jello board, and the font tools (`tools/Vectorize`, `tools/FontAtlas`).
- `claude/file-map.md` has one entry per file and a "Where to find things" index. Read it before searching the code.

## Text
In-game text goes through `UiText`: Arkhane (a glyph atlas built from `claude/arkhane.svg` by `tools/FontAtlas`) for larger sizes, Chakra Petch for small ones. The ImGui tools panel uses Chakra Petch.
