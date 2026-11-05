# Birb Cube: Plan

**Status and order live on the Jello board** (https://claude.ai/artifact/1HH1kCcnvGa5QSPGNb9S5C; Claude: `ArtifactData` list of `games/birbcube/cards`). This doc keeps the design for work not yet built: code patterns to follow, each open step's questions and checks, and the backlog. Finished steps and old findings: `claude/archive/history-to-session-35.md`.

**Session start:** `file-map.md`, `session-log.md`, then the board ("Built, needs testing" first, then the top of "Up next"). Thursday afternoons: the de-tangling pass first (`qa-plan.md` section 7).
**As work lands:** move and tick cards. Change this doc only when a design, question or check changes. A new idea gets a card in "Ideas and unplaced", plus a section here if it has questions worth keeping. When a step is built, cut its section here down to anything later steps need.

---

## 1. Goal for this stretch

Turn the table into a physical, hands-on cabin toy, and the world inside into a small, lived-in garden. Built so far: the radio, the wood-and-brass pedestal, projector and cube, the garden (ground, rocks, pool, sprouts), hands, the rocker, Designer species, personality (eleven trait scales), day and night, rain, needs, and the feather economy (feathers drop, a shelf per species, the ledger). Still to come:

- **Simulation staples:** a storyteller that paces gentle events (Step 11), social behavior (12b), wind and snow. Cozy rule: nothing punishes being away, paying attention gets you more.
- **Weather and garden:** thunderstorms, realistic rainbows, a stream, trees that grow, migration, then a species randomizer. The DJ dance party as a storyteller event.
- **Medieval flair,** a few small passes (weather words and pool and stream names first), species heraldry later.
- **Comfort items and a bird bath** (13c).
- **Critters** that come and go alongside the birds (Step 14).
- **The pedestal:** the day dial and a smaller pedestal.

---

## 2. Patterns to follow in the code

**Everything follows `World.HalfSize`:** random ground points, edge arrivals (`BirdSpawner.SetupEdgeArrival`, at HalfSize + 2), leaving flights, `World.IsInside`, the minimap scale and pan limit, `ViewWindow.JumpTo`, `GroundWear`.

**Anything that sits on the ground goes through `Rock.PushOut` and `Pool.PushOut`**: bird steps (via `BirdMover.OnGround`), seed placed on the ground, feathers, air-arrival targets, sprouts, loaded things. New ground things (critters, a bird bath, trees) should too. Seed landing in water sinks (`Food.Land`). Puddles are not obstacles; a new puddle just won't form under a feeder, rock, sprout or heap.

**The pool is one shape written twice:** `Pool.DistanceAt` in C# and `waterDistance` in `hologram.fs` (incl. the `0.9` blend). Change both. The stream joins this shape (see Streams), so the same rule covers it. Puddles are only roughly the same in both (C# uses plain circles; the shader frays the edge), so a sipping bird stands near, not exactly on, the drawn edge.

**The weather is read, not pushed:** anything that cares asks `Weather.Instance` (`Rain`, `IsRaining`, `GroundWet`, `SpoilFactor`, `Cloud`); birds check `Bird.MindsRain`. Storms, wind and snow should follow the same pattern: a number on `Weather`, a uniform in `RainDrawing.SetWeatherUniforms`, particles in the grain batches in box space.

**New physical controls get a hand by asking `Hands`:** `Hands.Press`, `Hands.GripKnob` or `Hands.HoldRing` every frame the control is hovered or held, `Hands.Tap` on a quick click. No reaches while the ring turns (`HandsFree` in `FrameInput`).

**A pressable button:** copy the power button. State (`ButtonPush`, a 0-1 press curve) in the owner's Update; the drawing sinks the part by `ButtonPush * PushDepth`; your own click `.wav` with a fallback.

**A new control goes in its `GameEngine` phase:** a room control in `UpdateRoomControls`, a box control in `UpdateScene` or `UpdateBoxClicks`; mouse facts for later phases in `FrameInput`.

**Anything per species reads the `Species` entry** and works for any number of species (feathers, shelves, critter reactions, LEDs, seasons for migration).

**A new trait scale:** add the scale and the traits to the enums in `Traits.cs`, an entry in `info` and a place in `stops`, any numbers it needs in `TraitEffects`, and a `ScaleWeight` if it should be rare. Then read the number where the behavior is. Each scale adds a row to the bird card, so raise `CardHeight` in `Hud.cs` by `SliderRowHeight`. Traits that can't be held together go in `Clashes`.

**Changed defaults don't reach an existing settings file:** a new `SimTuning` default shows only after right-clicking its slider (or deleting the `Tuning` block). New fields start at their defaults.

**The feeding numbers (current):** 1-seed piles every 0.5 s held still, one per 0.35 units dragged; natural drops 3.5 a minute (about 5 seed); rotted seed fades; ground seed spoils 2.5x faster in steady rain. A bird eats about 1 seed a meal, hungry about every 70 s. Feathers: a full meal drops one 22% of the time (33% for regulars and named birds, 1.6x again for Open-handed birds). Seed is priced in the paying species' own feathers (`Seeds.JarPrice`: a jar 2, a sack 4 jars, Blue Barley a jar 5), a first guess to tune by play.

---

## 3. Open steps

Each step: questions, build, smoke test (`qa-plan.md` section 3) plus the step's checks, you commit, version +0.01. Order is on the board.

### Medieval flair pass
Decided Nov 3: do it in small passes, cheapest first, never all at once. The rule is in `vision.md` (Flavor): flavor goes in nouns, headers and pictures, never in instructions or numbers; no "Ye", no thee/thou, no puns in item names; no flavor work on ImGui panels.
**Pass 1 (strings only):** the weather words in `Weather.Word` (Fair, Clouding over, Drizzle, Rain, Clearing; the storm word comes with Thunderstorms), and names for the pool and the stream. Nothing on screen names the pool today, so decide where a name shows (a log line when a bird bathes, the minimap, the bird card's doing line).
**Pass 2:** shop wording (a house-style unit for a jar and a sack, a title for the ledger panel).
**Pass 3:** species heraldry, the one that changes how the game feels: a device per species on the minimap dots, pedestal LEDs, feather counts and shelf plaques (`MiniMap.cs`, `Pedestal.cs`, `Hud.cs`, `StoreRenderer.cs`, `SeedStore.cs`), a field on `Species` for Designer species. A session by itself.
**Pass 4:** canonical hours on the day dial, once the dial exists.
**Questions to ask then (pass 1):** the words themselves (Claude drafts, you edit); which words stay plain because they have to be read at a glance; where the pool and stream names show.
**Checks:** the HUD word still fits its slot; the words still read in order as a spell builds and clears; nothing the player acts on got flavored; the names appear wherever the plain word did.

### Thunderstorms
Design in `vision.md` (Time, weather and events). Decided Nov 1: lightning is a soft glow swelling inside the clouds and fog, no bolts in the cube; faint far-off bolts on the Hologram View's treeline only; a Settings switch softens or turns off the glow (photosensitivity, and the style rule).
**Build sketch:** a storm kind of rain spell on `Weather` (`Storm` 0-1, rarer, by season), a `Flash` value read by `HologramRenderer.HoloUniforms` (light and haze lift) and `HorizonSky` (cloud glow, distant bolts in `horizon.fs`); thunder with a delay after each glow, louder when "closer"; placeholder sounds from `tools/make_sounds.py` (added last) until real ones exist. Birds: storm reactions next to RAIN in `Bird.cs`, arrivals cut in `BirdSpawner.cs`; take cover under trees once trees exist.
**Questions to ask then:** how often and in which seasons; does a storm always start as rain and build, or roll in fast; does the Hawk's-Shadow-like scatter happen on a close thunderclap (nervous birds by Nerve); does the radio crackle on a strike; the storm word for the HUD.
**Checks:** the glow never reads as a flash at 1x and 4x; the Settings switch works and is saved; thunder lags the glow; storms survive save and load; draw ms unchanged.

### Rainbows
Design in `vision.md`. Decided Nov 1: Hologram View sky only; real conditions (sun up, below 42 degrees, rain on the side opposite the sun).
**Build sketch:** rain today has no side; it's one number for the whole sky. A rainbow needs one: `Weather` gets a rain front direction (where the rain is moving off to) during Clearing, so rain can still be "over there" while the sun shines here. `HorizonSky` asks for the antisolar point (`WorldClock.SunDirection` flipped) and draws the bow in `horizon.fs`: a band at about 42 degrees (secondary at about 51, reversed, fainter), strength from sun height, rain on that side and cloud gaps.
**Questions to ask then:** does the front direction also tilt the rain streaks; can a rainbow show in the room view at all (a faint arc in the fog, or nothing); does it nudge birds (a call, Rain-proud ones preen); a log line the first time one appears.
**Checks:** never shows with the sun above 42 degrees, at night, or without rain opposite; sits opposite the sun as the ring turns; fades smoothly; no change outside the Hologram View.

### Streams
Design in `vision.md` (The garden). Decided Nov 1: one per world, in from the fog, winding to feed the pool; shallow edges for sipping and bathing; birds hop across narrow parts and fly over wide ones.
**Do first:** split `BirdMover.cs`'s WATER section into its own partial file (on the de-tangling card), so stream trips land in a smaller file.
**Build sketch:** the stream as a few curve points with a width along them, placed with the pool in `Pool.PlaceNew` (or a `Stream` beside it), its distance blended into `Pool.DistanceAt` and `waterDistance` in `hologram.fs` the same way (both, matching). Flow direction along the curve for the shader (moving glints) and for seed carried before it sinks. Water trips pick the nearest bank point, pool or stream. Saved; older saves get one placed clear of rocks, feeders and sprouts.
**Questions to ask then:** does it end in the pool or pass by it; a stepping stone or two; does rain raise it; does a feeder or sprout ever go next to it; its sound (a soft trickle when near).
**Checks:** birds never walk through the deep middle; the C# and shader edges match; seed in it drifts and sinks; old saves load with a stream in a clear spot; minimap shows it; draw ms unchanged (it's in the floor shader).

### Trees
Design in `vision.md`. Decided Nov 1: saplings grow over several in-game days to about half the cube's height; birds perch, roost and shelter in them.
**Do first:** the BirdMover split above (perching and roosting live there too).
**Build sketch:** a `Tree` entity (growth 0-1, saved), placed per new world clear of the pool, stream, rocks and feeders, maybe sprouting in lush ground over time. Drawn from simple shapes through the hologram shader (trunk and a few canopy lumps, sway in wind later). Perch spots on branches like feeder perches (`TakePerch`); roosting picks a tree before the ground; storms send birds under the canopy. Trees go through the ground push-out like rocks. The Watchman trait (sits long and often on rocks) should like trees too.
**Questions to ask then:** how many per world and how long to full size; can they die or be removed; do new ones ever seed themselves; fruit or berries as food; do they fade like birds near the cube's walls (tall things show the fog's edge more); do they shade the ground or slow grass wear.
**Checks:** never taller than about half the cube; perches fill and free cleanly; roosting in trees at night; growth survives save and load; no tree on the pool, stream or a rock; draw ms with full-grown trees.

### Migration
Design in `vision.md` (Species). Decided Nov 1: seasonal species plus a passing flock that stops over at a season's turn.
**After Step 11:** the stopover flock is a storyteller event, close to the Murmuration; build them together or reuse one for the other.
**Build sketch:** a `Seasons` field on `Species` (built-in entries in `Birds.cs`, Designer species in their file and the Designer's species editor); `Species.CanAppear` checks the season; regulars of an out-of-season species stay away until it's back. The stopover: a flock of 8-15 of one species flies in, eats, and leaves together within a few minutes.
**Questions to ask then:** which built-in species migrate and when; does a migrant's last bird say goodbye in the chat; are flocks a species of their own (only seen passing) or one you can also get as a resident; does a stopover show out of view (logged); does it count as "seen" for rare species.
**Checks:** out-of-season species never arrive; regulars come back when their season does; the flock arrives and leaves together; seasons survive save and load; the Designer saves and loads the seasons.

### Species randomizer
Design in `vision.md`. After the pieces above, so a random species can be seasonal and use trees and streams.
**Build sketch:** a generator that fills a `SpeciesFile` (name, color, size, common or rare with a lure, tastes, feeders, leans, voice borrowed at a random pitch, seasons) and a `BirbShape` from the part library, then saves both through `SpeciesFiles.Save` and `BirbFile`. Names from house-style word parts (a color or quality plus a bird word: Ember-, Dusk-, Gild-, Rime-, -wing, -crest, -finch, -dove).
**Questions to ask then:** where it's triggered (a Designer button, a new world, a rare stranger arriving in the garden); does a random species stay forever or visit once; the name word lists; how far looks can stray from the built-in shapes; can it produce rare species.
**Checks:** every random species loads, saves and shows in the LEDs and ATTRACTS; names never repeat an existing species; deleting its file removes it cleanly like any Designer species.

### DJ dance party
Design in `vision.md` (replaces Dance Club Mode). A rare storyteller event: DJ Songbird and DJ Blue-J turn up and the garden dances for about a song. After Step 11 (the storyteller fires it).
**Questions to ask then:** needs the radio on and follows its beat, or brings its own tune; who the DJs are (special one-off visitors, species of their own, or ordinary birds given the part) and what they look like (decks on a rock? on a tree branch?); how rare; which birds join (a trait?); the dance poses in `BirbAnimator.cs`; how it differs from the everyday radio dance; a house-style name for the event.
**Checks:** no strobing or hard pulses (the style rule and the lightning Settings switch both apply); everyone settles back cleanly after; the event survives save and load or drops cleanly.

### Weather: wind and snow
**Questions to ask then:** which first; does snow replace rain in winter; which traits react (a cold-hardy trait, or Frostcrests leaning to it); does wind move the rain streaks; snow on the pool, stream and puddles; whether a forecast ever shows in the room (a barometer was offered, not picked).
**Checks:** particles stay inside the cube and fade at the fog; seed spoil and drift change with the weather; weather survives save and load; draw ms with snow on.

### The day dial
The FEATHERS window becomes a drum dial: a painted band (night blue, dawn rose, day gold, dusk amber, sun and moon on it) scrolling behind the glass past a fixed pointer. Full description in `vision.md`. The HUD clock stays until the dial reads well on its own.
**Questions to ask then:** does the weather show on it (clouds on the band, a rain glyph); what happens to the feather total meanwhile (the HUD grid has the counts); before or after the smaller pedestal (both change the same band); canonical hours as marks along its edge (flair pass 4).

### Smaller pedestal
Shrink the wooden pedestal; the cube keeps its exact size and height.
**Touches:** the pedestal's shapes, the ring (the hand rests on it), the band with the rocker and logo, the FEATHERS window, the lens and beams (same cube corners from a smaller base), the room camera's framing, the Hologram View's grow-out animation, the hands' reach poses.
**Questions to ask then:** how much smaller, narrower only or shorter too; does the ring shrink or stay wide as a lip; do the rocker, logo and window shrink or does the band get crowded; does the cube sit higher; before or after the day dial.
**Checks:** cube size and position on screen unchanged; beams meet the cube's corners; ring, rocker, logo and radio still click and the hands land on each; the Hologram View still grows out cleanly; the default room view frames well.

### Step 11: Storyteller and events
- **Storyteller:** one pacer (`Storyteller`) that picks the next event from a weighted list, with a cooldown for quiet stretches, weighted by time of day, weather and how busy the garden is.
- **Events** (pick 3-4 to start; Gilded Hour needs feathers, built, and Squabble needs 12b): The Hawk's Shadow (birds freeze or scatter into the fog and creep back; Nerve decides), Murmuration (a big flock strips the seed and is gone in a minute), Rare visitor (a species that only comes under conditions such as dawn in winter; builds on the lure and seen set), Windfall (a gust drops seed or berries), Squabble (two birds fight over a feeder, the loser sulks on a rock), Lost fledgling (a young bird alone, collected later by a regular of its kind), Gilded Hour (rare golden light, extra feathers). Later: the migration stopover and the DJ dance party.
- No critter events until Step 14 is decided.
- **Questions to ask:** which events first; quiet stretch lengths; how an event is announced (log line, chat label, sound); do events happen out of view (likely yes, logged); a dev button to fire each; does the Hawk's Shadow send bathers out of the water and wake nappers; is the Murmuration the same thing as the migration stopover.
- **Checks:** events never clump; each ends cleanly and birds go back to normal; events survive save and load (or are dropped cleanly); the dev buttons fire each one.

### Step 12: Personality, the rest
Three layers in the long run: **personality** (fixed, built), **mood** (later), **memory** (Backlog H). Built scales (eleven): Nerve, Appetite, Roaming, Voice, Hours, Weather (Rain-proud), Bathing (Dabbler), Perching (Watchman), Taste (Particular), Loyalty (Oath-keeper), Giving (Open-handed). The last four are rare one-trait scales (`ScaleWeight` 0.2); no species lean any of them yet, so a species could be given leans in `Birds.cs` if one should feel more watchful or generous. Claude drafts trait names and effects, you edit them.

**Traits not built yet (draft, names in the house style):**

| Trait | Kind | What it nudges |
|---|---|---|
| Flock-kin / Loner | Pair | Flocking pull; settles its home near other birds or away. |
| Quarrelsome | One-off, 12b | Shoos others off a perch or pile; sulks on a rock when it loses. |

Later for built traits: Nerve decides freezing or fleeing in the Hawk's Shadow (Step 11).

**12b: Social (after Step 11).** Quarrelsome birds shoo others off perches and piles; the loser sulks on a rock; Nerve decides who gives way (shared with the Squabble event). Flock-kin birds follow a neighbor of their kind for a while; Loners keep their distance.

**12c: Mood (undecided).** Mood made from needs, weather and crowding, nudging behavior (sings more, leaves sooner), shown on the card.

### Step 13b-2: The feather store (built, 5.15)
What's left: price tuning by play. Income is about 0.22 drops per meal; aim for a jar (10 seed) at 2 feathers and about 1 feather per 4-5 meals (`Seeds.JarPrice`, `SimTuning.FeatherDropChance`, `RegularFeatherBonus` in `Bird.cs`). The FEATHERS window keeps the total until the day dial replaces it. The Muck Scraper's old spot by the corn barrel is free.

### Step 13c: Later (undecided)
- Comfort items on each species' shelf (dust bath, thistle sock, sunning stone) that change how long birds stay and how often they return.
- **Bird bath:** a store item placed like a feeder (`FeederPlacer` is the pattern), sipped and bathed at like the pool, with perches on the rim; would take thirsty traffic off the pool. Questions: shelf and price, how many, does it need filling.
- Mulch.
- More weather tie-ins: rain helps sprouts, wind carries seed, snow buries ground seed.

### Step 14: Critters
After feathers, since critters go after seed and feeders. Small animals that come out of the fog, poke around, eat and leave, simulated like birds.
**Questions to ask:** which critters and their names; do they steal from feeders or only eat ground seed; do birds react (scatter, keep distance); clickable and nameable; in the Bird Log; Designer-built or simple shapes; do they wear grass, climb rocks or trees, join events, eat sprouts, drink at the pool, stream or puddles (and go through `Pool.PushOut`); how they take the rain; needs like the birds'. Likely a shared base with birds or a separate `Critter` with its own mover; decide then.

### Later: walk around the cabin
A speed pass first (section 4). Open: WASD pans the window and would clash with walking.

---

## 4. Speed

Baseline at 4.77: 60 FPS (vsync), 16.7 ms frame, 0.07 ms update. The cost is drawing, not simulation. Still open:
- Room shapes are separate draws per box; fine for now. The hands add a few dozen draws while on screen.
- Small: LINQ in `BirdSpawner.cs` and `BucketPanel.cs`. Not worth touching yet.
- The Hologram View draws birds, seed and rain in the misty margin too (up to 4x rain drops). Measure draw ms there and with lots of seed down.
- Each feather on the ground is 2 model draws; a garden full of feathers (long Feather lasts setting, lots of birds, Open-handed birds) is worth a measure.
- Snow should go in the grain batches like rain. Trees are new model draws; measure with all of them full-grown.

**When:** one focused pass before the walk-around cabin.

## 5. Testing

- Smoke test after every step (`qa-plan.md` section 3), plus the step's checks (subtasks on its board card).
- Compare speed numbers in a Release build ("Build Birb Cube.bat").
- Worth building soon: the F1 debug panel (`qa-plan.md` section 5).
- Compile checks and screenshots in Claude's workspace: `tools/ShotDriver/README.md`.

## 6. Keeping token use down

- One feature per conversation. Start fresh rather than continuing a long thread.
- Start each session with the file map, the log and the board; open only the files the step needs.
- Ask for status from the board ("what's next?"), not by having the docs re-read.
- Answer known clarifying questions in your first message.
- Do number tuning yourself: changing a constant and rebuilding costs nothing.
- Use a smaller model for small mechanical edits, if your plan lets you switch.
- De-tangling on Thursday afternoons, right after the token reset.
- New looks for existing species cost no tokens: make them in the Designer and Release into the Cube.
- Log anything built outside a session in one line of `session-log.md` or a board card.
- Keep the docs lean: the log gets 1-2 lines per session, the file map describes only the current code, and each fact lives in one doc. A doc can only be rewritten whole, so a small edit to a long doc costs a full copy of it: batch doc updates to the end of a session.

---

## Backlog

### B. What you can contribute (no code needed)
- **Birb part models:** `PartLibrary.cs` loads every `.glb` in `resources/parts/` (create the folder). Blender rules: origin at the joint; grow up along +Z; about 2 units tall, width and depth -1 to 1; low poly; one white or light gray material; apply transforms; export glTF Binary, selected objects only, +Y Up. Name as `slot_style` (`beak_hooked`, `crest_fan`, `wing_round`...). Good first set: 3 beaks, 3 crests, 2 tails, 2 wings. Test: drop it in, build, press B, pick it in the Mesh dropdown.
- **Birb designs and species** in the Designer (in a Release copy they save to that copy's `resources/`, so copy them back). Give eye parts the Eye role so they blink.
- **Names and writing:** `Birds.cs` (incl. feather names), `Seeds.cs`, `ChatMessages.cs`, `Shopkeeper.cs`, `SeedStore.cs`, trait names and flavor lines in `Traits.cs`, the weather words in `Weather.Word` (plus a storm word); critter names (Step 14), event names (Step 11), comfort item names (Step 13c), a name for the pool and the stream, word parts for the species randomizer.
- **Tuning numbers:** most live in the tools panel; the rest are listed in the file map's "Where to find things".
- **Reference footage:** cardinals, doves and finches at a feeder; squirrels and chipmunks around one; birds drinking and bathing; birds hunched in the rain; birds in a passing flock.
- **Sounds:** a real rain loop to replace `rain.wav`; real feather sounds to replace `feather_land.wav` and `feather_pickup.wav`; thunder (far rolls and closer cracks); a stream trickle; wind ambience for later; music for the radio (`Radio/`); real sips and splashes (`sip_1.wav`, `splash_1.wav`... any count); Designer species calls (`call_<key>_N.wav`). The unused coin sounds are kept for a later use.
- **Hand, rock, tree and radio models** in Blender (hands would need loading code).

### C. Birb Builder: character-creator style
Gap: part offsets are measured from the birb's middle, not the parent (no sockets).
Target: slots (Body, Head, Beak, Crest, Eyes, Wings, Tail, Legs, Feet) with mesh pickers from `slot_style` names; sockets; per-slot sliders and color presets; automatic symmetry; randomize; the raw editor kept as Advanced.
Order: sockets; slot UI; sliders and colors; randomize. (The species randomizer would reuse the randomize step; doing sockets first makes random bodies hold together.)
Decisions: side window vs. full-screen workshop; can custom birbs be called anytime?

### D. More bird animation (all in `BirbAnimator.cs`, low risk)
Done: squash and stretch, blink, fluff, wing stretch, sip, bathe, shake off, the rain hunch, naps, preen, scratch. Still to do: 2. anticipation and overshoot; 3. follow-through springs on crest, tail, wingtips; 5. look-at targets (food, birds, cursor); 6. timing variety (holds and snaps); 7. dust puffs, bouncing grains, wing gusts; 8. an optional 12 fps "toy" mode.

### G. More garden (not decided)
Raked gravel curving around the rocks, moss patches on the ground, a stump or log, stepping stones, a shrub border. Around the pool and stream: reeds or rushes, lily pads, a stepping stone into the shallows.

### H. After the staples (not decided)
- **Bonds and families:** birds that eat near each other become pals and travel together; rivals chase each other off feeders; a bonded pair nests (in a tree, once trees exist), chicks follow a parent and inherit looks and traits (each trait with a chance per parent). The biggest attachment payoff and the biggest build.
- **Memory:** a favorite feeder, a rival, the player's attention (a bird you follow or name grows used to you).
- **The chronicle:** a journal in the cabin recording notable moments in the house voice: firsts, rare visitors, last visits, pairs, hatchings, the first rainbow. Doubles as a life list.
- **Small ecology loops:** garden pieces bought with feathers (bird bath, log, berry bush) that change which birds come.

### I. Feeding ideas not picked (Oct 25)
Click-to-toss handfuls; named blends that call a particular visitor; a feather chain unlocking shelves; keeping feathers as a collection in the cabin; trust (a well-fed regular perches nearer, lands on the ring or a hand); a chorus that swells when many birds eat together.
