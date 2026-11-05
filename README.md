# Birb Cube

A cozy, hologram-style 3D bird simulation for Windows. Sit at a bird terrarium in a spaceship cabin, scatter seed, collect feathers and watch the birds go about their day.

Built with C# (.NET 10), [Raylib-cs](https://github.com/ChrisDill/Raylib-cs) and Dear ImGui via [rlImGui-cs](https://github.com/raylib-extras/rlImGui-cs). Windows 10/11, 64-bit only.

## Play

1. Download the latest `BirbCube-<version>-win-x64.zip` from [Releases](../../releases).
2. Unzip it somewhere you can write to (Desktop or Documents).
3. Double-click `BirbCube.exe`. Nothing else needs installing.

Windows may warn about an unsigned app: click "More info", then "Run anyway". `Read Me First.txt` in the zip has the controls and the radio.

## Build from source

Needs the [.NET 10 SDK](https://dotnet.microsoft.com/download).

- Run it: `dotnet run`
- Fast local copy with shortcuts: `Build Birb Cube.bat` (makes a "Birb Cube" folder next to the project; needs the .NET 10 runtime to play)
- Release zip for sharing: `Make Release Zip.bat` (self-contained, no .NET needed to play; writes to "Birb Cube Release" next to the project)

Shaders, sounds, fonts and the starfield live in `resources/` and are copied to the build output automatically. Your save, settings and radio folder sit next to the exe and are not tracked by git.
