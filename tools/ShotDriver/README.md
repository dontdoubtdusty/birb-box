# ShotDriver

Claude's screenshot tool. It runs the real game on a virtual screen in Claude's Linux workspace, follows a plain-text script, and saves named screenshots. It's never shipped and the game never uses it: `BirdsApp.csproj` leaves `tools\` out of the build.

## Each session (Claude)

The workspace is wiped between sessions, and Claude has no shell on your computer, so:

1. **Stage** the repo into the workspace (it lands at `/mnt/user-data/uploads/birb-box/`):
   - every top-level `*.cs`, `BirdsApp.csproj`
   - all of `resources/` and all of `tools/`
   - `bin/Release/net10.0/win-x64/`: `Raylib-cs.dll`, `ImGui.NET.dll`, `rlImGui.dll`, `NAudio.Core.dll`, `NAudio.Wasapi.dll`, `NAudio.Vorbis.dll`, `NVorbis.dll`
   - `bin/Debug/net10.0/runtimes/linux-x64/native/`: `libraylib.so`, `libcimgui.so`
2. Copy it to a working folder, then `bash tools/setup-workspace.sh`. This installs .NET 10, glslang and xvfb, sets up a null sound device, checks the DLLs are there, and builds the driver into `~/birbshot/build`.
3. `bash tools/shot.sh <script.txt> [out folder] [--save birbcube-save.json]`

Each run starts in a fresh sandbox (`~/birbshot/run`) with a new world, unless `--save` copies in a save. Your Designer birbs and species are copied into that sandbox. Nothing is written back to the repo. A full tour takes about 30 seconds.

## Script commands

One command per line. `#` starts a comment. Every line is checked before the game starts, so a typo fails at once.

| Command | What it does |
|---|---|
| `wait <s>` | Runs the sim forward that many sim seconds, without drawing (fast) |
| `frames <n>` | Updates and draws n frames |
| `shot <name>` | Saves `<out>/<name>.png`, taken through the game's own F9 path |
| `size <w> <h>` | Window size (starts at 1280x720; the virtual screen is 1920x1080) |
| `power on\|off` | Projector; waits until the cube has fully risen or gone |
| `scene birds\|store` | The rocker; waits until the fog clears |
| `view room\|hologram` | Dives into the garden or back out; waits for the dive and for the hands to drop away |
| `tools on\|off` | The Tab tools panel |
| `yaw <deg>` | Turns the ring. Use multiples of 10: the ring settles into 10-degree notches |
| `pitch <deg>` | Tilts the room camera, or the garden camera when inside |
| `distance <d>` | Room camera distance (29 at start; lower leans in) |
| `zoom <z>` | Bird world zoom (0.25 Wide, 0.5 Field, 1 Close, 3 Perch) |
| `center <x> <z>` | Moves the view window in the world |
| `hour <h>` | Sets today's hour and holds the clock |
| `season <name>` | Skips to spring, summer, autumn or winter (same hour) and holds the clock |
| `clock hold\|run` | Holds or releases the clock |
| `rain [strength]` / `showers` / `drizzle` | Starts rain, waits for it to build all the way, holds the weather |
| `clear` | Clears up, waits for a clear sky, holds the weather |
| `soak` | Soaks the ground (puddles still need rain) |
| `birds <n>` | Calls n birds (they walk or fly in; follow with `wait 20` or so) |
| `scatter <n>` | Scatters n seed piles in view |
| `set <path> <value>` | Sets any field or property: `set SimTuning.Now.MaxBirds 12`, `set engine.Camera.Pitch 0.6` |
| `call <path> [args]` | Calls any method: `call engine.Weather.Soak`, `call engine.Clock.NextDay` |
| `print <path>` | Prints a value: `print engine.DrawMs`, `print engine.Clock.TimeText` |
| `log [n]` | Prints the last n Bird Log lines |

A path starts at `engine` (the running `GameEngine`) or at a class name (its static members). Private members work too. Values can be numbers, `true`/`false`, enum names, text, or `x,y,z` for vectors. A value computed once a frame (such as `engine.Clock.TimeText` after `call ...SetHour`) shows the change only after a `wait` or a `frames`.

See `scripts/tour.txt` for an example. It also works as the driver's own smoke test.

## When it breaks

- **"GameEngine.Update was not found"** (or Draw): the game renamed one of them. Fix the names in `ShotDriver.StartGame`.
- **A crash at start-up after a new load step:** `ShotDriver.StartGame` copies `GameEngine.Run`'s start-up. If `Run` gains a step, add it there too.
- **A command waited too long:** it names what it was waiting for (for example "the rain building up"). Usually something it waits on, like `Projector.Ready`, changed meaning.
- Runs aren't identical from one to the next: the game uses `Random.Shared`, which can't be seeded.
