# Session Log

Session count: 48.

**Status and order live on the Jello board:** local files, one JSON file per card in `claude/board/birbcube/cards/` (columns in `claude/board/birbcube/game.json`: `ideas`, `next`, `doing`, `test` = "Built, needs testing", `done`). You open it with `Jello Board.bat`; Claude reads and edits the files directly (the page picks up file edits within 2 s). At session start, after this log and the file map, read the board: cards with `"columnId": "test"` first, then the lowest `order` in `"next"`. Move a card by changing `columnId` (and `order`, its sort number in the column); tick a subtask with `"completed": true`; a new card is a new file whose name is its id. The old claude.ai board (https://claude.ai/artifact/1HH1kCcnvGa5QSPGNb9S5C) is a frozen backup from Nov 5; don't edit it.

**Writing this log:** one row per session, one or two lines: version, what was built or decided, anything left open. A version's checks go on its board card, tuning pointers in the file map's "Where to find things", design decisions in `vision.md`. Full rows for sessions 1-35 (decisions, checks, tuning notes): `claude/archive/history-to-session-35.md`. Doc sections cut on Nov 5 (git basics, old Step 11 design, claude.ai workspace tricks): `claude/archive/docs-cut-2026-11-05.md`. Search it when needed; don't read it at session start.

**De-tangling:** weekly, Thursday afternoon after the token reset; no every-5th-session rule. Next date and watch list: `qa-plan.md` section 7.

**Open bugs:** none (`qa-plan.md` section 6).

**Committed:** everything through 5.10 (confirmed Nov 1). Never logged: 4.94, 4.95, 5.02, 5.05 and 5.08 (likely small fixes made while testing). 5.11-5.15 are tested (Nov 3) but their commit isn't confirmed. 5.16-5.19 and the BirdMover split are tested by you (said Nov 5); commit not confirmed. 5.20 and 5.21: the code said 5.21 at session 47's start, but nothing records what they changed (ask). 5.22 (session 47) tested by you (said Nov 5, hologram shader fine); commit not confirmed. `Make Release Zip.bat` run and works (Nov 5); the clean-PC test and the GitHub upload are still open on its card. 5.23 (session 48) built, untested, uncommitted.

**Workspace tooling:** on this PC, compile with `dotnet build`. The screenshot driver (`tools/ShotDriver/README.md`) only runs in a Linux workspace with xvfb, and WSL isn't installed here (checked Nov 5); don't write a new driver. Driver tricks that aren't in the README, for when a Linux workspace is available:
- A refactor that should change nothing: build old and new code, each with a small test file in place of `Program.cs` that prints the tables, and diff the printouts. For a pure move of code between files of one class, comparing every line of old against new (a line-bag diff) plus a compile (and the driver's tour, where it can run) is enough.
- Headless bird tests: open a hidden raylib window under `xvfb-run` and call `PartLibrary.Load` first.
- Before/after looks: make one save (a script ending `set engine.Saves.autosaveTimer 0` then `frames 3` writes it to `~/birbshot/run`), load it paused into both builds with `--save`. The scan sweep runs on the real clock, so a teal band can still differ.
- Behavior stats: a throwaway static class in the workspace copy of `tools/ShotDriver/` (never written back), `call`ed between `wait` lines; generate long scripts with Python. Three in-game days take a few minutes. Session 44's `WaterProbe` (reads a mover's private water state by reflection, sampled every 4 s with raised drink and bath chances) is the pattern.
- Close-ups of one bird: in a throwaway copy, auto-select and follow a wandering bird from `UpdateSimulation` behind a static flag (`set GameEngine.Pick true`), then `zoom 3`. Birds are still small at `distance 16`; lean in further for poses.
- Screenshots of the bird card (session 45): in the workspace copy only, add `public static bool PickFirst` to `GameEngine` (select the first bird from `UpdateSimulation`), and statics in `Hud` for a forced "all traits" flag and a fake mouse position (`set Hud.MouseX 400`, `set Hud.MouseY 152`) so a hover tooltip can be shot. The window is 1280x720 in the shots. Never written back.
- Sound checks: the workspace has a null sound device, so a script can `print` a sound class's private state (`print Ambience.level`) to prove a loop loads, starts and fades, but nothing can be heard.

| # | Date | Version | What we did |
|---|---|---|---|
| 1 | 10-22 | 4.41 | File map; instructions rewrite. |
| 2 | 10-22 | 4.42 | Step 0: unused files removed, HologramRenderer split (WorldDrawer, FieldEdges). Step 1: draw ms and model-draw count. |
| 3 | 10-22 | 4.43-4.44 | Step 2: the radio (folder and web stations, NAudio decoding, speaker sound). |
| 4 | 10-22 | 4.47 | Steps 3-5: pedestal in the room pass, beams, cube hologram that turns like a turntable. |
| 5 | 10-22 | 4.48 | Floating cube, hollow pedestal, one lens; the logo is the power button (`Projector.cs`). |
| 6 | 10-23 | 4.49-4.50 | Square floor to the walls; saving (`SaveGame.cs`, autosave). |
| 7 | 10-24 | 4.51 | Step 6: 56x56 world, grass and dirt, ground wear. |
| 8 | 10-24 | 4.52 | Step 7: rocks (perching, seed rolls off, lush ring). |
| 9 | 10-24 | 4.53-4.57 | Feeders 3x; homes, lifespans, losing interest, regulars; name tags; hologram bucket; shinier brass. |
| 10 | 10-24 | 4.58-4.59 | Wood pedestal with a brass ring; Step 8: hands; `Settings.cs`. |
| 11 | 10-24 | 4.61-4.67 | Rarer calls with a crowd hush; Step 8b (hands without arms); feeder card; lean in to the glass; tube perches; radio and props moved; rocker in the band. |
| 12 | 10-25 | 4.70 | Shared ring turn; Hologram View eye level; hands close the cube; ring hand by drag direction. (4.68-4.69 Hologram View built outside a session.) |
| 13 | 10-25 | 4.71-4.72 | Double-click to follow a bird; starfield window. |
| 14 | 10-25 | 4.73-4.75 | Taller band, logo press with a click, readable labels (built outside a session, logged after). De-tangling made weekly. |
| 15 | 10-25 | - | Feeding brainstorm: gunk goes, sprouts, slower pouring, per-species feathers replace coins. |
| 16 | 10-25 | 4.78-4.82 | Baseline speed; feeding numbers; gunk removed; blinks, squash, idle acts; hologram quality; moss. (4.76-4.77 `SimTuning` outside a session.) |
| 17 | 10-26 | 4.83 | Renamed to Birb Cube (exe, save and settings files, build script). Step 10a decisions. |
| 18 | 10-26 | 4.84-4.85 | Personality brainstorm; `FramePacing` for B1; `Traits` (12a). Logged afterward. |
| 19 | 10-26 | 4.86 | Layered seed heaps, falling stream and bounce, instanced grain drawing. |
| 20 | 10-27 | 4.87-4.91 | Sprouts (4.89); Step 15 added to the plan; Designer ID fix. Logged afterward; 4.87, 4.88, 4.90, 4.91 unrecorded. |
| 21 | 10-29 | - | De-tangling: `MousePicking`, `GameWindow`, `Pouring` out of GameEngine (now phases); `SeedDrawing`; `SeedHeap`. |
| 22 | 10-29 | 4.92 | B1 closed. Step 10a: `WorldClock`, day and night light, roosting, dawn chorus, Hours traits, HUD dial. |
| 23 | 10-29 | 4.93 | The pool: sipping and bathing, drawn in the floor shader. |
| 24 | 10-30 | 4.96 | Step 10b: rain, puddles, Rain-proud, rain sound. |
| 25 | 10-30 | 4.97 | The Hologram View becomes the observer mode (dive into the garden, columns slide away). |
| 26 | 10-30 | 4.98 | Step 15a: species as data (`Species.cs`; `BirdType` gone). |
| 27 | 10-30 | 4.99 | Step 15b: Designer species (`SpeciesFile.cs`, lure seeds, LED rows). |
| 28 | 10-30 | 5.00-5.01 | Store lamp at night; Hologram View sky and treeline (`HorizonSky`, `horizon.fs`). |
| 29 | 10-31 | 5.03 | Time keys (P, 1/2/3, -/+). |
| 30 | 10-31 | - | Reusable screenshot driver in `tools/`; the csproj leaves `tools\` out. |
| 31 | 10-31 | 5.04 | Dawn, dusk and night lighting: golden hour, blue hour, moonlit night. |
| 32 | 10-31 | 5.07 | B2 fixed (rain columns near the eye). (5.05-5.06 `SkyOrbs`, clouds: outside a session.) |
| 33 | 11-01 | 5.09 | The Hologram View shows the room view's patch, with a misty margin past the walls. (5.08 `StationFinder`: outside a session.) |
| 34 | 11-01 | 5.10 | Thirst and rest needs, naps, Night Owls sleep by day, the Dabbler. |
| 35 | 11-01 | - | Jello board built; status and order moved there. |
| 36 | 11-01 | - | Docs trimmed: history archived, file map shows the current state only, plan keeps open steps and patterns, one de-tangling watch list (`qa-plan.md` section 7). `SkyOrbs`, `skyorb.fs` and `StationFinder` added to the file map. |
| 37 | 11-01 | 5.11-5.13 | Preen and scratch idle acts; Selected bird readout in the Tab panel (replaces the F1 panel); sit spells (`SitChance`) and shorter act gaps so acts actually show. Tested Nov 3. |
| 38 | 11-01 | - | Planned only, no code: thunderstorms (soft cloud glow), rainbows (Hologram View, real conditions), streams, growing trees birds perch in, migration (seasonal species plus stopover flocks), species randomizer, DJ dance party (replaces Dance Club Mode). Cards on the board, design in `vision.md`, questions in the plan. |
| 39 | 11-03 | 5.14 | Step 13b-1: feathers replace coins (`Feather.cs`, `FeatherPurse.cs`): per-species drops, drift, counts, HUD collection grid, FEATHERS window; the store pays in feathers of any kind until 13b-2. Tested Nov 3. |
| 40 | 11-03 | 5.15 | Step 13b-2: one shelf per species (appears after its first feather), seeds priced in that species' feathers, paging, the shopkeeper's ledger (3 of one kind for 1 of another, `Ledger.cs`), lure crate (any feather until the rare species is collected), feeders paid in a matching species' feather and drawn bigger. Restock timers no longer saved. Tested Nov 3. |
| 41 | 11-03 | 5.16 | Four new traits, each a rare scale of its own: Watchman, Particular, Oath-keeper (never with Wayfarer), Open-handed. Bird card gets 4 more slider rows (taller). Written back to your project, not compiled: build and test first. Coin sounds stay (kept for a later use). Next flair pick: weather words and pool/stream names. |
| 42 | 11-03 | 5.17 | Ring pulls toward N/W/S/E (`Dial.cs`; not compiled, build and test first). Checked the repo for friends: they can't just click the build script (needs the .NET 10 SDK; adds shortcuts; stale README), so a self-contained release zip is queued. Feather visibility and hover, blurry chat labels and player rocks are carded with estimates; none started. |
| 43 | 11-03 | 5.18 | Forest ambience (`Ambience.cs`): a stereo bed loop and nine random one-shots made from your recording, Hologram View and daytime only, ducked by rain, radio and the menu. Compiled and run headless in the workspace (also compiles 5.16 and 5.17); not heard yet. Night bed carded as future work. |
| 44 | 11-05 | - | De-tangling, BirdMover only: the water half moved verbatim to `BirdMover.Water.cs` (partial class; 1041 lines became 713 + 338). No behavior change intended: every line accounted for, driver builds, tour ran, a probe saw all five water states plus thirst, flying and bath trips. Not built or tested by you. File map and qa-plan updated; Traits row wording fixed. Version not bumped (no feature). |
| 45 | 11-05 | 5.19 | Bird card shows only the 2-3 standout traits; hold Left Alt for all eleven. Every row has a tooltip: flavor line plus effect numbers generated from `TraitEffects` (`Traits.EffectLines`), or an "average" note. Decided: traits stay discrete (no per-scale numbers). Compiled and screenshot-checked in the workspace; not built or tested by you. De-tangling skipped. |
| 46 | 11-05 | - | Setup: CLAUDE.md written and slimmed (working rules, Wrap up), docs moved to `claude/` (`vision.md` is the design doc), `Coin.cs` dropped and `Make Release Zip.bat` added to the file map. Version stays 5.19. |
| 47 | 11-05 | 5.22 | Step 11, first pass: the Storyteller pacer (`Storyteller.cs`, `StoryEvent.cs`) with Hawk's Shadow, Murmuration and Windfall; bird records on the card; Tab panel section and fire buttons; log line plus a label in view, no sounds. Built with `dotnet build` (first session in Claude Code on Windows); shader untested. Next: test 5.22, then Rare visitor / Lost fledgling / Gilded Hour, or 12b (Squabble). |
| 48 | 11-05 | 5.23 | World Beautification A-C: `Terrain.cs` (seed-rolled height grid, saved as seed and height, FormatVersion 2, levelled around the pool), ground things follow it (`Bird.StandY`, seed slides downhill, puddles in hollows, mouse clicks ray-march), floor is a lit grid mesh. Tab slider for next-world height. Compiled only; floor mesh and shader untested (card v523). Next: test 5.23, then D (real pool dip), E (stream), F (reeds, stones). |
