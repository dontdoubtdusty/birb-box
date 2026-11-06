# Birb Cube: Plan

**Status and order live on the Jello board** (`claude/board/birbcube/`, one JSON file per card; open it with `Jello Board.bat`; how Claude uses it: top of `session-log.md`).

**One home per fact.** `vision.md` holds the design (what the game is and does). This doc holds, for open steps only, the build sketch, the questions to ask and the checks, plus the code patterns and the backlog of things you can make. A board card is one line plus its subtasks, pointing here. An idea with no questions worth keeping lives only on its card. When a step is built, cut its section here down to what later steps need. Finished steps and old findings: `claude/archive/`.

**Session start:** `file-map.md`, `session-log.md`, then the board ("Built, needs testing" first, then the top of "Up next"). Thursday afternoons: the de-tangling pass first (`qa-plan.md` section 7).

---

## 1. Goal for this stretch

Turn the table into a physical, hands-on cabin toy, and the world inside into a small, lived-in garden. What's built is in `vision.md` ([Built] tags); what's next is the board.

---

## 2. Patterns to follow in the code

**Everything follows `World.HalfSize`:** random ground points, edge arrivals (`BirdSpawner.SetupEdgeArrival`, at HalfSize + 2), leaving flights, `World.IsInside`, the minimap scale and pan limit, `ViewWindow.JumpTo`, `GroundWear`.

**Anything that sits on the ground goes through `Rock.PushOut` and `Pool.PushOut`**: bird steps (via `BirdMover.OnGround`), seed placed on the ground, feathers, air-arrival targets, sprouts, loaded things. New ground things (critters, a bird bath, trees) should too. Seed landing in water sinks (`Food.Land`). Puddles are not obstacles; a new puddle just won't form under a feeder, rock, sprout or heap.

**The pool is one shape written twice:** `Pool.DistanceAt` in C# and `waterDistance` in `hologram.fs` (incl. the `0.9` blend). Change both. The stream joins this shape (see World Beautification), so the same rule covers it. Puddles are only roughly the same in both (C# uses plain circles; the shader frays the edge), so a sipping bird stands near, not exactly on, the drawn edge.

**The weather is read, not pushed:** anything that cares asks `Weather.Instance` (`Rain`, `IsRaining`, `GroundWet`, `SpoilFactor`, `Cloud`); birds check `Bird.MindsRain`. Storms, wind and snow should follow the same pattern: a number on `Weather`, a uniform in `RainDrawing.SetWeatherUniforms`, particles in the grain batches in box space.

**A storyteller event** is a `StoryEvent` subclass (`CanFire`, `Weight`, `Start`, `Update` returning false when done, `Drop` for save and load) in one `StoryCategory` (Big, Gift, Small), added to the list in `Storyteller`. `HawkShadow`, `Murmuration` and `Windfall` are the examples. Running events are not saved; they drop cleanly.

**New physical controls get a hand by asking `Hands`:** `Hands.Press`, `Hands.GripKnob` or `Hands.HoldRing` every frame the control is hovered or held, `Hands.Tap` on a quick click. No reaches while the ring turns (`HandsFree` in `FrameInput`).

**A pressable button:** copy the power button. State (`ButtonPush`, a 0-1 press curve) in the owner's Update; the drawing sinks the part by `ButtonPush * PushDepth`; your own click `.wav` with a fallback.

**A new control goes in its `GameEngine` phase:** a room control in `UpdateRoomControls`, a box control in `UpdateScene` or `UpdateBoxClicks`; mouse facts for later phases in `FrameInput`.

**Anything per species reads the `Species` entry** and works for any number of species (feathers, shelves, critter reactions, LEDs, seasons for migration).

**A new trait scale:** add the scale and the traits to the enums in `Traits.cs`, an entry in `info` and a place in `stops`, any numbers it needs in `TraitEffects`, and a `ScaleWeight` if it should be rare. Then read the number where the behavior is. Traits that can't be held together go in `Clashes`.

**Changed defaults don't reach an existing settings file:** a new `SimTuning` default shows only after right-clicking its slider (or deleting the `Tuning` block). New fields start at their defaults.

