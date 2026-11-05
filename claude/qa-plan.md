# Birb Cube QA Action Plan

The goal: every change is small, tested, and saved, so you can always step back to the last version that worked. What's waiting to be tested is on the Jello board ("Built, needs testing").

---

## 1. The safety net: git

- The repo is `dontdoubtdusty/birb-box` on GitHub, cloned to `D:\My Documents\rpret\GitHub\birb-box`. Work only in that folder. (The repo and folder keep the old name unless you rename them.)
- You commit and push with **GitHub Desktop**. Claude never commits.
- Git-ignored: `bin/`, `obj/`, the save and settings files (`birbcube-*.json`, old `birbbox-*.json`), the `Radio/` folder.

**Words you need:**
- **Commit** = a save point with a note. You can return to any commit.
- **Push** = upload your commits to GitHub (an off-computer backup).
- **Discard changes** = throw away everything since the last commit: the "undo the whole mess" button.
- **Tag** = a label on a commit, for version numbers (e.g. `v5.10-alpha`).

Later, once commits feel normal: **branches** let you try a risky idea off to the side and keep it only if it works.

**Habits worth keeping:** commit after each version bump, not after several. Log every version made outside a session in one line of `session-log.md` or a board card.

---

## 2. The change loop (every feature, every time)

1. **Start clean.** GitHub Desktop shows no uncommitted changes.
2. **One thing at a time.** "Birds hop when idle," not "rework birds."
3. **Build and run** after each small step, not at the end.
4. **Smoke test** (section 3). About 2 minutes.
5. **Commit** with a message saying what works now.
6. **Stuck rule:** if it's broken and you can't fix it in about 20 minutes, **discard changes** and try a smaller step.

---

## 3. Smoke test (about 2 minutes, after every change)

Run the game and check:

