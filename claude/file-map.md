# Birb Cube File Map

One entry per file: what it does today. Read this first each session, then open only the files the task needs. Update it when a file is added, split, renamed or deleted, and when a file's job changes. Describe the current state only; history lives in git, the session log and `claude/archive/`.

Line counts are rough (refreshed Nov 1 for the larger files; store files Nov 3; `BirdMover.cs` and `BirdMover.Water.cs`, `Traits.cs`, `Hud.cs` and the Storyteller files Nov 5). Files over ~400 lines are marked *. Length only flags a file to look at; a long file with one job is fine (see `qa-plan.md` section 7).

---

## Start and main loop

| File | Lines | What it does |
|---|---|---|
| `Program.cs` | 40 | Entry point. Creates GameEngine, runs it, catches crashes. |
| `GameEngine.cs` * | 711 | Main loop and input routing; owns every system (incl. `Clock`, `Weather`, `Story` (the Storyteller) and `Feathers`, the purse; `Tag` and `Ledger`, the store's two panels). `UpdateSystems` is a list of phases in order: `UpdateBackground` (sounds, radio, projector, autosave), `ReadInput` (who has the mouse and keyboard; F, F9; columns slide in the Hologram View), the Esc menu, `UpdateRoomControls` (radio, power button), the Designer (`UpdateDesigning`), Space and Tab, time keys (`UpdateTimeKeys`: P, 1/2/3, -/+ along `SpeedSteps`; `SetSpeed`, `StepSpeed`), `UpdateScene` (rocker, scene switch, store hover and its panels), `UpdateCameraAndRing` (wheel, double-clicks, Hologram View, ring turn and which hand holds it, panning), `UpdateBoxClicks` (logo, rocker, store via `ClickInStore`, birds' floor, pouring), `UpdateSimulation` (clock, weather, world, `CheckRareSeen`, arrivals, `Story.Update`), `UpdatePanels` (hands the Storyteller's labels to `ChatMessages`). Mouse facts ride along in `FrameInput`. Then the hands, then Draw. Selection (`SelectedBird`, `SelectedFeeder`, `MoveSelectedFeeder`, `FollowBirdOnDoubleClick`). Holds the version, UpdateMs, DrawMs and `Pacing`. Loads species and designs (`Birds.LoadAll`), the save and settings; saves both on quit. The screenshot driver copies `Run`'s start-up and calls the private `Update` and `Draw`. |
| `GameWindow.cs` | 106 | The window (`engine.Window`): title and icon, fullscreen (borderless or true) and back, F9 screenshots. |
| `MousePicking.cs` | 134 | What the mouse points at (`engine.Mouse`): rays in the room, the hologram and the un-turned box; `FloorPoint`; the nearest feather, feeder or bird. Only things inside the cube take clicks. Asks only, changes nothing. |
| `Pouring.cs` | 46 | Holding right mouse pours the bucket (`engine.Pour`): a pile every `PileSpacing` of travel or `PileInterval` held still. |
| `FramePacing.cs` | 74 | Vsync sets the pace; a timer cap at the refresh rate takes over if vsync is off. Counts slow frames for the tools panel. |
| `WorldClock.cs` | 319 | The bird world's calendar and time of day (`engine.Clock`, `WorldClock.Instance`). Days move only with sim time (`SimTuning.DayMinutes`), saved; seasons of 5 days; the sun worked out like the real one at 40.7 N (`Sunrise`, `Sunset`, `FirstGlow`, `LastGlow`, `SunElevation`, `SunDirection`); `Phase`. `Light` (a `DayLight`: ambient, direct, direction, darkness, `Shade` for faces turned away, `Golden`) blended from the LIGHT KEYS; `MoonDirection`. When birds are about: `IsBedtime`, `IsOwlBedtime`, `ArrivalShare`, `IsDawnChorus`, `IsQuietEvening`. Text: `TimeText`, `DateText`, `Stamp`. Dev: `SetHour`, `NextDay`, `NextSeason`, `Held`. State only. |
| `Weather.cs` | 222 | The weather, rain only so far (`engine.Weather`, `Weather.Instance`). Dry and rain spells in world hours (`HoursLeft`; by season, divided by `SimTuning.RainOften`; some drizzle). `Cloud`, `Rain`, `GroundWet`; owns the `Puddles`. `IsRaining`, `SpoilFactor`, `Word` (Fair, Clouding over, Drizzle, Rain, Clearing), `Overcast` (the light under cloud, no golden hour), `Haze`, `RainClock`. Dev: `StartRain`, `ClearUp`, `Soak`, `Held`. Saved; a new world starts fair with rain due on Day 1. State only. |
| `SaveGame.cs` * | 445 | The save file (`birbcube-save.json`): autosave every 2 min and on quit, load on start, New world. On load: pulls things inside the world, off rocks and out of the pool; places a pool and rocks for saves that lack them; rolls missing needs; drops birds of species whose file is gone (`KeepKnownSpecies`). A bird at the water steps onto the shore. Holds the plain `...Save` classes, each with `ToSave` / `FromSave`. Species are saved by key as text; `SeenSpecies` (plus the old `FrostcrestSeen`, still written). Feather counts (by species key, zeros included) and loose feathers (`FeatherSave`) are saved; an old save's coins are not read. The store's restock timers are not saved (old saves' `RestockTimers` are ignored). The Storyteller's timers (`StorySave`, not a running event; `Storyteller.AfterLoad` sends a saved flock off); each bird's `Records` and `Flock` flag. |
| `Settings.cs` | 134 | The settings file (`birbcube-settings.json`, separate so New world keeps it): volumes, fullscreen, name tags, hand color, hologram quality, and the tools panel's numbers (`SimTuning`, the hologram look, the radio dial). Saved when the Esc menu closes and on quit. Sim speed isn't saved. |
| `SimTuning.cs` | 97 | Every simulation number the tools panel can change: hunger, thirst and tiring, arrivals, max birds, lifespans, losing interest, regulars, calls, perching, home radius, feather drops and life, natural drops, max food, spoil, wear and regrow, day and night, weather, the pool, sprouts, the Storyteller's pace (`StoryPace`, quiet and rest minutes, cooldowns). `Now` is live (saved), `Defaults` as written, `Clamp` keeps them sane. Traits multiply some per bird. |
| `Helpers.cs` | 99 | `Palette` (colors), `Rng`, `Assets` (finds files in resources and the project folder). |
| `Enums.cs` | 31 | Bird states and poses (incl. `Roost`, used for naps too, and `Sip`, `Bathe`, `ShakeOff`). |
| `EventLog.cs` | 33 | The Bird Log's lines (saved with the world), stamped "[Day 3 18:42]", up to 120. Start-up lines survive the save's log loading. |
| `DrawStats.cs` | 16 | Counts model draws per frame for the tools panel. |

## The player

| File | Lines | What it does |
|---|---|---|
| `Hands.cs` * | 455 | Two hands from simple shapes, each ending in a closed brass cuff. Hidden until needed; a hand fades in (dithered) near its pose and eases in: a finger presses buttons, thumb and index turn knobs, a hand slides with the ring (left or right by the drag's first direction). `Spread` pushes the cube apart entering the Hologram View and presses it together leaving. Held up beside the Esc menu's Settings page. Color: `Tone` or `Yellow`. Update and Draw are separate sections. |
| `FeatherPurse.cs` | 84 | The player's feathers (`engine.Feathers`): a count per species key (a species is listed once its first feather is collected, and stays at zero), `Total`, `Has`, `Collected`, `Add`. Spending: `TrySpend` (one species' feathers: shelf and feeder prices), `TrySpendAny` (any kind, biggest stack first: only the store's lure seed), `Exchange` (the ledger's trade, `ExchangeRate` of one kind for one of another that's already collected). `Version` goes up when a species is first collected or the purse is restored, so the store rebuilds its shelves. `AddToAll` (cheat), `ToSave` / `Restore`. Feathers are only spent, never shown as objects in the room. |

## The world and its contents

| File | Lines | What it does |
|---|---|---|
| `Terrain.cs` | 200 | The ground's shape: a height grid (0.5 spacing) rolled per new world from a seed, saved as seed and height (`TerrainSave`), levelled around the pool (`FlattenAround`). `HeightAt`, `NormalAt`, `SlopeAt`, `DownhillAt`, `OnGround`, `RayHit` (mouse clicks on slopes), `Version` (the floor mesh rebuilds when it changes). Flat (height 0) for old saves. Every ground thing stands on `HeightAt`; birds use `Bird.StandY`. |
| `World.cs` | 94 | Holds every entity; world size (`HalfSize` 28, so 56x56); queued adds and removes; cached per-type lists; `Clear`; random ground points; `PullInside`. |
| `Entity.cs` | 18 | Base class: an ID and a world position. |
| `Rock.cs` | 143 | A rock: footprint, low dome (`HeightAt`). `PlaceNew` per new world (2 big, 3-5 small, clear of feeders and the pool), saved. `At` and `PushOut` keep things off rocks. Drawn in `WorldDrawer`. |
| `Pool.cs` | 259 | One pond per world: up to 3 circles blended by a smooth minimum, placed before the rocks, saved. `DistanceAt` (below 0 in the water; `waterDistance` in `hologram.fs` is the same math), `OutwardAt`, `MoveTo`, static `Current`, `Distance`, `IsWater`, `PushOut`. Ripple rings and splash droplets for drawing. |
| `Puddles.cs` | 133 | Rain puddles (owned by `Weather`): up to 6 form on worn dirt (`GroundWear.At`) while it rains, clear of everything else; fill and dry with the ground. `Nearest` for sipping birds. Saved. Drawn by the floor shader. |
| `GroundWear.cs` | 176 | How worn the grass is everywhere: birds and ground seed wear it, it grows back; `At` reads one spot. The lush ring around rocks and the pool's shore (`Lush`, rebuilt, not saved). Saved. `WorldDrawer` makes it the floor's texture. |
| `ViewWindow.cs` | 178 | The patch shown in the cube: center, zoom, yaw (shared by birds and store), panning (stops at the world's edge), `Follow`, cube size, the square edge test (`EdgeAmount`, matches the fog), world/box/scene conversions. `Contains`: inside the cube (simulation, clicks, chat lines, LEDs). The Hologram View's fog: `Immersion`, `ObserverMargin` (the garden fades out this share of the half-width past the walls; same number as in `hologram.fs`), `ShownReach`, `IsShown` (what gets drawn), `FogFade` (for labels). |
| `Food.cs` * | 473 | A seed pile: falling, rolling off rocks, landing (joins a nearby pile as new top layers; sinks in the pool), eaten from its topmost fresh layer of the wanted seed (`TakeBite`), each layer spoiling and fading on its own (faster in rain), sprouting once a heap's worth has rotted (`TrySprout`). A sprout's seedheads are a Food too (`Plant`). `HeapRadius`, `HasFresh`, `ClearAway`. |
| `SeedHeap.cs` | 62 | `SeedLayer` (one arrival of one seed) and `SeedHeap` (the heap's shape), shared by `Food` and `SeedDrawing`. |
| `Sprout.cs` | 215 | A grassy tuft where a heap rotted: grows, carries seedheads birds peck (its `Heads`), sheds a little seed, dies back (about 15 min). `TryPlant` keeps sprouts apart and off rocks, water and feeders. Saved. Stages read by `SproutDrawing`. |
| `FoodSpawner.cs` | 79 | Makes food: poured piles (`PileAmount`), scatter buttons, natural drops (toward the middle, never in a rock or the pool). `ClearGroundSeed` cheat. |
| `Feather.cs` | 120 | A feather a bird drops after a full meal, of its own species. Pops out, floats down swaying (never faster than `FallSpeed`), lands (never in a rock; washes up from the pool), lies flat, fades after `SimTuning.FeatherLifetime` (twice as fast out of view). Clicking it adds to the purse, plays a sound and logs "Collected an Ember Plume." Saved (`FeatherSave`). Drawn in `WorldDrawer.DrawFeathers`. |
| `Feeder.cs` | 247 | A placed feeder: catches seed, keeps it fresh, can be moved. Perches (`TakePerch`, `HasRoomFor`) for feeders that have them. `FeederPlacer` refuses spots near another feeder, a rock or the water. |
| `Feeders.cs` | 85 | Feeder types (Tray, Tube: 2 perches on one rod), each with its price and `PaidIn`, the key of the species whose feathers pay for it. `AppealFor` asks the species. `WorldScale` 3. |

## Birds: behavior (simulation)

| File | Lines | What it does |
|---|---|---|
| `Species.cs` | 116 | The `Species` class: `Key` (never shown; saves and call files use it), `BuiltIn`, `Name`, `FeatherName` (a built-in's is in `Birds.cs`, a Designer species' is "<Name> Feather"), `Looks`, `Size`, `Color`, `Common`, `Lure` (a rare species' lure seed), `Tastes`, `Feeders`, `Leans`, voice (`VoiceKey`, `VoicePitch`). `TasteFor`, `Eats`, `AppealFor`, `LeanFor`, `CanAppear`. Built from a built-in entry or a `SpeciesFile`; `CopyFrom` for Designer edits. `BirdLook`: one look a species can have. |
| `SpeciesFile.cs` | 144 | A Designer species on disk: `SpeciesFile` (`From`, `Copy`, `Clean`) and `SpeciesFiles` (`resources/species`: `List`, `NewKey`, `Save`, `Load`; the key is the file name). |
| `Birds.cs` | 166 | The four built-in species, one entry each (key, name, looks, size, color, common, tastes, feeders, leans, lure, feather name). `Birds.All`, `Common`, `Find`, `SameKey`, `Version`. Rare species seen: `IsDiscovered`, `MarkSeen`, `SaveSeen` / `RestoreSeen`. `LoadAll` (species files, then designs), `Add`, `Changed`, `AddDesign`. |
| `Traits.cs` * | 464 | The trait table: eleven scales, each trait with name, flavor line, commonness and effects. Nerve (Faint-hearted, Wary, Stout-hearted), Appetite (Dainty, Big Eater), Roaming (Hearthbound, Wayfarer), Voice (Hushed, Town Crier), Hours (Early Riser, Night Owl), and six scales with a single trait each, rolled rarely (`ScaleWeight` 0.2): Weather (Rain-proud), Bathing (Dabbler), Perching (Watchman), Taste (Particular), Loyalty (Oath-keeper), Giving (Open-handed). `TraitEffects` (every number a trait can change). `Roll` gives 2-3 traits on different scales by `ScaleWeight`; `Clashes` / `Unclash` (an Oath-keeper never holds Wayfarer: it becomes Hearthbound); `AsNightOwl`, `AsDayBird`, `AsRainProud`; `EffectsOf`, `Clean`, `Words`. Tooltip text for the bird card: `AverageNote` (what average means on a scale) and `EffectLines` (a trait's changed numbers as `EffectLine`s, worked out from `TraitEffects` through `numberLines` and `switchLines`; add a line there when `TraitEffects` gets a new number). Species leans live in `Birds.cs` or the species file. |
| `Bird.cs` * | 760 | What a bird wants. STORYTELLER EVENTS: `Freeze` (keeps dead still; the card says "Frozen still"), `Startle` (wakes a napper), `JoinFlock` / `FlockMember` (a murmuration bird: stays hungry, no feathers, never a regular), `Quiet` (no chat labels), `Records` / `AddRecord` (up to 8 "..., day N" lines, saved, carried with regulars); `StartLeaving(null)` leaves without a log line. Needs (NEEDS block): `Hunger`, `Thirst`, `Rest`; `SeeToNeeds` picks the most pressing (food, water via the mover, or a nap, `StartNap`); a need at zero for `NeedLimit` sends it off. Finding and eating food (feeder perches, stopping outside big heaps); a full meal can drop a feather of its species (`SimTuning.FeatherDropChance`, times `Nature.FeatherDrop` for Open-handed birds and `RegularFeatherBonus` for regulars and named birds). Calling (crowd hush, dawn chorus, quiet evening, less in rain). Night (`UpdateNight`): fly off to roost or stay (`Roosting`); Night Owls sleep by day. Rain (`MindsRain`). Arriving, leaving, home patch, lifespan, losing interest, named flag, visits. `Species`, `Personality`, `Nature`, `FavoriteSeed`, looks. Leaving hands it to the regulars. First split candidate now (see `qa-plan.md` section 7). |
| `BirdMover.cs` * | 713 | How a bird moves: walking, hopping, flocking (by Nerve), flying, home and edge forces, around rocks and the pool (`OnGround` keeps every step off rocks and out of deep water; the pool's room and steer distances are here), perching on rocks (`WillShare`; sit length times `Nature.PerchStay`), roosting and napping spots (`StartRoost`, `ResumeRoost`, `EndRoost`), arriving and leaving, scoring food (`FindNearestFood`: a Particular bird takes a pile with its favorite seed before any other unless starving). A `partial` class: the water half is in `BirdMover.Water.cs`. |
| `BirdMover.Water.cs` | 338 | The other half of `BirdMover`, the bird's trips to water: passing visits (`TryStartWaterVisit`), puddle sips (`StartPuddleVisit`), thirsty trips from anywhere (`StartThirstVisit`, flying a low arc if far, `FlyArc`), sipping until quenched or bathing (`UpdateWaterVisit`), `OnWaterTrip`, `IsBathTrip`, `WaterActivity` (the card's words), `EndWaterVisit`. Holds the water state and the water-only constants (`WaterReach`, `PuddleReach`, `FlyToWaterReach`, `SipDrink`, `SipRate`, `BathRate`...). |
| `BirdSpawner.cs` | 315 | Arrivals by the fresh seed about, scaled by time of day and cut in rain; only Night Owls at night (pull times the species' Night Owl lean), day arrivals lose Night Owl (`BecomeDayBird`), rain arrivals often Rain-proud. Rare species fly straight in (`IsLured`). Name counters per species key. The regulars (saved; returns by time of day; `BackFromNightChance`, `PickByReturnWeight`). `CheckRareSeen` unlocks a rare species' other tastes. `FlyDownTo` (public; a flock shares one approach side), `CallBack` (a bird the hawk scattered flies back near home, same visit). |
| `Storyteller.cs` | 230 | The Storyteller (`engine.Story`, Step 11): the pacer. Rhythm: quiet, light beat (Gift or Small), quiet, Big beat, rest; only one event at a time and the countdown waits for it. Category cooldowns; weighted roll among allowed events (`Pick`); `Snapshot` builds the `StoryContext` (birds, ground seed, daytime, rain, busy share, focus: the view 70% of the time while watching). `Fire` (tools panel), `Announce` / `TakeAnnouncements` (labels in view), `ToSave` / `Restore`, `AfterLoad`. State only. |
| `StoryEvent.cs` | 40 | The `StoryEvent` base (`CanFire`, `Weight`, `Start`, `Update`, `Drop`), `StoryCategory` (Big, Gift, Small) and `StoryContext`. |
| `HawkShadow.cs` | 270 | Big event: a hawk-shaped shadow crosses the world through the focus. Birds near it react once by Nerve: a Watchman calls the alarm first (wider reach after), Faint-hearted scatter, Wary half, average 40%, the rest freeze; Stout-hearted at a feeder hold it; bathers scatter, nappers wake. Scattered birds come back by `BirdSpawner.CallBack`. One summary log line, records. `Active`, `Position`, `Direction`, `Strength` for drawing. |
| `Murmuration.cs` | 170 | Big event: a flock of 6-15 of one common species (picked by pull over the ground seed; size follows the seed) flies in from one side, strips the seed, leaves together after 60-90 s or when its seed is gone. Garden birds close by get a record. `Plural`. Built to be reused by the migration stopover. |
| `Windfall.cs` | 85 | Gift event: a gust drops 3-6 piles of seed the birds here like (never a lure seed) near the focus, over a couple of seconds. |
| `Seeds.cs` | 131 | The seed table: names, colors, prices (`JarPrice`: feathers of the shelf's species), spoil times, grain looks. `RollFavorite`; `AddAttraction` (how strongly seed draws each species; used by ATTRACTS and arrivals). |

## Birds: looks (drawing and design)

| File | Lines | What it does |
|---|---|---|
| `BirbShape.cs` | 150 | A birb's body as data: parts with joints, scale, rotation, color, role (Body, Head, Beak, Wing, Tail, Leg, Eye). |
| `StarterBirbs.cs` | 111 | The built-in birb shapes. |
| `PartLibrary.cs` | 91 | Part meshes: sphere, cone, cube, plus any `.glb` in `resources/parts/`. |
| `BirbBuilder.cs` | 65 | Draws a BirbShape part by part through the hologram shader. |
| `BirbAnimator.cs` * | 434 | Draw-only. Poses to part movement (hop, peck, flap, head turns, roost, sip, bathe, shake off), squash and stretch, blinks, idle acts, the rain hunch (`AddHunch`). |
| `MotionStyle.cs` | 17 | Movement numbers for one birb. |
| `BirbDesigner.cs` * | 699 | The Birb Designer (B): edit parts, preview (always midday; test poses), save, load, release into the cube. Species section: pick a species, New..., and edit a Designer species' details (each edit saves at once: `Finished`, `CommitSpecies`). The species editor is a split candidate. |
| `BirbFile.cs` | 101 | A design as `.json` in `resources/birbs/`; `Species` is the species key. |

## Drawing the hologram

| File | Lines | What it does |
|---|---|---|
| `HologramRenderer.cs` * | 420 | Runs the passes: hologram contents to an off-screen texture (at `Quality`, through the lifted camera, skipped while off), then the room, pedestal, rocker and beams, then the texture through `post.fs`, then the hands. Owns the hologram, grain and post shaders; `HoloUniforms` sets the light (from `WorldClock`, dimmed by `Weather.Overcast`, turned with the ring), `shadeColor`, `golden`, `nightLevel`, the fog haze (night blue, rain gray, the sky's color at twilight via `TwilightSkyHaze` / `TwilightSkyShare`), the store lamp (off outside the store), `observer`, `worldHalf`, the Hawk's Shadow (`hawkShadow`, `hawkDir`, `HawkWingspan`; only where cloud shadows fall). The Designer gets `DayLight.Noon`. Hologram View: fades the cube's edges, hands the fog `observer`, draws `HorizonSky`, sets `alphaFix`; stops drawing the room once it's gone. Also the zoo camera and glow helpers. |
| `HorizonSky.cs` | 217 | The Hologram View's sky. `Refresh` works out this moment's colors (SKY KEYS by the sun's height, grayed under cloud, `SetClouds`), the trees' colors and how much stars, sun and moon show. `Horizon` is what the fog fades into. `Draw` lays `horizon.fs` over the screen, faded in with immersion. The visible moon is a full moon opposite the sun. Draw-only. |
| `SkyOrbs.cs` | 213 | The sun and moon under the cube's ceiling in the room view: one wheel turning once a day along the garden's east-west line (turns with the ring), the sun crossing by day and the moon by night, each fading at the walls; the moon shows its phase. Drawn in the hologram pass after the floor without writing depth, so birds draw over them. Fades out in the Hologram View (it has its own sky), during start-up and scene switches, and under cloud. Not in the store or the Designer. Draw-only. |
| `WorldDrawer.cs` | 278 | Draws the bird world: floor (a lit grid mesh of the whole world from `Terrain`, built in world units and scaled and moved into the cube by `DrawFloor`; wear texture, pool and weather uniforms), rocks with moss, feeders, feathers (`DrawFeathers`: a flat vane in the species' color and a pale quill, two model draws each), birds, selection rings. Draws what `ViewWindow.IsShown` allows. A `partial` class in four files (with `SeedDrawing`, `SproutDrawing`, `RainDrawing`). |
| `SeedDrawing.cs` | 295 | Seed piles as heaps of grains (layers, stragglers, dark core), falling streams and landing hops, spoiling grains. Owns the two instanced grain batches; sprouts, droplets (`AddDroplets`) and rain (`AddRain`) add to them. |
| `SproutDrawing.cs` | 128 | Sprouts as chains of thin blocks (`AddBend`) with seedheads (`AddHead`), growing, swaying, browning. All in the grain batches. |
| `RainDrawing.cs` | 112 | `SetWeatherUniforms` (rain, wetness, puddles for the floor shader); `AddRain`: up to 420 streaks (more over the Hologram View's margin) and ground splashes, in box space, placed from `Weather.RainClock` so nothing is stored. Drops near the eye shrink (`NearSizeCap`) and vanish (`NearVanish`). |
| `FieldEdges.cs` | 210 | The cube's glowing edges and scanline walls (`fieldwall.fs`); rise with start-up, fade (`shown`) in the Hologram View. `DrawProjection`: the lens beams in the room pass. |
| `Pedestal.cs` * | 622 | The pedestal in the room pass: dark wood cabinet, polished brass ring, brass fittings, hollow well, lens, logo (power button, sinks when pressed), BIRBS / FEATHERS windows (the feather total), species LEDs (`Birds.All`, 8 to a row). `LogoFace`, `RingTop` for the hands. One object, left whole. |
| `Rocker.cs` | 230 | The BIRB / SHOP rocker in the wood band, with brass words either side. Switches scenes only while fully on. `RayHits`, `FacePoint`. |
| `LatheMesh.cs` | 58 | Spins a side profile into a round mesh (pedestal parts). |
| `Projector.cs` | 89 | Power on/off and the start-up timeline; `PressButton`, `ButtonPush`. State only. |
| `BoxMode.cs` | 50 | Which scene shows (Birds or Store) and the fog transition (`SwitchTo`). |
| `ChatMessages.cs` * | 490 | Labels in the hologram: chat lines ("entered / rejoined / left the chat"), name tags (`NameLabels`: Off, Named, All), z's over sleeping birds (`UpdateSleepers`), event labels (`Announce`: the Storyteller's events in view, plain text, 7 s). `Quiet` birds get no chat labels; flock birds no name tags. Readability: plate, outline, lightened color, growth with distance. Faded by `ViewWindow.FogFade`. |
| `FeederModel.cs` | 65 | Draws a feeder from simple shapes (world, store, placing ghost). |
| `UnitShapes.cs` | 92 | Unit box, cylinder, sphere, stretched when drawn (`BallTurned`). One set per shader. |
| `HoloCamera.cs` | 250 | The camera. Faces the pedestal, never circles; sideways drag turns the contents (`IsTurning`, `TurnStartedRight`), up/down tilts. Room view zooms to `FrameDistance`, then leans in. Hologram View: the cube grows (`GrowScale`, `GrowLift`) while the camera dives in to stand in the garden (`InsideDistance`, `EnterTime`, `LeaveTime`, `DiveCurve`, `FovPush`); `Growth`, `Immersion`, `RoomHidden`. `HologramCamera` (the lifted, scaled camera). Reports clicks and double-clicks. |
| `Dial.cs` | 54 | How the ring feels: coasting, friction, notches, clicks. Turns `View.Yaw`. |

## The room

| File | Lines | What it does |
|---|---|---|
| `Backdrop.cs` * | 432 | The cabin: floor, wall, the starfield window (`sky.fs`, drifting, shifting with the camera, painted planet in front), lamps, crest, table, chairs. Owns `room.fs` (`SetShine`, `SetDissolve`, `SetProjectorLights`). Calls the radio's drawing. Ignores time of day and weather. Not drawn once the Hologram View hides it. |
| `TableProps.cs` | 279 | Mug with steam, books, tablet, drawing, candle. |

## The radio

| File | Lines | What it does |
|---|---|---|
| `Radio.cs` * | 453 | The cathedral radio beside the pedestal: shape, controls, mouse input, drawing (FM dial, station marks), hover tag. Clicking the tuning window opens the station finder. `HandTarget` for the hands. |
| `RadioPlayer.cs` * | 544 | The radio's sound: station list and dial, tuning and drift, the decoder, one Raylib stream, static, volume, ducking, `ObserverShare` in the Hologram View. |
| `RadioStation.cs` | 141 | One station: a folder (own clock, shuffle, `station.txt`) or a web stream. |
| `RadioDecoder.cs` | 129 | Decodes a file or stream on a background thread (NAudio) into a 44.1 kHz stereo ring buffer. |
| `RadioSpeaker.cs` | 101 | Small-speaker sound: bass and treble cut, narrower stereo, soft peaks, crackle on weak signal. |
| `WebStations.cs` * | 510 | Radio Browser stations in the background: genres from `Radio/web-stations.txt`, your own stations and genres (`Radio/my-stations.json`), searching (`Found`), an offline cache. User agent `BirbCube/<version>`. |
| `StationFinder.cs` | 311 | The station finder window (ImGui stand-in; its later home is the tablet): Find (search Radio Browser by name, genre or country, or paste a link; Listen previews, Add puts it on the dial) and My dial (your stations and genres). Changes save at once; the stations live in `WebStations`. |

## The store (Gilded Granary)

| File | Lines | What it does |
|---|---|---|
| `SeedStore.cs` * | 460 | The store's state: its turn copies `View.Yaw`; shelves, hover, buying, restock timers. A section of the shelf for each species whose feather you hold (`RebuildShelves`, four to a page, first on the left; `TurnPage`; `SellableSeeds`: seed it likes at 0.2 or more), jars and sacks priced in its own feathers. The crate by the door holds a jar of each rare species' lure seed until you hold that species' feather; it takes feathers of any kind (`StockLureCrate`). The two feeders (`StockFeeders`), paid in `Feeders.PaidIn`. `Controls`: the ledger book and the page arrows. `ShelfItem` (key, price, `PaidWith`, click sphere). The lamp (`LampLevel`, `UpdateLamp`): on when the bird world is dark enough, with a warm-up stutter. |
| `StoreRenderer.cs` * | 448 | Draws the store in the hologram (round inside the cube), lit by the time of day; the frosted bulb glows while lit (`selfLit`) and lights the shop (`SetLampLight`). Feather plaques over each section (`DrawPlaques`), the ledger book and page arrows (`DrawControls`), feeders at `SeedStore.FeederShopScale`. Never shows the pool, puddles or rain. |
| `Shopkeeper.cs` | 131 | The birb behind the counter (any species with a look that's common or seen), where it looks, hops on a purchase. |
| `PriceTag.cs` | 120 | The hanging price tag with Buy and Cancel: what it is, the price in the paying feathers, how many you hold, a hint at the ledger when short. |
| `Ledger.cs` | 221 | The feather swap panel opened from the ledger book on the counter (ImGui-free stand-in): pick a kind to give (3 held or more) and one to get, then Trade (`FeatherPurse.Exchange`). Scrolls past 8 kinds. State, clicks and drawing in separate sections. |

## Screen UI (2D)

| File | Lines | What it does |
|---|---|---|
| `SidePanels.cs` | 190 | The two brass side columns, slots and buttons (speed buttons read `GameEngine.SpeedSteps`). The stats screen grows with the feather collection (`StatsHeightFor`). In the Hologram View they slide off and peek back near the screen edge (`UpdateSlide`, `PeekBand`, `KeepReach`, `SlideTime`). |
| `BucketPanel.cs` * | 493 | Left column: seed bags, scoops, Stir/Empty, the see-through hologram bucket (`BucketShape`), ATTRACTS (up to 4 species, or 3 and "N more"). Stand-in, not split. |
| `SeedBucket.cs` | 184 | The bucket's contents as layers; stirring, scoops; `PredictBirds` for ATTRACTS. |
| `Hud.cs` * | 644 | Right column stats (BIRB CUBE): birds, food, the time (word, "Day 3, Spring", 24-hour dial), the weather word, then the feather collection (`DrawFeatherGrid`: a dot and a count per collected species, hover for the feather's name). The card: the selected bird (rename, visits, state, Food / Water / Rest bars via `DrawNeedBar`, then a trait row for each scale where the bird holds a trait, so only the 2-3 that stand out; holding Left Alt shows all eleven scales (`ShownScales`); the card's height follows the rows shown (`BirdCardHeight`). Hovering a row opens a tooltip under the card (`DrawTraitTip`): the trait's name and flavor line, or "<Scale>: average" and its note, then the trait's changed numbers from `Traits.EffectLines`. Then "Fond of" with the Alt hint, the bird's last 3 records (`DrawRecords`), Follow, Center) or the selected feeder (fill, seeds, birds eating, Move, Center). Stand-in, not split. |
| `MiniMap.cs` | 299 | The world map: pool, rocks, sprouts, seed and bird dots, view outline, click/drag, zoom. Becomes the zoo camera in the store. |
| `PauseMenu.cs` | 371 | The Esc menu: Resume, Designer, Fullscreen, Settings (volumes, hand color, Groening yellow, fullscreen type, name tags, quality), Controls, New world, Quit. Saves settings on close. |
| `UIHandler.cs` * | 433 | The ImGui tools panel (Tab): speed numbers, `SimTuning` sliders by section (Birds, Food, Feathers, lifetimes, behavior, World, Water), Day and night, Weather, Storyteller (`DrawStoryTools`: running event, next beat, cooldowns, a fire button per event, pace sliders), trait re-roll, Cheats (incl. "+10 of each feather"), Hologram look, the bird tracker (hover for needs and trait numbers; double-click to follow), log, controls list (`ControlLines`). Off-default sliders tinted, reset on right-click. |
| `UiDraw.cs` | 38 | Shared panel and button drawing. |
| `UiText.cs` | 50 | Non-ImGui text in Quicksand, one font load per size. |

## Sound

| File | Lines | What it does |
|---|---|---|
| `Sounds.cs` | 368 | Every sound effect: volumes, hum, the rain loop, bird calls by species key (`call_<key>_N.wav`, or a borrowed voice at `VoicePitch`, `CallsFor`) with the crowd hush (`CalledNearby`), pecks, sips, splashes, feather landing and pickup, clicks, rocker and power clicks (your recordings, with fallbacks; `PowerClickLead`), projector. The Hologram View's mix (`ObserverBirdBoost`, `ObserverHumShare`). `Update` also hands `Ambience` its inputs (`daylight` and `radioHeard` come from `GameEngine.UpdateBackground`). |
| `Ambience.cs` | 128 | The forest heard from inside the garden: a looping stereo bed (`forest_day_bed.ogg`, music stream, starts from a random spot on each visit) and random one-shots (`forest_day_1.wav`, `_2`, ..., every `GapMin` to `GapMax` seconds, random pitch, volume and pan, never the same twice running). Only in the Hologram View (`observer`), only by day (`daylight`, fading with the light through dusk, silent at night); ducked by rain, the radio and the Esc menu; follows the Birds volume. Optional: no files, no sound. `TargetLevel` is the state, `Update` plays it. |
| `tools/make_sounds.py` | - | Generates the placeholder `.wav` files in `resources/sounds/` (new sounds are added last so older files come out the same). Not used for `rocker_click.wav`, `power_click.wav` or the forest files. |

## Tools for Claude's workspace (not part of the game)

Run only in Claude's Linux workspace; `BirdsApp.csproj` leaves `tools\` out of the game's build. How to use them: `tools/ShotDriver/README.md`.

| File | Lines | What it does |
|---|---|---|
| `tools/setup-workspace.sh` | 51 | One-time setup per session: .NET 10, glslang, xvfb, null sound device, checks the staged DLLs and natives, builds the driver. |
| `tools/shot.sh` | 30 | Runs a script in a fresh sandbox (new world or `--save`) on a 1920x1080 virtual screen. |
| `tools/ShotDriver/ShotDriver.csproj` | - | Builds the game's `.cs` files (minus `Program.cs`) plus the driver against the DLLs in `bin/Release/net10.0/win-x64`. `nuget.config` has no sources. |
| `tools/ShotDriver/ShotDriver.cs` | 163 | Start-up (a copy of `GameEngine.Run`'s; keep in step), `Advance`, `Frames`, `shot` through the game's F9 path. |
| `tools/ShotDriver/ShotCommands.cs` | 198 | The script commands; every line checked before the game starts. |
| `tools/ShotDriver/MemberPath.cs` | 130 | `set`, `call`, `print` by dotted path, public or private. |
| `tools/ShotDriver/README.md` | - | Staging list, steps, commands, fixes. Keep its command table in step with `ShotCommands.cs`. |
| `tools/ShotDriver/scripts/tour.txt` | - | A look at most of the game; also the driver's smoke test (about 30 s). |

## The Jello board (task board, not part of the game)

Runs on your PC: `Jello Board.bat` starts the server and opens http://localhost:5178/. `BirdsApp.csproj` leaves `tools\` out of the game's build.

| File | Lines | What it does |
|---|---|---|
| `Jello Board.bat` | 9 | `dotnet run` of `tools/JelloBoard` pointed at `claude\board`; close its window to stop the board. |
| `tools/JelloBoard/Program.cs` | 286 | The server (`HttpListener`, localhost only): the page from `web\`, `/api/board` (every game and card, plus files it could not read), `/api/rev` (a fingerprint of the files, polled by the page), PUT / PATCH (merge) / DELETE per game or card, written through a temp file. If a board is already running it just opens the browser. |
| `tools/JelloBoard/web/index.html` | 1009 | The board page (ported from the claude.ai Jello artifact): games, columns, drag and drop, subtasks, archive, export and import. `Store` talks to the server and reloads when the files change. |
| `tools/JelloBoard/web/lib/` | - | Local copies of marked, DOMPurify and canvas-confetti, so the page works offline. |
| `claude/board/<game>/game.json` | - | A game's title, pitch, color and columns. |
| `claude/board/<game>/cards/<id>.json` | - | One card: `title`, `columnId`, `order`, `priority`, `description` (Markdown), `tags`, `checklist` (`id`, `text`, `completed`), `archived`, `createdAt`, `updatedAt`. The file name is the id. |

## Shaders (`resources/shaders/`)

| File | What it does |
|---|---|
| `hologram.vs` / `hologram.fs` | Everything inside the hologram. Lighting from uniforms (`lightDir`, `ambientLight`, `directLight`); the TIME OF DAY LOOK block (`shadeColor`, `golden` with `GroundSunShare` and `GoldenTintEase`, `nightLevel` with `NightMute` and `NightRim`); the store's lamp (`lampPos`, `lampColor`, `lampReach`, `LampIndoor`) and `selfLit`; tint, rim glow, scan bands, flat shading, moss. The ground (`groundColor`: grass and dirt noise, wear and lush from `texture1`, grid, `groundWet`). The pool (`waterDistance`, same math as `Pool.DistanceAt`; shore, shallows, swells, glint, sheen, sparkles, waterline, ripples), puddles (`puddleDistance`) and `rainRings`. The Hawk's Shadow (`hawkShape`, `HawkDarkness`, `HawkSkyShare`), applied beside the cloud shadows. The fog: square at the walls (`fogStart`, matches `ViewWindow.EdgeAmount`), toward `hazeColor`; in the Hologram View (`observer`) the cube clear and a round-cornered fade past the walls (`ObserverMargin`, `worldHalf`). `reveal` for start-up. `useVertexColor` only for grains. |
| `horizon.fs` | The Hologram View's sky over the whole screen, turned into box space: gradient, sun-side glow, stars, sun and moon discs, three rows of hills and trees melting into mist, a faint ridge line. |
| `skyorb.fs` | The sun and moon discs with their halos for `SkyOrbs`. |
| `grain.vs` | Instanced grains (seed, sprout parts, droplets, rain) with `hologram.fs`; per-grain color in the transform's unused bottom row. |
| `post.fs` / `post.vs` | Glow and scanlines over the finished hologram; `alphaFix` restores true alpha in the Hologram View. |
| `fieldwall.vs` / `fieldwall.fs` | The see-through walls with lines drifting upward. |
| `sky.vs` / `sky.fs` | The cabin window's stars: softened, a few twinkling. |
| `room.vs` / `room.fs` | Room lighting: top light and two lamps, `shine` for brass and a low sheen for varnish and skin, the box's own light, `dissolve` for the hands. |

## Other files

| File | What it does |
|---|---|
| `resources/starfield.png` | The cabin window's stars (1920x1080; any picture works). |
| `resources/birbbox_logo_black.png` / `_white.png` | The logo glyph (old names kept on purpose). |
| `resources/sounds/` | Sound effects; most from `tools/make_sounds.py`, `rocker_click.wav` and `power_click.wav` yours. Numbered files (`sip_N`, `splash_N`, `call_<key>_N`) are all loaded, so recordings can replace or add to them. `feather_land.wav` and `feather_pickup.wav` are placeholders. `coin_land.wav` and `coin_pickup.wav` are unused but kept on purpose, for a later use. |
| `resources/sounds/ambience/` | The forest ambience, cut from your recording: `forest_day_bed.ogg` (2:25 stereo loop) and `forest_day_1.wav` ... `_9.wav` (the one-shots; any count, numbered without gaps). A night set would sit beside them (`forest_night_*`). |
| `resources/fonts/` | Quicksand and Chakra Petch, with OFL licenses. |
| `resources/birbs/` | Designer saves, each naming its species by key. Give eye parts the Eye role so they blink. |
| `resources/species/` | Designer species, one `<Key>.json` each. Deleting one removes the species (its birds leave on next load). A Release copy has its own. |
| `BirdsApp.csproj` | Raylib-cs 8.0.0 (raylib 6.0), ImGui, NAudio.Wasapi 2.2.1 and NAudio.Vorbis 1.5.0 (kept on 2.x on purpose). Builds `BirbCube.exe`; copies `resources/`; leaves `tools\` out. |
| `Build Birb Cube.bat` | Release copy in a "Birb Cube" folder next to the project, with Desktop and Start menu shortcuts. |
| `Make Release Zip.bat` | Self-contained win-x64 release zip in "Birb Cube Release" next to the project; reads the version from `GameEngine.cs`; never touches your play folder or save. |
| `README.md` | How to run it. |
| `CLAUDE.md` | Working rules and a short architecture guide for Claude Code. |
| `claude/instructions-for-claude.md` | Your notes and requests for Claude (currently notes on RimWorld's storyteller). Read when you write "Instructions:". |
| `screenshots/` | Four Oct 19 screenshots (tracked). New ones are `birbcube_<date>.png`. |
| `imgui.ini` | ImGui window positions. Safe to delete. |
| `birbcube-save.json` | The saved world (git-ignored). A broken one is renamed `.broken.json`. |
| `birbcube-settings.json` | Settings and tools panel tuning (git-ignored). Delete its `Tuning` block to get new defaults. |
| `Radio/` | The radio's folder (git-ignored): station sub-folders, `web-stations.txt`, `my-stations.json`, the cache. Next to the .exe in a Release copy. |
| `bin/`, `obj/` | Build output. `bin/Release/net10.0/win-x64` holds the DLLs the workspace compiles against; `bin/Debug/net10.0/runtimes/linux-x64/native` the Linux raylib. |

**Leftovers you can delete whenever you like:** the five old `.glb` files in `resources/` (`birb`, `cardinal`, `cardinal_f`, `dove`, `finch`; unused since 4.42, still tracked); old `birbbox-save.json` / `birbbox-settings.json` (no longer read); the old "Birb Box" release folder and its shortcuts. The `CoinDropChance` and `CoinLifetime` entries in your settings file's `Tuning` block are ignored. The unused coin sounds stay on purpose. The repo and folder are still named `birb-box`; renaming them is your call.

---

## Where to find things

- **Version number:** `GameEngine.Version`
- **Frame order:** `GameEngine.UpdateSystems` (one line per phase). A new control goes in its phase; mouse facts for later phases go in `FrameInput`.
- **Tools panel numbers (defaults):** `SimTuning.cs`. Live values are in `birbcube-settings.json` (a changed default shows only after right-clicking its slider or deleting the `Tuning` block).
- **Screenshot driver:** `tools/ShotDriver/README.md`; new commands in `ShotCommands.BuildCommands`; `HandsSettle` in `ShotCommands.cs`; `ShotDriver.StepSeconds`; screen size in `tools/shot.sh`.
- **Feathers:** drop chance and how long one lasts: `FeatherDropChance`, `FeatherLifetime` in `SimTuning.cs`; the regulars' bonus: `RegularFeatherBonus` in `Bird.cs`; the Open-handed bonus: `FeatherDrop` in `Traits.cs`. Pop, float speed, sway, tilt, fade: top of `Feather.cs`; its size: `Feather.Length`, `Width`, `Thickness`; the look: `WorldDrawer.DrawFeathers`. Names: `featherName` in `Birds.cs` (a Designer species' is made in `Species.cs`). The spending rules: `FeatherPurse.TrySpend`, `TrySpendAny`, `Exchange` (`ExchangeRate`). HUD collection: `FeatherColumns`, `FeatherMaxShown`, `StatsHeightFor` in `SidePanels.cs`, `DrawFeatherGrid` in `Hud.cs`. Pedestal window: `RefreshReadouts` in `Pedestal.cs`. Sounds: `PlayFeatherLand` / `PlayFeatherPickup` in `Sounds.cs`, `feather_land` / `feather_pickup` in `make_sounds.py`.
- **The store's shelves and prices:** jar price per seed: `JarPrice` in `Seeds.cs` (a sack is `SackPriceInJars` jars, in `ShelfItem`); feeder prices and who pays: `Feeders.cs` (`price`, `paidIn`). Which seeds a species' section sells: `MinShelfTaste` and `SellableSeeds` in `SeedStore.cs`. Sections per page, gaps, jars per row: SECTIONS block there. The lure crate: the CRATE constants and `StockLureCrate`. Feeder size in the shop: `FeederShopScale`; their spots: `StockFeeders`. Plaques, ledger book and arrows: `DrawPlaques`, `DrawControls` in `StoreRenderer.cs`. The ledger panel: `Ledger.cs`.
- **Needs:** rates `ThirstRate`, `RestRate` (tools panel, Birds) and `ThirstyBathShare` (Water) in `SimTuning.cs`. Thresholds, nap length and refill, what cuts a nap or trip short, how long a need can sit at zero: the NEEDS block at the top of `Bird.cs`. Which need goes first: `Bird.SeeToNeeds`. Flying to water, sip size, most sips: `FlyToWaterReach`, `WaterFlightArc`, `SipDrink`, `MaxSips` at the top of `BirdMover.Water.cs`. Trait effects: `ThirstSpeed`, `RestSpeed`, `BathShare`, `BathLength` in `Traits.cs`. Night Owl hours: `OwlSleepAfterSunrise`, `OwlWakeBeforeSunset` in `WorldClock.cs`. Card bars: `DrawNeedBar`, `NeedGap`, `NeedLow` in `Hud.cs`.
- **The store's lamp:** on/off darkness, warm-up, stutter: THE LAMP at the top of `SeedStore.cs`. Light color, reach, bulb colors and spot: `LampLight`, `LampReach`, `BulbOff`, `BulbOn`, `BulbSpot` in `StoreRenderer.cs`. Indoor share: `LampIndoor` in `hologram.fs`.
- **The Hologram View's sky:** colors through the day: SKY KEYS in `HorizonSky.cs`; hologram lean, tree green, mist, cloud gray: LOOK there; when stars, sun and moon show: end of `HorizonSky.Refresh`. Sizes, star count, ridge glow: SHAPES in `horizon.fs`; the rows: end of `main` there; crowns: `canopy`.
- **The room view's sun and moon:** THE WHEEL and SIZES at the top of `SkyOrbs.cs`.
- **Designer species:** each one's file in `resources/species/`. Cleaning: `SpeciesFile.Clean`. Keys: `SpeciesFiles.NewKey`. Rare rule: `Birds.IsDiscovered`, `Seeds.AddAttraction`, `BirdSpawner.IsLured`; "spotted" line: `CheckRareSeen` / `OtherSeedsText`. Editor slider ranges: `DrawSpeciesDetails` in `BirbDesigner.cs`. LED rows: `LedsPerRow`, `LedRowGap`, `LedRowShrink` in `Pedestal.cs`. ATTRACTS rows: `MaxAttractsRows`, `RowsWithMore` in `BucketPanel.cs`.
- **Rain and weather:** how often, rain spoil: `RainOften`, `RainSpoil` in `SimTuning.cs`. Spells, seasons, drizzle, cloud and rain easing, wetting and drying, light under cloud, rain haze: top of `Weather.cs`. Puddles: top of `Puddles.cs`. Drops (count, speed, size, splash, colors, near-eye shrink): top of `RainDrawing.cs`. Wet ground, puddle look, rain rings: `groundWet`, `puddleDistance`, `rainRings` in `hologram.fs`. Hunch: HUNCHED IN THE RAIN in `BirbAnimator.cs`. Calls and leaving: RAIN in `Bird.cs`. Arrivals: RAIN in `BirdSpawner.cs`. Puddle sipping: `PuddleReach`, `PuddleMinFill` in `BirdMover.Water.cs`. Rain-proud share: `ScaleWeight` in `Traits.cs` and `leans` in `Birds.cs`. Loudness: `RainVolume`, `RainQuietShare` in `Sounds.cs`. HUD row: `DrawStats` in `Hud.cs`. The weather words: `Weather.Word`.
- **The pool:** size, shape, placement, wading depth, ripples, droplet cap: top of `Pool.cs` and `Pool.PlaceNew` (keep `Blend` equal to the `0.9` in `waterDistance`). Passing sips and baths, max bathers: `DrinkChance`, `BathChance`, `MaxBathers` in `SimTuning.cs`. How close birds get and steer around the water, wading depth: THE POOL block at the top of `BirdMover.cs` (`PoolRoom`, `PoolSteerReach`, `WadeDepth`, `SinkShare`). Walking distance, give-up time, shake and bath timing, sip speed: the water constants and the trip code in `BirdMover.Water.cs`. Poses: Sip, Bathe, ShakeOff in `BirbAnimator.PoseMotion`. Water look: `waterHeight`, `waterColor` and the pool parts of `main` in `hologram.fs`. Shore grass: `PoolLushShare` in `GroundWear.cs`. Droplets: `DropletColor`, `AddDroplets` in `SeedDrawing.cs`. Loudness: `SipVolume`, `SplashVolume` in `Sounds.cs`.
- **Day and night:** day length, night brightness, roosting: `DayMinutes`, `NightBrightness`, `RoostChance`, `MaxRoosting` in `SimTuning.cs`. Days per season, start hour, latitude, twilight depth, waking, roosting, chorus and quiet times, Night Owl hours: top of `WorldClock.cs`. Light colors, shade, golden: LIGHT KEYS in `WorldClock.cs`. Low-sun ground, tint easing, night mute and rim: TIME OF DAY LOOK in `hologram.fs`. Night haze: `NightHaze`, `NightHazeShare`; twilight fog: `TwilightSkyHaze`, `TwilightSkyShare` (top of `HologramRenderer.cs`). No golden hour under cloud: `Weather.Overcast`. Roost and nap reach: `RoostReach` in `BirdMover.cs`. Roosting hunger and call rates: NIGHT in `Bird.cs`. Night arrivals and returns: `NightOwlShare`, `BackFromNightChance` in `BirdSpawner.cs`. Roost pose: `RoostFluff`, `RoostSquash` in `BirbAnimator.cs`. HUD dial: THE CLOCK DIAL in `Hud.cs`.
- **Traits:** the table, `ThreeTraitChance`, `ScaleWeight`, `Clashes`, `TraitEffects` in `Traits.cs`; species leans in `Birds.cs` (or the species file). The newer traits' numbers: `PerchChance` and `PerchStay` (Watchman), `Particular` (taste; used in `BirdMover.FindNearestFood`), `ReturnWeight` (Oath-keeper), `FeatherDrop` (Open-handed). Favorite seed pull: `FavoritePull` in `Bird.cs`; which seeds qualify: `FavoriteMinTaste` in `Seeds.cs`. Bird card rows: `CardBaseHeight`, `SliderRowHeight`, `ShownScales` (which scales get a row; Left Alt shows all) in `Hud.cs`. Trait tooltip: `DrawTraitTip`, `TipGap` in `Hud.cs`; its words and numbers: `averageNotes`, `numberLines`, `switchLines` in `Traits.cs`.
- **Forest ambience:** loudness (`BedVolume`, `ShotVolume`), how much rain, the radio and the menu cover it (`RainDuck`, `RadioDuck`, `QuietShare`), fade speed, one-shot gaps, pitch, volume and pan spread: the constants at the top of `Ambience.cs`. When it is heard (Hologram View, daylight): `Ambience.TargetLevel`; the inputs are passed in `GameEngine.UpdateBackground`. Files: `resources/sounds/ambience/`.
- **Storyteller and events:** pace, quiet and rest lengths, cooldowns: `StoryPace`, `StoryQuietMinutes`, `StoryRestMinutes`, `BigCooldownMinutes`, `GiftCooldownMinutes` in `SimTuning.cs` (tools panel, Storyteller). Jitter, retry, how often events center on the view: top of `Storyteller.cs`. Each event's numbers (reach, scatter chances, freeze times, return delays; flock size, stay; piles, spread) and its `CanFire` / `Weight`: TUNING at the top of `HawkShadow.cs`, `Murmuration.cs`, `Windfall.cs`. Shadow size: `HawkWingspan` in `HologramRenderer.cs`; shape and darkness: `hawkShape`, `HawkDarkness` in `hologram.fs`. Event labels: `EventLifetime`, `EventLift` in `ChatMessages.cs`. Records kept and shown: `MaxRecords` in `Bird.cs`, `RecordsShown` in `Hud.cs`. A new event: a `StoryEvent` subclass added to the `events` list in `Storyteller.cs`.
- **Frame pacing, slow frames:** top of `FramePacing.cs`.
- **Window size, fullscreen, screenshot folder:** `GameWindow.cs`.
- **Click target sizes:** `MousePicking.cs`.
- **Seeds:** names, prices, spoil: `Seeds.cs`. Who eats what: `tastes` in `Birds.cs` (or the species file).
- **Species:** `Birds.cs` and `resources/species/`. The key is the first text in each entry or the file name; never change it.
- **Bird shapes:** `StarterBirbs.cs`, `resources/birbs/*.json`.
- **Shop text and name:** `Shopkeeper.cs`, `SeedStore.cs`.
- **Controls list:** `UIHandler.ControlLines`.
- **Hologram color, glow, scanlines, curtain:** top of `HologramRenderer.cs` (also saved once changed in the tools panel). Quality: `HologramRenderer.Quality`, `CycleQuality` in `PauseMenu.cs`.
- **Cube and pedestal size, fog width:** `ViewWindow.HalfSize`, `CubeScale`, `Height`, `FogStart`.
- **World size:** `World.HalfSize`. Natural drop spots: `FoodSpawner.Update`.
- **The ground:** height (`SimTuning.TerrainHeight`, Tab panel World), swell sizes and the flat margin at the walls (`BigSwell`, `SmallSwell`, `EdgeFade`), the pool bank (`BankWidth`) at the top of `Terrain.cs`. Seed sliding on slopes (`Slide*` in `Food.cs`). Puddle hollows (`HollowDepth` in `Puddles.cs`).
- **Pouring:** `PileInterval`, `PileSpacing` in `Pouring.cs`; `FoodSpawner.PileAmount`. Natural drops: `SimTuning.DropsPerMinute`, `FoodSpawner.WildPileAmount`. Fading: `Food.FadeAfter`; rot color `RotColor` in `SeedDrawing.cs`.
- **Seed heaps:** joining, `JoinReach` / `JoinHeapShare` in `Food.cs` (keep `JoinReach` under `PileSpacing`); bite size, `Slope`, packing, stragglers in `SeedHeap.cs`; straggler bounce is each seed's `Spread`. Fall, landing, heap core: top of `SeedDrawing.cs`. Where birds stop to peck: `UpdateSeeking` in `Bird.cs`.
- **Sprouts:** `SproutChance`, `SproutMinSeed`, `SproutMinutes`, `MaxSprouts` in `SimTuning.cs`; stages, heads, shedding, spacing: top of `Sprout.cs`; looks: SPROUT LOOKS in `SproutDrawing.cs`; where one comes up: `Food.TrySprout`.
- **Ground:** `groundColor`, `grassColor`, `dirtColor` in `hologram.fs`; wear and regrow, lush ring width: top of `GroundWear.cs` (times `WearSpeed`, `RegrowSpeed`).
- **Rocks:** count, sizes, spacing: top of `Rock.cs`, `Rock.PlaceNew`. Color and lumps: `WorldDrawer.DrawRocks`. Moss: `MossMin` / `MossMax` in `WorldDrawer.cs`, the moss block in `hologram.fs`. Perching: `SimTuning.PerchChance`, `PerchReach` in `BirdMover.cs`, `SetPerch`. Roll-off speed: top of `Food.cs`.
- **Feeders:** `Feeders.WorldScale`; who uses which: `feeders` in `Birds.cs`; tube perches: the Tube entry in `Feeders.cs` and `FeederModel.cs`.
- **Bird life:** `LifespanMinMinutes/MaxMinutes`, `LoseInterestMinutes` in `SimTuning.cs`; `ScarceFoodFactor` in `Bird.cs`. Roaming: `SimTuning.HomeRadius`, `EdgeMargin` in `BirdMover.cs`. Regulars: `SimTuning.RegularChance`, `MaxRegulars` in `BirdSpawner.cs`. Rare unlock: `CheckRareSeen`, `Birds.IsDiscovered`; the Frostcrest's `lure:` in `Birds.cs`.
- **Animation:** SQUASH AND STRETCH, BLINKS, IDLE ACTS at the top of `BirbAnimator.cs`.
- **Calls:** `CallDelayMin/Max` in `SimTuning.cs`, `HushRetryMin/Max` in `Bird.cs`; `CallVolume`, `HushRadius`, `HushTime` in `Sounds.cs`.
- **Labels:** size and height at the top of `ChatMessages.cs`; READABILITY block there.
- **Bird card and stats panel:** `CardBaseHeight`, `TraitSize`, `FlavorSize` in `Hud.cs`; the stats panel's height: `StatsHeightFor` in `SidePanels.cs`.
- **Bucket shape:** `BucketShape` in `BucketPanel.cs`.
- **Pedestal looks:** `PlinthWood`, `BodyWood`, `WoodShine`, `RingBrass`, `RingShine`, `BrassShine` in `Pedestal.cs`; `HighlightPower`, `HighlightStrength`, `ReflectBand` in `room.fs`. Band and fitting heights: `LipBottom`, `PlateY`, `WindowY`, `ButtonY` (keep `Rocker.CenterY` in the band's middle).
- **Power button:** `PushDepth` in `Pedestal.cs`; `PushIn`, `PushHold`, `PushOut` in `Projector.cs`; `PowerClickVolume`, `PowerClickLead` in `Sounds.cs`. Start-up timing: top of `Projector.cs`.
- **Rocker:** top of `Rocker.cs`. Scene fog time: `FogTime` in `BoxMode.cs`.
- **Hands:** size, speed, fade, linger, gaps, twist, slide: top of `Hands.cs`; `ToneStops`, `GroeningYellow`; FINGERS and FINGER CURLS blocks; `CuffBrass`, `DrawHand`.
- **Settings and saves:** `SettingsData` and the top of `Settings.cs`; autosave interval and file name at the top of `SaveGame.cs`, the `...Save` classes at its bottom.
- **Beams, well, lens:** top of `FieldEdges.cs`, top of `Pedestal.cs`. Cube float height: `HoloCamera.HoloLift`. Box light reach: top of `room.fs`.
- **The Hologram View:** growth, standing spot, tilt range, dive timing and curve, widening, room fade: top of `HoloCamera.cs` (`GrowScale`, `GrowLift`, `InsideDistance`, `InsideAimY`, `HoloStartPitch`, `HoloMinPitch` / `HoloMaxPitch`, `EnterTime`, `LeaveTime`, `DiveCurve`, `FovPush`, `ImmerseFrom`). The misty margin: `ObserverMargin` in `ViewWindow.cs` and `hologram.fs` (keep equal). Column peek: `PeekBand`, `KeepReach`, `SlideTime` in `SidePanels.cs`. Sound: `ObserverBirdBoost`, `ObserverHumShare` in `Sounds.cs`, `ObserverShare` in `RadioPlayer.cs`.
- **Cabin window:** `WindowLeft`..., `SkyPixelsPerUnit`, `SkyDrift`, `WindowDepth`, `SkyTint` at the top of `Backdrop.cs`; `Soften`, `SoftShare`, `TwinkleShare`, `TwinkleDepth` in `sky.fs`; planet: `Backdrop.PaintPlanet`.
- **Camera:** `MinDistance`, `MaxDistance`, `NearAimY`, `FarAimY`, `DoubleClickTime`, `DoubleClickReach` in `HoloCamera.cs`. Follow speed: `ViewWindow.Follow`.
- **Table layout:** top of `TableProps.cs`; the radio's `Spot` and `YawDegrees` in `Radio.cs`.
- **Limits:** `SimTuning.MaxBirds`, `MaxFood`, `FeatherDropChance`, `FeatherLifetime`, `SpoilSpeed`.
- **Speed numbers:** `GameEngine.UpdateMs` / `DrawMs`, `DrawStats`, `FramePacing`, shown in `UIHandler.DrawControlPanel`. All grains (seed, sprouts, droplets, rain) are 2 model draws; the pool and puddles add none; each feather on the ground is 2; the Hologram View's sky is one 2D rectangle.
- **Radio:** placement, size, knob feel: top of `Radio.cs`. Static, ducking, warm-up, drift, web timeouts: top of `RadioPlayer.cs`. Speaker tone: top of `RadioSpeaker.cs`. Web genres: `Radio/web-stations.txt`; your stations: `Radio/my-stations.json`.