**The feeding numbers (current):** 1-seed piles every 0.5 s held still, one per 0.35 units dragged; natural drops 3.5 a minute (about 5 seed); rotted seed fades; ground seed spoils 2.5x faster in steady rain. A bird eats about 1 seed a meal, hungry about every 70 s. Feathers: a full meal drops one 22% of the time (33% for regulars and named birds, 1.6x again for Open-handed birds). Seed is priced in the paying species' own feathers (`Seeds.JarPrice`: a jar 2, a sack 4 jars, Blue Barley a jar 5). Aim: about 1 feather per 4-5 meals and a jar (10 seed) for 2 feathers, so a species roughly pays for its own food. Still to tune by play (`Seeds.JarPrice`, `SimTuning.FeatherDropChance`, `RegularFeatherBonus` in `Bird.cs`).

---

## 3. Open steps

Each step: questions, build, smoke test (`qa-plan.md` section 3) plus the step's checks, you commit, version +0.01. Order is on the board; design is in `vision.md`.

### Step 11: Storyteller, more events
Built (5.22): the pacer, Hawk's Shadow, Murmuration, Windfall, bird records on the card, Tab panel fire buttons.
**Still to come:** Rare visitor (a species that only comes under conditions such as dawn in winter; per-species conditions on `Species`, builds on the lure and seen set), Lost fledgling (needs a young-bird behavior; collected later by a regular of its kind, only while one is away), Gilded Hour (a light effect plus extra feathers, sized by the birds present), Squabble (with 12b). Later: the migration stopover (reuse `Murmuration`) and the DJ dance party. No critter events until Step 14 is decided.
**Questions to ask then:** the conditions for each rare visitor; what a fledgling looks like and does while alone; how golden the Gilded Hour gets inside the style rule, and how many extra feathers; which of these earn a line in a bird's record.
**Checks:** each new event fires from its Tab button, ends cleanly and drops cleanly on save and load; events still never clump; sizes follow what's in the garden, not feathers held.

### Step 12: Personality, the rest
Built scales (eleven): Nerve, Appetite, Roaming, Voice, Hours, Weather (Rain-proud), Bathing (Dabbler), Perching (Watchman), Taste (Particular), Loyalty (Oath-keeper), Giving (Open-handed). The last four are rare one-trait scales (`ScaleWeight` 0.2); no species lean any of them yet, so a species could be given leans in `Birds.cs` if one should feel more watchful or generous. Claude drafts trait names and effects, you edit them.

**Traits not built yet (draft, names in the house style):**

| Trait | Kind | What it nudges |
|---|---|---|
| Flock-kin / Loner | Pair | Flocking pull; settles its home near other birds or away. |
| Quarrelsome | One-off, 12b | Shoos others off a perch or pile; sulks on a rock when it loses. |

**12b: Social.** Quarrelsome birds shoo others off perches and piles; the loser sulks on a rock; Nerve decides who gives way (shared with the Squabble event). Flock-kin birds follow a neighbor of their kind for a while; Loners keep their distance.