- [ ] Launches without errors in the console; the last save and settings come back
- [ ] Click the logo: it presses in with a click, then the box powers up (lens, beams, cube rises)
- [ ] Drag sideways on the box: the contents turn with the ring, a hand rests on the ring. Drag up/down: the camera tilts
- [ ] Mouse wheel zooms in and out, including leaning in to the glass and back out
- [ ] WASD pans the view window; panning stops at the world's edge
- [ ] Clicking the minimap jumps the window there
- [ ] Birds wander in and out through the fog. None pop in or out in plain view
- [ ] Pour seed (hold right mouse): it lands where the cursor is, including after panning; holding still grows one heap, dragging leaves a trail
- [ ] Birds notice food and eat it; the bucket's ATTRACTS list names the birds its seed calls
- [ ] The pool is there (and on the minimap); birds walk around it, and now and then one sips or bathes
- [ ] Tab panel, Weather, Rain: clouds gather and the light dims, rain falls, the HUD says Rain; Clear up brings the light back
- [ ] Clicking a bird selects it: its card shows the Food, Water and Rest bars and seven trait rows, all fitting; double-clicking a bird follows it; name tags are readable
- [ ] Clicking a feeder selects it; Move on its card lets you set it down somewhere else
- [ ] A fed bird drops a feather (or use the Tab panel's "+10 of each feather"); clicking a feather picks it up: the HUD grid and the pedestal's FEATHERS total go up
- [ ] Double-click empty box: the camera dives in to stand in the garden (columns slide away and peek back near an edge); double-click again: back to the room view as it was
- [ ] Press the rocker: the scene fogs over to the store and back
- [ ] Buying a seed spends feathers, and the HUD counts and the pedestal total update
- [ ] Radio: power on, tune, change volume; a hand reaches each control; clicking the dial opens the station finder
- [ ] F toggles fullscreen and back; F9 saves a screenshot (a line in the Bird Log)
- [ ] P, 1/2/3 and -/+ change the speed, and the right speed button lights
- [ ] Pause menu (Esc) opens and closes; Settings changes stick after a restart
- [ ] Birb Designer opens, edits, saves, and loads a birb (its preview stays clear and bright, even in rain); its Species dropdown lists every species
- [ ] Game closes cleanly

Anything that fails goes in the bug log (section 6) before you fix it.

---

## 4. Full test (before bumping the version number)

Everything in the smoke test, plus the edge cases where bugs like to hide:

- [ ] Pan to each world edge and corner at every zoom. The view never shows past the world
- [ ] Throw food at the very edge of the cube, into the fog, onto a rock (it rolls off), and into the pool (it sinks with a ripple)
- [ ] Click a UI panel that overlaps the hologram. Does the click leak through and throw food?
- [ ] Select a bird, then let it walk out of view. Does the selection break?
- [ ] Select a bathing bird: the ring and its name tag look right while it's low in the water
- [ ] Try to set a feeder down at the water's edge: "Too close to the water."
- [ ] Press the rocker or the logo during a start-up or scene change. Nothing doubles up
- [ ] Open the Designer (B) from the store: the box fogs back to the birds and the rocker follows. Open it from inside the Hologram View: the view closes first
- [ ] Inside the Hologram View: turn all the way round and look straight down (no wall or corner line), pan to the world's edge (grass fades out, nothing beyond), try night and rain, watch draw ms
- [ ] Lots of birds: does it slow down? (FPS, update ms and draw ms in the Tab panel)
- [ ] Lots of seed down: draw ms stays about the same (grains are drawn in two batches)
- [ ] Lots of feathers down (raise "Feather lasts"): draw ms stays reasonable; feathers never rest on a rock or in the pool, and fade away if left
- [ ] Steady rain at every zoom: streaks fade at the walls and ceiling, splash on the ground but not the water, no thick columns near the eye in the Hologram View, draw ms barely moves
- [ ] Rain at night: the streaks still read and the haze stays dark, not gray
- [ ] Puddles: with worn dirt about (or Soak the ground during rain), puddles form there, birds sip at them, and they shrink after the rain; none under a feeder, rock or seed heap
- [ ] In rain: most birds hunch and none start a bath; a Rain-proud bird stands tall; the rain sound follows the Birds volume and goes quiet in the store and with the power off
- [ ] Needs: in the afternoon birds nap and get up; a thirsty bird far from the pool flies over and drinks until full; Night Owls sleep by day; no bird leaves "parched" or "worn out" in normal play
- [ ] Every species: Blue Barley brings a Frostcrest, a tube feeder turns doves away, each species' call plays, its pedestal LED lights while one is in view, and its feathers have their own name and HUD dot
- [ ] Resize the window, minimize it, Alt-Tab away and back, toggle fullscreen (both types)
- [ ] Leave it running 10+ minutes. Still smooth? Do birds bunch up or vanish? Do sprouts come up, get pecked and die back? Does anyone get stuck at the pool's edge or a puddle?
- [ ] Quit and relaunch: birds (traits, names, looks, needs, naps), rocks, the pool, feeders, seed heaps, sprouts, feather counts, loose feathers, the view and the weather come back the same; a bird that was bathing is on the shore; the next new bird of a kind gets the next number
- [ ] Designer: save with a long name, an odd name, overwrite an existing birb
- [ ] Spend all your feathers and try to buy something (a refusal in the log, nothing goes negative)
- [ ] Release copy ("Build Birb Cube.bat"): starts from the Desktop shortcut, saves next to `BirbCube.exe`, the radio finds its music, the rain sound plays

Then: commit, add a **tag** with the new version number, push.

---

## 5. In-game debug panel (proposed, not built yet)

A hidden ImGui window, toggled with a key like F1, to set up situations directly instead of waiting for them:

- FPS, total bird count, birds in view
- Selected bird's current state and target (wandering, seeking food, at the water, napping)
- Buttons: spawn a bird here, remove all birds
- Toggle outlines for the world boundary and view window

Much of this already lives in the Tab tools panel (speed numbers, cheats incl. "+10 of each feather", the bird tracker, trait re-roll, Day and night, Water, Weather). This is a code change, so it goes through the usual questions first.

---

## 6. Bug log

For each bug: **Version**, **What I did**, **What happened**, **What I expected**, **Status** (open / fixed in ___). If you can't make it happen again on purpose, write it down anyway and mark it "random."

### Open bugs

None.

### Fixed

**B2. Rain streaks near the camera are tall, thick columns in the Hologram View** (since 4.97; fixed in 5.07, confirmed Oct 31). Drops live in box space at full size, so standing inside put some a few tenths of a unit from the eye. Fix: `RainDrawing.cs` shrinks drops closer than `NearSizeCap` and makes them vanish by `NearVanish`.

**B1. Starfield drift stutters** (4.82-4.83; fixed in 4.84, confirmed Oct 29). Fix: `FramePacing.cs`, vsync alone paces frames (a 60 FPS timer on top drifted against it). **If it comes back:** `Backdrop.SkyOffset`, the softening in `sky.fs`, the slow-frame count in the Tab panel.

---

## 7. Code-health check (the de-tangling pass, Thursday afternoons)

Once a week, Thursday afternoon after the token reset. Full passes so far: session 2 (4.42) and session 21 (Oct 29). Session 44 (Nov 5) did only the `BirdMover.cs` split. **Next: Thursday, Nov 12.** This section is the only watch list; keep it current.

**Warning signs:**
- A file you can't describe in one sentence
- A file that both updates logic and draws (the project rule is to keep these apart)
- The same chunk of code copy-pasted in two places
- Changing one thing keeps breaking something unrelated
- Over roughly 400 lines is worth a look, but length alone isn't a reason to split. A long file with one job is fine.

**The exercise:** one sentence per file saying what it's responsible for. Any file that needs "and" more than once is a candidate to split.

**Watch list (line counts Nov 1, at 5.10; `BirdMover` Nov 5):**
- `Bird.cs` (732): **first at the next pass.** Wants, eating, voice, night, rain, needs, personality and looks in one file. The night and NEEDS sections are the easiest to move out (a partial-class file, the way `BirdMover.Water.cs` was done).
- `BirdMover.cs` (713): split Nov 5 (the water half is `BirdMover.Water.cs`, 338). Walking, flocking, perching, roosting and arriving remain; perching and roosting could be their own file if trees make them grow.
- `BirbDesigner.cs` (699): the species editor is a job of its own; a partial-class file.
- `GameEngine.cs` (711): split once already. If it climbs toward 800, split again.
- `WebStations.cs` (510) and `RadioPlayer.cs` (544): grew with the station finder (5.08). Check whether the saved-stations part of `WebStations` is its own job.
- `ChatMessages.cs` (490): chat lines, name tags and sleep z's. Look at whether the z's belong with it.
- `Food.cs` (473), `SaveGame.cs` (445), `UIHandler.cs` (433), `HologramRenderer.cs` (420): one job each for now; watch them if critters add to them (feathers already did).

**Long but one job (left whole on purpose):** `Pedestal.cs` (622), `Hands.cs` (455), `Radio.cs` (453), `BirbAnimator.cs` (434), `Backdrop.cs` (432).
**Stand-ins (not worth splitting; to be replaced by in-world controls):** `Hud.cs` (562), `BucketPanel.cs` (493).

Always commit before a de-tangling pass. Restructuring should change zero behavior, so the smoke test should pass the same afterward.

**Proving a split changed nothing (done for `BirdMover`, Nov 5):** compare every line of the old file against the new files (only the class line, `using`, header comments and braces should differ), compile with the screenshot driver, run `tools/ShotDriver/scripts/tour.txt`, and for behavior code run a short script that raises the relevant chances and samples the private state.

---

## 8. Exploring the code

Suggested reading order, following how one frame runs:

1. `Program.cs`: the entry point, a few lines
2. `GameEngine.cs`: `Run` calls `Update`, then `Draw`; `UpdateSystems` lists the frame's phases in order, so read it like a table of contents
3. `MousePicking.cs`: how a click on the screen becomes a click on a bird, feeder or feather
4. `World.cs`, `Bird.cs`, `BirdMover.cs` (and `BirdMover.Water.cs`): what a bird is and how it decides where to go
5. `Species.cs` and `Birds.cs`: what a kind of bird is, and the four built-in ones
6. `WorldClock.cs` and `Weather.cs`: the time of day, the sun, when birds are about, and the rain
7. `ViewWindow.cs`, `HoloCamera.cs`: what you're looking at
8. `HologramRenderer.cs`, then `WorldDrawer.cs` and its other parts (`SeedDrawing.cs`, `SproutDrawing.cs`, `RainDrawing.cs`): how it all gets drawn
9. `Pool.cs` with the pool part of `hologram.fs`: one shape written twice, in C# for the birds and in GLSL for the picture
10. `RainDrawing.AddRain`: particles with no stored state, each drop's place worked out from a clock and its number

**Tools in your editor (Visual Studio / VS Code / Rider):**
- **Go to Definition** (F12): jump to where something is defined
- **Find All References** (Shift+F12): see everywhere something is used
- **Breakpoints** (F9): pause the running game on a line and inspect values. The best way to learn what the code does, and to hunt bugs.