### Medieval flair pass
Decided Nov 3: small passes, cheapest first, never all at once. Rules: `vision.md`, Flavor.
**Pass 1 (strings only):** the weather words in `Weather.Word` (Fair, Clouding over, Drizzle, Rain, Clearing; the storm word comes with Thunderstorms), and names for the pool and the stream.
**Pass 2:** shop wording (a house-style unit for a jar and a sack, a title for the ledger panel).
**Pass 3:** species heraldry, the one that changes how the game feels: a device per species on the minimap dots, pedestal LEDs, feather counts and shelf plaques (`MiniMap.cs`, `Pedestal.cs`, `Hud.cs`, `StoreRenderer.cs`, `SeedStore.cs`), a field on `Species` for Designer species. A session by itself.
**Pass 4:** canonical hours on the day dial, once the dial exists.
**Questions to ask then (pass 1):** the words themselves (Claude drafts, you edit); which words stay plain because they have to be read at a glance; where the pool and stream names show (nothing on screen names the pool today: a log line when a bird bathes, the minimap, the bird card's doing line).
**Checks:** the HUD word still fits its slot; the words still read in order as a spell builds and clears; nothing the player acts on got flavored; the names appear wherever the plain word did.

### Thunderstorms
**Build sketch:** a storm kind of rain spell on `Weather` (`Storm` 0-1, rarer, by season), a `Flash` value read by `HologramRenderer.HoloUniforms` (light and haze lift) and `HorizonSky` (cloud glow, distant bolts in `horizon.fs`); thunder with a delay after each glow, louder when "closer"; placeholder sounds from `tools/make_sounds.py` (added last) until real ones exist. Birds: storm reactions next to RAIN in `Bird.cs`, arrivals cut in `BirdSpawner.cs`; take cover under trees once trees exist.
**Questions to ask then:** how often and in which seasons; does a storm always start as rain and build, or roll in fast; does a close thunderclap scatter nervous birds (by Nerve, like the Hawk's Shadow); does the radio crackle on a strike; the storm word for the HUD.
**Checks:** the glow never reads as a flash at 1x and 4x; the Settings switch works and is saved; thunder lags the glow; storms survive save and load; draw ms unchanged.

### Rainbows
**Build sketch:** rain today has no side; it's one number for the whole sky. A rainbow needs one: `Weather` gets a rain front direction (where the rain is moving off to) during Clearing, so rain can still be "over there" while the sun shines here. `HorizonSky` asks for the antisolar point (`WorldClock.SunDirection` flipped) and draws the bow in `horizon.fs`: a band at about 42 degrees (secondary at about 51, reversed, fainter), strength from sun height, rain on that side and cloud gaps.
**Questions to ask then:** does the front direction also tilt the rain streaks; can a rainbow show in the room view at all (a faint arc in the fog, or nothing); does it nudge birds (a call, Rain-proud ones preen); a log line the first time one appears.
**Checks:** never shows with the sun above 42 degrees, at night, or without rain opposite; sits opposite the sun as the ring turns; fades smoothly; no change outside the Hologram View.

### World Beautification (uneven ground, pool dip, stream, water touches)
**The catch:** the code assumes flat ground at Y=0 everywhere (about 20 garden and bird files: movers, landings, `Food`, `Feather`, `Rock`, `Sprout`, `Feeder`, `Puddles`, `RainDrawing`, `HawkShadow`, `MousePicking.FloorPoint`, `ChatMessages`, `HoloCamera`'s inside eye, `BirdSpawner` landings). Step B is that sweep and is the bulk of the work.
**Steps:**
- **A. `Terrain`:** a height grid over the world (about 0.5-unit spacing), rolled per new world from a seed, saved. `HeightAt`, `NormalAt`. An all-zero terrain is the flat case, so old saves just get that. Bump `FormatVersion`; an old save shows a one-time offer of a New world.
- **B. Ground things follow it:** every Y=0 in the list above reads `Terrain.HeightAt`. Seed rolls downhill (reuse the rock roll-off in `Food`). Puddles form in hollows. Mouse floor clicks ray-march the height grid.
- **C. Floor mesh:** the flat floor quad becomes a grid mesh with normals, same `hologram.fs` (it still reaches past the cube for the Hologram View's margin). Check the fog still hides the cut at the cube's walls.
- **D. Pool dip:** terrain carved under the pool shape; a flat water level; banks slope in. Pool shape still written twice (`Pool.DistanceAt`, `waterDistance`).
- **E. Stream:** curve points from a spring inside the garden downhill to the pool, a width along them, carved into the terrain; distance blended into the pool shape in both places; flow direction for moving glints and for seed drifting before it sinks. Water trips (`BirdMover.Water.cs`) pick the nearest bank point, pool or stream. Hop or fly across by width. Trickle sound (placeholder from `make_sounds.py`, added last). Minimap.
- **F. Water touches:** reeds or rushes on the banks, lily pads on the pool, a few stepping stones birds stand on to drink. Instanced like grains where possible.
**Questions to ask then:** does rain raise the stream; do birds perch on reeds; does a feeder or sprout ever go next to the stream; how many stepping stones; the spring's look.
**Checks:** nothing floats above or sinks into the ground (birds, seed, feathers, rocks, feeders, sprouts, splashes, labels); clicks land where the cursor is on slopes; birds never walk through deep water; the C# and shader water edges match; seed in the stream drifts and sinks; an old save loads flat and offers New world; minimap shows the stream; draw ms (floor mesh) about the same.

### Trees
Perching and roosting are still in `BirdMover.cs` (about 713 lines); look at it before adding tree perches, and split if it has grown another job.
**Build sketch:** a `Tree` entity (growth 0-1, saved), placed per new world clear of the pool, stream, rocks and feeders, maybe sprouting in lush ground over time. Drawn from simple shapes through the hologram shader (trunk and a few canopy lumps, sway in wind later). Perch spots on branches like feeder perches (`TakePerch`); roosting picks a tree before the ground; storms send birds under the canopy. Trees go through the ground push-out like rocks. The Watchman trait (sits long and often on rocks) should like trees too.
**Questions to ask then:** how many per world and how long to full size; can they die or be removed; do new ones ever seed themselves; fruit or berries as food; do they fade like birds near the cube's walls (tall things show the fog's edge more); do they shade the ground or slow grass wear.
**Checks:** never taller than about half the cube; perches fill and free cleanly; roosting in trees at night; growth survives save and load; no tree on the pool, stream or a rock; draw ms with full-grown trees.

### Migration
**Build sketch:** a `Seasons` field on `Species` (built-in entries in `Birds.cs`, Designer species in their file and the Designer's species editor); `Species.CanAppear` checks the season; regulars of an out-of-season species stay away until it's back. The stopover: a `StoryEvent` close to `Murmuration` (reuse it), a flock of 8-15 of one species that flies in, eats, and leaves together within a few minutes.
**Questions to ask then:** which built-in species migrate and when; does a migrant's last bird say goodbye in the chat; are flocks a species of their own (only seen passing) or one you can also get as a resident; does a stopover show out of view (logged); does it count as "seen" for rare species.
**Checks:** out-of-season species never arrive; regulars come back when their season does; the flock arrives and leaves together; seasons survive save and load; the Designer saves and loads the seasons.

### Species randomizer
After the pieces above, so a random species can be seasonal and use trees and streams.
**Build sketch:** a generator that fills a `SpeciesFile` (name, color, size, common or rare with a lure, tastes, feeders, leans, voice borrowed at a random pitch, seasons) and a `BirbShape` from the part library, then saves both through `SpeciesFiles.Save` and `BirbFile`. Names from house-style word parts (a color or quality plus a bird word: Ember-, Dusk-, Gild-, Rime-, -wing, -crest, -finch, -dove).
**Questions to ask then:** where it's triggered (a Designer button, a new world, a rare stranger arriving in the garden); does a random species stay forever or visit once; the name word lists; how far looks can stray from the built-in shapes; can it produce rare species.
**Checks:** every random species loads, saves and shows in the LEDs and ATTRACTS; names never repeat an existing species; deleting its file removes it cleanly like any Designer species.

### DJ dance party
A `StoryEvent` (Big).
**Questions to ask then:** needs the radio on and follows its beat, or brings its own tune; who the DJs are (special one-off visitors, species of their own, or ordinary birds given the part) and what they look like (decks on a rock? on a tree branch?); how rare; which birds join (a trait?); the dance poses in `BirbAnimator.cs`; how it differs from the everyday radio dance; a house-style name for the event.
**Checks:** no strobing or hard pulses (the style rule and the lightning Settings switch both apply); everyone settles back cleanly after; the event drops cleanly on save and load.

### Weather: wind and snow
**Questions to ask then:** which first; does snow replace rain in winter; which traits react (a cold-hardy trait, or Frostcrests leaning to it); does wind move the rain streaks; snow on the pool, stream and puddles; whether a forecast ever shows in the room (a barometer was offered, not picked).
**Checks:** particles stay inside the cube and fade at the fog; seed spoil and drift change with the weather; weather survives save and load; draw ms with snow on.

### The day dial (first of the pedestal-front pair)
**Questions to ask then:** does the weather show on it (clouds on the band, a rain glyph); what happens to the feather total meanwhile (the HUD grid has the counts); canonical hours as marks along its edge (flair pass 4).
**Build sketch:** a scrolling texture behind the existing FEATHERS window glass plus a fixed pointer. The HUD clock stays until the dial reads well on its own.

### Smaller pedestal (after the day dial)
Shrink the wooden pedestal; the cube keeps its exact size and height.
**Touches:** the pedestal's shapes, the ring (the hand rests on it), the band with the rocker and logo, the FEATHERS window, the lens and beams (same cube corners from a smaller base), the room camera's framing, the Hologram View's grow-out animation, the hands' reach poses.
**Questions to ask then:** how much smaller, narrower only or shorter too; does the ring shrink or stay wide as a lip; do the rocker, logo and window shrink or does the band get crowded; does the cube sit higher.
**Checks:** cube size and position on screen unchanged; beams meet the cube's corners; ring, rocker, logo and radio still click and the hands land on each; the Hologram View still grows out cleanly; the default room view frames well.

### Step 14: Critters
After feathers, since critters go after seed and feeders. Small animals that come out of the fog, poke around, eat and leave, simulated like birds.
**Questions to ask:** which critters and their names; do they steal from feeders or only eat ground seed; do birds react (scatter, keep distance); clickable and nameable; in the Bird Log; Designer-built or simple shapes; do they wear grass, climb rocks or trees, join events, eat sprouts, drink at the pool, stream or puddles (and go through `Pool.PushOut`); how they take the rain; needs like the birds'. Likely a shared base with birds or a separate `Critter` with its own mover; decide then.

---

## 4. Speed

Baseline at 4.77: 60 FPS (vsync), 16.7 ms frame, 0.07 ms update. The cost is drawing, not simulation. Still open:
- Room shapes are separate draws per box; fine for now. The hands add a few dozen draws while on screen.
- Small: LINQ in `BirdSpawner.cs` and `BucketPanel.cs`. Not worth touching yet.
- The Hologram View draws birds, seed and rain in the misty margin too (up to 4x rain drops). Measure draw ms there and with lots of seed down.
- Each feather on the ground is 2 model draws; a garden full of feathers (long Feather lasts setting, lots of birds, Open-handed birds) is worth a measure.
- Snow should go in the grain batches like rain. Trees are new model draws; measure with all of them full-grown.

**When:** one focused pass before the walk-around cabin (open: WASD pans the window and would clash with walking).

## 5. Testing

- Smoke test after every step (`qa-plan.md` section 3), plus the step's checks (subtasks on its board card).
- Compare speed numbers in a Release build ("Build Birb Cube.bat").
- Compile checks: `dotnet build`. Headless screenshots (`tools/ShotDriver/README.md`) need a Linux workspace with xvfb, not available on this PC.

## 6. Keeping token use down

- One feature per conversation; start fresh rather than continuing a long thread.
- Start each session with the file map, the log and the board; open only the files the step needs.
- Answer known clarifying questions in your first message.
- Do number tuning yourself: changing a constant and rebuilding costs nothing.
- New looks for existing species cost no tokens: make them in the Designer and Release into the Cube.
- Keep the docs lean: one home per fact (top of this doc), the log gets 1-2 lines per session, the file map describes only the current code.

---

## Backlog: what you can contribute (no code needed)
- **Birb part models:** `PartLibrary.cs` loads every `.glb` in `resources/parts/` (create the folder). Blender rules: origin at the joint; grow up along +Z; about 2 units tall, width and depth -1 to 1; low poly; one white or light gray material; apply transforms; export glTF Binary, selected objects only, +Y Up. Name as `slot_style` (`beak_hooked`, `crest_fan`, `wing_round`...). Good first set: 3 beaks, 3 crests, 2 tails, 2 wings. Test: drop it in, build, press B, pick it in the Mesh dropdown.
- **Birb designs and species** in the Designer (in a Release copy they save to that copy's `resources/`, so copy them back). Give eye parts the Eye role so they blink.
- **Names and writing:** `Birds.cs` (incl. feather names), `Seeds.cs`, `ChatMessages.cs`, `Shopkeeper.cs`, `SeedStore.cs`, trait names and flavor lines in `Traits.cs`, the weather words in `Weather.Word` (plus a storm word); critter names (Step 14), event names (Step 11), comfort item names (Step 13c), a name for the pool and the stream, word parts for the species randomizer.
- **Tuning numbers:** most live in the tools panel; the rest are listed in the file map's "Where to find things".
- **Reference footage:** cardinals, doves and finches at a feeder; squirrels and chipmunks around one; birds drinking and bathing; birds hunched in the rain; birds in a passing flock.
- **Sounds:** a real rain loop to replace `rain.wav`; real feather sounds to replace `feather_land.wav` and `feather_pickup.wav`; thunder (far rolls and closer cracks); a stream trickle; wind ambience for later; a night forest recording (its own card); music for the radio (`Radio/`); real sips and splashes (`sip_1.wav`, `splash_1.wav`... any count); Designer species calls (`call_<key>_N.wav`). The unused coin sounds are kept for a later use.
- **Hand, rock, tree and radio models** in Blender (hands would need loading code).
